import { randomUUID } from "node:crypto";
import type { SqliteDb } from "./db.js";
import type { ReminderStatus, SessionRecord, SyncReminderRecord, UserRecord, UserStatus } from "./types.js";
import { nowIso } from "./security.js";

interface AuditInput {
  actorType: "system" | "admin" | "user";
  actorId?: string | null;
  action: string;
  targetId?: string | null;
  details?: unknown;
}

export interface SyncReminderInput {
  id: string;
  title: string;
  dueAtUtc: number;
  priority: number;
  createdAtUtc: number;
  status: ReminderStatus;
  lastFiredAtUtc: number | null;
  ackedAtUtc: number | null;
  snoozeUntilUtc: number | null;
  telegramEscalatedAtUtc: number | null;
  updatedAtUtc: number;
  deletedAtUtc: number | null;
}

function isTerminalStatus(status: ReminderStatus): boolean {
  return status === "acked" || status === "cancelled";
}

function shouldKeepExistingReminder(existing: SyncReminderRecord, incoming: SyncReminderInput): boolean {
  if (existing.client_updated_at_utc > incoming.updatedAtUtc) {
    return true;
  }

  if (isTerminalStatus(existing.status) && !isTerminalStatus(incoming.status)) {
    return true;
  }

  return false;
}

function normalizeServerTimestamp(candidate: number): number {
  const now = Date.now();
  return candidate >= now ? candidate + 1 : now;
}

export class AuthStore {
  constructor(private readonly db: SqliteDb) {}

  getUserByUsername(username: string): UserRecord | null {
    const row = this.db
      .prepare("SELECT * FROM users WHERE username = ? LIMIT 1")
      .get(username) as UserRecord | undefined;
    return row ?? null;
  }

  getUserById(userId: string): UserRecord | null {
    const row = this.db
      .prepare("SELECT * FROM users WHERE id = ? LIMIT 1")
      .get(userId) as UserRecord | undefined;
    return row ?? null;
  }

  createPendingUser(username: string, passwordHash: string): { created: boolean; status?: UserStatus } {
    const existing = this.getUserByUsername(username);
    if (existing) {
      return { created: false, status: existing.status };
    }

    const id = randomUUID();
    const now = nowIso();

    this.db
      .prepare(
        `INSERT INTO users (id, username, password_hash, status, created_at, updated_at)
         VALUES (?, ?, ?, 'pending', ?, ?)`
      )
      .run(id, username, passwordHash, now, now);

    this.addAudit({
      actorType: "system",
      action: "auth.register.pending",
      targetId: id,
      details: { username }
    });

    return { created: true };
  }

  requeueRejectedUser(username: string, passwordHash: string): { success: boolean; userId?: string; status?: UserStatus } {
    const user = this.getUserByUsername(username);
    if (!user) {
      return { success: false };
    }

    if (user.status !== "rejected") {
      return { success: false, status: user.status };
    }

    const now = nowIso();
    this.db
      .prepare(
        `UPDATE users
         SET status='pending',
             password_hash=?,
             updated_at=?,
             approved_at=NULL,
             approved_by=NULL,
             rejected_at=NULL,
             rejected_by=NULL,
             reject_reason=NULL,
             failed_login_attempts=0,
             locked_until_utc=NULL,
             last_failed_login_at=NULL
         WHERE id=?`
      )
      .run(passwordHash, now, user.id);

    this.addAudit({
      actorType: "system",
      action: "auth.register.requeued",
      targetId: user.id,
      details: { username: user.username }
    });

    return { success: true, userId: user.id, status: "pending" };
  }

  createActiveUserByAdmin(username: string, passwordHash: string, adminActor: string): { created: boolean; status?: UserStatus } {
    const existing = this.getUserByUsername(username);
    if (existing) {
      return { created: false, status: existing.status };
    }

    const id = randomUUID();
    const now = nowIso();

    this.db
      .prepare(
        `INSERT INTO users (
          id, username, password_hash, status, created_at, updated_at, approved_at, approved_by
        ) VALUES (?, ?, ?, 'active', ?, ?, ?, ?)`
      )
      .run(id, username, passwordHash, now, now, now, adminActor);

    this.addAudit({
      actorType: "admin",
      actorId: adminActor,
      action: "admin.user.create.active",
      targetId: id,
      details: { username }
    });

    return { created: true };
  }

