using System.IO;
using Microsoft.Data.Sqlite;
using NNotify.Models;
using NNotify.Services;

namespace NNotify.Data;

public sealed class ReminderRepository
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public ReminderRepository()
    {
        _dbPath = Path.Combine(ErrorLogger.BaseDirectory, "NNotify.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public string DbPath => _dbPath;

    public async Task InitializeAsync()
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS reminders (
  id TEXT PRIMARY KEY,
  title TEXT NOT NULL,
  due_at_utc INTEGER NOT NULL,
  priority INTEGER NOT NULL,
  created_at_utc INTEGER NOT NULL,
  status TEXT NOT NULL,
  last_fired_at_utc INTEGER NULL,
  acked_at_utc INTEGER NULL,
  snooze_until_utc INTEGER NULL,
  telegram_escalated_at_utc INTEGER NULL
);
CREATE INDEX IF NOT EXISTS idx_reminders_status_due ON reminders(status, due_at_utc);
CREATE INDEX IF NOT EXISTS idx_reminders_snooze ON reminders(snooze_until_utc);
";
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to initialize database", ex);
            throw;
        }
    }

    public async Task AddReminderAsync(Reminder reminder)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO reminders (
  id, title, due_at_utc, priority, created_at_utc, status,
  last_fired_at_utc, acked_at_utc, snooze_until_utc, telegram_escalated_at_utc
) VALUES (
  $id, $title, $due, $priority, $created, $status,
  $lastFired, $acked, $snooze, $telegramEscalated
);";
        BindReminder(command, reminder);
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateReminderAsync(Reminder reminder)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE reminders
SET title = $title,
    due_at_utc = $due,
    priority = $priority,
    status = $status,
    last_fired_at_utc = $lastFired,
    acked_at_utc = $acked,
    snooze_until_utc = $snooze,
    telegram_escalated_at_utc = $telegramEscalated
WHERE id = $id;";
        BindReminder(command, reminder);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<Reminder?> GetByIdAsync(string id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM reminders WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadReminder(reader);
        }

        return null;
    }

    public async Task<List<Reminder>> GetUpcomingAsync(int limit = 100)
    {
        var reminders = new List<Reminder>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT * FROM reminders
WHERE status IN ('scheduled', 'snoozed', 'fired')
ORDER BY COALESCE(snooze_until_utc, due_at_utc) ASC
LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            reminders.Add(ReadReminder(reader));
        }

        return reminders;
    }

    public async Task<List<Reminder>> GetMissedAsync(int limit = 200)
    {
        var reminders = new List<Reminder>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT * FROM reminders
WHERE status = 'missed'
ORDER BY COALESCE(snooze_until_utc, due_at_utc) DESC
LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            reminders.Add(ReadReminder(reader));
        }

        return reminders;
    }

    public async Task<List<Reminder>> GetHistoryAsync(int limit = 300)
    {
        var reminders = new List<Reminder>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT * FROM reminders
WHERE status IN ('acked', 'cancelled')
ORDER BY COALESCE(acked_at_utc, last_fired_at_utc, due_at_utc, created_at_utc) DESC
LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            reminders.Add(ReadReminder(reader));
        }

        return reminders;
    }

    public async Task<Reminder?> GetNextActiveAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT * FROM reminders
WHERE status IN ('scheduled', 'snoozed')
ORDER BY COALESCE(snooze_until_utc, due_at_utc) ASC
LIMIT 1;";

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadReminder(reader);
        }

        return null;
    }

    public async Task<List<Reminder>> GetDueActiveAsync(DateTimeOffset utcNow)
    {
        var due = new List<Reminder>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT * FROM reminders
WHERE status IN ('scheduled', 'snoozed')
  AND COALESCE(snooze_until_utc, due_at_utc) <= $now
ORDER BY COALESCE(snooze_until_utc, due_at_utc) ASC;";
        command.Parameters.AddWithValue("$now", ToUnix(utcNow));

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            due.Add(ReadReminder(reader));
        }

        return due;
    }

    public async Task MarkMissedAtStartupAsync(DateTimeOffset utcNow)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE reminders
SET status = 'missed'
WHERE status IN ('scheduled', 'snoozed', 'fired')
  AND acked_at_utc IS NULL
  AND COALESCE(snooze_until_utc, due_at_utc) < $now;";
        command.Parameters.AddWithValue("$now", ToUnix(utcNow));
        await command.ExecuteNonQueryAsync();
    }

    public async Task MarkStaleFiredAsMissedAsync(DateTimeOffset utcNow, TimeSpan minFiredAge)
    {
        var cutoff = utcNow - minFiredAge;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE reminders
SET status = 'missed'
WHERE status = 'fired'
  AND acked_at_utc IS NULL
  AND last_fired_at_utc IS NOT NULL
  AND last_fired_at_utc <= $cutoff;";
        command.Parameters.AddWithValue("$cutoff", ToUnix(cutoff));
        await command.ExecuteNonQueryAsync();
    }

    public async Task SetFiredAsync(string id, DateTimeOffset utcNow)
    {
        await UpdateStatusCoreAsync(
            id,
            ReminderStatus.Fired,
            lastFiredAtUtc: utcNow,
            ackedAtUtc: null,
            snoozeUntilUtc: null,
            telegramEscalatedAtUtc: null);
    }

    public async Task AckAsync(string id, DateTimeOffset utcNow)
    {
        await UpdateStatusCoreAsync(
            id,
            ReminderStatus.Acked,
            lastFiredAtUtc: null,
            ackedAtUtc: utcNow,
            snoozeUntilUtc: null,
            telegramEscalatedAtUtc: null,
            keepExistingLastFired: true,
            keepExistingTelegramEscalated: true);
    }

    public async Task SnoozeAsync(string id, DateTimeOffset snoozeUntilUtc)
    {
        await UpdateStatusCoreAsync(
            id,
            ReminderStatus.Snoozed,
            lastFiredAtUtc: null,
            ackedAtUtc: null,
            snoozeUntilUtc: snoozeUntilUtc,
            telegramEscalatedAtUtc: null,
            keepExistingLastFired: true,
            keepExistingTelegramEscalated: true);
    }

    public async Task SetTelegramEscalatedAsync(string id, DateTimeOffset utcNow)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE reminders
