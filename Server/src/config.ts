import fs from "node:fs";

export interface AppConfig {
  host: string;
  port: number;
  databaseUrl: string;
  jwtAccessSecret: string;
  refreshTokenPepper: string;
  accessTokenTtlSeconds: number;
  refreshTokenTtlDays: number;
  tlsCertPath: string;
  tlsKeyPath: string;
  allowHttp: boolean;
  allowedOrigin?: string;
  adminTelegramBotToken?: string;
  adminTelegramChatId?: string;
}

function readRequired(name: string): string {
  const value = process.env[name]?.trim();
  if (!value) {
    throw new Error(`Missing required env var: ${name}`);
  }

  return value;
}

function readInt(name: string, fallback: number): number {
  const raw = process.env[name];
  if (!raw) {
    return fallback;
  }

  const value = Number.parseInt(raw, 10);
  if (!Number.isFinite(value) || value <= 0) {
    throw new Error(`Invalid integer env var ${name}: ${raw}`);
  }

  return value;
}

export function loadConfig(): AppConfig {
  const host = process.env.HOST?.trim() || "0.0.0.0";
  const port = readInt("PORT", 5334);
  const databaseUrl = readRequired("DATABASE_URL");
  const jwtAccessSecret = readRequired("JWT_ACCESS_SECRET");
  const refreshTokenPepper = readRequired("REFRESH_TOKEN_PEPPER");
  const accessTokenTtlSeconds = readInt("ACCESS_TOKEN_TTL_SECONDS", 900);
  const refreshTokenTtlDays = readInt("REFRESH_TOKEN_TTL_DAYS", 30);
  const allowHttp = process.env.ALLOW_HTTP === "1";

  const tlsCertPath = process.env.TLS_CERT_PATH?.trim() || "";
  const tlsKeyPath = process.env.TLS_KEY_PATH?.trim() || "";

  if (!allowHttp) {
    if (!tlsCertPath || !tlsKeyPath) {
      throw new Error("TLS_CERT_PATH and TLS_KEY_PATH are required when ALLOW_HTTP is not enabled.");
    }

    if (!fs.existsSync(tlsCertPath)) {
      throw new Error(`TLS cert file not found: ${tlsCertPath}`);
    }

    if (!fs.existsSync(tlsKeyPath)) {
      throw new Error(`TLS key file not found: ${tlsKeyPath}`);
    }
  }

  const allowedOrigin = process.env.ALLOWED_ORIGIN?.trim() || undefined;
  const adminTelegramBotToken = process.env.ADMIN_TELEGRAM_BOT_TOKEN?.trim() || undefined;
  const adminTelegramChatId = process.env.ADMIN_TELEGRAM_CHAT_ID?.trim() || undefined;

  return {
    host,
    port,
    databaseUrl,
    jwtAccessSecret,
    refreshTokenPepper,
    accessTokenTtlSeconds,
    refreshTokenTtlDays,
    tlsCertPath,
    tlsKeyPath,
    allowHttp,
    allowedOrigin,
    adminTelegramBotToken,
    adminTelegramChatId,
  };
}