  approveUser(username: string, adminActor: string): { success: boolean; reason?: string } {
    const user = this.getUserByUsername(username);
    if (!user) {
      return { success: false, reason: "not_found" };
    }

    if (user.status === "active") {
      return { success: false, reason: "already_active" };
    }

    if (user.status === "blocked") {
      return { success: false, reason: "blocked" };
    }

    const now = nowIso();
    this.db
      .prepare(
        `UPDATE users
         SET status='active', updated_at=?, approved_at=?, approved_by=?, rejected_at=NULL, rejected_by=NULL, reject_reason=NULL,
             failed_login_attempts=0, locked_until_utc=NULL, last_failed_login_at=NULL
         WHERE id=?`
      )
      .run(now, now, adminActor, user.id);

    this.addAudit({
      actorType: "admin",
      actorId: adminActor,
      action: "admin.user.approve",
      targetId: user.id,
      details: { username: user.username }
    });

    return { success: true };
  }

  rejectUser(username: string, adminActor: string, reason?: string): { success: boolean; reason?: string } {
    const user = this.getUserByUsername(username);
    if (!user) {
      return { success: false, reason: "not_found" };
    }

    const now = nowIso();
    this.db
      .prepare(
        `UPDATE users
         SET status='rejected', updated_at=?, rejected_at=?, rejected_by=?, reject_reason=?
         WHERE id=?`
      )
      .run(now, now, adminActor, reason ?? null, user.id);

    this.addAudit({
      actorType: "admin",
      actorId: adminActor,
      action: "admin.user.reject",
      targetId: user.id,
      details: { username: user.username, reason: reason ?? null }
    });

    return { success: true };
  }

  blockUser(username: string, adminActor: string): { success: boolean; reason?: string } {
    const user = this.getUserByUsername(username);
    if (!user) {
      return { success: false, reason: "not_found" };
    }

    const now = nowIso();
    this.db
      .prepare("UPDATE users SET status='blocked', updated_at=? WHERE id=?")
      .run(now, user.id);

    this.db
      .prepare("UPDATE sessions SET revoked_at=?, revoked_reason='user_blocked' WHERE user_id=? AND revoked_at IS NULL")
      .run(now, user.id);

    this.addAudit({
      actorType: "admin",
      actorId: adminActor,
      action: "admin.user.block",
      targetId: user.id,
      details: { username: user.username }
    });

    return { success: true };
  }

  unblockUser(username: string, adminActor: string): { success: boolean; reason?: string } {
    const user = this.getUserByUsername(username);
    if (!user) {
      return { success: false, reason: "not_found" };
    }

    if (user.status !== "blocked") {
      return { success: false, reason: "not_blocked" };
    }

    const now = nowIso();
    this.db
      .prepare(
        `UPDATE users
         SET status='active', updated_at=?, approved_at=?, approved_by=?, rejected_at=NULL, rejected_by=NULL, reject_reason=NULL,
             failed_login_attempts=0, locked_until_utc=NULL, last_failed_login_at=NULL
         WHERE id=?`
      )
      .run(now, now, adminActor, user.id);

    this.addAudit({
      actorType: "admin",
      actorId: adminActor,
      action: "admin.user.unblock",
      targetId: user.id,
      details: { username: user.username }
    });

    return { success: true };
  }

  restoreRejectedUser(username: string, adminActor: string): { success: boolean; reason?: string } {
    const user = this.getUserByUsername(username);
    if (!user) {
      return { success: false, reason: "not_found" };
    }

    if (user.status !== "rejected") {
      return { success: false, reason: "not_rejected" };
    }

    const now = nowIso();
    this.db
      .prepare(
        `UPDATE users
         SET status='active', updated_at=?, approved_at=?, approved_by=?, rejected_at=NULL, rejected_by=NULL, reject_reason=NULL,
             failed_login_attempts=0, locked_until_utc=NULL, last_failed_login_at=NULL
         WHERE id=?`
      )
      .run(now, now, adminActor, user.id);

    this.addAudit({
      actorType: "admin",
      actorId: adminActor,
      action: "admin.user.restore",
      targetId: user.id,
      details: { username: user.username }
    });

    return { success: true };
  }

