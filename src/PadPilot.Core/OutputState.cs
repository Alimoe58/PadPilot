namespace PadPilot.Core;

public enum MicLed : byte
{
    Off = 0,
    On = 1,
    Pulse = 2,
}

public enum LedBrightness : byte
{
    High = 0,
    Medium = 1,
    Low = 2,
}

[Flags]
public enum PlayerLeds : byte
{
    None = 0,
    Led1 = 1 << 0,
    Led2 = 1 << 1,
    Led3 = 1 << 2,
    Led4 = 1 << 3,
    Led5 = 1 << 4,
}

/// <summary>Everything we can set on the controller. Sent as a whole with every output report.</summary>
public sealed record OutputState
{
    public byte LightbarRed { get; init; }
    public byte LightbarGreen { get; init; }
    public byte LightbarBlue { get; init; }

    public PlayerLeds PlayerLeds { get; init; }
    public LedBrightness PlayerLedBrightness { get; init; }
    public MicLed MicLed { get; init; }

    /// <summary>Left motor: strong, low-frequency rumble.</summary>
    public byte RumbleLeft { get; init; }

    /// <summary>Right motor: weak, high-frequency rumble.</summary>
    public byte RumbleRight { get; init; }

    public TriggerEffect LeftTrigger { get; init; } = TriggerEffect.Off;
    public TriggerEffect RightTrigger { get; init; } = TriggerEffect.Off;

    /// <summary>Rumble and triggers off; used when the app closes so nothing stays stiff or buzzing.</summary>
    public OutputState Released() => this with
    {
        RumbleLeft = 0,
        RumbleRight = 0,
        LeftTrigger = TriggerEffect.Off,
        RightTrigger = TriggerEffect.Off,
    };
}
