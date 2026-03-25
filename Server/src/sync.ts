import type { FastifyInstance, FastifyReply, FastifyRequest } from "fastify";
import type { Pool } from "pg";
import type { AppConfig } from "./config.js";
import type { SyncReminderInput } from "./types.js";
import { authenticateRequest } from "./auth.js";

interface SyncRoutesDeps {
  config: AppConfig;
  db: Pool;
  events: SyncEventHub;
}

interface UpsertBody {
  items?: SyncReminderInput[];
}

interface ChangesQuery {
  since?: string;
  limit?: string;
}

export class SyncEventHub {
  private readonly subscribers = new Map<string, Set<(payload: SyncEvent) => void>>();

  subscribe(userId: string, callback: (payload: SyncEvent) => void): () => void {
    const set = this.subscribers.get(userId) ?? new Set<(payload: SyncEvent) => void>();
    set.add(callback);
    this.subscribers.set(userId, set);

    return () => {
      const current = this.subscribers.get(userId);
      if (!current) {
        return;
      }

      current.delete(callback);
      if (current.size === 0) {
        this.subscribers.delete(userId);
      }
    };
  }

  publish(userId: string, payload: SyncEvent): void {
    const listeners = this.subscribers.get(userId);
    if (!listeners) {
      return;
    }

    for (const listener of listeners) {
      listener(payload);
    }
  }
}

interface SyncEvent {
  kind: "changed" | "heartbeat";
  atUtc: string;
}