  listUsers(status?: UserStatus): UserRecord[] {
    if (status) {
      return this.db
        .prepare("SELECT * FROM users WHERE status=? ORDER BY created_at DESC")
        .all(status) as UserRecord[];
    }

    return this.db.prepare("SELECT * FROM users ORDER BY created_at DESC").all() as UserRecord[];
  }

  setUserTelegramTarget(userId: string, telegramUserId: string | null): void {
    this.db
      .prepare("UPDATE users SET telegram_user_id=?, updated_at=? WHERE id=?")
      .run(telegramUserId, nowIso(), userId);
  }

  listSyncRemindersSince(userId: string, sinceUtcMs: number): SyncReminderRecord[] {
    return this.db
      .prepare(
        `SELECT * FROM sync_reminders
         WHERE user_id=? AND updated_at_utc > ?
         ORDER BY updated_at_utc ASC`
      )
      .all(userId, sinceUtcMs) as SyncReminderRecord[];
  }

  listSyncRemindersForUser(userId: string, limit = 500): SyncReminderRecord[] {
    return this.db
      .prepare(
        `SELECT * FROM sync_reminders
         WHERE user_id=?
         ORDER BY updated_at_utc DESC
         LIMIT ?`
      )
      .all(userId, limit) as SyncReminderRecord[];
  }

  getSyncReminder(userId: string, reminderId: string): SyncReminderRecord | null {
    const row = this.db
      .prepare("SELECT * FROM sync_reminders WHERE user_id=? AND id=? LIMIT 1")
      .get(userId, reminderId) as SyncReminderRecord | undefined;
    return row ?? null;
  }

  upsertSyncReminder(userId: string, deviceId: string, input: SyncReminderInput): SyncReminderRecord {
    const existing = this.getSyncReminder(userId, input.id);
    if (existing && shouldKeepExistingReminder(existing, input)) {
      return existing;
    }

    const duplicateCandidate = this.hasPotentialDuplicate(userId, input.id, input.title, input.dueAtUtc) ? 1 : 0;
    const serverUpdatedAtUtc = normalizeServerTimestamp(existing?.updated_at_utc ?? 0);

    if (existing) {
      this.db
        .prepare(
          `UPDATE sync_reminders
           SET title=?,
               due_at_utc=?,
               priority=?,
               created_at_utc=?,
               status=?,
               last_fired_at_utc=?,
               acked_at_utc=?,
               snooze_until_utc=?,
               telegram_escalated_at_utc=?,
               updated_at_utc=?,
               client_updated_at_utc=?,
               deleted_at_utc=?,
               source_device_id=?,
               duplicate_candidate=?
           WHERE user_id=? AND id=?`
        )
        .run(
          input.title,
          input.dueAtUtc,
          input.priority,
          input.createdAtUtc,
          input.status,
          input.lastFiredAtUtc,
          input.ackedAtUtc,
          input.snoozeUntilUtc,
          input.telegramEscalatedAtUtc,
          serverUpdatedAtUtc,
          input.updatedAtUtc,
          input.deletedAtUtc,
          deviceId,
          duplicateCandidate,
          userId,
          input.id
        );
    } else {
      this.db
        .prepare(
          `INSERT INTO sync_reminders (
            id, user_id, title, due_at_utc, priority, created_at_utc, status,
            last_fired_at_utc, acked_at_utc, snooze_until_utc, telegram_escalated_at_utc,
            updated_at_utc, client_updated_at_utc, deleted_at_utc, source_device_id, duplicate_candidate
          ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`
        )
        .run(
          input.id,
          userId,
          input.title,
          input.dueAtUtc,
          input.priority,
          input.createdAtUtc,
          input.status,
          input.lastFiredAtUtc,
          input.ackedAtUtc,
          input.snoozeUntilUtc,
          input.telegramEscalatedAtUtc,
          serverUpdatedAtUtc,
          input.updatedAtUtc,
          input.deletedAtUtc,
          deviceId,
          duplicateCandidate
        );
    }

    if (duplicateCandidate) {
      this.markPotentialDuplicates(userId, input.title, input.dueAtUtc, serverUpdatedAtUtc);
    }

    return this.getSyncReminder(userId, input.id)!;
  }

