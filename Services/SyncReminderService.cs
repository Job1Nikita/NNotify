using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using NNotify.Data;
using NNotify.Models;

namespace NNotify.Services;

public sealed class SyncReminderService : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int SyncBatchSize = 40;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ReminderRepository _repository;
    private readonly SettingsService _settingsService;
    private readonly SyncAuthService _authService;
    private readonly Func<AppSettings> _settingsAccessor;
    private readonly Action _saveSettings;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _signal = new(0, int.MaxValue);

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public SyncReminderService(
        ReminderRepository repository,
        SettingsService settingsService,
        SyncAuthService authService,
        Func<AppSettings> settingsAccessor,
        Action saveSettings,
        HttpClient? httpClient = null)
    {
        _repository = repository;
        _settingsService = settingsService;
        _authService = authService;
        _settingsAccessor = settingsAccessor;
        _saveSettings = saveSettings;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public event Action<IReadOnlyList<Reminder>>? RemoteRemindersApplied;

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
            SignalSync();
            if (_loopTask is not null)
            {
                await _loopTask;
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to stop sync service", ex);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _loopTask = null;
        }
    }

    public void SignalSync()
    {
        try
        {
            _signal.Release();
        }
        catch
        {
            // Ignore release races.
        }
    }

    public bool IsSyncActive()
    {
        var settings = _settingsAccessor();
        return !string.IsNullOrWhiteSpace(settings.SyncServerUrl) &&
               !string.IsNullOrWhiteSpace(settings.SyncRefreshTokenEncrypted);
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await SyncOnceAsync(cancellationToken);
                await _signal.WaitAsync(PollInterval, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                ErrorLogger.Log("Sync reminder loop failed", ex);
                try
                {
                    await Task.Delay(PollInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task SyncOnceAsync(CancellationToken cancellationToken)
    {
        var settings = _settingsAccessor();
        if (!TryGetServerUri(settings, out var serverUri))
        {
            return;
        }

        var accessToken = await EnsureAccessTokenAsync(serverUri, settings, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        await EnsureInitialLocalPublishAsync(settings);
        await PushDirtyAsync(serverUri, settings, accessToken, cancellationToken);
        await PullRemoteAsync(serverUri, settings, accessToken, cancellationToken);
    }

    public async Task EnsureInitialLocalPublishAsync()
    {
        await EnsureInitialLocalPublishAsync(_settingsAccessor());
    }

    private async Task EnsureInitialLocalPublishAsync(AppSettings settings)
    {
        if (settings.SyncInitialLocalPublishCompleted)
        {
            return;
        }

        await _repository.MarkAllLocalForInitialSyncAsync();
        settings.SyncInitialLocalPublishCompleted = true;
        _saveSettings();
    }

    private async Task PushDirtyAsync(Uri serverUri, AppSettings settings, string accessToken, CancellationToken cancellationToken)
    {
        var dirty = await _repository.GetDirtyForSyncAsync();
        var telegramUserId = ResolveSyncTelegramUserId(settings);
        var normalizedTelegramUserId = string.IsNullOrWhiteSpace(telegramUserId) ? string.Empty : telegramUserId.Trim();
        var telegramTargetChanged = !string.Equals(
            settings.SyncLastUploadedTelegramUserId ?? string.Empty,
            normalizedTelegramUserId,
            StringComparison.Ordinal);

        if (dirty.Count == 0 && !telegramTargetChanged)
        {
            return;
        }

        var dirtyChunks = dirty.Count == 0
            ? [Array.Empty<Reminder>()]
            : dirty.Chunk(SyncBatchSize);
        var shouldUploadTelegramTarget = telegramTargetChanged;

        foreach (var chunk in dirtyChunks)
        {
            var payload = new SyncBatchRequest
            {
                DeviceId = EnsureDeviceId(settings),
                TelegramUserId = shouldUploadTelegramTarget ? normalizedTelegramUserId : null,
                Changes = chunk.Select(ToDto).ToList()
            };

            var response = await SendAuthorizedAsync(
                HttpMethod.Post,
                new Uri(serverUri, "/v1/sync/reminders/batch"),
                accessToken,
                payload,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return;
            }

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<SyncResponse>(JsonOptions, cancellationToken);
            await _repository.MarkSyncedAsync(chunk.Select(r => r.Id));

            if (shouldUploadTelegramTarget)
            {
                settings.SyncLastUploadedTelegramUserId = normalizedTelegramUserId;
                _saveSettings();
                shouldUploadTelegramTarget = false;
            }

            if (body?.Reminders is { Count: > 0 })
            {
                var applied = await _repository.ApplyRemoteRemindersAsync(body.Reminders.Select(FromDto));
                NotifyRemoteApplied(applied);
            }
        }
    }

    private async Task PullRemoteAsync(Uri serverUri, AppSettings settings, string accessToken, CancellationToken cancellationToken)
    {
        var endpoint = new Uri(serverUri, $"/v1/sync/reminders?since={settings.SyncLastPulledChangeUtc}");
        using var response = await SendAuthorizedAsync(HttpMethod.Get, endpoint, accessToken, payload: null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SyncResponse>(JsonOptions, cancellationToken);
        if (body is null)
        {
            return;
        }

        if (body.Reminders.Count > 0)
        {
            var applied = await _repository.ApplyRemoteRemindersAsync(body.Reminders.Select(FromDto));
            NotifyRemoteApplied(applied);
            settings.SyncLastPulledChangeUtc = Math.Max(
                settings.SyncLastPulledChangeUtc,
                body.Reminders.Max(r => r.UpdatedAtUtc));
        }

        settings.SyncLastPulledChangeUtc = Math.Max(settings.SyncLastPulledChangeUtc, body.ServerTimeUtc - 1);
        _saveSettings();
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpMethod method,
        Uri endpoint,
        string accessToken,
        object? payload,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private async Task<string?> EnsureAccessTokenAsync(Uri serverUri, AppSettings settings, CancellationToken cancellationToken)
    {
        var accessToken = _settingsService.GetSyncAccessToken(settings);
        if (!string.IsNullOrWhiteSpace(accessToken) &&
            settings.SyncAccessTokenExpiresAtUtc is { } expiresAt &&
            expiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return accessToken;
        }

        var refreshToken = _settingsService.GetSyncRefreshToken(settings);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var result = await _authService.RefreshAsync(
            serverUri,
            refreshToken,
            EnsureDeviceId(settings),
            Environment.MachineName,
            cancellationToken);

        if (!result.Success ||
            string.IsNullOrWhiteSpace(result.AccessToken) ||
            string.IsNullOrWhiteSpace(result.RefreshToken))
        {
            return null;
        }

        if (_settingsService.SetSyncSession(settings, result.AccessToken, result.RefreshToken, result.AccessTokenExpiresAtUtc))
        {
            _saveSettings();
            return result.AccessToken;
        }

        return null;
    }

    private void NotifyRemoteApplied(IReadOnlyList<Reminder> reminders)
    {
        if (reminders.Count > 0)
        {
            RemoteRemindersApplied?.Invoke(reminders);
        }
    }

    private static bool TryGetServerUri(AppSettings settings, out Uri serverUri)
    {
        serverUri = null!;
        if (string.IsNullOrWhiteSpace(settings.SyncServerUrl))
        {
            return false;
        }

        var raw = settings.SyncServerUrl.Contains("://", StringComparison.Ordinal)
            ? settings.SyncServerUrl
            : $"https://{settings.SyncServerUrl}";

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var parsed) ||
            !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        serverUri = parsed;
        return true;
    }

    private static string EnsureDeviceId(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.SyncDeviceId))
        {
            return settings.SyncDeviceId;
        }

        settings.SyncDeviceId = Guid.NewGuid().ToString("N");
        return settings.SyncDeviceId;
    }

    private static string? ResolveSyncTelegramUserId(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.SyncTelegramUserId))
        {
            return settings.SyncTelegramUserId;
        }

        return string.IsNullOrWhiteSpace(settings.TelegramUserId) ? null : settings.TelegramUserId;
    }

    private static SyncReminderDto ToDto(Reminder reminder)
    {
        return new SyncReminderDto
        {
            Id = reminder.Id,
            Title = reminder.Title,
            DueAtUtc = ToUnixMs(reminder.DueAtUtc),
            Priority = reminder.Priority,
            CreatedAtUtc = ToUnixMs(reminder.CreatedAtUtc),
            Status = reminder.Status,
            LastFiredAtUtc = ToUnixMs(reminder.LastFiredAtUtc),
            AckedAtUtc = ToUnixMs(reminder.AckedAtUtc),
            SnoozeUntilUtc = ToUnixMs(reminder.SnoozeUntilUtc),
            TelegramEscalatedAtUtc = ToUnixMs(reminder.TelegramEscalatedAtUtc),
            UpdatedAtUtc = ToUnixMs(reminder.SyncUpdatedAtUtc),
            DeletedAtUtc = ToUnixMs(reminder.SyncDeletedAtUtc)
        };
    }

    private static Reminder FromDto(SyncReminderDto dto)
    {
        return new Reminder
        {
            Id = dto.Id,
            Title = dto.Title,
            DueAtUtc = FromUnixMs(dto.DueAtUtc),
            Priority = dto.Priority,
            CreatedAtUtc = FromUnixMs(dto.CreatedAtUtc),
            Status = dto.Status,
            LastFiredAtUtc = FromUnixMsNullable(dto.LastFiredAtUtc),
            AckedAtUtc = FromUnixMsNullable(dto.AckedAtUtc),
            SnoozeUntilUtc = FromUnixMsNullable(dto.SnoozeUntilUtc),
            TelegramEscalatedAtUtc = FromUnixMsNullable(dto.TelegramEscalatedAtUtc),
            SyncUpdatedAtUtc = FromUnixMs(dto.UpdatedAtUtc),
            SyncDeletedAtUtc = FromUnixMsNullable(dto.DeletedAtUtc),
            SyncDirty = false,
            SyncDuplicateCandidate = dto.DuplicateCandidate
        };
    }

    private static long ToUnixMs(DateTimeOffset value) => value.ToUnixTimeMilliseconds();

    private static long? ToUnixMs(DateTimeOffset? value) => value?.ToUnixTimeMilliseconds();

    private static DateTimeOffset FromUnixMs(long value) => DateTimeOffset.FromUnixTimeMilliseconds(value);

    private static DateTimeOffset? FromUnixMsNullable(long? value) => value.HasValue ? FromUnixMs(value.Value) : null;

    public void Dispose()
    {
        _signal.Dispose();
        _cts?.Dispose();
        _httpClient.Dispose();
    }

    private sealed class SyncBatchRequest
    {
        public string DeviceId { get; set; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TelegramUserId { get; set; }

        public List<SyncReminderDto> Changes { get; set; } = [];
    }

    private sealed class SyncResponse
    {
        public long ServerTimeUtc { get; set; }
        public List<SyncReminderDto> Reminders { get; set; } = [];
    }

    private sealed class SyncReminderDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public long DueAtUtc { get; set; }
        public int Priority { get; set; }
        public long CreatedAtUtc { get; set; }
        public string Status { get; set; } = ReminderStatus.Scheduled;
        public long? LastFiredAtUtc { get; set; }
        public long? AckedAtUtc { get; set; }
        public long? SnoozeUntilUtc { get; set; }
        public long? TelegramEscalatedAtUtc { get; set; }
        public long UpdatedAtUtc { get; set; }
        public long? DeletedAtUtc { get; set; }
        public bool DuplicateCandidate { get; set; }
    }
}