SET telegram_escalated_at_utc = $value
WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$value", ToUnix(utcNow));
        await command.ExecuteNonQueryAsync();
    }

    public async Task SetMissedAsync(string id)
    {
        await UpdateStatusCoreAsync(
            id,
            ReminderStatus.Missed,
            lastFiredAtUtc: null,
            ackedAtUtc: null,
            snoozeUntilUtc: null,
            telegramEscalatedAtUtc: null,
            keepExistingLastFired: true,
            keepExistingTelegramEscalated: true);
    }

    public async Task CancelAsync(string id, DateTimeOffset utcNow)
    {
        await UpdateStatusCoreAsync(
            id,
            ReminderStatus.Cancelled,
            lastFiredAtUtc: null,
            ackedAtUtc: utcNow,
            snoozeUntilUtc: null,
            telegramEscalatedAtUtc: null,
            keepExistingLastFired: true,
            keepExistingTelegramEscalated: true);
    }

    public async Task DeleteAsync(string id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
DELETE FROM reminders
WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    private async Task UpdateStatusCoreAsync(
        string id,
        string status,
        DateTimeOffset? lastFiredAtUtc,
        DateTimeOffset? ackedAtUtc,
        DateTimeOffset? snoozeUntilUtc,
        DateTimeOffset? telegramEscalatedAtUtc,
        bool keepExistingLastFired = false,
        bool keepExistingTelegramEscalated = false)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $@"
UPDATE reminders
SET status = $status,
    last_fired_at_utc = {(keepExistingLastFired ? "COALESCE(last_fired_at_utc, $lastFired)" : "$lastFired")},
    acked_at_utc = $acked,
    snooze_until_utc = $snooze,
    telegram_escalated_at_utc = {(keepExistingTelegramEscalated ? "COALESCE(telegram_escalated_at_utc, $telegramEscalated)" : "$telegramEscalated")}
WHERE id = $id;";

        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$lastFired", ToDbValue(lastFiredAtUtc));
        command.Parameters.AddWithValue("$acked", ToDbValue(ackedAtUtc));
        command.Parameters.AddWithValue("$snooze", ToDbValue(snoozeUntilUtc));
        command.Parameters.AddWithValue("$telegramEscalated", ToDbValue(telegramEscalatedAtUtc));
        await command.ExecuteNonQueryAsync();
    }

    private static void BindReminder(SqliteCommand command, Reminder reminder)
    {
        command.Parameters.AddWithValue("$id", reminder.Id);
        command.Parameters.AddWithValue("$title", reminder.Title);
        command.Parameters.AddWithValue("$due", ToUnix(reminder.DueAtUtc));
        command.Parameters.AddWithValue("$priority", reminder.Priority);
        command.Parameters.AddWithValue("$created", ToUnix(reminder.CreatedAtUtc));
        command.Parameters.AddWithValue("$status", reminder.Status);
        command.Parameters.AddWithValue("$lastFired", ToDbValue(reminder.LastFiredAtUtc));
        command.Parameters.AddWithValue("$acked", ToDbValue(reminder.AckedAtUtc));
        command.Parameters.AddWithValue("$snooze", ToDbValue(reminder.SnoozeUntilUtc));
        command.Parameters.AddWithValue("$telegramEscalated", ToDbValue(reminder.TelegramEscalatedAtUtc));
    }

    private static Reminder ReadReminder(SqliteDataReader reader)
    {
        return new Reminder
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            Title = reader.GetString(reader.GetOrdinal("title")),
            DueAtUtc = FromUnix(reader.GetInt64(reader.GetOrdinal("due_at_utc"))),
            Priority = reader.GetInt32(reader.GetOrdinal("priority")),
            CreatedAtUtc = FromUnix(reader.GetInt64(reader.GetOrdinal("created_at_utc"))),
            Status = reader.GetString(reader.GetOrdinal("status")),
            LastFiredAtUtc = GetNullableDate(reader, "last_fired_at_utc"),
            AckedAtUtc = GetNullableDate(reader, "acked_at_utc"),
            SnoozeUntilUtc = GetNullableDate(reader, "snooze_until_utc"),
            TelegramEscalatedAtUtc = GetNullableDate(reader, "telegram_escalated_at_utc")
        };
    }

    private static DateTimeOffset? GetNullableDate(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return FromUnix(reader.GetInt64(ordinal));
    }

    private static object ToDbValue(DateTimeOffset? value)
    {
        return value.HasValue ? ToUnix(value.Value) : DBNull.Value;
    }

    private static long ToUnix(DateTimeOffset value)
    {
        return value.ToUnixTimeSeconds();
    }

    private static DateTimeOffset FromUnix(long unixSeconds)
    {
        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
    }
}
