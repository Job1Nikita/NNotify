using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NNotify.Models;

namespace NNotify.Services;

public sealed class SettingsService
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("NNotify.SettingsEntropy.v1");
    private readonly string _settingsPath;

    public SettingsService()
    {
        _settingsPath = Path.Combine(ErrorLogger.BaseDirectory, "settings.json");
    }

    public string SettingsPath => _settingsPath;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to load settings", ex);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to save settings", ex);
        }
    }

    public string? GetBotToken(AppSettings settings)
    {
        return Decrypt(settings.TelegramBotTokenEncrypted, settings.TelegramBotTokenPlain, "Failed to decrypt Telegram token");
    }

    public bool SetBotToken(AppSettings settings, string? token)
    {
        var previousEncrypted = settings.TelegramBotTokenEncrypted;
        var previousPlain = settings.TelegramBotTokenPlain;

        if (string.IsNullOrWhiteSpace(token))
        {
            settings.TelegramBotTokenEncrypted = null;
            settings.TelegramBotTokenPlain = null;
            return true;
        }

        try
        {
            settings.TelegramBotTokenEncrypted = Encrypt(token);
            settings.TelegramBotTokenPlain = null;
            return true;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to encrypt Telegram token, token was not updated", ex);
            settings.TelegramBotTokenEncrypted = previousEncrypted;
            settings.TelegramBotTokenPlain = previousPlain;
            return false;
        }
    }

    public string? GetSyncAccessToken(AppSettings settings)
    {
        return Decrypt(settings.SyncAccessTokenEncrypted, null, "Failed to decrypt sync access token");
    }

    public string? GetSyncRefreshToken(AppSettings settings)
    {
        return Decrypt(settings.SyncRefreshTokenEncrypted, null, "Failed to decrypt sync refresh token");
    }

    public bool SetSyncSession(
        AppSettings settings,
        string? accessToken,
        string? refreshToken,
        DateTimeOffset? accessTokenExpiresAtUtc)
    {
        var previousAccessEncrypted = settings.SyncAccessTokenEncrypted;
        var previousRefreshEncrypted = settings.SyncRefreshTokenEncrypted;
        var previousExpiresAt = settings.SyncAccessTokenExpiresAtUtc;

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
        {
            ClearSyncSession(settings);
            return true;
        }

        try
        {
            settings.SyncAccessTokenEncrypted = Encrypt(accessToken);
            settings.SyncRefreshTokenEncrypted = Encrypt(refreshToken);
            settings.SyncAccessTokenExpiresAtUtc = accessTokenExpiresAtUtc;
            return true;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to encrypt sync session, session was not updated", ex);
            settings.SyncAccessTokenEncrypted = previousAccessEncrypted;
            settings.SyncRefreshTokenEncrypted = previousRefreshEncrypted;
            settings.SyncAccessTokenExpiresAtUtc = previousExpiresAt;
            return false;
        }
    }

    public void ClearSyncSession(AppSettings settings)
    {
        settings.SyncAccessTokenEncrypted = null;
        settings.SyncRefreshTokenEncrypted = null;
        settings.SyncAccessTokenExpiresAtUtc = null;
    }

    private static string Encrypt(string value)
    {
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    private static string? Decrypt(string? encryptedBase64, string? fallback, string errorLogMessage)
    {
        if (!string.IsNullOrWhiteSpace(encryptedBase64))
        {
            try
            {
                var bytes = Convert.FromBase64String(encryptedBase64);
                var decrypted = ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(errorLogMessage, ex);
            }
        }

        return fallback;
    }
}
