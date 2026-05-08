import fs from "node:fs";
import path from "node:path";
import Database from "better-sqlite3";

export type SqliteDb = Database.Database;

export function openDatabase(databasePath: string): SqliteDb {
  const directory = path.dirname(databasePath);
  if (!fs.existsSync(directory)) {
    fs.mkdirSync(directory, { recursive: true });
  }

  const db = new Database(databasePath);
  db.pragma("journal_mode = WAL");
  db.pragma("synchronous = FULL");
  db.pragma("foreign_keys = ON");
  db.pragma("busy_timeout = 5000");

  migrate(db);
  return db;
}

function migrate(db: SqliteDb): void {
  db.exec(`
    CREATE TABLE IF NOT EXISTS users (
      id TEXT PRIMARY KEY,
      username TEXT NOT NULL UNIQUE,
      password_hash TEXT NOT NULL,
      status TEXT NOT NULL CHECK(status IN ('pending','active','blocked','rejected')),
      created_at TEXT NOT NULL,
      updated_at TEXT NOT NULL,
      approved_at TEXT,
      approved_by TEXT,
      rejected_at TEXT,
      rejected_by TEXT,
      reject_reason TEXT,
      failed_login_attempts INTEGER NOT NULL DEFAULT 0,
      locked_until_utc TEXT,
      last_failed_login_at TEXT,
      telegram_user_id TEXT
    );

    CREATE TABLE IF NOT EXISTS sessions (
      id TEXT PRIMARY KEY,
      user_id TEXT NOT NULL,
      device_id TEXT NOT NULL,
      device_name TEXT NOT NULL,
      refresh_token_hash TEXT NOT NULL UNIQUE,
      created_at TEXT NOT NULL,
      expires_at TEXT NOT NULL,
      revoked_at TEXT,
      revoked_reason TEXT,
      last_used_at TEXT,
      ip TEXT,
      user_agent TEXT,
      FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
    );

    CREATE TABLE IF NOT EXISTS audit_log (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      at_utc TEXT NOT NULL,
      actor_type TEXT NOT NULL,
      actor_id TEXT,
      action TEXT NOT NULL,
      target_id TEXT,
      details_json TEXT
    );

    CREATE TABLE IF NOT EXISTS telegram_callback_log (
      callback_id TEXT PRIMARY KEY,
      processed_at_utc TEXT NOT NULL
    );

    CREATE TABLE IF NOT EXISTS refresh_token_history (
      token_hash TEXT PRIMARY KEY,
      user_id TEXT NOT NULL,
      session_id TEXT,
      first_seen_at TEXT NOT NULL,
      reason TEXT NOT NULL,
      FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
    );

    CREATE TABLE IF NOT EXISTS sync_reminders (
      id TEXT PRIMARY KEY,
      user_id TEXT NOT NULL,
      title TEXT NOT NULL,
      due_at_utc INTEGER NOT NULL,
      priority INTEGER NOT NULL,
      created_at_utc INTEGER NOT NULL,
      status TEXT NOT NULL CHECK(status IN ('scheduled','fired','acked','snoozed','cancelled','missed')),
      last_fired_at_utc INTEGER,
      acked_at_utc INTEGER,
      snooze_until_utc INTEGER,
      telegram_escalated_at_utc INTEGER,
      telegram_escalation_attempts INTEGER NOT NULL DEFAULT 0,
      telegram_escalation_next_retry_utc INTEGER,
      telegram_escalation_last_error TEXT,
      updated_at_utc INTEGER NOT NULL,
      client_updated_at_utc INTEGER NOT NULL DEFAULT 0,
      deleted_at_utc INTEGER,
      source_device_id TEXT,
      duplicate_candidate INTEGER NOT NULL DEFAULT 0,
      FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
    );

    CREATE INDEX IF NOT EXISTS idx_users_status ON users(status);
    CREATE INDEX IF NOT EXISTS idx_sessions_user_id ON sessions(user_id);
    CREATE INDEX IF NOT EXISTS idx_sessions_device_id ON sessions(device_id);
    CREATE INDEX IF NOT EXISTS idx_sessions_expires_at ON sessions(expires_at);
    CREATE INDEX IF NOT EXISTS idx_audit_at_utc ON audit_log(at_utc);
    CREATE INDEX IF NOT EXISTS idx_refresh_history_user_id ON refresh_token_history(user_id);
    CREATE INDEX IF NOT EXISTS idx_sync_reminders_user_updated ON sync_reminders(user_id, updated_at_utc);
    CREATE INDEX IF NOT EXISTS idx_sync_reminders_escalation ON sync_reminders(status, telegram_escalated_at_utc, due_at_utc, snooze_until_utc);
  `);

  ensureColumn(db, "users", "failed_login_attempts", "ALTER TABLE users ADD COLUMN failed_login_attempts INTEGER NOT NULL DEFAULT 0");
  ensureColumn(db, "users", "locked_until_utc", "ALTER TABLE users ADD COLUMN locked_until_utc TEXT");
  ensureColumn(db, "users", "last_failed_login_at", "ALTER TABLE users ADD COLUMN last_failed_login_at TEXT");
  ensureColumn(db, "users", "telegram_user_id", "ALTER TABLE users ADD COLUMN telegram_user_id TEXT");
  ensureColumn(db, "sync_reminders", "duplicate_candidate", "ALTER TABLE sync_reminders ADD COLUMN duplicate_candidate INTEGER NOT NULL DEFAULT 0");
  ensureColumn(db, "sync_reminders", "telegram_escalation_attempts", "ALTER TABLE sync_reminders ADD COLUMN telegram_escalation_attempts INTEGER NOT NULL DEFAULT 0");
  ensureColumn(db, "sync_reminders", "telegram_escalation_next_retry_utc", "ALTER TABLE sync_reminders ADD COLUMN telegram_escalation_next_retry_utc INTEGER");
  ensureColumn(db, "sync_reminders", "telegram_escalation_last_error", "ALTER TABLE sync_reminders ADD COLUMN telegram_escalation_last_error TEXT");

  const reminderColumns = db.prepare("PRAGMA table_info(sync_reminders)").all() as Array<{ name: string }>;
  const hadClientUpdatedAt = reminderColumns.some((c) => c.name === "client_updated_at_utc");
  ensureColumn(db, "sync_reminders", "client_updated_at_utc", "ALTER TABLE sync_reminders ADD COLUMN client_updated_at_utc INTEGER NOT NULL DEFAULT 0");
  if (!hadClientUpdatedAt) {
    db.prepare(
      "UPDATE sync_reminders SET client_updated_at_utc = updated_at_utc, updated_at_utc = ?"
    ).run(Date.now());
  }
  db.exec(`
    CREATE INDEX IF NOT EXISTS idx_users_locked_until ON users(locked_until_utc);
    CREATE INDEX IF NOT EXISTS idx_sync_reminders_escalation_retry
      ON sync_reminders(telegram_escalation_next_retry_utc, telegram_escalation_attempts);
  `);
}

function ensureColumn(db: SqliteDb, table: string, column: string, alterSql: string): void {
  const columns = db.prepare(`PRAGMA table_info(${table})`).all() as Array<{ name: string }>;
  const exists = columns.some((c) => c.name === column);
  if (!exists) {
    db.exec(alterSql);
  }
}
