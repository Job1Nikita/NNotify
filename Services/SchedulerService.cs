using NNotify.Data;
using NNotify.Localization;
using NNotify.Models;

namespace NNotify.Services;

public sealed class SchedulerService : IDisposable
{
    private static readonly TimeSpan MaxSemaphoreWaitTimeout = TimeSpan.FromMilliseconds(int.MaxValue);
    private readonly ReminderRepository _repository;
    private readonly TelegramService _telegramService;
    private readonly SettingsService _settingsService;
    private readonly Func<AppSettings> _settingsAccessor;
    private readonly SemaphoreSlim _wakeSignal = new(0, int.MaxValue);

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public SchedulerService(
        ReminderRepository repository,
        TelegramService telegramService,
        SettingsService settingsService,
        Func<AppSettings> settingsAccessor)
    {
        _repository = repository;
        _telegramService = telegramService;
        _settingsService = settingsService;
        _settingsAccessor = settingsAccessor;
    }

    public Func<Reminder, Task<OverlayAction>>? ReminderDueHandlerAsync { get; set; }
    public event Action? RemindersChanged;

    public void Start()
    {
        if (_loopTask is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => LoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        try
        {
            _cts.Cancel();
            SignalReschedule();
            if (_loopTask is not null)
            {
                await _loopTask;
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to stop scheduler", ex);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _loopTask = null;
        }
    }

    public void SignalReschedule()
    {
        try
        {
            _wakeSignal.Release();
        }
        catch
        {
            // Ignore release races.
        }
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await HandleDueRemindersAsync(cancellationToken);

                var nextReminder = await _repository.GetNextActiveAsync();
                var now = DateTimeOffset.UtcNow;
                var delay = nextReminder is null
                    ? TimeSpan.FromSeconds(30)
                    : nextReminder.EffectiveDueUtc - now;

                if (delay < TimeSpan.Zero)
                {
                    delay = TimeSpan.Zero;
                }

                var waitTimeoutMs = ClampWaitTimeoutMilliseconds(delay);
                await _wakeSignal.WaitAsync(waitTimeoutMs, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                ErrorLogger.Log("Scheduler loop error", ex);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private static int ClampWaitTimeoutMilliseconds(TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
        {
            return 0;
        }

        if (delay >= MaxSemaphoreWaitTimeout)
        {
            return int.MaxValue;
        }

        return (int)Math.Ceiling(delay.TotalMilliseconds);
    }

    private async Task HandleDueRemindersAsync(CancellationToken cancellationToken)
    {
        var dueReminders = await _repository.GetDueActiveAsync(DateTimeOffset.UtcNow);
        foreach (var reminder in dueReminders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await _repository.SetFiredAsync(reminder.Id, DateTimeOffset.UtcNow);
            reminder.Status = ReminderStatus.Fired;
            reminder.LastFiredAtUtc = DateTimeOffset.UtcNow;

            OverlayAction action = OverlayAction.Timeout;
            if (ReminderDueHandlerAsync is not null)
            {
                try
                {
                    action = await ReminderDueHandlerAsync(reminder);
                }
                catch (Exception ex)
                {
                    ErrorLogger.Log("Reminder due handler failed", ex);
                }
            }

            await ApplyReminderActionAsync(reminder, action, cancellationToken);
            RemindersChanged?.Invoke();
        }
    }

    private async Task ApplyReminderActionAsync(Reminder reminder, OverlayAction action, CancellationToken cancellationToken)
    {
        switch (action)
        {
            case OverlayAction.Ack:
                await _repository.AckAsync(reminder.Id, DateTimeOffset.UtcNow);
                return;

            case OverlayAction.Snooze5:
                await _repository.SnoozeAsync(reminder.Id, DateTimeOffset.UtcNow.AddMinutes(5));
                SignalReschedule();
                return;

            case OverlayAction.Snooze15:
                await _repository.SnoozeAsync(reminder.Id, DateTimeOffset.UtcNow.AddMinutes(15));
                SignalReschedule();
                return;

            case OverlayAction.Edited:
                SignalReschedule();
                return;

            case OverlayAction.Timeout:
            default:
                await EscalateToTelegramAsync(reminder, cancellationToken);
                await _repository.SetMissedAsync(reminder.Id);
                return;
        }
    }

    private async Task EscalateToTelegramAsync(Reminder reminder, CancellationToken cancellationToken)
    {
        try
        {
            var settings = _settingsAccessor();
            var botToken = _settingsService.GetBotToken(settings);
            var targetId = ResolveTelegramTargetId(settings);

            if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(targetId))
            {
                return;
            }

            var message = BuildTelegramMessage(reminder);

            var sent = await _telegramService.SendEscalationAsync(botToken, targetId, message, cancellationToken);
            if (sent)
            {
                await _repository.SetTelegramEscalatedAsync(reminder.Id, DateTimeOffset.UtcNow);
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Telegram escalation failed", ex);
        }
    }

    private static string BuildTelegramMessage(Reminder reminder)
    {
        var dueLocal = reminder.EffectiveDueUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
        var cleanTitle = CollapseToSingleLine(reminder.Title);
        return $"{reminder.PriorityTelegramHeader}\n{cleanTitle}\n{Loc.Text("TelegramScheduledPrefix")} {dueLocal}";
    }

    private static string CollapseToSingleLine(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Loc.Text("TelegramUntitled");
        }

        return string.Join(" ", value
            .Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string? ResolveTelegramTargetId(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.TelegramChatId))
        {
            return settings.TelegramChatId;
        }

        if (!string.IsNullOrWhiteSpace(settings.TelegramUserId))
        {
            return settings.TelegramUserId;
        }

        return null;
    }

    public void Dispose()
    {
        _wakeSignal.Dispose();
        _cts?.Dispose();
    }
}