  listReminderEscalationCandidates(cutoffUtcMs: number, nowUtcMs: number, limit = 100): Array<SyncReminderRecord & { telegram_user_id: string | null }> {
    return this.db
      .prepare(
        `SELECT r.*, u.telegram_user_id
         FROM sync_reminders r
         JOIN users u ON u.id = r.user_id
         WHERE r.deleted_at_utc IS NULL
           AND r.telegram_escalated_at_utc IS NULL
           AND r.acked_at_utc IS NULL
           AND r.status IN ('scheduled','snoozed','fired','missed')
           AND COALESCE(r.snooze_until_utc, r.due_at_utc) <= ?
           AND r.telegram_escalation_attempts < 3
           AND (
             r.telegram_escalation_next_retry_utc IS NULL
             OR r.telegram_escalation_next_retry_utc <= ?
           )
         ORDER BY COALESCE(r.snooze_until_utc, r.due_at_utc) ASC
         LIMIT ?`
      )
      .all(cutoffUtcMs, nowUtcMs, limit) as Array<SyncReminderRecord & { telegram_user_id: string | null }>;
  }

  markSyncReminderEscalated(userId: string, reminderId: string, escalatedAtUtcMs: number): void {
    this.db
      .prepare(
        `UPDATE sync_reminders
         SET status='missed',
             telegram_escalated_at_utc=?,
             telegram_escalation_attempts=0,
             telegram_escalation_next_retry_utc=NULL,
             telegram_escalation_last_error=NULL,
             updated_at_utc=?
         WHERE user_id=? AND id=? AND telegram_escalated_at_utc IS NULL`
      )
      .run(escalatedAtUtcMs, escalatedAtUtcMs, userId, reminderId);
  }

  markSyncReminderEscalationFailed(
    userId: string,
    reminderId: string,
    failedAtUtcMs: number,
    errorMessage: string,
    permanentFailure: boolean
  ): void {
    const existing = this.getSyncReminder(userId, reminderId);
    if (!existing || existing.telegram_escalated_at_utc !== null) {
      return;
    }

    const attempts = Math.min((existing.telegram_escalation_attempts ?? 0) + 1, 3);
    const nextRetryUtc = permanentFailure || attempts >= 3
      ? null
      : failedAtUtcMs + this.telegramEscalationBackoffMs(attempts);
    const normalizedError = errorMessage.slice(0, 512);

    this.db
      .prepare(
        `UPDATE sync_reminders
         SET status='missed',
             telegram_escalation_attempts=?,
             telegram_escalation_next_retry_utc=?,
             telegram_escalation_last_error=?,
             updated_at_utc=?
         WHERE user_id=? AND id=? AND telegram_escalated_at_utc IS NULL`
      )
      .run(
        permanentFailure ? 3 : attempts,
        nextRetryUtc,
        normalizedError,
        failedAtUtcMs,
        userId,
        reminderId
      );
  }

  private telegramEscalationBackoffMs(attempts: number): number {
    switch (attempts) {
      case 1:
        return 60_000;
      case 2:
        return 5 * 60_000;
      default:
        return 30 * 60_000;
    }
  }

  private hasPotentialDuplicate(userId: string, reminderId: string, title: string, dueAtUtc: number): boolean {
    const row = this.db
      .prepare(
        `SELECT id FROM sync_reminders
         WHERE user_id=?
           AND id<>?
           AND deleted_at_utc IS NULL
           AND due_at_utc=?
           AND lower(trim(title))=lower(trim(?))
           AND status NOT IN ('acked','cancelled')
         LIMIT 1`
      )
      .get(userId, reminderId, dueAtUtc, title) as { id: string } | undefined;
    return !!row;
  }

  private markPotentialDuplicates(userId: string, title: string, dueAtUtc: number, serverUpdatedAtUtc: number): void {
    this.db
      .prepare(
        `UPDATE sync_reminders
         SET duplicate_candidate=1,
             updated_at_utc=MAX(updated_at_utc, ?)
         WHERE user_id=?
           AND deleted_at_utc IS NULL
           AND due_at_utc=?
           AND lower(trim(title))=lower(trim(?))
           AND status NOT IN ('acked','cancelled')`
      )
      .run(serverUpdatedAtUtc, userId, dueAtUtc, title);
  }

