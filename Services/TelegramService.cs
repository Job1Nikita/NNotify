using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace NNotify.Services;

public sealed class TelegramService
{
    private readonly HttpClient _httpClient;

    public TelegramService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async Task<bool> SendEscalationAsync(string botToken, string chatId, string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
        {
            return false;
        }

        var endpoint = $"https://api.telegram.org/bot{botToken}/sendMessage";
        var payload = new TelegramSendMessageRequest
        {
            ChatId = chatId,
            Text = message,
            DisableNotification = false
        };

        var retries = 2;
        for (var attempt = 0; attempt <= retries; attempt++)
        {
            try
            {
                using var response = await _httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                if ((int)response.StatusCode >= 500 && attempt < retries)
                {
                    await Task.Delay(GetBackoff(attempt), cancellationToken);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < retries)
                {
                    await Task.Delay(GetBackoff(attempt + 1), cancellationToken);
                    continue;
                }

                return false;
            }
            catch (HttpRequestException) when (attempt < retries)
            {
                await Task.Delay(GetBackoff(attempt), cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < retries)
            {
                await Task.Delay(GetBackoff(attempt), cancellationToken);
            }
        }

        return false;
    }

    private static TimeSpan GetBackoff(int attempt)
    {
        return attempt switch
        {
            0 => TimeSpan.FromSeconds(1),
            1 => TimeSpan.FromSeconds(2),
            _ => TimeSpan.FromSeconds(4)
        };
    }

    private sealed class TelegramSendMessageRequest
    {
        [JsonPropertyName("chat_id")]
        public string ChatId { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("disable_notification")]
        public bool DisableNotification { get; set; }
    }
}
