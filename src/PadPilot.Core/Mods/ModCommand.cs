using System.Globalization;
using System.Text.Json;

namespace PadPilot.Core.Mods;

/// <summary>
/// One command from a game mod. Every part is optional; only what is set gets changed.
/// Format: see docs/MOD-PROTOKOLL.md.
/// </summary>
public sealed record ModCommand
{
    /// <summary>Drop all earlier mod settings first (the Outputs tab applies again).</summary>
    public bool Reset { get; init; }

    public (byte R, byte G, byte B)? Lightbar { get; init; }
    public PlayerLeds? PlayerLeds { get; init; }
    public MicLed? MicLed { get; init; }
    public (byte Left, byte Right)? Rumble { get; init; }
    public TriggerEffect? LeftTrigger { get; init; }
    public TriggerEffect? RightTrigger { get; init; }

    /// <summary>Nothing to change: the sender only wants the status reply.</summary>
    public bool IsStatusRequest => !Reset && Lightbar == null && PlayerLeds == null && MicLed == null
        && Rumble == null && LeftTrigger == null && RightTrigger == null;
}

public static class ModCommandParser
{
    public static bool TryParse(string json, out ModCommand? command, out string? error)
    {
        command = null;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new FormatException("expected a JSON object { ... }");
            command = Parse(doc.RootElement);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static ModCommand Parse(JsonElement root)
    {
        var cmd = new ModCommand();
        foreach (var prop in root.EnumerateObject())
        {
            var v = prop.Value;
            cmd = prop.Name.ToLowerInvariant() switch
            {
                "version" => cmd,
                "reset" => cmd with { Reset = v.GetBoolean() },
                "lightbar" => cmd with { Lightbar = ParseColor(v) },
                "playerleds" => cmd with { PlayerLeds = ParsePlayerLeds(v) },
                "micled" => cmd with { MicLed = ParseEnum<MicLed>(v, "micLed") },
                "rumble" => cmd with { Rumble = (Byte(v, "left"), Byte(v, "right")) },
                "lefttrigger" => cmd with { LeftTrigger = ParseTrigger(v) },
                "righttrigger" => cmd with { RightTrigger = ParseTrigger(v) },
                _ => throw new FormatException($"unknown field \"{prop.Name}\""),
            };
        }
        return cmd;
    }

    /// <summary>"#RRGGBB" or { "r": 0-255, "g": 0-255, "b": 0-255 }.</summary>
    private static (byte, byte, byte) ParseColor(JsonElement v)
    {
        if (v.ValueKind == JsonValueKind.String)
        {
            string s = v.GetString()!.Trim().TrimStart('#');
            if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                throw new FormatException("lightbar must be \"#RRGGBB\" or { r, g, b }");
            return ((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        }
        return (Byte(v, "r"), Byte(v, "g"), Byte(v, "b"));
    }

    /// <summary>Bit mask 0–31 or an array of five on/off values (left to right).</summary>
    private static PlayerLeds ParsePlayerLeds(JsonElement v)
    {
        if (v.ValueKind == JsonValueKind.Number)
            return (PlayerLeds)(Math.Clamp(v.GetInt32(), 0, 31));

        if (v.ValueKind != JsonValueKind.Array || v.GetArrayLength() != 5)
            throw new FormatException("playerLeds must be 0-31 or an array of 5 values");
        var leds = PlayerLeds.None;
        int i = 0;
        foreach (var led in v.EnumerateArray())
        {
            bool on = led.ValueKind == JsonValueKind.True || (led.ValueKind == JsonValueKind.Number && led.GetInt32() != 0);
            if (on)
                leds |= (PlayerLeds)(1 << i);
            i++;
        }
        return leds;
    }

    /// <summary>"off" or { "mode": "off|resistance|section|weapon|vibration", ...parameters }.</summary>
    private static TriggerEffect ParseTrigger(JsonElement v)
    {
        string mode = v.ValueKind == JsonValueKind.String ? v.GetString()! : Get(v, "mode")?.GetString() ?? "";
        return mode.ToLowerInvariant() switch
        {
            "off" => TriggerEffect.Off,
            "resistance" => TriggerEffect.Resistance(Int(v, "start", 0), Int(v, "strength", 5)),
            "section" => TriggerEffect.Section(Int(v, "start", 2), Int(v, "end", 6), Int(v, "strength", 5)),
            "weapon" => TriggerEffect.Weapon(Int(v, "start", 2), Int(v, "end", 6), Int(v, "strength", 8)),
            "vibration" => TriggerEffect.Vibration(Int(v, "start", 0), Int(v, "strength", 5), Int(v, "frequency", 20)),
            _ => throw new FormatException($"unknown trigger mode \"{mode}\" (off, resistance, section, weapon, vibration)"),
        };
    }

    private static T ParseEnum<T>(JsonElement v, string field) where T : struct, Enum =>
        Enum.TryParse(v.GetString(), ignoreCase: true, out T value)
            ? value
            : throw new FormatException($"{field} must be one of: {string.Join(", ", Enum.GetNames<T>()).ToLowerInvariant()}");

    private static byte Byte(JsonElement obj, string name) =>
        (byte)Math.Clamp(Get(obj, name)?.GetInt32() ?? throw new FormatException($"missing \"{name}\""), 0, 255);

    private static int Int(JsonElement obj, string name, int fallback) =>
        obj.ValueKind == JsonValueKind.Object ? Get(obj, name)?.GetInt32() ?? fallback : fallback;

    /// <summary>Property lookup ignoring upper/lower case, so "Strength" and "strength" both work.</summary>
    private static JsonElement? Get(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object)
            return null;
        foreach (var p in obj.EnumerateObject())
        {
            if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return p.Value;
        }
        return null;
    }
}
