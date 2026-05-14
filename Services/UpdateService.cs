using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace NNotify.Services;

public sealed class UpdateService
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/Job1Nikita/NNotify/releases/latest";
    private const string DefaultReleasesPageUrl = "https://github.com/Job1Nikita/NNotify/releases";
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static string ReleasesPageUrl => DefaultReleasesPageUrl;

    public async Task<UpdateCheckResult> CheckLatestAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = ResolveCurrentVersionText();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("NNotify", currentVersion));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return UpdateCheckResult.Failed(currentVersion, $"GitHub returned {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagElement))
            {
                return UpdateCheckResult.Failed(currentVersion, "GitHub release response does not contain tag_name.");
            }

            var latestVersionText = NormalizeVersionText(tagElement.GetString());
            if (string.IsNullOrWhiteSpace(latestVersionText) ||
                !Version.TryParse(latestVersionText, out var latestVersion) ||
                !Version.TryParse(NormalizeVersionText(currentVersion), out var current))
            {
                return UpdateCheckResult.Failed(currentVersion, "Could not parse release version.");
            }

            var releaseUrl = root.TryGetProperty("html_url", out var urlElement)
                ? urlElement.GetString()
                : null;

            return new UpdateCheckResult(
                Success: true,
                IsUpdateAvailable: CompareVersions(latestVersion, current) > 0,
                CurrentVersion: currentVersion,
                LatestVersion: latestVersionText,
                ReleaseUrl: string.IsNullOrWhiteSpace(releaseUrl) ? DefaultReleasesPageUrl : releaseUrl,
                ErrorMessage: null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return UpdateCheckResult.Failed(currentVersion, ex.Message);
        }
    }

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        };
    }

    private static string ResolveCurrentVersionText()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(UpdateService).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            return NormalizeVersionText(informational);
        }

        var version = assembly.GetName().Version;
        return version is null
            ? "0.0.0"
            : $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
    }

    private static string NormalizeVersionText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        var suffixIndex = normalized.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
        {
            normalized = normalized[..suffixIndex];
        }

        return normalized.Trim();
    }

    private static int CompareVersions(Version left, Version right)
    {
        var leftNormalized = NormalizeVersion(left);
        var rightNormalized = NormalizeVersion(right);
        return leftNormalized.CompareTo(rightNormalized);
    }

    private static Version NormalizeVersion(Version version)
    {
        return new Version(
            Math.Max(0, version.Major),
            Math.Max(0, version.Minor),
            Math.Max(0, version.Build),
            Math.Max(0, version.Revision));
    }
}

public sealed record UpdateCheckResult(
    bool Success,
    bool IsUpdateAvailable,
    string CurrentVersion,
    string? LatestVersion,
    string? ReleaseUrl,
    string? ErrorMessage)
{
    public static UpdateCheckResult Failed(string currentVersion, string errorMessage)
    {
        return new UpdateCheckResult(false, false, currentVersion, null, null, errorMessage);
    }
}
