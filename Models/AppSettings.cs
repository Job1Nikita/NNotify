namespace NNotify.Models;

public sealed class AppSettings
{
    public string? TelegramBotTokenEncrypted { get; set; }
    public string? TelegramBotTokenPlain { get; set; }
    public string? TelegramChatId { get; set; }
    public string? TelegramUserId { get; set; }
    public string? SyncServerUrl { get; set; }
    public string? SyncUsername { get; set; }
    public string? SyncDeviceId { get; set; }
    public string? SyncAccessTokenEncrypted { get; set; }
    public string? SyncRefreshTokenEncrypted { get; set; }
    public DateTimeOffset? SyncAccessTokenExpiresAtUtc { get; set; }
    public bool HotKeyEnabled { get; set; } = true;
    public string HotKeyGesture { get; set; } = "Ctrl+Alt+R";
    public bool MainWindowTopmost { get; set; }
    public bool UseDarkTheme { get; set; }
}
