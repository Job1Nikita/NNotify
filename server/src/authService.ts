import { config } from "./config.js";
import { createAccessToken } from "./jwt.js";
import { AuthStore } from "./store.js";
import { createOpaqueToken, hashPassword, hashRefreshToken, verifyPassword } from "./security.js";
import { TelegramModerationService } from "./telegram.js";
import type { UserRecord } from "./types.js";

function normalizeUsername(raw: string): string {
  return raw.trim().toLowerCase();
}

function validateUsername(username: string): boolean {
  return /^[a-z0-9._-]{3,32}$/.test(username);
}

function validatePassword(password: string): boolean {
  if (password.length < config.passwordMinLength) {
    return false;
  }

  const hasLower = /[a-z]/.test(password);
  const hasUpper = /[A-Z]/.test(password);
  const hasDigit = /\d/.test(password);
  return hasLower && hasUpper && hasDigit;
}

function lockUntilIso(nowMs: number): string {
  return new Date(nowMs + config.loginLockoutMinutes * 60 * 1000).toISOString();
}

function isLockActive(lockedUntilUtc: string | null, nowMs: number): boolean {
  if (!lockedUntilUtc) {
    return false;
  }

  const lockedUntilMs = Date.parse(lockedUntilUtc);
  return Number.isFinite(lockedUntilMs) && lockedUntilMs > nowMs;
}

export class AuthService {
  constructor(
    private readonly store: AuthStore,
    private readonly telegram: TelegramModerationService
  ) {}

  async register(input: { username: string; password: string; sourceIp?: string | null }): Promise<{ statusCode: number; body: Record<string, unknown> }> {
    if (!config.allowClientRegistration) {
      return {
        statusCode: 403,
        body: { message: "Client self-registration is disabled by server policy." }
      };
    }

    const username = normalizeUsername(input.username);
    if (!validateUsername(username)) {
      return {
        statusCode: 400,
        body: { message: "Invalid username. Use 3-32 chars: latin letters, digits, dot, underscore or hyphen." }
      };
    }

    if (!validatePassword(input.password)) {
      return {
        statusCode: 400,
        body: {
          message: `Weak password. Minimum ${config.passwordMinLength} chars, include upper/lowercase letters and a digit.`
        }
      };
    }

    const existing = this.store.getUserByUsername(username);
    if (existing) {
      if (existing.status === "pending") {
        return {
          statusCode: 202,
          body: { message: "Registration request was submitted. Wait for admin approval." }
        };
      }

      if (existing.status === "rejected") {
        const passwordHash = await hashPassword(input.password);
        const requeued = this.store.requeueRejectedUser(username, passwordHash);
        if (requeued.success) {
          this.telegram
            .notifyPendingRegistration({ username: existing.username, userId: existing.id, sourceIp: input.sourceIp })
            .catch(() => {
              // notification errors must not fail registration path
            });
        }

        return {
          statusCode: 202,
          body: { message: "Registration request was submitted. Wait for admin approval." }
        };
      }

      if (existing.status === "active") {
        return {
          statusCode: 409,
          body: { message: "User with this login already exists." }
        };
      }

      if (existing.status === "blocked") {
        return {
          statusCode: 403,
          body: { message: "Access denied. Contact your server administrator." }
        };
      }
    }

    const passwordHash = await hashPassword(input.password);
    const result = this.store.createPendingUser(username, passwordHash);
    if (result.created) {
      const created = this.store.getUserByUsername(username);
      if (created) {
        this.telegram
          .notifyPendingRegistration({ username: created.username, userId: created.id, sourceIp: input.sourceIp })
          .catch(() => {
            // notification errors must not fail registration path
          });
      }
    }

    return {
      statusCode: 202,
      body: { message: "Registration request was submitted. Wait for admin approval." }
    };
  }

