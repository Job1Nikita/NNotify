import crypto from "node:crypto";
import argon2 from "argon2";
import jwt from "jsonwebtoken";
import type { AppConfig } from "./config.js";
import type { AccessClaims } from "./types.js";

export async function hashPassword(password: string): Promise<string> {
  return argon2.hash(password, {
    type: argon2.argon2id,
    memoryCost: 19456,
    timeCost: 3,
    parallelism: 1,
  });
}

export async function verifyPassword(hash: string, password: string): Promise<boolean> {
  try {
    return await argon2.verify(hash, password);
  } catch {
    return false;
  }
}

export function createAccessToken(config: AppConfig, userId: string, username: string): string {
  const payload: AccessClaims = {
    sub: userId,
    username,
    typ: "access",
  };

  return jwt.sign(payload, config.jwtAccessSecret, {
    algorithm: "HS256",
    expiresIn: config.accessTokenTtlSeconds,
  });
}

export function verifyAccessToken(config: AppConfig, token: string): AccessClaims {
  const decoded = jwt.verify(token, config.jwtAccessSecret, {
    algorithms: ["HS256"],
  });

  if (typeof decoded !== "object" || !decoded || decoded.typ !== "access") {
    throw new Error("Invalid access token payload.");
  }

  return decoded as AccessClaims;
}

export function createRefreshToken(): string {
  return crypto.randomBytes(48).toString("base64url");
}

export function hashRefreshToken(refreshToken: string, pepper: string): string {
  return crypto
    .createHash("sha256")
    .update(refreshToken)
    .update(pepper)
    .digest("hex");
}

export function isValidUsername(username: string): boolean {
  return /^[a-zA-Z0-9_.-]{3,64}$/.test(username);
}

export function isValidPassword(password: string): boolean {
  return password.length >= 8 && password.length <= 256;
}