import fs from "node:fs";
import path from "node:path";
import dotenv from "dotenv";
import { z } from "zod";

dotenv.config();

const schema = z.object({
  NODE_ENV: z.enum(["development", "production", "test"]).default("development"),
  HOST: z.string().default("127.0.0.1"),
  PORT: z.coerce.number().int().min(1).max(65535).default(3100),
  TRUST_PROXY: z
    .string()
    .optional()
    .transform((v) => String(v ?? "true").toLowerCase() === "true"),
  PUBLIC_BASE_URL: z.string().url().default("https://nnotify.example.com"),

  DATABASE_PATH: z.string().default("./data/nnotify_auth.db"),

  JWT_ACCESS_SECRET: z.string().min(64),
  REFRESH_TOKEN_PEPPER: z.string().min(64),
  JWT_ISSUER: z.string().min(1).default("nnotify-auth"),
  JWT_AUDIENCE: z.string().min(1).default("nnotify-client"),
  ACCESS_TOKEN_TTL_SECONDS: z.coerce.number().int().min(120).max(3600).default(900),
  REFRESH_TOKEN_TTL_DAYS: z.coerce.number().int().min(1).max(120).default(30),
  LOGIN_MAX_ATTEMPTS: z.coerce.number().int().min(3).max(20).default(5),
  LOGIN_LOCKOUT_MINUTES: z.coerce.number().int().min(1).max(120).default(10),
  SESSION_CLEANUP_INTERVAL_MINUTES: z.coerce.number().int().min(1).max(1440).default(15),
  SESSION_REVOKED_RETENTION_DAYS: z.coerce.number().int().min(1).max(365).default(30),
  AUDIT_RETENTION_DAYS: z.coerce.number().int().min(7).max(3650).default(180),
  TELEGRAM_CALLBACK_RETENTION_DAYS: z.coerce.number().int().min(1).max(365).default(30),
  REJECTED_USER_RETENTION_DAYS: z.coerce.number().int().min(1).max(365).default(3),

  PASSWORD_MIN_LENGTH: z.coerce.number().int().min(8).max(128).default(10),
  ALLOW_CLIENT_REGISTRATION: z
    .string()
    .optional()
    .transform((v) => String(v ?? "true").toLowerCase() === "true"),
  BOOTSTRAP_ADMIN_NAME: z.string().min(1).default("server-admin"),

  TELEGRAM_MODERATION_ENABLED: z
    .string()
    .optional()
    .transform((v) => String(v ?? "false").toLowerCase() === "true"),
  TELEGRAM_BOT_TOKEN: z.string().optional().default(""),
  REMINDER_TELEGRAM_BOT_TOKEN: z.string().optional().default(""),
  TELEGRAM_ADMIN_CHAT_ID: z.string().optional().default(""),
  TELEGRAM_ADMIN_USER_IDS: z.string().optional().default(""),
  TELEGRAM_WEBHOOK_SECRET: z.string().optional().default(""),
  TELEGRAM_WEBHOOK_HEADER_SECRET: z.string().optional().default(""),
  REMINDER_TIME_ZONE: z.string().min(1).default("Europe/Moscow")
});

const parsed = schema.safeParse(process.env);
if (!parsed.success) {
  // Fail fast: do not run in insecure or unknown config state.
  throw new Error(`Invalid environment: ${parsed.error.message}`);
}

const env = parsed.data;

const resolvedDatabasePath = path.resolve(process.cwd(), env.DATABASE_PATH);
const databaseDir = path.dirname(resolvedDatabasePath);
if (!fs.existsSync(databaseDir)) {
  fs.mkdirSync(databaseDir, { recursive: true });
}

const telegramAdminUserIds = env.TELEGRAM_ADMIN_USER_IDS.split(",")
  .map((v) => v.trim())
  .filter(Boolean);

export const config = {
  nodeEnv: env.NODE_ENV,
  host: env.HOST,
  port: env.PORT,
  trustProxy: env.TRUST_PROXY,
  publicBaseUrl: env.PUBLIC_BASE_URL,
  databasePath: resolvedDatabasePath,
  jwtAccessSecret: env.JWT_ACCESS_SECRET,
  refreshTokenPepper: env.REFRESH_TOKEN_PEPPER,
  jwtIssuer: env.JWT_ISSUER,
  jwtAudience: env.JWT_AUDIENCE,
  accessTokenTtlSeconds: env.ACCESS_TOKEN_TTL_SECONDS,
  refreshTokenTtlDays: env.REFRESH_TOKEN_TTL_DAYS,
  loginMaxAttempts: env.LOGIN_MAX_ATTEMPTS,
  loginLockoutMinutes: env.LOGIN_LOCKOUT_MINUTES,
  sessionCleanupIntervalMinutes: env.SESSION_CLEANUP_INTERVAL_MINUTES,
  sessionRevokedRetentionDays: env.SESSION_REVOKED_RETENTION_DAYS,
  auditRetentionDays: env.AUDIT_RETENTION_DAYS,
  telegramCallbackRetentionDays: env.TELEGRAM_CALLBACK_RETENTION_DAYS,
  rejectedUserRetentionDays: env.REJECTED_USER_RETENTION_DAYS,
  passwordMinLength: env.PASSWORD_MIN_LENGTH,
  allowClientRegistration: env.ALLOW_CLIENT_REGISTRATION,
  bootstrapAdminName: env.BOOTSTRAP_ADMIN_NAME,
  telegramModerationEnabled: env.TELEGRAM_MODERATION_ENABLED,
  telegramBotToken: env.TELEGRAM_BOT_TOKEN,
  reminderTelegramBotToken: env.REMINDER_TELEGRAM_BOT_TOKEN,
  telegramAdminChatId: env.TELEGRAM_ADMIN_CHAT_ID,
  telegramAdminUserIds,
  telegramWebhookSecret: env.TELEGRAM_WEBHOOK_SECRET,
  telegramWebhookHeaderSecret: env.TELEGRAM_WEBHOOK_HEADER_SECRET,
  reminderTimeZone: env.REMINDER_TIME_ZONE
} as const;

export type AppConfig = typeof config;
