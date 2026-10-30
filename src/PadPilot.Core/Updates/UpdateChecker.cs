using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace PadPilot.Core.Updates;

/// <summary>A release on GitHub that is newer than the running version.</summary>
public sealed record UpdateInfo(Version Version, string Url);

/// <summary>Asks GitHub for the latest PadPilot release. Sends nothing but a plain request for the release list.</summary>
public static class UpdateChecker
{
    public const string LatestReleaseApi = "https://api.github.com/repos/Alimoe58/PadPilot/releases/latest";
    public const string ReleasesPage = "https://github.com/Alimoe58/PadPilot/releases/latest";

    /// <summary>"v1.2.0", "1.2" or "1.2.0-beta" → 1.2.0; null if it isn't a version.</summary>
    public static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        string s = text.Trim().TrimStart('v', 'V');
        int cut = s.IndexOfAny(['-', '+', ' ']);
        if (cut >= 0)
            s = s[..cut];
        return Version.TryParse(s, out var v) ? Normalize(v) : null;
    }

    public static bool IsNewer(Version latest, Version current) => Normalize(latest) > Normalize(current);

    /// <summary>
    /// The latest release if it is newer than <paramref name="current"/>, otherwise null.
    /// Throws <see cref="HttpRequestException"/>, <see cref="TaskCanceledException"/> or <see cref="JsonException"/> when GitHub can't be asked.
    /// </summary>
    public static async Task<UpdateInfo?> CheckAsync(Version current, CancellationToken cancel = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PadPilot", current.ToString(3)));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var stream = await http.GetStreamAsync(LatestReleaseApi, cancel);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancel);
        var root = json.RootElement;

        var latest = ParseVersion(root.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null);
        if (latest == null || !IsNewer(latest, current))
            return null;
        string url = root.TryGetProperty("html_url", out var html) && html.GetString() is string u
            && u.StartsWith("https://github.com/", StringComparison.Ordinal) ? u : ReleasesPage;
        return new UpdateInfo(latest, url);
    }

    /// <summary>Missing parts count as 0, so 1.2 equals 1.2.0.</summary>
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