  async login(input: {
    username: string;
    password: string;
    deviceId: string;
    deviceName: string;
    ip?: string | null;
    userAgent?: string | null;
  }): Promise<{ statusCode: number; body: Record<string, unknown> }> {
    const username = normalizeUsername(input.username);
    const user = this.store.getUserByUsername(username);
    const nowMs = Date.now();

    if (!user) {
      return {
        statusCode: 401,
        body: { message: "Invalid username or password." }
      };
    }

    if (user.status === "pending") {
      return {
        statusCode: 403,
        body: { message: "Access denied. Account is pending admin approval." }
      };
    }

    if (user.status === "blocked" || user.status === "rejected") {
      return {
        statusCode: 403,
        body: { message: "Access denied. Contact your server administrator." }
      };
    }

    if (isLockActive(user.locked_until_utc, nowMs)) {
      this.store.addAudit({
        actorType: "user",
        actorId: user.id,
        action: "auth.login.blocked_by_lockout",
        details: { username: user.username, lockedUntilUtc: user.locked_until_utc }
      });

      return {
        statusCode: 429,
        body: { message: "Too many failed attempts. Try again later." }
      };
    }

    if (user.locked_until_utc && !isLockActive(user.locked_until_utc, nowMs) && user.failed_login_attempts >= config.loginMaxAttempts) {
      this.store.resetFailedLoginState(user.id);
      user.failed_login_attempts = 0;
      user.locked_until_utc = null;
    }

    const validPassword = await verifyPassword(input.password, user.password_hash);
    if (!validPassword) {
      const nextAttempt = (user.failed_login_attempts ?? 0) + 1;
      const shouldLock = nextAttempt >= config.loginMaxAttempts;
      const lockedUntilUtc = shouldLock ? lockUntilIso(nowMs) : null;
      this.store.markFailedLoginAttempt(user.id, lockedUntilUtc);
      this.store.addAudit({
        actorType: "user",
        actorId: user.id,
        action: "auth.login.failed_password",
        details: { username: user.username, attempts: nextAttempt, lockedUntilUtc }
      });

      return {
        statusCode: 401,
        body: { message: "Invalid username or password." }
      };
    }

    this.store.resetFailedLoginState(user.id);

    if (!input.deviceId || input.deviceId.length < 8 || input.deviceId.length > 128) {
      return {
        statusCode: 400,
        body: { message: "Invalid deviceId." }
      };
    }

    if (!input.deviceName || input.deviceName.length < 1 || input.deviceName.length > 128) {
      return {
        statusCode: 400,
        body: { message: "Invalid deviceName." }
      };
    }

    const refreshToken = createOpaqueToken(48);
    const refreshTokenHash = hashRefreshToken(refreshToken, config.refreshTokenPepper);
    const refreshExpiresAt = new Date(Date.now() + config.refreshTokenTtlDays * 24 * 60 * 60 * 1000).toISOString();

    const session = this.store.createSession({
      userId: user.id,
      deviceId: input.deviceId,
      deviceName: input.deviceName,
      refreshTokenHash,
      expiresAt: refreshExpiresAt,
      ip: input.ip,
      userAgent: input.userAgent
    });

    const accessToken = await createAccessToken({
      userId: user.id,
      username: user.username,
      sessionId: session.id,
      deviceId: input.deviceId,
      issuer: config.jwtIssuer,
      audience: config.jwtAudience,
      ttlSeconds: config.accessTokenTtlSeconds,
      secret: config.jwtAccessSecret
    });

    return {
      statusCode: 200,
      body: {
        accessToken,
        refreshToken,
        accessTokenExpiresInSeconds: config.accessTokenTtlSeconds
      }
    };
  }

