namespace PadPilot.Core;

/// <summary>
/// One adaptive trigger effect: a mode byte plus 10 parameter bytes, as written into the
/// output report. The trigger travel is split into 10 zones (0 = at rest, 9 = fully pressed).
/// Encodings follow the publicly documented DualSense trigger effect formats.
/// </summary>
public sealed class TriggerEffect
{
    public const int Size = 11;

    private const byte ModeOff = 0x05;
    private const byte ModeFeedback = 0x21;
    private const byte ModeWeapon = 0x25;
    private const byte ModeVibration = 0x26;

    private readonly byte[] _bytes;

    private TriggerEffect(byte[] bytes) => _bytes = bytes;

    public ReadOnlySpan<byte> Bytes => _bytes;

    public static TriggerEffect Off { get; } = new(Make(ModeOff));

    /// <summary>Constant resistance from <paramref name="start"/> (0–9) to the end, strength 1–8.</summary>
    public static TriggerEffect Resistance(int start, int strength) => Section(start, 9, strength);

    /// <summary>Resistance only between zones <paramref name="start"/> and <paramref name="end"/> (0–9), strength 1–8.</summary>
    public static TriggerEffect Section(int start, int end, int strength)
    {
        start = Math.Clamp(start, 0, 9);
        end = Math.Clamp(end, start, 9);
        uint force = (uint)(Math.Clamp(strength, 1, 8) - 1);

        uint activeZones = 0, forceZones = 0;
        for (int zone = start; zone <= end; zone++)
        {
            activeZones |= 1u << zone;
            forceZones |= force << (3 * zone);
        }

        var b = Make(ModeFeedback);
        WriteZones(b, activeZones, forceZones);
        return new TriggerEffect(b);
    }

    /// <summary>
    /// Resistance that builds between <paramref name="start"/> (2–7) and <paramref name="end"/>
    /// (start+1 – 8) and then gives way, like a gun trigger breaking. Strength 1–8.
    /// </summary>
    public static TriggerEffect Weapon(int start, int end, int strength)
    {
        start = Math.Clamp(start, 2, 7);
        end = Math.Clamp(end, start + 1, 8);
        uint zones = (1u << start) | (1u << end);

        var b = Make(ModeWeapon);
        b[1] = (byte)zones;
        b[2] = (byte)(zones >> 8);
        b[3] = (byte)(Math.Clamp(strength, 1, 8) - 1);
        return new TriggerEffect(b);
    }

    /// <summary>Vibration from <paramref name="start"/> (0–9) to the end, amplitude 1–8, frequency in Hz (1–255).</summary>
    public static TriggerEffect Vibration(int start, int amplitude, int frequency)
    {
        start = Math.Clamp(start, 0, 9);
        uint strength = (uint)(Math.Clamp(amplitude, 1, 8) - 1);

        uint activeZones = 0, amplitudeZones = 0;
        for (int zone = start; zone <= 9; zone++)
        {
            activeZones |= 1u << zone;
            amplitudeZones |= strength << (3 * zone);
        }

        var b = Make(ModeVibration);
        WriteZones(b, activeZones, amplitudeZones);
        b[9] = (byte)Math.Clamp(frequency, 1, 255);
        return new TriggerEffect(b);
    }

    private static byte[] Make(byte mode)
    {
        var b = new byte[Size];
        b[0] = mode;
        return b;
    }

    private static void WriteZones(byte[] b, uint activeZones, uint valueZones)
    {
        b[1] = (byte)activeZones;
        b[2] = (byte)(activeZones >> 8);
        b[3] = (byte)valueZones;
        b[4] = (byte)(valueZones >> 8);
        b[5] = (byte)(valueZones >> 16);
        b[6] = (byte)(valueZones >> 24);
    }
}
