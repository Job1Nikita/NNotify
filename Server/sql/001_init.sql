CREATE TABLE IF NOT EXISTS users (
  id TEXT PRIMARY KEY,
  username TEXT NOT NULL UNIQUE,
  password_hash TEXT NOT NULL,
  status TEXT NOT NULL CHECK (status IN ('pending', 'approved', 'rejected')),
  created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  approved_at_utc TIMESTAMPTZ NULL,
  approved_by TEXT NULL
);

CREATE TABLE IF NOT EXISTS refresh_sessions (
  id TEXT PRIMARY KEY,
  user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  device_id TEXT NOT NULL,
  refresh_token_hash TEXT NOT NULL UNIQUE,
  created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  expires_at_utc TIMESTAMPTZ NOT NULL,
  revoked_at_utc TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS idx_refresh_sessions_user_device
  ON refresh_sessions (user_id, device_id);

CREATE TABLE IF NOT EXISTS reminders (
  id TEXT NOT NULL,
  user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  title TEXT NOT NULL,
  due_at_utc TIMESTAMPTZ NOT NULL,
  priority INTEGER NOT NULL,
  created_at_utc TIMESTAMPTZ NOT NULL,
  status TEXT NOT NULL,
  last_fired_at_utc TIMESTAMPTZ NULL,
  acked_at_utc TIMESTAMPTZ NULL,
  snooze_until_utc TIMESTAMPTZ NULL,
  telegram_escalated_at_utc TIMESTAMPTZ NULL,
  deleted_at_utc TIMESTAMPTZ NULL,
  updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY (user_id, id)
);

CREATE INDEX IF NOT EXISTS idx_reminders_user_updated
  ON reminders (user_id, updated_at_utc);

CREATE TABLE IF NOT EXISTS sync_events (
  id BIGSERIAL PRIMARY KEY,
  user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  reminder_id TEXT NULL,
  kind TEXT NOT NULL,
  created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_sync_events_user_created
  ON sync_events (user_id, created_at_utc);