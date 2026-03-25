export interface AccessClaims {
  sub: string;
  username: string;
  typ: "access";
  iat?: number;
  exp?: number;
}

export type UserStatus = "pending" | "approved" | "rejected";

export interface SyncReminderInput {
  id: string;
  title: string;
  dueAtUtc: string;
  priority: number;
  createdAtUtc: string;
  status: string;
  lastFiredAtUtc?: string | null;
  ackedAtUtc?: string | null;
  snoozeUntilUtc?: string | null;
  telegramEscalatedAtUtc?: string | null;
  deleted?: boolean;
}