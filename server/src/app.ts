import Fastify from "fastify";
import helmet from "@fastify/helmet";
import { randomUUID } from "node:crypto";
import { readFile } from "node:fs/promises";
import path from "node:path";
import sensible from "@fastify/sensible";
import rateLimit from "@fastify/rate-limit";
import type { FastifyReply } from "fastify";
import { z } from "zod";
import { config } from "./config.js";
import { openDatabase } from "./db.js";
import { AuthStore } from "./store.js";
import { TelegramModerationService } from "./telegram.js";
import { AuthService } from "./authService.js";
import { verifyAccessToken } from "./jwt.js";
import type { ReminderStatus, SyncReminderRecord, UserStatus } from "./types.js";

const registerSchema = z.object({
  username: z.string().min(1).max(64),
  password: z.string().min(1).max(256)
});

const loginSchema = z.object({
  username: z.string().min(1).max(64),
  password: z.string().min(1).max(256),
  deviceId: z.string().min(1).max(128),
  deviceName: z.string().min(1).max(128)
});

const logoutSchema = z.object({
  refreshToken: z.string().min(1).max(1024),
  deviceId: z.string().min(1).max(128)
});

const refreshSchema = z.object({
  refreshToken: z.string().min(1).max(1024),
  deviceId: z.string().min(1).max(128),
  deviceName: z.string().min(1).max(128)
});

const reminderStatusSchema = z.enum(["scheduled", "fired", "acked", "snoozed", "cancelled", "missed"]);

const syncReminderSchema = z.object({
  id: z.string().min(1).max(128),
  title: z.string().max(2048),
  dueAtUtc: z.number().int().nonnegative(),
  priority: z.number().int().min(0).max(2),
  createdAtUtc: z.number().int().nonnegative(),
  status: reminderStatusSchema,
  lastFiredAtUtc: z.number().int().nonnegative().nullable(),
  ackedAtUtc: z.number().int().nonnegative().nullable(),
  snoozeUntilUtc: z.number().int().nonnegative().nullable(),
  telegramEscalatedAtUtc: z.number().int().nonnegative().nullable(),
  updatedAtUtc: z.number().int().nonnegative(),
  deletedAtUtc: z.number().int().nonnegative().nullable()
});

const syncBatchSchema = z.object({
  deviceId: z.string().min(1).max(128),
  telegramUserId: z.string().trim().max(128).nullable().optional(),
  changes: z.array(syncReminderSchema).max(200)
});

const webLoginSchema = z.object({
  username: z.string().min(1).max(64),
  password: z.string().min(1).max(256)
});

const webReminderCreateSchema = z.object({
  title: z.string().trim().min(1).max(2048),
  dueAtUtc: z.number().int().positive(),
  priority: z.number().int().min(0).max(2)
});

const webReminderUpdateSchema = z.object({
  title: z.string().trim().min(1).max(2048).optional(),
  dueAtUtc: z.number().int().positive().optional(),
  priority: z.number().int().min(0).max(2).optional()
});

const webSnoozeSchema = z.object({
  minutes: z.number().int().refine((value) => value === 5 || value === 15)
});

const RequestBodyLimitBytes = 256 * 1024;
const WebDeviceName = "NNotify Web/PWA";
const WebAccessCookieName = "nn_access";
const WebRefreshCookieName = "nn_refresh";
const WebDeviceCookieName = "nn_device";
const WebCsrfCookieName = "nn_csrf";
const WebStaticRoot = path.resolve(process.cwd(), "web");

function getClientIp(headers: Record<string, unknown>, fallback: string): string {
  const xff = headers["x-forwarded-for"];
  if (typeof xff === "string" && xff.length > 0) {
    const first = xff.split(",")[0]?.trim();
    if (first) {
      return first;
    }
  }

  return fallback;
}

function toReminderDto(record: SyncReminderRecord): Record<string, unknown> {
  return {
    id: record.id,
    title: record.title,
    dueAtUtc: record.due_at_utc,
    priority: record.priority,
    createdAtUtc: record.created_at_utc,
    status: record.status,
    lastFiredAtUtc: record.last_fired_at_utc,
    ackedAtUtc: record.acked_at_utc,
    snoozeUntilUtc: record.snooze_until_utc,
    telegramEscalatedAtUtc: record.telegram_escalated_at_utc,
    updatedAtUtc: record.updated_at_utc,
    deletedAtUtc: record.deleted_at_utc,
    duplicateCandidate: record.duplicate_candidate === 1
  };
}

function formatReminderDueTime(utcMs: number): string {
  try {
    const parts = new Intl.DateTimeFormat("ru-RU", {
      timeZone: config.reminderTimeZone,
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      hour12: false
    }).formatToParts(new Date(utcMs));

    const value = (type: string) => parts.find((part) => part.type === type)?.value ?? "";
    return `${value("day")}.${value("month")}.${value("year")} ${value("hour")}:${value("minute")}`;
  } catch {
    return new Date(utcMs).toISOString().slice(0, 16).replace("T", " ");
  }
}

function buildReminderTelegramMessage(record: SyncReminderRecord): string {
  const dueLocal = formatReminderDueTime(record.snooze_until_utc ?? record.due_at_utc);
  const header = record.priority === 0
    ? "Напоминание высокой важности 🚨"
    : record.priority === 2
      ? "Напоминание низкой важности 💡"
      : "Напоминание средней важности 🔔";
  return `${header}\n${record.title || "Без текста"}\nЗапланировано: ${dueLocal}`;
}

