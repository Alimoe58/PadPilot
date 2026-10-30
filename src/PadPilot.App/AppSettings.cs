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

    /// <summary>"de" or "en"; null means English (the default).</summary>
    public string? Language { get; set; }

    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; }
    public bool StartMinimized { get; set; }

    /// <summary>Name of the profile in use (see ProfileStore).</summary>
    public string? ActiveProfile { get; set; }

    /// <summary>Switch to a game's profile while it runs (see Profile.Games).</summary>
    public bool AutoSwitchProfiles { get; set; } = true;

    /// <summary>Gyro and accelerometer switched off in the controller.</summary>
    public bool GyroDisabled { get; set; }

    public bool CheckForUpdates { get; set; } = true;

    /// <summary>Version the user chose to skip ("1.2.0"); not offered again at startup.</summary>
    public string? SkippedUpdate { get; set; }

    public bool LowBatteryWarning { get; set; } = true;
    public int LowBatteryLevel { get; set; } = 20;
    public bool LowBatteryBlink { get; set; } = true;

    /// <summary>Turn a Bluetooth controller off after this long without input.</summary>
    public PadPilot.Core.Power.IdleTimeout IdleTurnOff { get; set; } = PadPilot.Core.Power.IdleTimeout.Never;

    public PadPilot.Core.Emulation.EmulationMode EmulationMode { get; set; } = PadPilot.Core.Emulation.EmulationMode.Off;
    public bool RumblePassthrough { get; set; } = true;

    /// <summary>Hide the real DualSense via HidHide while the emulation is active.</summary>
    public bool HideControllerWithHidHide { get; set; } = true;

    // What PadPilot changed in HidHide, so a crash can be undone on the next start.
    public string? HidHideHiddenInstance { get; set; }
    public bool HidHideCloakSetByUs { get; set; }

    public bool ModUdpEnabled { get; set; } = true;
    public int ModUdpPort { get; set; } = PadPilot.Core.Mods.UdpModServer.DefaultPort;
    public bool ModFileEnabled { get; set; }

    /// <summary>File for the text-file method; null means the default next to the settings.</summary>
    public string? ModFilePath { get; set; }

    public static string DefaultModFilePath => Path.Combine(Path.GetDirectoryName(FilePath)!, "mod-commands.json");

    public PadPilot.Core.Mouse.TouchpadMouseSettings TouchpadMouse { get; set; } = new();
    public PadPilot.Core.Mouse.GyroMouseSettings GyroMouse { get; set; } = new();

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
