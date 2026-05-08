import { randomBytes, createHash } from "node:crypto";
import { hash, verify } from "@node-rs/argon2";

export async function hashPassword(password: string): Promise<string> {
  return hash(password, {
    algorithm: 2,
    memoryCost: 19456,
    timeCost: 2,
    parallelism: 1,
    outputLen: 32
  });
}

export async function verifyPassword(password: string, hashed: string): Promise<boolean> {
  return verify(hashed, password);
}

export function createOpaqueToken(size = 48): string {
  return randomBytes(size).toString("base64url");
}

export function hashRefreshToken(token: string, pepper: string): string {
  return createHash("sha256").update(token).update(pepper).digest("hex");
}

export function nowIso(): string {
  return new Date().toISOString();
}
