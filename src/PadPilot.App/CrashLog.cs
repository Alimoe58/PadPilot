using System.IO;
using System.Text;

namespace PadPilot.App;

/// <summary>
/// Unexpected errors go to %AppData%\PadPilot\crash.log, so they can be pasted into bug reports.
/// The user name and the user's folders are replaced by placeholders before anything is written.
/// </summary>
public static class CrashLog
{
    private const string Separator = "=====";
    private const int MaxSize = 64 * 1024;

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PadPilot", "crash.log");

    public static void Write(Exception ex, string source)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var entry = new StringBuilder()
                .AppendLine($"{Separator} {DateTime.Now:yyyy-MM-dd HH:mm:ss} · PadPilot {UpdateViewModel.CurrentText} · {source}")
                .AppendLine(Sanitize(ex.ToString()))
                .ToString();

            // Keep the file small: drop the oldest entries.
            string old = File.Exists(FilePath) ? File.ReadAllText(FilePath) : "";
            string text = old + entry;
            if (text.Length > MaxSize)
            {
                int cut = text.IndexOf(Separator, text.Length - MaxSize, StringComparison.Ordinal);
                text = cut >= 0 ? text[cut..] : entry;
            }
            File.WriteAllText(FilePath, text);
        }
        catch (Exception logError) when (logError is IOException or UnauthorizedAccessException)
        {
            // Nothing sensible left to do if even the log can't be written.
        }
    }

    /// <summary>The most recent entry, or null if PadPilot never crashed.</summary>
    public static string? LastEntry()
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;
            string text = File.ReadAllText(FilePath);
            int start = text.LastIndexOf(Separator, StringComparison.Ordinal);
            return start < 0 ? null : text[start..].TrimEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Replaces personal folder names and the user name with placeholders.</summary>
    public static string Sanitize(string text)
    {
        foreach (var (folder, placeholder) in new[]
        {
            (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%"),
            (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "%APPDATA%"),
            (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%"),
        })
        {
            if (folder.Length > 3)
                text = text.Replace(folder, placeholder, StringComparison.OrdinalIgnoreCase);
        }
        string user = Environment.UserName;
        return user.Length >= 3 ? text.Replace(user, "<user>", StringComparison.OrdinalIgnoreCase) : text;
    }
}
