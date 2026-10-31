using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PadPilot.Core;
using PadPilot.Core.Emulation;
using PadPilot.Core.Mapping;
using PadPilot.Core.Mouse;

namespace PadPilot.App;

/// <summary>One adaptive trigger as set on the Outputs tab.</summary>
public sealed record TriggerProfile
{
    public TriggerMode Mode { get; init; }
    public int Start { get; init; } = 2;
    public int End { get; init; } = 6;
    public int Strength { get; init; } = 5;
    public int Frequency { get; init; } = 20;
}

/// <summary>Everything on the Outputs tab except the rumble test sliders.</summary>
public sealed record OutputProfile
{
    public string Lightbar { get; init; } = "#3B82F6";
    public bool[] PlayerLeds { get; init; } = [false, false, true, false, false];
    public LedBrightness LedBrightness { get; init; } = LedBrightness.High;
    public MicLed MicLed { get; init; } = MicLed.Off;
    public TriggerProfile LeftTrigger { get; init; } = new();
    public TriggerProfile RightTrigger { get; init; } = new();

    /// <summary>The lightbar shows the battery level (green → red) instead of the colour.</summary>
    public bool LightbarShowsBattery { get; init; }
}

/// <summary>
/// A named set of controller settings. Device-specific values (gyro calibration) and app options
/// (language, window, mods) are not part of a profile.
/// </summary>
public sealed record Profile
{
    public int Version { get; init; } = 1;
    public OutputProfile Outputs { get; init; } = new();

    public DeadzoneSettings LeftStick { get; init; } = new();
    public DeadzoneSettings RightStick { get; init; } = new();
    public DeadzoneSettings L2 { get; init; } = new() { Inner = 0 };
    public DeadzoneSettings R2 { get; init; } = new() { Inner = 0 };

    public EmulationMode EmulationMode { get; init; }
    public bool HideControllerWithHidHide { get; init; } = true;
    public bool RumblePassthrough { get; init; } = true;

    public TouchpadMouseSettings TouchpadMouse { get; init; } = new();
    public GyroMouseSettings GyroMouse { get; init; } = new();

    public MappingSettings Mapping { get; init; } = new();

    /// <summary>Games (file names like "game.exe") that switch to this profile when they are in the foreground.</summary>
    public string[] Games { get; init; } = [];
}

/// <summary>Profiles as one JSON file each in %AppData%\PadPilot\profiles.</summary>
public static class ProfileStore
{
    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PadPilot", "profiles");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static IReadOnlyList<string> Names()
    {
        if (!Directory.Exists(Folder))
            return [];
        return Directory.GetFiles(Folder, "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static bool Exists(string name) => File.Exists(PathOf(name));

    public static Profile? Load(string name)
    {
        try
        {
            return JsonSerializer.Deserialize<Profile>(File.ReadAllText(PathOf(name)), Json);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public static void Save(string name, Profile profile)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(PathOf(name), JsonSerializer.Serialize(profile, Json));
    }

    /// <summary>Writes a profile to a file of the user's choice, for sharing.</summary>
    public static void Export(string path, Profile profile) => File.WriteAllText(path, JsonSerializer.Serialize(profile, Json));

    /// <summary>
    /// Reads a profile someone shared. Returns null if the file isn't a PadPilot profile; values that
    /// make no sense (missing parts, impossible keys, extreme speeds, odd game names) are cleaned up.
    /// </summary>
    public static Profile? ReadShared(string path)
    {
        try
        {
            if (new FileInfo(path).Length > 1_000_000)
                return null;
            string text = File.ReadAllText(path);
            using (var doc = JsonDocument.Parse(text))
            {
                // Any JSON object would deserialize into default settings; require at least one part of a profile.
                if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                    !(doc.RootElement.TryGetProperty("Outputs", out _) || doc.RootElement.TryGetProperty("Mapping", out _)))
                    return null;
            }
            return JsonSerializer.Deserialize<Profile>(text, Json) is Profile p ? Sanitize(p) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>"Name", or "Name (2)", "Name (3)" … if that is taken.</summary>
    public static string UniqueName(string name, IEnumerable<string> taken)
    {
        var used = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
        string candidate = name;
        for (int i = 2; used.Contains(candidate); i++)
            candidate = $"{name} ({i})";
        return candidate;
    }

    private static Profile Sanitize(Profile p)
    {
        var outputs = p.Outputs ?? new OutputProfile();
        var touch = p.TouchpadMouse ?? new TouchpadMouseSettings();
        var gyro = p.GyroMouse ?? new GyroMouseSettings();
        return p with
        {
            Outputs = outputs with
            {
                Lightbar = outputs.Lightbar ?? "#3B82F6",
                LedBrightness = Enum.IsDefined(outputs.LedBrightness) ? outputs.LedBrightness : LedBrightness.High,
                MicLed = Enum.IsDefined(outputs.MicLed) ? outputs.MicLed : MicLed.Off,
                PlayerLeds = outputs.PlayerLeds ?? [],
                LeftTrigger = outputs.LeftTrigger ?? new TriggerProfile(),
                RightTrigger = outputs.RightTrigger ?? new TriggerProfile(),
            },
            LeftStick = p.LeftStick ?? new DeadzoneSettings(),
            RightStick = p.RightStick ?? new DeadzoneSettings(),
            L2 = p.L2 ?? new DeadzoneSettings { Inner = 0 },
            R2 = p.R2 ?? new DeadzoneSettings { Inner = 0 },
            EmulationMode = Enum.IsDefined(p.EmulationMode) ? p.EmulationMode : EmulationMode.Off,
            TouchpadMouse = touch with
            {
                Sensitivity = Limit(touch.Sensitivity, 0.02, 2),
                Acceleration = Limit(touch.Acceleration, 0, 1),
                ScrollSpeed = Limit(touch.ScrollSpeed, 0.04, 4),
            },
            GyroMouse = gyro with { PixelsPerDegree = Limit(gyro.PixelsPerDegree, 0.2, 20), Bias = default },
            Mapping = (p.Mapping ?? new MappingSettings()).Sanitized(),
            Games = (p.Games ?? [])
                .Where(g => g != null && g.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                            && g.Length <= 260 && g.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
        };
    }

    private static double Limit(double value, double min, double max) => double.IsFinite(value) ? Math.Clamp(value, min, max) : min;

    public static void Delete(string name) => File.Delete(PathOf(name));

    public static void Rename(string oldName, string newName) => File.Move(PathOf(oldName), PathOf(newName));

    /// <summary>Turns user input into a usable file name, or null if nothing usable is left.</summary>
    public static string? CleanName(string? input)
    {
        if (input == null)
            return null;
        var invalid = Path.GetInvalidFileNameChars();
        string name = new string(input.Where(c => !invalid.Contains(c)).ToArray()).Trim().TrimEnd('.');
        return name.Length == 0 ? null : name.Length > 40 ? name[..40] : name;
    }

    private static string PathOf(string name) => Path.Combine(Folder, name + ".json");
}