function parseCookieHeader(header: unknown): Record<string, string> {
  const raw = Array.isArray(header) ? header[0] : header;
  if (typeof raw !== "string" || raw.length === 0) {
    return {};
  }

  const result: Record<string, string> = {};
  for (const chunk of raw.split(";")) {
    const separatorIndex = chunk.indexOf("=");
    if (separatorIndex <= 0) {
      continue;
    }

    const key = chunk.slice(0, separatorIndex).trim();
    const value = chunk.slice(separatorIndex + 1).trim();
    if (!key) {
      continue;
    }

    try {
      result[key] = decodeURIComponent(value);
    } catch {
      result[key] = value;
    }
  }

  return result;
}

function buildCookie(name: string, value: string, options: { maxAgeSeconds?: number; httpOnly?: boolean; secure?: boolean } = {}): string {
  const parts = [
    `${name}=${encodeURIComponent(value)}`,
    "Path=/",
    "SameSite=Lax"
  ];

  if (options.maxAgeSeconds !== undefined) {
    parts.push(`Max-Age=${Math.max(0, Math.floor(options.maxAgeSeconds))}`);
  }

  if (options.httpOnly !== false) {
    parts.push("HttpOnly");
  }

  if (options.secure !== false) {
    parts.push("Secure");
  }

  return parts.join("; ");
}

function buildDeleteCookie(name: string, secure: boolean): string {
  return buildCookie(name, "", { maxAgeSeconds: 0, httpOnly: true, secure });
}

function shouldUseSecureCookie(headers: Record<string, unknown>): boolean {
  const proto = headers["x-forwarded-proto"];
  const forwardedProto = Array.isArray(proto) ? proto[0] : proto;
  return config.nodeEnv === "production" || forwardedProto === "https";
}

function createCsrfToken(): string {
  return randomUUID().replace(/-/g, "");
}

function resolveWebStaticPath(relativePath: string): string | null {
  const normalized = path.normalize(relativePath).replace(/^[/\\]+/, "");
  const resolved = path.resolve(WebStaticRoot, normalized);
  if (resolved === WebStaticRoot || resolved.startsWith(`${WebStaticRoot}${path.sep}`)) {
    return resolved;
  }

  return null;
}

function webMimeType(filePath: string): string {
  const ext = path.extname(filePath).toLowerCase();
  switch (ext) {
    case ".html":
      return "text/html; charset=utf-8";
    case ".css":
      return "text/css; charset=utf-8";
    case ".js":
      return "application/javascript; charset=utf-8";
    case ".json":
    case ".webmanifest":
      return "application/manifest+json; charset=utf-8";
    case ".svg":
      return "image/svg+xml";
    case ".png":
      return "image/png";
    case ".ico":
      return "image/x-icon";
    default:
      return "application/octet-stream";
  }
}

