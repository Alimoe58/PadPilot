using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PadPilot.Core;

namespace PadPilot.App;

/// <summary>Small per-user settings file in %AppData%\PadPilot\settings.json.</summary>
public sealed class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PadPilot", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Current { get; } = Load();

    /// <summary>"de" or "en"; null means follow the Windows language.</summary>
    public string? Language { get; set; }

    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; }
    public bool StartMinimized { get; set; }

    public DeadzoneSettings LeftStick { get; set; } = new();
    public DeadzoneSettings RightStick { get; set; } = new();
    public DeadzoneSettings L2 { get; set; } = new() { Inner = 0 };
    public DeadzoneSettings R2 { get; set; } = new() { Inner = 0 };

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (IOException)
        {
            // Settings are a convenience; the app works without them.
        }
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
        }
        return new AppSettings();
    }
}
