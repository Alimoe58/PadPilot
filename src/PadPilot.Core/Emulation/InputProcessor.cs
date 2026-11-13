namespace PadPilot.Core.Emulation;

/// <summary>Deadzones for both sticks and both triggers, applied before input reaches a virtual controller.</summary>
public sealed record DeadzoneProfile
{
    public DeadzoneSettings LeftStick { get; init; } = new();
    public DeadzoneSettings RightStick { get; init; } = new();
    public DeadzoneSettings L2 { get; init; } = new() { Inner = 0 };
    public DeadzoneSettings R2 { get; init; } = new() { Inner = 0 };
}

/// <summary>
/// Controller input after deadzones. Sticks −1…1 (Y down is positive, like the DualSense), triggers 0…1.
/// Touch, motion and battery are passed through unchanged for virtual controllers that have them (DualShock 4).
/// </summary>
public readonly record struct ProcessedInput(StickVector Left, StickVector Right, double L2, double R2, Buttons Buttons)
{
    public TouchPoint Touch1 { get; init; }
    public TouchPoint Touch2 { get; init; }
    public Vector3S Gyro { get; init; }
    public Vector3S Accel { get; init; }
    public int BatteryPercent { get; init; }
    public bool Charging { get; init; }
}

public static class InputProcessor
{
    public static ProcessedInput Process(ControllerState s, DeadzoneProfile p) => new(
        Deadzones.ApplyStick(Deadzones.NormalizeStick(s.LeftX, s.LeftY), p.LeftStick),
        Deadzones.ApplyStick(Deadzones.NormalizeStick(s.RightX, s.RightY), p.RightStick),
        Deadzones.ApplyTrigger(s.L2, p.L2),
        Deadzones.ApplyTrigger(s.R2, p.R2),
        s.Buttons)
    {
        Touch1 = s.Touch1,
        Touch2 = s.Touch2,
        Gyro = s.Gyro,
        Accel = s.Accel,
        BatteryPercent = s.BatteryPercent,
        Charging = s.Charge is ChargeStatus.Charging or ChargeStatus.Full,
    };

    /// <summary>−1…1 to an Xbox stick axis. Xbox counts up as positive, so pass −Y for vertical axes.</summary>
    public static short ToXboxAxis(double value) => (short)Math.Round(Math.Clamp(value, -1, 1) * short.MaxValue);

    /// <summary>−1…1 to a DualShock 4 stick axis (0–255, 128 = centre, down/right is larger).</summary>
    public static byte ToDs4Axis(double value) =>
        (byte)Math.Round(127.5 + Math.Clamp(value, -1, 1) * 127.5, MidpointRounding.AwayFromZero);

    /// <summary>0…1 to a trigger value 0–255.</summary>
    public static byte ToTrigger(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
}
