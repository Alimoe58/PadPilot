using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace PadPilot.Core.Updates;

/// <summary>
/// A release on GitHub that is newer than the running version. <see cref="Download"/> is set when the
/// release has a PadPilot.exe with a checksum, so it can be installed automatically.
/// </summary>
public sealed record UpdateInfo(Version Version, string Url, ReleaseFile? Download = null);

/// <summary>The PadPilot.exe of a release: where to get it, how big it is and its SHA-256 checksum (lowercase hex).</summary>
public sealed record ReleaseFile(string Url, long Size, string Sha256);

/// <summary>Asks GitHub for the latest PadPilot release. Sends nothing but a plain request for the release list.</summary>
public static class UpdateChecker
{
    public const string LatestReleaseApi = "https://api.github.com/repos/Alimoe58/PadPilot/releases/latest";
    public const string ReleasesPage = "https://github.com/Alimoe58/PadPilot/releases/latest";

    /// <summary>Release files are only taken from this repository's download links.</summary>
    public const string DownloadPrefix = "https://github.com/Alimoe58/PadPilot/releases/download/";

    public const string ExeName = "PadPilot.exe";

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
        return ParseRelease(json.RootElement, current);
    }

    /// <summary>Reads GitHub's "latest release" answer; null when it isn't newer than <paramref name="current"/>.</summary>
    public static UpdateInfo? ParseRelease(JsonElement root, Version current)
    {
        var latest = ParseVersion(root.TryGetProperty("tag_name", out var tag) && tag.ValueKind == JsonValueKind.String ? tag.GetString() : null);
        if (latest == null || !IsNewer(latest, current))
            return null;
        string url = root.TryGetProperty("html_url", out var html) && html.ValueKind == JsonValueKind.String
            && html.GetString() is string u && u.StartsWith("https://github.com/", StringComparison.Ordinal) ? u : ReleasesPage;
        return new UpdateInfo(latest, url, FindExe(root));
    }

    /// <summary>PadPilot.exe among the release's files, if it has a download link from this repository and a SHA-256 checksum.</summary>
    private static ReleaseFile? FindExe(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var asset in assets.EnumerateArray())
        {
            if (Text(asset, "name") != ExeName)
                continue;
            string? url = Text(asset, "browser_download_url");
            string? digest = Text(asset, "digest");
            long size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out long n) ? n : 0;
            if (url == null || !url.StartsWith(DownloadPrefix, StringComparison.Ordinal) || size <= 0
                || digest == null || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                return null;
            string sha = digest["sha256:".Length..].ToLowerInvariant();
            return sha.Length == 64 && sha.All(Uri.IsHexDigit) ? new ReleaseFile(url, size, sha) : null;
        }
        return null;
    }

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>Missing parts count as 0, so 1.2 equals 1.2.0.</summary>
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