  async refresh(input: {
    refreshToken: string;
    deviceId: string;
    deviceName: string;
  }): Promise<{ statusCode: number; body: Record<string, unknown> }> {
    if (!input.refreshToken || !input.deviceId || !input.deviceName) {
      return {
        statusCode: 400,
        body: { message: "refreshToken, deviceId and deviceName are required." }
      };
    }

    const currentHash = hashRefreshToken(input.refreshToken, config.refreshTokenPepper);
    const session = this.store.findSessionByTokenHash(currentHash);
    const nowMs = Date.now();

    if (!session) {
      const history = this.store.findRefreshTokenHistory(currentHash);
      if (history) {
        this.store.revokeAllSessionsForUser(history.user_id, "refresh_token_reuse_detected");
        this.store.addAudit({
          actorType: "system",
          actorId: history.user_id,
          action: "auth.refresh.reuse_detected",
          details: { reason: history.reason, deviceId: input.deviceId }
        });
      }

      return {
        statusCode: 401,
        body: { message: "Invalid session token." }
      };
    }

    if (session.revoked_at) {
      this.store.revokeAllSessionsForUser(session.user_id, "refresh_after_revoke_detected");
      this.store.addAudit({
        actorType: "system",
        actorId: session.user_id,
        action: "auth.refresh.reuse_after_revoke",
        details: { sessionId: session.id, deviceId: input.deviceId }
      });
      return {
        statusCode: 401,
        body: { message: "Invalid session token." }
      };
    }

    if (session.device_id !== input.deviceId) {
      this.store.addAudit({
        actorType: "system",
        actorId: session.user_id,
        action: "auth.refresh.device_mismatch",
        details: { sessionId: session.id, expectedDeviceId: session.device_id, actualDeviceId: input.deviceId }
      });
      return {
        statusCode: 401,
        body: { message: "Invalid session token." }
      };
    }

    if (Date.parse(session.expires_at) <= nowMs) {
      this.store.revokeSessionByTokenHash(currentHash, input.deviceId, "expired");
      return {
        statusCode: 401,
        body: { message: "Session expired." }
      };
    }

    const user = this.store.getUserById(session.user_id);
    if (!user || user.status !== "active") {
      return {
        statusCode: 403,
        body: { message: "Access denied." }
      };
    }

    const nextRefreshToken = createOpaqueToken(48);
    const nextRefreshHash = hashRefreshToken(nextRefreshToken, config.refreshTokenPepper);
    const nextRefreshExpiresAt = new Date(Date.now() + config.refreshTokenTtlDays * 24 * 60 * 60 * 1000).toISOString();
    const rotated = this.store.rotateSessionRefreshToken(session.id, currentHash, nextRefreshHash, nextRefreshExpiresAt);
    if (!rotated) {
      return {
        statusCode: 409,
        body: { message: "Session rotation conflict. Retry login." }
      };
    }

    const accessToken = await createAccessToken({
      userId: user.id,
      username: user.username,
      sessionId: session.id,
      deviceId: input.deviceId,
      issuer: config.jwtIssuer,
      audience: config.jwtAudience,
      ttlSeconds: config.accessTokenTtlSeconds,
      secret: config.jwtAccessSecret
    });

    return {
      statusCode: 200,
      body: {
        accessToken,
        refreshToken: nextRefreshToken,
        accessTokenExpiresInSeconds: config.accessTokenTtlSeconds
      }
    };
  }

  logout(input: { refreshToken: string; deviceId: string }): { statusCode: number; body: Record<string, unknown> } {
    if (!input.refreshToken || !input.deviceId) {
      return {
        statusCode: 400,
        body: { message: "refreshToken and deviceId are required." }
      };
    }

    const hash = hashRefreshToken(input.refreshToken, config.refreshTokenPepper);
    this.store.revokeSessionByTokenHash(hash, input.deviceId, "logout");

    return {
      statusCode: 200,
      body: { message: "Signed out." }
    };
  }

  cleanupExpiredData(): {
    sessionsDeleted: number;
    revokedHistoryDeleted: number;
    callbacksDeleted: number;
    auditDeleted: number;
    rejectedUsersDeleted: number;
  } {
    const now = Date.now();
    const nowIso = new Date(now).toISOString();
    const revokedCutoffIso = new Date(now - config.sessionRevokedRetentionDays * 24 * 60 * 60 * 1000).toISOString();
    const auditCutoffIso = new Date(now - config.auditRetentionDays * 24 * 60 * 60 * 1000).toISOString();
    const callbackCutoffIso = new Date(now - config.telegramCallbackRetentionDays * 24 * 60 * 60 * 1000).toISOString();
    const rejectedCutoffIso = new Date(now - config.rejectedUserRetentionDays * 24 * 60 * 60 * 1000).toISOString();
    return this.store.cleanup(nowIso, revokedCutoffIso, auditCutoffIso, callbackCutoffIso, rejectedCutoffIso);
  }

  approvePendingUserById(userId: string, actor: string): { ok: boolean; message: string; username?: string } {
    const user = this.store.getUserById(userId);
    if (!user) {
      return { ok: false, message: "User not found." };
    }

    const result = this.store.approveUser(user.username, actor);
    if (!result.success) {
      return { ok: false, message: `Could not approve user: ${result.reason ?? "unknown"}.` };
    }

    return { ok: true, message: `User ${user.username} approved.`, username: user.username };
  }

