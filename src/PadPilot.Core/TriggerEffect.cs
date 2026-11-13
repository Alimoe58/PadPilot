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
    private const byte ModeBow = 0x22;
    private const byte ModeGalloping = 0x23;
    private const byte ModeMachine = 0x27;

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

    /// <summary>
    /// Resistance that changes along the pull: <paramref name="startStrength"/> at zone <paramref name="start"/>
    /// (0–8), rising or falling evenly to <paramref name="endStrength"/> at <paramref name="end"/> (start+1 – 9),
    /// and staying there to the end. Strengths 1–8.
    /// </summary>
    public static TriggerEffect Slope(int start, int end, int startStrength, int endStrength)
    {
        start = Math.Clamp(start, 0, 8);
        end = Math.Clamp(end, start + 1, 9);
        startStrength = Math.Clamp(startStrength, 1, 8);
        endStrength = Math.Clamp(endStrength, 1, 8);

        uint activeZones = 0, forceZones = 0;
        for (int zone = start; zone <= 9; zone++)
        {
            double t = Math.Min(1, (zone - start) / (double)(end - start));
            uint force = (uint)(Math.Round(startStrength + (endStrength - startStrength) * t) - 1);
            activeZones |= 1u << zone;
            forceZones |= force << (3 * zone);
        }

        var b = Make(ModeFeedback);
        WriteZones(b, activeZones, forceZones);
        return new TriggerEffect(b);
    }

    /// <summary>
    /// Like drawing a bow: resistance <paramref name="strength"/> (1–8) between <paramref name="start"/> (0–7)
    /// and <paramref name="end"/> (start+1 – 8), then it snaps back with <paramref name="snapForce"/> (1–8).
    /// </summary>
    public static TriggerEffect Bow(int start, int end, int strength, int snapForce)
    {
        start = Math.Clamp(start, 0, 7);
        end = Math.Clamp(end, start + 1, 8);
        uint zones = (1u << start) | (1u << end);
        uint forces = (uint)((Math.Clamp(strength, 1, 8) - 1) | ((Math.Clamp(snapForce, 1, 8) - 1) << 3));

        var b = Make(ModeBow);
        b[1] = (byte)zones;
        b[2] = (byte)(zones >> 8);
        b[3] = (byte)forces;
        b[4] = (byte)(forces >> 8);
        return new TriggerEffect(b);
    }

    /// <summary>
    /// A two-beat kick like a galloping horse between <paramref name="start"/> (0–8) and <paramref name="end"/>
    /// (start+1 – 9). The beats fall at <paramref name="firstFoot"/> (0–6) and <paramref name="secondFoot"/>
    /// (firstFoot+1 – 7) of an 8-step cycle; <paramref name="frequency"/> in cycles per second (1–255).
    /// </summary>
    public static TriggerEffect Galloping(int start, int end, int firstFoot, int secondFoot, int frequency)
    {
        start = Math.Clamp(start, 0, 8);
        end = Math.Clamp(end, start + 1, 9);
        firstFoot = Math.Clamp(firstFoot, 0, 6);
        secondFoot = Math.Clamp(secondFoot, firstFoot + 1, 7);
        uint zones = (1u << start) | (1u << end);

        var b = Make(ModeGalloping);
        b[1] = (byte)zones;
        b[2] = (byte)(zones >> 8);
        b[3] = (byte)(secondFoot | (firstFoot << 3));
        b[4] = (byte)Math.Clamp(frequency, 1, 255);
        return new TriggerEffect(b);
    }

    /// <summary>
    /// Vibration that alternates between two strengths (<paramref name="strengthA"/>, <paramref name="strengthB"/>,
    /// 1–8) between <paramref name="start"/> (0–8) and <paramref name="end"/> (start+1 – 9).
    /// <paramref name="frequency"/> in Hz (1–255), <paramref name="period"/> in tenths of a second per switch (0–255).
    /// </summary>
    public static TriggerEffect Machine(int start, int end, int strengthA, int strengthB, int frequency, int period)
    {
        start = Math.Clamp(start, 0, 8);
        end = Math.Clamp(end, start + 1, 9);
        uint zones = (1u << start) | (1u << end);
        int strengths = (Math.Clamp(strengthA, 1, 8) - 1) | ((Math.Clamp(strengthB, 1, 8) - 1) << 3);

        var b = Make(ModeMachine);
        b[1] = (byte)zones;
        b[2] = (byte)(zones >> 8);
        b[3] = (byte)strengths;
        b[4] = (byte)Math.Clamp(frequency, 1, 255);
        b[5] = (byte)Math.Clamp(period, 0, 255);
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
