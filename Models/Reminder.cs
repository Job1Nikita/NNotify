using NNotify.Localization;

namespace NNotify.Models;

public static class ReminderStatus
{
    public const string Scheduled = "scheduled";
    public const string Fired = "fired";
    public const string Acked = "acked";
    public const string Snoozed = "snoozed";
    public const string Cancelled = "cancelled";
    public const string Missed = "missed";
}

public sealed class Reminder
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset DueAtUtc { get; set; }
    public int Priority { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = ReminderStatus.Scheduled;
    public DateTimeOffset? LastFiredAtUtc { get; set; }
    public DateTimeOffset? AckedAtUtc { get; set; }
    public DateTimeOffset? SnoozeUntilUtc { get; set; }
    public DateTimeOffset? TelegramEscalatedAtUtc { get; set; }

    public DateTimeOffset EffectiveDueUtc => SnoozeUntilUtc ?? DueAtUtc;
    public DateTime EffectiveDueLocal => EffectiveDueUtc.LocalDateTime;
    public DateTimeOffset HistoryMomentUtc => AckedAtUtc ?? LastFiredAtUtc ?? EffectiveDueUtc;
    public DateTime HistoryMomentLocal => HistoryMomentUtc.LocalDateTime;
    public string EscalatedLocalText => TelegramEscalatedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;

    public string StatusLabel => Status switch
    {
        ReminderStatus.Scheduled => Loc.Text("ReminderStatusScheduled"),
        ReminderStatus.Fired => Loc.Text("ReminderStatusFired"),
        ReminderStatus.Acked => Loc.Text("ReminderStatusAcked"),
        ReminderStatus.Snoozed => Loc.Text("ReminderStatusSnoozed"),
        ReminderStatus.Cancelled => Loc.Text("ReminderStatusCancelled"),
        ReminderStatus.Missed => Loc.Text("ReminderStatusMissed"),
        _ => Status
    };

    public string PriorityLabel => Priority switch
    {
        0 => Loc.Text("PriorityHigh"),
        2 => Loc.Text("PriorityLow"),
        _ => Loc.Text("PriorityMedium")
    };

    public string PriorityTelegramLabel => Priority switch
    {
        0 => Loc.Text("TelegramHeaderHigh"),
        2 => Loc.Text("TelegramHeaderLow"),
        _ => Loc.Text("TelegramHeaderMedium")
    };

    public string PriorityTelegramHeader => Priority switch
    {
        0 => Loc.Text("TelegramHeaderHigh"),
        2 => Loc.Text("TelegramHeaderLow"),
        _ => Loc.Text("TelegramHeaderMedium")
    };
}
