export type UserStatus = "pending" | "active" | "blocked" | "rejected";
export type ReminderStatus = "scheduled" | "fired" | "acked" | "snoozed" | "cancelled" | "missed";

export interface UserRecord {
  id: string;
  username: string;
  password_hash: string;
  status: UserStatus;
  created_at: string;
  updated_at: string;
  approved_at: string | null;
  approved_by: string | null;
  rejected_at: string | null;
  rejected_by: string | null;
  reject_reason: string | null;
  failed_login_attempts: number;
  locked_until_utc: string | null;
  last_failed_login_at: string | null;
  telegram_user_id: string | null;
}

export interface SyncReminderRecord {
  id: string;
  user_id: string;
  title: string;
  due_at_utc: number;
  priority: number;
  created_at_utc: number;
  status: ReminderStatus;
  last_fired_at_utc: number | null;
  acked_at_utc: number | null;
  snooze_until_utc: number | null;
  telegram_escalated_at_utc: number | null;
  telegram_escalation_attempts: number;
  telegram_escalation_next_retry_utc: number | null;
  telegram_escalation_last_error: string | null;
  updated_at_utc: number;
  client_updated_at_utc: number;
  deleted_at_utc: number | null;
  source_device_id: string | null;
  duplicate_candidate: number;
}

export interface SessionRecord {
  id: string;
  user_id: string;
  device_id: string;
  device_name: string;
  refresh_token_hash: string;
  created_at: string;
  expires_at: string;
  revoked_at: string | null;
  revoked_reason: string | null;
  ip: string | null;
  user_agent: string | null;
  last_used_at: string | null;
}

export interface RegisterBody {
  username: string;
  password: string;
}

export interface LoginBody {
  username: string;
  password: string;
  deviceId: string;
  deviceName: string;
}

export interface LogoutBody {
  refreshToken: string;
  deviceId: string;
}

export interface RefreshBody {
  refreshToken: string;
  deviceId: string;
  deviceName: string;
}