  createSession(input: {
    userId: string;
    deviceId: string;
    deviceName: string;
    refreshTokenHash: string;
    expiresAt: string;
    ip?: string | null;
    userAgent?: string | null;
  }): { id: string } {
    const now = nowIso();
    const id = randomUUID();

    this.db
      .prepare(
        `INSERT INTO sessions (
          id, user_id, device_id, device_name, refresh_token_hash,
          created_at, expires_at, ip, user_agent
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)`
      )
      .run(
        id,
        input.userId,
        input.deviceId,
        input.deviceName,
        input.refreshTokenHash,
        now,
        input.expiresAt,
        input.ip ?? null,
        input.userAgent ?? null
      );

    this.addAudit({
      actorType: "user",
      actorId: input.userId,
      action: "auth.login.session_created",
      targetId: id,
      details: { deviceId: input.deviceId, deviceName: input.deviceName }
    });

    return { id };
  }

  findSessionByTokenHash(refreshTokenHash: string): SessionRecord | null {
    const row = this.db
      .prepare("SELECT * FROM sessions WHERE refresh_token_hash = ? LIMIT 1")
      .get(refreshTokenHash) as SessionRecord | undefined;

    return row ?? null;
  }

  findRefreshTokenHistory(tokenHash: string): { token_hash: string; user_id: string; session_id: string | null; reason: string } | null {
    const row = this.db
      .prepare("SELECT token_hash, user_id, session_id, reason FROM refresh_token_history WHERE token_hash = ? LIMIT 1")
      .get(tokenHash) as { token_hash: string; user_id: string; session_id: string | null; reason: string } | undefined;
    return row ?? null;
  }

  rotateSessionRefreshToken(
    sessionId: string,
    expectedCurrentHash: string,
    nextHash: string,
    nextExpiresAt: string
  ): boolean {
    const now = nowIso();
    const tx = this.db.transaction(() => {
      const session = this.db
        .prepare("SELECT id, user_id, refresh_token_hash FROM sessions WHERE id=? AND revoked_at IS NULL LIMIT 1")
        .get(sessionId) as { id: string; user_id: string; refresh_token_hash: string } | undefined;

      if (!session || session.refresh_token_hash !== expectedCurrentHash) {
        return false;
      }

      this.db
        .prepare(
          "INSERT OR IGNORE INTO refresh_token_history(token_hash, user_id, session_id, first_seen_at, reason) VALUES (?, ?, ?, ?, ?)"
        )
        .run(session.refresh_token_hash, session.user_id, session.id, now, "rotated");

      const result = this.db
        .prepare(
          `UPDATE sessions
           SET refresh_token_hash=?, expires_at=?, last_used_at=?
           WHERE id=? AND refresh_token_hash=? AND revoked_at IS NULL`
        )
        .run(nextHash, nextExpiresAt, now, sessionId, expectedCurrentHash);

      return result.changes > 0;
    });

    return tx();
  }

  revokeAllSessionsForUser(userId: string, reason: string): number {
    const now = nowIso();
    const result = this.db
      .prepare("UPDATE sessions SET revoked_at=?, revoked_reason=? WHERE user_id=? AND revoked_at IS NULL")
      .run(now, reason, userId);

    if (result.changes > 0) {
      this.addAudit({
        actorType: "system",
        actorId: userId,
        action: "auth.sessions.revoke_all",
        details: { reason, count: result.changes }
      });
    }

    return result.changes;
  }

  markFailedLoginAttempt(userId: string, lockedUntilUtc: string | null): number {
    const now = nowIso();
    this.db
      .prepare(
        `UPDATE users
         SET failed_login_attempts = failed_login_attempts + 1,
             last_failed_login_at = ?,
             updated_at = ?,
             locked_until_utc = COALESCE(?, locked_until_utc)
         WHERE id = ?`
      )
      .run(now, now, lockedUntilUtc, userId);

    const row = this.db
      .prepare("SELECT failed_login_attempts FROM users WHERE id = ? LIMIT 1")
      .get(userId) as { failed_login_attempts: number } | undefined;
    return row?.failed_login_attempts ?? 0;
  }