  rejectPendingUserById(userId: string, actor: string): { ok: boolean; message: string; username?: string } {
    const user = this.store.getUserById(userId);
    if (!user) {
      return { ok: false, message: "User not found." };
    }

    const result = this.store.rejectUser(user.username, actor, "Rejected from Telegram moderation");
    if (!result.success) {
      return { ok: false, message: `Could not reject user: ${result.reason ?? "unknown"}.` };
    }

    return { ok: true, message: `User ${user.username} rejected.`, username: user.username };
  }

  blockUserById(userId: string, actor: string): { ok: boolean; message: string; username?: string } {
    const user = this.store.getUserById(userId);
    if (!user) {
      return { ok: false, message: "User not found." };
    }

    const result = this.store.blockUser(user.username, actor);
    if (!result.success) {
      return { ok: false, message: `Could not block user: ${result.reason ?? "unknown"}.` };
    }

    return { ok: true, message: `User ${user.username} blocked.`, username: user.username };
  }

  unblockUserById(userId: string, actor: string): { ok: boolean; message: string; username?: string } {
    const user = this.store.getUserById(userId);
    if (!user) {
      return { ok: false, message: "User not found." };
    }

    const result = this.store.unblockUser(user.username, actor);
    if (!result.success) {
      return { ok: false, message: `Could not unblock user: ${result.reason ?? "unknown"}.` };
    }

    return { ok: true, message: `User ${user.username} unblocked.`, username: user.username };
  }

  deleteUserById(userId: string, actor: string): { ok: boolean; message: string; username?: string } {
    const user = this.store.getUserById(userId);
    if (!user) {
      return { ok: false, message: "User not found." };
    }

    const result = this.store.rejectUser(user.username, actor, "Deleted via Telegram moderation menu");
    if (!result.success) {
      return { ok: false, message: `Could not delete user: ${result.reason ?? "unknown"}.` };
    }

    return { ok: true, message: `User ${user.username} moved to deleted.`, username: user.username };
  }

  restoreDeletedUserById(userId: string, actor: string): { ok: boolean; message: string; username?: string } {
    const user = this.store.getUserById(userId);
    if (!user) {
      return { ok: false, message: "User not found." };
    }

    const result = this.store.restoreRejectedUser(user.username, actor);
    if (!result.success) {
      return { ok: false, message: `Could not restore user: ${result.reason ?? "unknown"}.` };
    }

    return { ok: true, message: `User ${user.username} restored.`, username: user.username };
  }

  async createActiveUserByAdmin(usernameRaw: string, password: string, actor: string): Promise<{ ok: boolean; message: string }> {
    const username = normalizeUsername(usernameRaw);
    if (!validateUsername(username)) {
      return { ok: false, message: "Invalid username format." };
    }

    if (!validatePassword(password)) {
      return { ok: false, message: `Weak password. Minimum ${config.passwordMinLength} chars, include upper/lowercase and digit.` };
    }

    const passwordHash = await hashPassword(password);
    const result = this.store.createActiveUserByAdmin(username, passwordHash, actor);
    if (!result.created) {
      return { ok: false, message: `User already exists (status: ${result.status}).` };
    }

    return { ok: true, message: `User ${username} created and activated.` };
  }

  approveByUsername(usernameRaw: string, actor: string): { ok: boolean; message: string } {
    const username = normalizeUsername(usernameRaw);
    const result = this.store.approveUser(username, actor);
    if (!result.success) {
      return { ok: false, message: `Approve failed: ${result.reason ?? "unknown"}.` };
    }

    return { ok: true, message: `User ${username} approved.` };
  }

  rejectByUsername(usernameRaw: string, actor: string, reason?: string): { ok: boolean; message: string } {
    const username = normalizeUsername(usernameRaw);
    const result = this.store.rejectUser(username, actor, reason);
    if (!result.success) {
      return { ok: false, message: `Reject failed: ${result.reason ?? "unknown"}.` };
    }

    return { ok: true, message: `User ${username} rejected.` };
  }

  blockByUsername(usernameRaw: string, actor: string): { ok: boolean; message: string } {
    const username = normalizeUsername(usernameRaw);
    const result = this.store.blockUser(username, actor);
    if (!result.success) {
      return { ok: false, message: `Block failed: ${result.reason ?? "unknown"}.` };
    }

    return { ok: true, message: `User ${username} blocked.` };
  }

  listUsers(status?: UserRecord["status"]): UserRecord[] {
    return this.store.listUsers(status);
  }
}
