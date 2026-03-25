using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace NNotify.Services;

public sealed class SyncAuthService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public SyncAuthService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    public async Task<SyncAuthResult> RegisterAsync(
        Uri serverBaseUri,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var endpoint = new Uri(serverBaseUri, "/v1/auth/register");
        var payload = new RegisterRequest
        {
            Username = username,
            Password = password
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new SyncAuthResult(true, "Registration request was submitted. Wait for admin approval.", PendingApproval: true);
            }

            var error = await TryReadErrorMessageAsync(response, cancellationToken);
            return response.StatusCode switch
            {
                HttpStatusCode.Conflict => new SyncAuthResult(false, "User with this login already exists."),
                HttpStatusCode.BadRequest => new SyncAuthResult(false, string.IsNullOrWhiteSpace(error) ? "Check login and password format." : error),
                _ => new SyncAuthResult(false, string.IsNullOrWhiteSpace(error) ? $"Registration error ({(int)response.StatusCode})." : error)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new SyncAuthResult(false, "Connection timeout.");
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Sync register request failed", ex);
            return new SyncAuthResult(false, "Registration request failed. Check server URL and TLS certificate.");
        }
    }

    public async Task<SyncLoginResult> LoginAsync(
        Uri serverBaseUri,
        string username,
        string password,
        string deviceId,
        string deviceName,
        CancellationToken cancellationToken)
    {
        var endpoint = new Uri(serverBaseUri, "/v1/auth/login");
        var payload = new LoginRequest
        {
            Username = username,
            Password = password,
            DeviceId = deviceId,
            DeviceName = deviceName
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, cancellationToken);
                if (loginResponse is null ||
                    string.IsNullOrWhiteSpace(loginResponse.AccessToken) ||
                    string.IsNullOrWhiteSpace(loginResponse.RefreshToken))
                {
                    return new SyncLoginResult(false, "Server returned invalid login response.");
                }

                var expiresAt = DateTimeOffset.UtcNow.AddSeconds(
                    loginResponse.AccessTokenExpiresInSeconds > 0
                        ? loginResponse.AccessTokenExpiresInSeconds
                        : 900);

                return new SyncLoginResult(
                    true,
                    "Signed in successfully.",
                    AccessToken: loginResponse.AccessToken,
                    RefreshToken: loginResponse.RefreshToken,
                    AccessTokenExpiresAtUtc: expiresAt);
            }

            var error = await TryReadErrorMessageAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return new SyncLoginResult(
                    false,
                    string.IsNullOrWhiteSpace(error) ? "Access denied. Account is pending admin approval." : error,
                    PendingApproval: true);
            }

            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => new SyncLoginResult(false, string.IsNullOrWhiteSpace(error) ? "Invalid username or password." : error),
                HttpStatusCode.BadRequest => new SyncLoginResult(false, string.IsNullOrWhiteSpace(error) ? "Check login request payload." : error),
                _ => new SyncLoginResult(false, string.IsNullOrWhiteSpace(error) ? $"Login error ({(int)response.StatusCode})." : error)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new SyncLoginResult(false, "Connection timeout.");
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Sync login request failed", ex);
            return new SyncLoginResult(false, "Login request failed. Check server URL and TLS certificate.");
        }
    }

    public async Task LogoutAsync(
        Uri serverBaseUri,
        string refreshToken,
        string deviceId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var endpoint = new Uri(serverBaseUri, "/v1/auth/logout");
        var payload = new LogoutRequest
        {
            RefreshToken = refreshToken,
            DeviceId = deviceId
        };

        try
        {
            using var _ = await _httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Sync logout request failed", ex);
        }
    }

    private static async Task<string?> TryReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions, cancellationToken);
            if (!string.IsNullOrWhiteSpace(error?.Message))
            {
                return error.Message.Trim();
            }

            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        }
        catch
        {
            return null;
        }
    }

    private sealed class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    private sealed class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
    }

    private sealed class LogoutRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
    }

    private sealed class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int AccessTokenExpiresInSeconds { get; set; }
    }

    private sealed class ErrorResponse
    {
        public string? Message { get; set; }
    }
}

public sealed record SyncAuthResult(bool Success, string Message, bool PendingApproval = false);

public sealed record SyncLoginResult(
    bool Success,
    string Message,
    string? AccessToken = null,
    string? RefreshToken = null,
    DateTimeOffset? AccessTokenExpiresAtUtc = null,
    bool PendingApproval = false);