  resetFailedLoginState(userId: string): void {
    const now = nowIso();
    this.db
      .prepare(
        `UPDATE users
         SET failed_login_attempts=0, locked_until_utc=NULL, last_failed_login_at=NULL, updated_at=?
         WHERE id=?`
      )
      .run(now, userId);
  }

  cleanup(
    nowUtcIso: string,
    revokedCutoffIso: string,
    auditCutoffIso: string,
    callbackCutoffIso: string,
    rejectedCutoffIso: string
  ): {
    sessionsDeleted: number;
    revokedHistoryDeleted: number;
    callbacksDeleted: number;
    auditDeleted: number;
    rejectedUsersDeleted: number;
  } {
    const tx = this.db.transaction(() => {
      const deleteExpired = this.db
        .prepare("DELETE FROM sessions WHERE expires_at < ?")
        .run(nowUtcIso).changes;

      const deleteRevokedOld = this.db
        .prepare("DELETE FROM refresh_token_history WHERE first_seen_at < ?")
        .run(revokedCutoffIso).changes;

      const deleteCallbacks = this.db
        .prepare("DELETE FROM telegram_callback_log WHERE processed_at_utc < ?")
        .run(callbackCutoffIso).changes;

      const deleteAudit = this.db
        .prepare("DELETE FROM audit_log WHERE at_utc < ?")
        .run(auditCutoffIso).changes;

      const deleteRejectedUsers = this.db
        .prepare("DELETE FROM users WHERE status='rejected' AND rejected_at IS NOT NULL AND rejected_at < ?")
        .run(rejectedCutoffIso).changes;

      return {
        sessionsDeleted: deleteExpired,
        revokedHistoryDeleted: deleteRevokedOld,
        callbacksDeleted: deleteCallbacks,
        auditDeleted: deleteAudit,
        rejectedUsersDeleted: deleteRejectedUsers
      };
    });

    return tx();
  }

  revokeSessionByTokenHash(refreshTokenHash: string, deviceId: string, reason: string): boolean {
    const now = nowIso();
    const result = this.db
      .prepare(
        `UPDATE sessions
         SET revoked_at=?, revoked_reason=?
         WHERE refresh_token_hash=? AND device_id=? AND revoked_at IS NULL`
      )
      .run(now, reason, refreshTokenHash, deviceId);

    if (result.changes > 0) {
      const session = this.db
        .prepare("SELECT user_id, id FROM sessions WHERE refresh_token_hash=? LIMIT 1")
        .get(refreshTokenHash) as { user_id: string; id: string } | undefined;
      if (session) {
        this.db
          .prepare(
            "INSERT OR IGNORE INTO refresh_token_history(token_hash, user_id, session_id, first_seen_at, reason) VALUES (?, ?, ?, ?, ?)"
          )
          .run(refreshTokenHash, session.user_id, session.id, now, "revoked");
      }

      this.addAudit({
        actorType: "system",
        action: "auth.logout.session_revoked",
        details: { deviceId, reason }
      });
      return true;
    }

    return false;
  }

  isTelegramCallbackProcessed(callbackId: string): boolean {
    const row = this.db
      .prepare("SELECT callback_id FROM telegram_callback_log WHERE callback_id = ? LIMIT 1")
      .get(callbackId) as { callback_id: string } | undefined;
    return !!row;
  }

  markTelegramCallbackProcessed(callbackId: string): void {
    this.db
      .prepare("INSERT OR IGNORE INTO telegram_callback_log(callback_id, processed_at_utc) VALUES (?, ?)")
      .run(callbackId, nowIso());
  }

  addAudit(input: AuditInput): void {
    const detailsJson = input.details === undefined ? null : JSON.stringify(input.details);
    this.db
      .prepare(
        `INSERT INTO audit_log(at_utc, actor_type, actor_id, action, target_id, details_json)
         VALUES (?, ?, ?, ?, ?, ?)`
      )
      .run(nowIso(), input.actorType, input.actorId ?? null, input.action, input.targetId ?? null, detailsJson);
  }
}