export async function registerSyncRoutes(app: FastifyInstance, deps: SyncRoutesDeps): Promise<void> {
  const { config, db, events } = deps;

  app.get<{ Querystring: ChangesQuery }>("/v1/sync/changes", async (request, reply) => {
    const user = await authenticateRequest(request, reply, config);
    if (!user) {
      return;
    }

    const sinceUtc = parseSince(request.query.since);
    const limit = normalizeLimit(request.query.limit);

    const rows = await db.query(
      `SELECT id,
              title,
              due_at_utc,
              priority,
              created_at_utc,
              status,
              last_fired_at_utc,
              acked_at_utc,
              snooze_until_utc,
              telegram_escalated_at_utc,
              deleted_at_utc,
              updated_at_utc
       FROM reminders
       WHERE user_id = $1
         AND updated_at_utc > $2
       ORDER BY updated_at_utc ASC
       LIMIT $3`,
      [user.id, sinceUtc, limit],
    );

    return reply.code(200).send({
      items: rows.rows.map((row) => ({
        id: row.id,
        title: row.title,
        dueAtUtc: row.due_at_utc?.toISOString?.() ?? row.due_at_utc,
        priority: row.priority,
        createdAtUtc: row.created_at_utc?.toISOString?.() ?? row.created_at_utc,
        status: row.status,
        lastFiredAtUtc: row.last_fired_at_utc?.toISOString?.() ?? null,
        ackedAtUtc: row.acked_at_utc?.toISOString?.() ?? null,
        snoozeUntilUtc: row.snooze_until_utc?.toISOString?.() ?? null,
        telegramEscalatedAtUtc: row.telegram_escalated_at_utc?.toISOString?.() ?? null,
        deleted: !!row.deleted_at_utc,
        updatedAtUtc: row.updated_at_utc?.toISOString?.() ?? row.updated_at_utc,
      })),
      nowUtc: new Date().toISOString(),
    });
  });

  app.post<{ Body: UpsertBody }>("/v1/sync/upsert", async (request, reply) => {
    const user = await authenticateRequest(request, reply, config);
    if (!user) {
      return;
    }

    const items = request.body.items ?? [];
    if (!Array.isArray(items) || items.length === 0) {
      return reply.code(400).send({ message: "items must be a non-empty array." });
    }

    if (items.length > 500) {
      return reply.code(400).send({ message: "Too many items in a single request (max 500)." });
    }

    const now = new Date();

    for (const item of items) {
      if (!isValidSyncItem(item)) {
        return reply.code(400).send({ message: "Invalid sync item payload." });
      }

      const deletedAtUtc = item.deleted ? now.toISOString() : null;

      await db.query(
        `INSERT INTO reminders (
           id,
           user_id,
           title,
           due_at_utc,
           priority,
           created_at_utc,
           status,
           last_fired_at_utc,
           acked_at_utc,
           snooze_until_utc,
           telegram_escalated_at_utc,
           deleted_at_utc,
           updated_at_utc
         )
         VALUES (
           $1, $2, $3, $4::timestamptz, $5, $6::timestamptz, $7,
           $8::timestamptz, $9::timestamptz, $10::timestamptz, $11::timestamptz,
           $12::timestamptz, $13::timestamptz
         )
         ON CONFLICT (user_id, id)
         DO UPDATE SET
           title = EXCLUDED.title,
           due_at_utc = EXCLUDED.due_at_utc,
           priority = EXCLUDED.priority,
           status = EXCLUDED.status,
           last_fired_at_utc = EXCLUDED.last_fired_at_utc,
           acked_at_utc = EXCLUDED.acked_at_utc,
           snooze_until_utc = EXCLUDED.snooze_until_utc,
           telegram_escalated_at_utc = EXCLUDED.telegram_escalated_at_utc,
           deleted_at_utc = EXCLUDED.deleted_at_utc,
           updated_at_utc = EXCLUDED.updated_at_utc`,
        [
          item.id,
          user.id,
          item.title,
          item.dueAtUtc,
          item.priority,
          item.createdAtUtc,
          item.status,
          item.lastFiredAtUtc ?? null,
          item.ackedAtUtc ?? null,
          item.snoozeUntilUtc ?? null,
          item.telegramEscalatedAtUtc ?? null,
          deletedAtUtc,
          now.toISOString(),
        ],
      );

      await db.query(
        `INSERT INTO sync_events (user_id, reminder_id, kind)
         VALUES ($1, $2, 'upsert')`,
        [user.id, item.id],
      );
    }

    events.publish(user.id, {
      kind: "changed",
      atUtc: now.toISOString(),
    });

    return reply.code(200).send({ updatedAtUtc: now.toISOString() });
  });

  app.get("/v1/sync/events", async (request, reply) => {
    const user = await authenticateRequest(request, reply, config);
    if (!user) {
      return;
    }

    reply.raw.setHeader("Content-Type", "text/event-stream");
    reply.raw.setHeader("Cache-Control", "no-cache");
    reply.raw.setHeader("Connection", "keep-alive");

    const send = (event: SyncEvent) => {
      reply.raw.write(`event: ${event.kind}\n`);
      reply.raw.write(`data: ${JSON.stringify(event)}\n\n`);
    };

    send({ kind: "heartbeat", atUtc: new Date().toISOString() });

    const unsubscribe = events.subscribe(user.id, send);
    const heartbeat = setInterval(() => {
      send({ kind: "heartbeat", atUtc: new Date().toISOString() });
    }, 20_000);

    request.raw.on("close", () => {
      clearInterval(heartbeat);
      unsubscribe();
    });

    return reply;
  });
}

function parseSince(input: string | undefined): string {
  if (!input) {
    return new Date(0).toISOString();
  }

  const parsed = new Date(input);
  if (Number.isNaN(parsed.getTime())) {
    return new Date(0).toISOString();
  }

  return parsed.toISOString();
}

function normalizeLimit(input: string | undefined): number {
  const parsed = Number.parseInt(input ?? "", 10);
  if (!Number.isFinite(parsed) || parsed <= 0) {
    return 500;
  }

  return Math.min(parsed, 1000);
}

function isValidSyncItem(item: SyncReminderInput): boolean {
  if (!item || typeof item !== "object") {
    return false;
  }

  if (!item.id || !item.title || !item.dueAtUtc || !item.createdAtUtc || !item.status) {
    return false;
  }

  if (!Number.isInteger(item.priority) || item.priority < 0 || item.priority > 2) {
    return false;
  }

  return true;
}