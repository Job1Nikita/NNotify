import { v4 as uuidv4 } from "uuid";
import type { FastifyInstance, FastifyReply, FastifyRequest } from "fastify";
import type { Pool } from "pg";
import type { AppConfig } from "./config.js";
import {
  createAccessToken,
  createRefreshToken,
  hashPassword,
  hashRefreshToken,
  isValidPassword,
  isValidUsername,
  verifyAccessToken,
  verifyPassword,
} from "./security.js";
import { notifyAdminAboutPendingRegistration } from "./telegram.js";

declare module "fastify" {
  interface FastifyRequest {
    authUser?: {
      id: string;
      username: string;
    };
  }
}

interface AuthRoutesDeps {
  config: AppConfig;
  db: Pool;
}

interface RegisterBody {
  username?: string;
  password?: string;
}

interface LoginBody {
  username?: string;
  password?: string;
  deviceId?: string;
  deviceName?: string;
}

interface RefreshBody {
  refreshToken?: string;
  deviceId?: string;
}

interface LogoutBody {
  refreshToken?: string;
  deviceId?: string;
}

export async function registerAuthRoutes(app: FastifyInstance, deps: AuthRoutesDeps): Promise<void> {
  const { config, db } = deps;

  app.post<{ Body: RegisterBody }>("/v1/auth/register", async (request, reply) => {
    const username = request.body.username?.trim() ?? "";
    const password = request.body.password ?? "";

    if (!isValidUsername(username)) {
      return reply.code(400).send({ message: "Invalid username format." });
    }

    if (!isValidPassword(password)) {
      return reply.code(400).send({ message: "Password must contain at least 8 characters." });
    }

    const exists = await db.query<{ id: string }>(
      "SELECT id FROM users WHERE username = $1 LIMIT 1",
      [username],
    );

    if (exists.rowCount && exists.rowCount > 0) {
      return reply.code(409).send({ message: "User already exists." });
    }

    const userId = uuidv4();
    const passwordHash = await hashPassword(password);

    await db.query(
      `INSERT INTO users (id, username, password_hash, status)
       VALUES ($1, $2, $3, 'pending')`,
      [userId, username, passwordHash],
    );

    await notifyAdminAboutPendingRegistration(config, username);

    return reply.code(202).send({ message: "Registration created. Waiting for admin approval.", status: "pending" });
  });

  app.post<{ Body: LoginBody }>("/v1/auth/login", async (request, reply) => {
    const username = request.body.username?.trim() ?? "";
    const password = request.body.password ?? "";
    const deviceId = request.body.deviceId?.trim() ?? "";

    if (!username || !password || !deviceId) {
      return reply.code(400).send({ message: "username, password and deviceId are required." });
    }

    const userResult = await db.query<{
      id: string;
      username: string;
      password_hash: string;
      status: "pending" | "approved" | "rejected";
    }>(
      `SELECT id, username, password_hash, status
       FROM users
       WHERE username = $1
       LIMIT 1`,
      [username],
    );

    if (!userResult.rowCount) {
      return reply.code(401).send({ message: "Invalid username or password." });
    }

    const user = userResult.rows[0];
    const passwordValid = await verifyPassword(user.password_hash, password);
    if (!passwordValid) {
      return reply.code(401).send({ message: "Invalid username or password." });
    }

    if (user.status === "pending") {
      return reply.code(403).send({ message: "Account is pending approval." });
    }

    if (user.status === "rejected") {
      return reply.code(403).send({ message: "Account was rejected by administrator." });
    }

    const refreshToken = createRefreshToken();
    const refreshHash = hashRefreshToken(refreshToken, config.refreshTokenPepper);
    const expiresAt = new Date(Date.now() + config.refreshTokenTtlDays * 24 * 60 * 60 * 1000);

    await db.query(
      `INSERT INTO refresh_sessions (id, user_id, device_id, refresh_token_hash, expires_at_utc)
       VALUES ($1, $2, $3, $4, $5)`,
      [uuidv4(), user.id, deviceId, refreshHash, expiresAt.toISOString()],
    );

    const accessToken = createAccessToken(config, user.id, user.username);
    return reply.code(200).send({
      accessToken,
      refreshToken,
      accessTokenExpiresInSeconds: config.accessTokenTtlSeconds,
    });
  });

  app.post<{ Body: RefreshBody }>("/v1/auth/refresh", async (request, reply) => {
    const refreshToken = request.body.refreshToken?.trim() ?? "";
    const deviceId = request.body.deviceId?.trim() ?? "";

    if (!refreshToken || !deviceId) {
      return reply.code(400).send({ message: "refreshToken and deviceId are required." });
    }

    const refreshHash = hashRefreshToken(refreshToken, config.refreshTokenPepper);

    const sessionResult = await db.query<{
      id: string;
      user_id: string;
      username: string;
      status: "pending" | "approved" | "rejected";
      expires_at_utc: Date;
      revoked_at_utc: Date | null;
    }>(
      `SELECT s.id,
              s.user_id,
              u.username,
              u.status,
              s.expires_at_utc,
              s.revoked_at_utc
       FROM refresh_sessions s
       JOIN users u ON u.id = s.user_id
       WHERE s.refresh_token_hash = $1
         AND s.device_id = $2
       LIMIT 1`,
      [refreshHash, deviceId],
    );

    if (!sessionResult.rowCount) {
      return reply.code(401).send({ message: "Invalid refresh token." });
    }

    const session = sessionResult.rows[0];
    if (session.revoked_at_utc || session.expires_at_utc.getTime() <= Date.now()) {
      return reply.code(401).send({ message: "Refresh token is expired or revoked." });
    }

    if (session.status !== "approved") {
      return reply.code(403).send({ message: "Account is not approved." });
    }

    const newRefreshToken = createRefreshToken();
    const newRefreshHash = hashRefreshToken(newRefreshToken, config.refreshTokenPepper);
    const newExpiresAt = new Date(Date.now() + config.refreshTokenTtlDays * 24 * 60 * 60 * 1000);

    await db.query(
      `UPDATE refresh_sessions
       SET refresh_token_hash = $1,
           expires_at_utc = $2
       WHERE id = $3`,
      [newRefreshHash, newExpiresAt.toISOString(), session.id],
    );

    const accessToken = createAccessToken(config, session.user_id, session.username);
    return reply.code(200).send({
      accessToken,
      refreshToken: newRefreshToken,
      accessTokenExpiresInSeconds: config.accessTokenTtlSeconds,
    });
  });

  app.post<{ Body: LogoutBody }>("/v1/auth/logout", async (request, reply) => {
    const refreshToken = request.body.refreshToken?.trim() ?? "";
    const deviceId = request.body.deviceId?.trim() ?? "";

    if (!refreshToken || !deviceId) {
      return reply.code(204).send();
    }

    const refreshHash = hashRefreshToken(refreshToken, config.refreshTokenPepper);
    await db.query(
      `UPDATE refresh_sessions
       SET revoked_at_utc = NOW()
       WHERE refresh_token_hash = $1
         AND device_id = $2`,
      [refreshHash, deviceId],
    );

    return reply.code(204).send();
  });

  app.get("/v1/auth/me", async (request, reply) => {
    const user = await authenticateRequest(request, reply, config);
    if (!user) {
      return;
    }

    return reply.code(200).send({ id: user.id, username: user.username });
  });
}

export async function authenticateRequest(
  request: FastifyRequest,
  reply: FastifyReply,
  config: AppConfig,
): Promise<{ id: string; username: string } | null> {
  const header = request.headers.authorization;
  if (!header || !header.startsWith("Bearer ")) {
    await reply.code(401).send({ message: "Missing Bearer token." });
    return null;
  }

  const token = header.slice("Bearer ".length).trim();
  if (!token) {
    await reply.code(401).send({ message: "Missing Bearer token." });
    return null;
  }

  try {
    const claims = verifyAccessToken(config, token);
    request.authUser = { id: claims.sub, username: claims.username };
    return request.authUser;
  } catch {
    await reply.code(401).send({ message: "Invalid or expired access token." });
    return null;
  }
}