export function buildApp() {
  const db = openDatabase(config.databasePath);
  const store = new AuthStore(db);

  const telegram = new TelegramModerationService(
    config.telegramModerationEnabled,
    config.telegramBotToken,
    config.telegramAdminChatId,
    config.telegramWebhookSecret,
    config.telegramWebhookHeaderSecret,
    config.publicBaseUrl,
    config.telegramAdminUserIds
  );

  const authService = new AuthService(store, telegram);

  const app = Fastify({
    logger: {
      level: config.nodeEnv === "production" ? "info" : "debug",
      redact: {
        paths: [
          "req.headers.authorization",
          "req.body.password",
          "req.body.refreshToken",
          "res.body.accessToken",
          "res.body.refreshToken"
        ],
        remove: true
      }
    },
    trustProxy: config.trustProxy,
    bodyLimit: RequestBodyLimitBytes,
    disableRequestLogging: true
  });

  app.register(sensible);
  app.register(helmet, {
    contentSecurityPolicy: false,
    crossOriginEmbedderPolicy: false
  });
  app.register(rateLimit, {
    global: false,
    max: 120,
    timeWindow: "1 minute"
  });

  app.addHook("onResponse", async (request, reply) => {
    if (reply.statusCode < 400) {
      return;
    }

    const payload = {
      req: {
        method: request.method,
        url: request.url,
        host: request.hostname,
        remoteAddress: request.ip
      },
      res: {
        statusCode: reply.statusCode
      }
    };

    if (reply.statusCode >= 500) {
      app.log.error(payload, "request failed");
    } else {
      app.log.warn(payload, "request rejected");
    }
  });

  app.addHook("onError", async (request, reply, error) => {
    app.log.error({
      err: error,
      req: {
        method: request.method,
        url: request.url,
        host: request.hostname,
        remoteAddress: request.ip
      },
      res: {
        statusCode: reply.statusCode
      }
    }, "request error");
  });

  try {
    authService.cleanupExpiredData();
  } catch (error) {
    app.log.error({ err: error }, "initial cleanup failed");
  }

  const cleanupTimer = setInterval(() => {
    try {
      const result = authService.cleanupExpiredData();
      if (result.sessionsDeleted || result.revokedHistoryDeleted || result.callbacksDeleted || result.auditDeleted || result.rejectedUsersDeleted) {
        app.log.info({ cleanup: result }, "cleanup completed");
      }
    } catch (error) {
      app.log.error({ err: error }, "cleanup failed");
    }
  }, config.sessionCleanupIntervalMinutes * 60 * 1000);
  cleanupTimer.unref();

  const escalationTimer = setInterval(async () => {
    if (!config.reminderTelegramBotToken) {
      return;
    }

    try {
      const nowUtcMs = Date.now();
      const cutoffUtcMs = nowUtcMs - 60_000;
      const candidates = store.listReminderEscalationCandidates(cutoffUtcMs, nowUtcMs, 100);
      for (const reminder of candidates) {
        if (!reminder.telegram_user_id) {
          continue;
        }

        const sent = await telegram.sendReminderMessage(
          config.reminderTelegramBotToken,
          reminder.telegram_user_id,
          buildReminderTelegramMessage(reminder)
        );
        if (sent.ok) {
          store.markSyncReminderEscalated(reminder.user_id, reminder.id, Date.now());
        } else {
          const failedAtUtcMs = Date.now();
          const errorMessage = sent.errorMessage ?? "Telegram reminder send failed.";
          store.markSyncReminderEscalationFailed(
            reminder.user_id,
            reminder.id,
            failedAtUtcMs,
            errorMessage,
            sent.permanentFailure
          );

          const logPayload = {
            userId: reminder.user_id,
            reminderId: reminder.id,
            telegramUserId: reminder.telegram_user_id,
            permanentFailure: sent.permanentFailure,
            errorMessage
          };
          if (sent.permanentFailure) {
            app.log.warn(logPayload, "sync reminder escalation stopped after permanent Telegram failure");
          } else {
            app.log.warn(logPayload, "sync reminder escalation retry scheduled");
          }
        }
      }
    } catch (error) {
      app.log.error({ err: error }, "sync reminder escalation failed");
    }
  }, 10_000);
  escalationTimer.unref();

  app.addHook("onClose", async () => {
    clearInterval(cleanupTimer);
    clearInterval(escalationTimer);
    db.close();
  });

  app.get("/health", async () => ({ ok: true }));

  app.post(
    "/v1/auth/register",
    {
      config: {
        rateLimit: {
          max: 8,
          timeWindow: "1 minute"
        }
      }
    },
    async (request, reply) => {
      const parsed = registerSchema.safeParse(request.body);
      if (!parsed.success) {
        return reply.code(400).send({ message: "Invalid request payload." });
      }

      const ip = getClientIp(request.headers as Record<string, unknown>, request.ip);
      const result = await authService.register({
        ...parsed.data,
        sourceIp: ip
      });
      return reply.code(result.statusCode).send(result.body);
    }
  );

  async function authenticateSyncRequest(request: { headers: Record<string, unknown> }): Promise<{ userId: string; deviceId: string } | null> {
    const raw = request.headers.authorization;
    const header = Array.isArray(raw) ? raw[0] : raw;
    if (typeof header !== "string" || !header.startsWith("Bearer ")) {
      return null;
    }

    try {
      const token = header.slice("Bearer ".length).trim();
      const verified = await verifyAccessToken(token, config.jwtIssuer, config.jwtAudience, config.jwtAccessSecret);
      const user = store.getUserById(verified.userId);
      if (!user || user.status !== "active") {
        return null;
      }

      return { userId: verified.userId, deviceId: verified.deviceId };
    } catch {
      return null;
    }
  }

  function setWebSessionCookies(
    reply: FastifyReply,
    headers: Record<string, unknown>,
    input: { accessToken: string; refreshToken: string; deviceId: string; csrfToken?: string }
  ): void {
    const secure = shouldUseSecureCookie(headers);
    const csrfToken = input.csrfToken ?? createCsrfToken();
    reply.header("Set-Cookie", [
      buildCookie(WebAccessCookieName, input.accessToken, {
        maxAgeSeconds: config.accessTokenTtlSeconds,
        httpOnly: true,
        secure
      }),
      buildCookie(WebRefreshCookieName, input.refreshToken, {
        maxAgeSeconds: config.refreshTokenTtlDays * 24 * 60 * 60,
        httpOnly: true,
        secure
      }),
      buildCookie(WebDeviceCookieName, input.deviceId, {
        maxAgeSeconds: config.refreshTokenTtlDays * 24 * 60 * 60,
        httpOnly: true,
        secure
      }),
      buildCookie(WebCsrfCookieName, csrfToken, {
        maxAgeSeconds: config.refreshTokenTtlDays * 24 * 60 * 60,
        httpOnly: false,
        secure
      })
    ]);
  }

  function clearWebSessionCookies(reply: FastifyReply, headers: Record<string, unknown>): void {
    const secure = shouldUseSecureCookie(headers);
    reply.header("Set-Cookie", [
      buildDeleteCookie(WebAccessCookieName, secure),
      buildDeleteCookie(WebRefreshCookieName, secure),
      buildDeleteCookie(WebDeviceCookieName, secure),
      buildDeleteCookie(WebCsrfCookieName, secure)
    ]);
  }

  function isWebCsrfValid(headers: Record<string, unknown>): boolean {
    const cookies = parseCookieHeader(headers.cookie);
    const rawHeader = headers["x-csrf-token"];
    const csrfHeader = Array.isArray(rawHeader) ? rawHeader[0] : rawHeader;
    return typeof csrfHeader === "string" && csrfHeader.length > 0 && csrfHeader === cookies[WebCsrfCookieName];
  }

  async function authenticateWebRequest(
    request: { headers: Record<string, unknown> },
    reply: FastifyReply,
    options: { allowRefresh?: boolean } = {}
  ): Promise<{ userId: string; username: string; deviceId: string } | null> {
    const cookies = parseCookieHeader(request.headers.cookie);
    const accessToken = cookies[WebAccessCookieName];
    const refreshToken = cookies[WebRefreshCookieName];
    const deviceId = cookies[WebDeviceCookieName];

    if (accessToken) {
      try {
        const verified = await verifyAccessToken(accessToken, config.jwtIssuer, config.jwtAudience, config.jwtAccessSecret);
        const user = store.getUserById(verified.userId);
        if (user?.status === "active") {
          return { userId: verified.userId, username: verified.username, deviceId: verified.deviceId };
        }
      } catch {
        // Access tokens are short-lived. Fall through to refresh-cookie rotation.
      }
    }

    if (!options.allowRefresh || !refreshToken || !deviceId) {
      return null;
    }

    const refreshed = await authService.refresh({
      refreshToken,
      deviceId,
      deviceName: WebDeviceName
    });
    if (refreshed.statusCode !== 200) {
      clearWebSessionCookies(reply, request.headers);
      return null;
    }

    const body = refreshed.body as { accessToken?: unknown; refreshToken?: unknown };
    if (typeof body.accessToken !== "string" || typeof body.refreshToken !== "string") {
      clearWebSessionCookies(reply, request.headers);
      return null;
    }

    setWebSessionCookies(reply, request.headers, {
      accessToken: body.accessToken,
      refreshToken: body.refreshToken,
      deviceId,
      csrfToken: cookies[WebCsrfCookieName] || createCsrfToken()
    });

    try {
      const verified = await verifyAccessToken(body.accessToken, config.jwtIssuer, config.jwtAudience, config.jwtAccessSecret);
      return { userId: verified.userId, username: verified.username, deviceId: verified.deviceId };
    } catch {
      clearWebSessionCookies(reply, request.headers);
      return null;
    }
  }

  function webReminderInputFromRecord(record: SyncReminderRecord, overrides: Partial<{
    title: string;
    dueAtUtc: number;
    priority: number;
    status: ReminderStatus;
    lastFiredAtUtc: number | null;
    ackedAtUtc: number | null;
    snoozeUntilUtc: number | null;
    telegramEscalatedAtUtc: number | null;
    deletedAtUtc: number | null;
  }>) {
    const now = Date.now();
    return {
      id: record.id,
      title: overrides.title ?? record.title,
      dueAtUtc: overrides.dueAtUtc ?? record.due_at_utc,
      priority: overrides.priority ?? record.priority,
      createdAtUtc: record.created_at_utc,
      status: overrides.status ?? record.status,
      lastFiredAtUtc: "lastFiredAtUtc" in overrides ? overrides.lastFiredAtUtc! : record.last_fired_at_utc,
      ackedAtUtc: "ackedAtUtc" in overrides ? overrides.ackedAtUtc! : record.acked_at_utc,
      snoozeUntilUtc: "snoozeUntilUtc" in overrides ? overrides.snoozeUntilUtc! : record.snooze_until_utc,
      telegramEscalatedAtUtc: "telegramEscalatedAtUtc" in overrides ? overrides.telegramEscalatedAtUtc! : record.telegram_escalated_at_utc,
      updatedAtUtc: now,
      deletedAtUtc: "deletedAtUtc" in overrides ? overrides.deletedAtUtc! : record.deleted_at_utc
    };
  }

  app.get("/v1/sync/reminders", async (request, reply) => {
    const auth = await authenticateSyncRequest(request);
    if (!auth) {
      return reply.code(401).send({ message: "Unauthorized" });
    }

    const rawSince = (request.query as { since?: string | number }).since ?? 0;
    const since = Math.max(0, Number(rawSince) || 0);
    const reminders = store.listSyncRemindersSince(auth.userId, since).map(toReminderDto);
    return {
      serverTimeUtc: Date.now(),
      reminders
    };
  });

  app.post("/v1/sync/reminders/batch", async (request, reply) => {
    const auth = await authenticateSyncRequest(request);
    if (!auth) {
      return reply.code(401).send({ message: "Unauthorized" });
    }

    const parsed = syncBatchSchema.safeParse(request.body);
    if (!parsed.success) {
      return reply.code(400).send({ message: "Invalid sync payload." });
    }

    if (parsed.data.telegramUserId !== undefined) {
      const target = parsed.data.telegramUserId?.trim();
      store.setUserTelegramTarget(auth.userId, target ? target : null);
    }

    const applied = parsed.data.changes.map((change) =>
      toReminderDto(store.upsertSyncReminder(auth.userId, parsed.data.deviceId || auth.deviceId, {
        ...change,
        status: change.status as ReminderStatus
      }))
    );

    return {
      serverTimeUtc: Date.now(),
      reminders: applied
    };
  });

  app.post(
    "/v1/auth/login",
    {
      config: {
        rateLimit: {
          max: 12,
          timeWindow: "1 minute"
        }
      }
    },
    async (request, reply) => {
      const parsed = loginSchema.safeParse(request.body);
      if (!parsed.success) {
        return reply.code(400).send({ message: "Invalid request payload." });
      }

      const ip = getClientIp(request.headers as Record<string, unknown>, request.ip);
      const ua = request.headers["user-agent"];

      const result = await authService.login({
        ...parsed.data,
        ip,
        userAgent: typeof ua === "string" ? ua.slice(0, 512) : null
      });

      return reply.code(result.statusCode).send(result.body);
    }
  );

  app.post(
    "/v1/auth/refresh",
    {
      config: {
        rateLimit: {
          max: 30,
          timeWindow: "1 minute"
        }
      }
    },
    async (request, reply) => {
      const parsed = refreshSchema.safeParse(request.body);
      if (!parsed.success) {
        return reply.code(400).send({ message: "Invalid request payload." });
      }

      const result = await authService.refresh(parsed.data);
      return reply.code(result.statusCode).send(result.body);
    }
  );

  app.post(
    "/v1/auth/logout",
    {
      config: {
        rateLimit: {
          max: 20,
          timeWindow: "1 minute"
        }
      }
    },
    async (request, reply) => {
      const parsed = logoutSchema.safeParse(request.body);
      if (!parsed.success) {
        return reply.code(400).send({ message: "Invalid request payload." });
      }

      const result = authService.logout(parsed.data);
      return reply.code(result.statusCode).send(result.body);
    }
  );

  app.post(
    "/v1/web/login",
    {
      config: {
        rateLimit: {
          max: 12,
          timeWindow: "1 minute"
        }
      }
    },
    async (request, reply) => {
      const parsed = webLoginSchema.safeParse(request.body);
      if (!parsed.success) {
        return reply.code(400).send({ message: "Invalid request payload." });
      }

      const cookies = parseCookieHeader(request.headers.cookie);
      const deviceId = cookies[WebDeviceCookieName] || `web-${randomUUID()}`;
      const ip = getClientIp(request.headers as Record<string, unknown>, request.ip);
      const ua = request.headers["user-agent"];
      const result = await authService.login({
        username: parsed.data.username,
        password: parsed.data.password,
        deviceId,
        deviceName: WebDeviceName,
        ip,
        userAgent: typeof ua === "string" ? ua.slice(0, 512) : null
      });

      if (result.statusCode !== 200) {
        return reply.code(result.statusCode).send(result.body);
      }

      const body = result.body as { accessToken?: unknown; refreshToken?: unknown };
      if (typeof body.accessToken !== "string" || typeof body.refreshToken !== "string") {
        return reply.code(500).send({ message: "Invalid server session response." });
      }

      setWebSessionCookies(reply, request.headers as Record<string, unknown>, {
        accessToken: body.accessToken,
        refreshToken: body.refreshToken,
        deviceId
      });

      return reply.send({ ok: true, username: parsed.data.username.trim().toLowerCase() });
    }
  );

  app.get("/v1/web/session", async (request, reply) => {
    const auth = await authenticateWebRequest(request, reply, { allowRefresh: true });
    if (!auth) {
      return reply.code(401).send({ authenticated: false });
    }

    return { authenticated: true, username: auth.username };
  });

  app.post("/v1/web/logout", async (request, reply) => {
    const cookies = parseCookieHeader(request.headers.cookie);
    const refreshToken = cookies[WebRefreshCookieName];
    const deviceId = cookies[WebDeviceCookieName];
    if (refreshToken && deviceId) {
      authService.logout({ refreshToken, deviceId });
    }

    clearWebSessionCookies(reply, request.headers as Record<string, unknown>);
    return { ok: true };
  });

  app.get("/v1/web/reminders", async (request, reply) => {
    const auth = await authenticateWebRequest(request, reply);
    if (!auth) {
      return reply.code(401).send({ message: "Unauthorized" });
    }

    const reminders = store.listSyncRemindersForUser(auth.userId, 500).map(toReminderDto);
    return {
      serverTimeUtc: Date.now(),
      reminders
    };
  });

  app.post("/v1/web/reminders", async (request, reply) => {
    const auth = await authenticateWebRequest(request, reply);
    if (!auth) {
      return reply.code(401).send({ message: "Unauthorized" });
    }

    if (!isWebCsrfValid(request.headers as Record<string, unknown>)) {
      return reply.code(403).send({ message: "CSRF token mismatch." });
    }

    const parsed = webReminderCreateSchema.safeParse(request.body);
    if (!parsed.success) {
      return reply.code(400).send({ message: "Invalid reminder payload." });
    }

    if (parsed.data.dueAtUtc <= Date.now()) {
      return reply.code(400).send({ message: "Reminder time must be in the future." });
    }

    const now = Date.now();
    const reminder = store.upsertSyncReminder(auth.userId, auth.deviceId, {
      id: randomUUID(),
      title: parsed.data.title,
      dueAtUtc: parsed.data.dueAtUtc,
      priority: parsed.data.priority,
      createdAtUtc: now,
      status: "scheduled",
      lastFiredAtUtc: null,
      ackedAtUtc: null,
      snoozeUntilUtc: null,
      telegramEscalatedAtUtc: null,
      updatedAtUtc: now,
      deletedAtUtc: null
    });

    return reply.code(201).send({ reminder: toReminderDto(reminder) });
  });

  app.patch("/v1/web/reminders/:id", async (request, reply) => {
    const auth = await authenticateWebRequest(request, reply);
    if (!auth) {
      return reply.code(401).send({ message: "Unauthorized" });
    }

    if (!isWebCsrfValid(request.headers as Record<string, unknown>)) {
      return reply.code(403).send({ message: "CSRF token mismatch." });
    }

    const reminderId = String((request.params as { id?: string }).id ?? "");
    const existing = store.getSyncReminder(auth.userId, reminderId);
    if (!existing || existing.deleted_at_utc !== null) {
      return reply.code(404).send({ message: "Reminder not found." });
    }

    const parsed = webReminderUpdateSchema.safeParse(request.body);
    if (!parsed.success) {
      return reply.code(400).send({ message: "Invalid reminder payload." });
    }

    if (parsed.data.dueAtUtc !== undefined && parsed.data.dueAtUtc <= Date.now()) {
      return reply.code(400).send({ message: "Reminder time must be in the future." });
    }

    const reminder = store.upsertSyncReminder(auth.userId, auth.deviceId, webReminderInputFromRecord(existing, {
      title: parsed.data.title,
      dueAtUtc: parsed.data.dueAtUtc,
      priority: parsed.data.priority,
      status: "scheduled",
      lastFiredAtUtc: null,
      ackedAtUtc: null,
      snoozeUntilUtc: null,
      telegramEscalatedAtUtc: null,
      deletedAtUtc: null
    }));

    return { reminder: toReminderDto(reminder) };
  });

  app.post("/v1/web/reminders/:id/ack", async (request, reply) => {
    const auth = await authenticateWebRequest(request, reply);
    if (!auth) {
      return reply.code(401).send({ message: "Unauthorized" });
    }

    if (!isWebCsrfValid(request.headers as Record<string, unknown>)) {
      return reply.code(403).send({ message: "CSRF token mismatch." });
    }

    const reminderId = String((request.params as { id?: string }).id ?? "");
    const existing = store.getSyncReminder(auth.userId, reminderId);
    if (!existing || existing.deleted_at_utc !== null) {
      return reply.code(404).send({ message: "Reminder not found." });
    }

    const now = Date.now();
    const reminder = store.upsertSyncReminder(auth.userId, auth.deviceId, webReminderInputFromRecord(existing, {
      status: "acked",
      ackedAtUtc: now,
      snoozeUntilUtc: null
    }));

    return { reminder: toReminderDto(reminder) };
  });

  app.post("/v1/web/reminders/:id/snooze", async (request, reply) => {
    const auth = await authenticateWebRequest(request, reply);
    if (!auth) {
      return reply.code(401).send({ message: "Unauthorized" });
    }

    if (!isWebCsrfValid(request.headers as Record<string, unknown>)) {
      return reply.code(403).send({ message: "CSRF token mismatch." });
    }

    const reminderId = String((request.params as { id?: string }).id ?? "");
    const existing = store.getSyncReminder(auth.userId, reminderId);
    if (!existing || existing.deleted_at_utc !== null) {
      return reply.code(404).send({ message: "Reminder not found." });
    }

    const parsed = webSnoozeSchema.safeParse(request.body);
    if (!parsed.success) {
      return reply.code(400).send({ message: "Invalid snooze payload." });
    }

    const reminder = store.upsertSyncReminder(auth.userId, auth.deviceId, webReminderInputFromRecord(existing, {
      status: "snoozed",
      snoozeUntilUtc: Date.now() + parsed.data.minutes * 60 * 1000,
      ackedAtUtc: null
    }));

    return { reminder: toReminderDto(reminder) };
  });

  app.delete("/v1/web/reminders/:id", async (request, reply) => {
    const auth = await authenticateWebRequest(request, reply);
    if (!auth) {
      return reply.code(401).send({ message: "Unauthorized" });
    }

    if (!isWebCsrfValid(request.headers as Record<string, unknown>)) {
      return reply.code(403).send({ message: "CSRF token mismatch." });
    }

    const reminderId = String((request.params as { id?: string }).id ?? "");
    const existing = store.getSyncReminder(auth.userId, reminderId);
    if (!existing || existing.deleted_at_utc !== null) {
      return reply.code(404).send({ message: "Reminder not found." });
    }

    const now = Date.now();
    const isHistoryItem = existing.status === "acked" || existing.status === "cancelled" || existing.status === "missed";
    const reminder = store.upsertSyncReminder(auth.userId, auth.deviceId, webReminderInputFromRecord(existing, isHistoryItem
      ? {
          deletedAtUtc: now
        }
      : {
          status: "cancelled",
          ackedAtUtc: now,
          snoozeUntilUtc: null,
          deletedAtUtc: null
        }));

    return { reminder: toReminderDto(reminder) };
  });

  type AdminSectionKey = "main" | "active" | "blocked" | "deleted" | "pending";

  function sectionTitle(section: AdminSectionKey): string {
    switch (section) {
      case "active":
        return "✅ Зарегистрированные пользователи";
      case "blocked":
        return "⛔ Заблокированные пользователи";
      case "deleted":
        return "🗑 Удаленные пользователи";
      case "pending":
        return "🕓 Ожидают подтверждения";
      default:
        return "🧭 Админка NNotify";
    }
  }

  function sectionStatus(section: AdminSectionKey): UserStatus | null {
    switch (section) {
      case "active":
        return "active";
      case "blocked":
        return "blocked";
      case "deleted":
        return "rejected";
      case "pending":
        return "pending";
      default:
        return null;
    }
  }

  function sectionActionRows(section: AdminSectionKey, userId: string): Array<Array<{ text: string; callback_data: string }>> {
    if (section === "active") {
      return [[
        { text: "⛔ Заблокировать", callback_data: `act:block:${userId}:active` },
        { text: "🗑 Удалить", callback_data: `act:delete:${userId}:active` }
      ]];
    }

    if (section === "blocked") {
      return [[
        { text: "✅ Разблокировать", callback_data: `act:unblock:${userId}:blocked` },
        { text: "🗑 Удалить", callback_data: `act:delete:${userId}:blocked` }
      ]];
    }

    if (section === "deleted") {
      return [[
        { text: "♻️ Восстановить", callback_data: `act:restore:${userId}:deleted` }
      ]];
    }

    if (section === "pending") {
      return [[
        { text: "✅ Подтвердить", callback_data: `act:approve:${userId}:pending` },
        { text: "🗑 Удалить", callback_data: `act:reject:${userId}:pending` }
      ]];
    }

    return [];
  }

  function buildAdminSectionView(section: AdminSectionKey): { text: string; keyboard?: Record<string, unknown> } {
    if (section === "main") {
      return {
        text: "🧭 *Главное меню*\nИспользуйте кнопки внизу для перехода по разделам ⬇️"
      };
    }

    const status = sectionStatus(section);
    const users = status ? authService.listUsers(status).slice(0, 20) : [];
    const header = `*${sectionTitle(section)}*`;

    const lines: string[] = [header, ""];
    if (users.length === 0) {
      lines.push("Пока пусто.");
    } else {
      users.forEach((user, index) => {
        lines.push(`${index + 1}. \`${user.username}\``);
      });
    }

    const keyboardRows: Array<Array<{ text: string; callback_data: string }>> = [];
    for (const user of users) {
      keyboardRows.push([{ text: `👤 ${user.username}`, callback_data: `noop:${user.id}` }]);
      keyboardRows.push(...sectionActionRows(section, user.id));
    }

    return {
      text: lines.join("\n"),
      keyboard: { inline_keyboard: keyboardRows }
    };
  }

  function localizeAdminActionResult(action: string, ok: boolean, username: string): string {
    if (!ok) {
      return `⚠️ Операция не выполнена: ${username}`;
    }

    switch (action) {
      case "approve":
        return `✅ Пользователь \`${username}\` подтвержден.`;
      case "reject":
      case "delete":
        return `🗑 Пользователь \`${username}\` удален.`;
      case "block":
        return `⛔ Пользователь \`${username}\` заблокирован.`;
      case "unblock":
        return `✅ Пользователь \`${username}\` разблокирован.`;
      case "restore":
        return `♻️ Пользователь \`${username}\` восстановлен.`;
      default:
        return `ℹ️ Операция выполнена для пользователя \`${username}\`.`;
    }
  }

  app.post("/v1/telegram/webhook/:secret", async (request, reply) => {
    const secret = String((request.params as { secret?: string }).secret ?? "");
    if (!config.telegramModerationEnabled || !telegram.isConfigured) {
      return reply.code(404).send({ message: "Not found" });
    }

    if (secret !== config.telegramWebhookSecret) {
      return reply.code(403).send({ message: "Forbidden" });
    }

    if (config.telegramWebhookHeaderSecret) {
      const rawHeader = request.headers["x-telegram-bot-api-secret-token"];
      const headerSecret = Array.isArray(rawHeader) ? rawHeader[0] : rawHeader;
      if (headerSecret !== config.telegramWebhookHeaderSecret) {
        return reply.code(403).send({ message: "Forbidden" });
      }
    }

    const body = request.body as Record<string, unknown>;

    const message = body?.message as Record<string, unknown> | undefined;
    if (message) {
      const from = message.from as Record<string, unknown> | undefined;
      const fromId = from?.id === undefined ? "" : String(from.id);
      if (!telegram.isAdminUser(fromId)) {
        return reply.code(200).send({ ok: true });
      }

      const chat = message.chat as Record<string, unknown> | undefined;
      const chatId = chat?.id === undefined ? "" : String(chat.id);
      const text = String(message.text ?? "");
      if (!chatId) {
        return reply.code(200).send({ ok: true });
      }

      if (telegram.isAdminMenuCommand(text)) {
        await telegram.sendPersistentMenuHint(chatId);
        await telegram.sendAdminMenu(chatId);
        return reply.code(200).send({ ok: true });
      }

      const sectionFromMessage = telegram.resolveSectionFromText(text);
      if (!sectionFromMessage) {
        return reply.code(200).send({ ok: true });
      }

      const view = buildAdminSectionView(sectionFromMessage);
      await telegram.sendMessage(chatId, view.text, view.keyboard);
      return reply.code(200).send({ ok: true });
    }

    const callbackQuery = body?.callback_query as Record<string, unknown> | undefined;
    if (!callbackQuery) {
      return reply.code(200).send({ ok: true });
    }

    const callbackId = String(callbackQuery.id ?? "");
    if (!callbackId) {
      return reply.code(200).send({ ok: true });
    }

    if (store.isTelegramCallbackProcessed(callbackId)) {
      return reply.code(200).send({ ok: true });
    }

    const from = callbackQuery.from as Record<string, unknown> | undefined;
    const fromId = from?.id === undefined ? "" : String(from.id);

    if (!telegram.isAdminUser(fromId)) {
      await telegram.answerCallback(callbackId, "Not allowed");
      store.markTelegramCallbackProcessed(callbackId);
      return reply.code(200).send({ ok: true });
    }

    const messageNode = callbackQuery.message as Record<string, unknown> | undefined;
    const messageId = Number(messageNode?.message_id ?? 0);
    const callbackChat = messageNode?.chat as Record<string, unknown> | undefined;
    const callbackChatId = callbackChat?.id === undefined ? "" : String(callbackChat.id);

    const data = String(callbackQuery.data ?? "");
    if (!data) {
      await telegram.answerCallback(callbackId, "Invalid action");
      store.markTelegramCallbackProcessed(callbackId);
      return reply.code(200).send({ ok: true });
    }

    if (data.startsWith("noop:")) {
      await telegram.answerCallback(callbackId, "Выберите действие ниже");
      store.markTelegramCallbackProcessed(callbackId);
      return reply.code(200).send({ ok: true });
    }

    if (data.startsWith("menu:")) {
      const rawSection = data.slice("menu:".length);
      const section = (["main", "active", "blocked", "deleted", "pending"] as const).includes(rawSection as AdminSectionKey)
        ? (rawSection as AdminSectionKey)
        : "main";

      const view = buildAdminSectionView(section);
      if (callbackChatId && Number.isFinite(messageId) && messageId > 0) {
        try {
          await telegram.editMessage(callbackChatId, messageId, view.text, view.keyboard);
        } catch {
          await telegram.sendMessage(callbackChatId, view.text, view.keyboard);
        }
      } else if (callbackChatId) {
        await telegram.sendMessage(callbackChatId, view.text, view.keyboard);
      }

      await telegram.answerCallback(callbackId, "Открыто");
      store.markTelegramCallbackProcessed(callbackId);
      return reply.code(200).send({ ok: true });
    }

    const legacyParts = data.split(":");
    if (legacyParts.length === 2 && (legacyParts[0] === "approve" || legacyParts[0] === "reject")) {
      const legacyAction = legacyParts[0];
      const legacyUserId = legacyParts[1] ?? "";
      const actor = `tg:${fromId}`;
      const legacyResult = legacyAction === "approve"
        ? authService.approvePendingUserById(legacyUserId, actor)
        : authService.rejectPendingUserById(legacyUserId, actor);

      await telegram.answerCallback(callbackId, legacyResult.ok ? "Готово" : "Ошибка");
      if (callbackChatId) {
        await telegram.sendMessage(callbackChatId, localizeAdminActionResult(legacyAction, legacyResult.ok, legacyResult.username ?? "unknown"));
        const view = buildAdminSectionView("pending");
        if (Number.isFinite(messageId) && messageId > 0) {
          await telegram.editMessage(callbackChatId, messageId, view.text, view.keyboard);
        }
      }

      store.markTelegramCallbackProcessed(callbackId);
      return reply.code(200).send({ ok: true });
    }

    if (data.startsWith("act:")) {
      const parts = data.split(":");
      const action = parts[1] ?? "";
      const userId = parts[2] ?? "";
      const returnSection = (parts[3] as AdminSectionKey | undefined) ?? "main";

      if (!action || !userId) {
        await telegram.answerCallback(callbackId, "Invalid action");
        store.markTelegramCallbackProcessed(callbackId);
        return reply.code(200).send({ ok: true });
      }

      const actor = `tg:${fromId}`;
      let result: { ok: boolean; message: string; username?: string };
      switch (action) {
        case "approve":
          result = authService.approvePendingUserById(userId, actor);
          break;
        case "reject":
          result = authService.rejectPendingUserById(userId, actor);
          break;
        case "block":
          result = authService.blockUserById(userId, actor);
          break;
        case "unblock":
          result = authService.unblockUserById(userId, actor);
          break;
        case "delete":
          result = authService.deleteUserById(userId, actor);
          break;
        case "restore":
          result = authService.restoreDeletedUserById(userId, actor);
          break;
        default:
          await telegram.answerCallback(callbackId, "Unknown action");
          store.markTelegramCallbackProcessed(callbackId);
          return reply.code(200).send({ ok: true });
      }

      const username = result.username ?? "unknown";
      await telegram.answerCallback(callbackId, result.ok ? "Готово" : "Ошибка");

      if (callbackChatId) {
        await telegram.sendMessage(callbackChatId, localizeAdminActionResult(action, result.ok, username));
      }

      const view = buildAdminSectionView(returnSection);
      if (callbackChatId && Number.isFinite(messageId) && messageId > 0) {
        try {
          await telegram.editMessage(callbackChatId, messageId, view.text, view.keyboard);
        } catch {
          await telegram.sendMessage(callbackChatId, view.text, view.keyboard);
        }
      }

      store.markTelegramCallbackProcessed(callbackId);
      return reply.code(200).send({ ok: true });
    }

    await telegram.answerCallback(callbackId, "Unknown action");
    store.markTelegramCallbackProcessed(callbackId);
    return reply.code(200).send({ ok: true });
  });

  async function serveWebFile(reply: FastifyReply, relativePath: string) {
    const requestedPath = relativePath === "/" || relativePath === "" ? "index.html" : relativePath.replace(/^\/+/, "");
    const filePath = resolveWebStaticPath(requestedPath);
    if (!filePath) {
      return reply.code(404).send({ message: "Not found" });
    }

    try {
      const content = await readFile(filePath);
      reply.type(webMimeType(filePath));
      if (requestedPath === "index.html" || requestedPath === "app.js" || requestedPath === "styles.css") {
        reply.header("Cache-Control", "no-cache");
      } else {
        reply.header("Cache-Control", "public, max-age=86400");
      }
      return reply.send(content);
    } catch {
      return reply.code(404).send({ message: "Not found" });
    }
  }

  app.get("/", async (_request, reply) => serveWebFile(reply, "index.html"));
  app.get("/index.html", async (_request, reply) => serveWebFile(reply, "index.html"));
  app.get("/styles.css", async (_request, reply) => serveWebFile(reply, "styles.css"));
  app.get("/app.js", async (_request, reply) => serveWebFile(reply, "app.js"));
  app.get("/manifest.webmanifest", async (_request, reply) => serveWebFile(reply, "manifest.webmanifest"));
  app.get("/sw.js", async (_request, reply) => serveWebFile(reply, "sw.js"));
  app.get("/favicon.ico", async (_request, reply) => serveWebFile(reply, "icons/icon-192.png"));
  app.get("/icons/:name", async (request, reply) => {
    const name = String((request.params as { name?: string }).name ?? "");
    return serveWebFile(reply, `icons/${name}`);
  });

  return { app, authService, store, telegram, db };
}
