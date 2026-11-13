using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace PadPilot.Core.Updates;

public sealed class UpdateFailedException(string message) : Exception(message);

/// <summary>
/// Replaces the running PadPilot.exe with a new release. Windows doesn't allow overwriting a running
/// program, but it does allow renaming it: the running exe becomes "PadPilot.exe.old", the new file
/// takes its name, and the new version deletes the old file once it runs.
/// </summary>
public static class Updater
{
    public const string OldSuffix = ".old";
    public const string DownloadSuffix = ".download";
    private const long MaxSize = 300L * 1024 * 1024;

    /// <summary>
    /// Self-update only works for the single-file release build in a folder PadPilot may write to.
    /// (A development build has its .dll files next to the exe, which a new exe alone wouldn't replace.)
    /// </summary>
    public static bool CanReplace(string exePath)
    {
        string? dir = Path.GetDirectoryName(exePath);
        if (dir == null || !File.Exists(exePath) || File.Exists(Path.Combine(dir, "PadPilot.dll")))
            return false;
        try
        {
            string probe = Path.Combine(dir, $".padpilot-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Downloads the release file next to <paramref name="exePath"/> and checks size and SHA-256.
    /// Returns the path of the checked file; throws <see cref="UpdateFailedException"/> (or a network error) otherwise.
    /// </summary>
    public static async Task<string> DownloadAsync(ReleaseFile file, string exePath, Version current,
        IProgress<double>? progress = null, CancellationToken cancel = default)
    {
        if (!file.Url.StartsWith(UpdateChecker.DownloadPrefix, StringComparison.Ordinal))
            throw new UpdateFailedException("unexpected download address");
        if (file.Size > MaxSize)
            throw new UpdateFailedException("file too big");

        string target = exePath + DownloadSuffix;
        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PadPilot", current.ToString(3)));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));

        using var response = await http.GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        // GitHub forwards release downloads to its file servers; anything else is refused.
        string host = response.RequestMessage?.RequestUri?.Host ?? "";
        if (!(host == "github.com" || host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase)))
            throw new UpdateFailedException($"unexpected download server {host}");

        try
        {
            using (var sha = SHA256.Create())
            await using (var input = await response.Content.ReadAsStreamAsync(timeout.Token))
            await using (var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, timeout.Token)) > 0)
                {
                    done += read;
                    if (done > file.Size)
                        throw new UpdateFailedException("file bigger than announced");
                    sha.TransformBlock(buffer, 0, read, null, 0);
                    await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                    progress?.Report((double)done / file.Size);
                }
                sha.TransformFinalBlock([], 0, 0);
                if (done != file.Size)
                    throw new UpdateFailedException("download incomplete");
                if (Convert.ToHexString(sha.Hash!).ToLowerInvariant() != file.Sha256)
                    throw new UpdateFailedException("checksum does not match");
            }
            return target;
        }
        catch
        {
            TryDelete(target);
            throw;
        }
    }

    /// <summary>
    /// Puts the downloaded file in place of the running exe. If the second step fails, the first is undone,
    /// so there is always a working PadPilot.exe.
    /// </summary>
    public static void Swap(string exePath, string downloadedPath)
    {
        string old = exePath + OldSuffix;
        TryDelete(old);
        File.Move(exePath, old);
        try
        {
            File.Move(downloadedPath, exePath);
        }
        catch
        {
            File.Move(old, exePath);
            throw;
        }
    }

    /// <summary>
    /// Removes what an update left behind. The old version may still be closing, so this retries for a while.
    /// Returns true when nothing is left.
    /// </summary>
    public static async Task<bool> CleanUpAsync(string exePath, TimeSpan? patience = null)
    {
        var until = DateTime.UtcNow + (patience ?? TimeSpan.FromSeconds(30));
        while (true)
        {
            bool done = TryDelete(exePath + OldSuffix) & TryDelete(exePath + DownloadSuffix);
            if (done || DateTime.UtcNow >= until)
                return done;
            await Task.Delay(500);
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
