using System.Text.Json.Serialization;

namespace PadPilot.Core.Mapping;

/// <summary>What a controller button does.</summary>
public enum ActionKind
{
    /// <summary>Unchanged: the virtual controller gets the button as usual.</summary>
    Default,
    /// <summary>The virtual controller doesn't get the button.</summary>
    Disabled,
    /// <summary>The virtual controller gets another button instead.</summary>
    Button,
    Key,
    Mouse,
    Special,
}

public enum MouseAction
{
    LeftClick,
    RightClick,
    MiddleClick,
    WheelUp,
    WheelDown,
}

public enum SpecialAction
{
    ToggleMicMute,
    NextProfile,
    TurnOffController,
}

[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    Win = 8,
}

public enum StickMode
{
    Default,
    Mouse,
    Wasd,
    ArrowKeys,
}

public sealed record ButtonAction
{
    public ActionKind Kind { get; init; }

    /// <summary>For <see cref="ActionKind.Button"/>: the button the virtual controller gets.</summary>
    public Buttons Target { get; init; }

    /// <summary>For <see cref="ActionKind.Key"/>: virtual-key code plus the modifiers held with it.</summary>
    public int Key { get; init; }
    public KeyModifiers Modifiers { get; init; }

    public MouseAction Mouse { get; init; }
    public SpecialAction Special { get; init; }

    /// <summary>True when the virtual controller should not see the original button.</summary>
    [JsonIgnore]
    public bool TakesButton => Kind != ActionKind.Default;
}

/// <summary>Button and stick mapping, part of a profile.</summary>
public sealed record MappingSettings
{
    /// <summary>Only buttons that do something other than <see cref="ActionKind.Default"/> are listed.</summary>
    public Dictionary<Buttons, ButtonAction> Buttons { get; init; } = [];

    public StickMode LeftStick { get; init; }
    public StickMode RightStick { get; init; }

    /// <summary>Mouse speed of a stick in mouse mode, in pixels per second at full deflection.</summary>
    public double StickMouseSpeed { get; init; } = 1200;

    [JsonIgnore]
    public bool IsEmpty => Buttons.Count == 0 && LeftStick == StickMode.Default && RightStick == StickMode.Default;

    public ButtonAction For(Buttons button) =>
        Buttons.TryGetValue(button, out var action) ? action : Default;

    public bool Uses(SpecialAction special) =>
        Buttons.Values.Any(a => a.Kind == ActionKind.Special && a.Special == special);

    private static readonly ButtonAction Default = new();
}
