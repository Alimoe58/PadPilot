namespace PadPilot.Core;

public enum ConnectionType
{
    Usb,
    Bluetooth,
}

public enum ChargeStatus
{
    Discharging,
    Charging,
    Full,
    Unknown,
}

[Flags]
public enum Buttons : uint
{
    None = 0,
    Square = 1 << 0,
    Cross = 1 << 1,
    Circle = 1 << 2,
    Triangle = 1 << 3,
    L1 = 1 << 4,
    R1 = 1 << 5,
    L2 = 1 << 6,
    R2 = 1 << 7,
    Create = 1 << 8,
    Options = 1 << 9,
    L3 = 1 << 10,
    R3 = 1 << 11,
    PS = 1 << 12,
    TouchpadClick = 1 << 13,
    Mute = 1 << 14,
    DPadUp = 1 << 15,
    DPadRight = 1 << 16,
    DPadDown = 1 << 17,
    DPadLeft = 1 << 18,
}

/// <summary>A finger on the touchpad. Coordinates range 0–1919 (X) and 0–1079 (Y).</summary>
public readonly record struct TouchPoint(bool Active, byte Id, int X, int Y);

public readonly record struct Vector3S(short X, short Y, short Z);

/// <summary>One parsed input report.</summary>
public sealed record ControllerState
{
    public byte LeftX { get; init; } = 128;
    public byte LeftY { get; init; } = 128;
    public byte RightX { get; init; } = 128;
    public byte RightY { get; init; } = 128;
    public byte L2 { get; init; }
    public byte R2 { get; init; }

    public Buttons Buttons { get; init; }

    public TouchPoint Touch1 { get; init; }
    public TouchPoint Touch2 { get; init; }

    /// <summary>Raw, uncalibrated gyro values.</summary>
    public Vector3S Gyro { get; init; }

    /// <summary>Raw, uncalibrated accelerometer values.</summary>
    public Vector3S Accel { get; init; }

    public int BatteryPercent { get; init; }
    public ChargeStatus Charge { get; init; }

    public bool IsPressed(Buttons button) => (Buttons & button) == button;
}
