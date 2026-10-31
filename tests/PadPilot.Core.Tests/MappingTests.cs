using PadPilot.Core.Mapping;
using PadPilot.Core.Mouse;

namespace PadPilot.Core.Tests;

public class MappingTests
{
    private sealed class Recorder : IKeyboardOutput, IMouseOutput
    {
        public List<string> Events { get; } = [];
        public int X, Y;
        public void Key(int virtualKey, bool down) => Events.Add($"{(down ? "+" : "-")}{virtualKey:X2}");
        public void Move(int dx, int dy) { X += dx; Y += dy; }
        public void Button(MouseButton button, bool down) => Events.Add($"{(down ? "+" : "-")}{button}");
        public void Wheel(int delta) => Events.Add($"wheel{delta}");
    }

    private readonly Recorder _out = new();
    private readonly MappingEngine _engine;

    public MappingTests() => _engine = new MappingEngine(_out, _out);

    private static MappingSettings Map(Buttons button, ButtonAction action) =>
        new() { Buttons = new() { [button] = action } };

    private static ControllerState Press(Buttons b) => new() { Buttons = b };

    [Fact]
    public void ButtonPressesAndReleasesAKey()
    {
        var m = Map(Buttons.Cross, new ButtonAction { Kind = ActionKind.Key, Key = 0x20 });
        _engine.Process(Press(Buttons.Cross), m, 0.004);
        _engine.Process(Press(Buttons.Cross), m, 0.004);
        _engine.Process(Press(Buttons.None), m, 0.004);
        Assert.Equal(["+20", "-20"], _out.Events);
    }

    [Fact]
    public void ModifiersArePressedFirstAndReleasedLast()
    {
        var m = Map(Buttons.Mute, new ButtonAction { Kind = ActionKind.Key, Key = 0x4D, Modifiers = KeyModifiers.Ctrl | KeyModifiers.Shift });
        _engine.Process(Press(Buttons.Mute), m, 0.004);
        _engine.Process(Press(Buttons.None), m, 0.004);
        Assert.Equal(["+11", "+10", "+4D", "-4D", "-10", "-11"], _out.Events);
    }

    [Fact]
    public void TwoButtonsOnTheSameKeyReleaseItOnlyWhenBothAreUp()
    {
        var key = new ButtonAction { Kind = ActionKind.Key, Key = 0x45 };
        var m = new MappingSettings { Buttons = new() { [Buttons.Cross] = key, [Buttons.Circle] = key } };
        _engine.Process(Press(Buttons.Cross), m, 0.004);
        _engine.Process(Press(Buttons.Cross | Buttons.Circle), m, 0.004);
        _engine.Process(Press(Buttons.Circle), m, 0.004);
        Assert.Equal(["+45"], _out.Events);
        _engine.Process(Press(Buttons.None), m, 0.004);
        Assert.Equal(["+45", "-45"], _out.Events);
    }

    [Fact]
    public void MouseClickAndWheel()
    {
        var m = new MappingSettings
        {
            Buttons = new()
            {
                [Buttons.R1] = new ButtonAction { Kind = ActionKind.Mouse, Mouse = MouseAction.RightClick },
                [Buttons.DPadUp] = new ButtonAction { Kind = ActionKind.Mouse, Mouse = MouseAction.WheelUp },
            },
        };
        _engine.Process(Press(Buttons.R1 | Buttons.DPadUp), m, 0.004);
        _engine.Process(Press(Buttons.None), m, 0.004);
        Assert.Equal(3, _out.Events.Count);
        Assert.Contains("+Right", _out.Events);
        Assert.Contains("-Right", _out.Events);
        Assert.Contains("wheel120", _out.Events);
    }

    [Fact]
    public void SpecialActionFiresOncePerPress()
    {
        var fired = new List<SpecialAction>();
        _engine.Special += fired.Add;
        var m = Map(Buttons.Mute, new ButtonAction { Kind = ActionKind.Special, Special = SpecialAction.ToggleMicMute });
        _engine.Process(Press(Buttons.Mute), m, 0.004);
        _engine.Process(Press(Buttons.Mute), m, 0.004);
        _engine.Process(Press(Buttons.None), m, 0.004);
        Assert.Equal([SpecialAction.ToggleMicMute], fired);
    }

    [Fact]
    public void ChangingTheMappingReleasesHeldKeys()
    {
        var m = Map(Buttons.Cross, new ButtonAction { Kind = ActionKind.Key, Key = 0x20 });
        _engine.Process(Press(Buttons.Cross), m, 0.004);
        _engine.Process(Press(Buttons.Cross), new MappingSettings(), 0.004);
        Assert.Equal(["+20", "-20"], _out.Events);
    }

    [Fact]
    public void ResetReleasesEverything()
    {
        var m = new MappingSettings
        {
            Buttons = new()
            {
                [Buttons.Cross] = new ButtonAction { Kind = ActionKind.Key, Key = 0x20 },
                [Buttons.R2] = new ButtonAction { Kind = ActionKind.Mouse, Mouse = MouseAction.LeftClick },
            },
        };
        _engine.Process(Press(Buttons.Cross | Buttons.R2), m, 0.004);
        _engine.Reset();
        Assert.Contains("-20", _out.Events);
        Assert.Contains("-Left", _out.Events);
    }

    [Fact]
    public void StickAsWasdWithHysteresis()
    {
        var m = new MappingSettings { LeftStick = StickMode.Wasd };
        _engine.Process(new ControllerState { LeftY = 0 }, m, 0.004);          // fully up
        Assert.Equal(["+57"], _out.Events);
        _engine.Process(new ControllerState { LeftY = 70 }, m, 0.004);         // about 45 % up: still held
        Assert.Equal(["+57"], _out.Events);
        _engine.Process(new ControllerState { LeftY = 128, LeftX = 255 }, m, 0.004); // centre up, fully right
        Assert.Equal(["+57", "-57", "+44"], _out.Events);
    }

    [Fact]
    public void StickAsMouseMovesInTheStickDirection()
    {
        var m = new MappingSettings { RightStick = StickMode.Mouse, StickMouseSpeed = 1000 };
        for (int i = 0; i < 100; i++)
            _engine.Process(new ControllerState { RightX = 255 }, m, 0.01);
        Assert.InRange(_out.X, 950, 1000);
        Assert.Equal(0, _out.Y);

        _out.X = 0;
        _engine.Process(new ControllerState { RightX = 135 }, m, 0.01);       // within the stick deadzone
        Assert.Equal(0, _out.X);
    }

    [Fact]
    public void TransformRemapsAndHidesButtonsForTheVirtualController()
    {
        var m = new MappingSettings
        {
            Buttons = new()
            {
                [Buttons.Cross] = new ButtonAction { Kind = ActionKind.Button, Target = Buttons.Circle },
                [Buttons.Square] = new ButtonAction { Kind = ActionKind.Key, Key = 0x20 },
                [Buttons.L2] = new ButtonAction { Kind = ActionKind.Disabled },
                [Buttons.Triangle] = new ButtonAction { Kind = ActionKind.Button, Target = Buttons.R2 },
            },
            LeftStick = StickMode.Wasd,
        };
        var s = new ControllerState
        {
            Buttons = Buttons.Cross | Buttons.Square | Buttons.L2 | Buttons.Triangle | Buttons.R1,
            L2 = 200, LeftX = 10, LeftY = 250, RightX = 40,
        };

        var t = MappingEngine.Transform(s, m);

        Assert.Equal(Buttons.Circle | Buttons.R2 | Buttons.R1, t.Buttons);
        Assert.Equal(0, t.L2);
        Assert.Equal(255, t.R2);
        Assert.Equal(128, t.LeftX);
        Assert.Equal(128, t.LeftY);
        Assert.Equal(40, t.RightX);
    }

    [Fact]
    public void SharedMappingIsCleanedUp()
    {
        var m = new MappingSettings
        {
            Buttons = new()
            {
                [Buttons.Cross] = new ButtonAction { Kind = ActionKind.Key, Key = 0x20, Modifiers = (KeyModifiers)0xFF },
                [Buttons.Circle] = new ButtonAction { Kind = ActionKind.Key, Key = 9999 },
                [Buttons.Square] = new ButtonAction { Kind = ActionKind.Button, Target = Buttons.Cross | Buttons.Circle },
                [Buttons.Cross | Buttons.Triangle] = new ButtonAction { Kind = ActionKind.Disabled },
                [(Buttons)(1u << 25)] = new ButtonAction { Kind = ActionKind.Disabled },
                [Buttons.R1] = new ButtonAction { Kind = (ActionKind)42 },
                [Buttons.L1] = new ButtonAction { Kind = ActionKind.Mouse, Mouse = MouseAction.RightClick },
            },
            LeftStick = (StickMode)7,
            StickMouseSpeed = 1e9,
        };

        var clean = m.Sanitized();

        Assert.Equal([Buttons.Cross, Buttons.L1], clean.Buttons.Keys.OrderBy(b => b));
        Assert.Equal(KeyModifiers.Ctrl | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Win, clean.Buttons[Buttons.Cross].Modifiers);
        Assert.Equal(StickMode.Default, clean.LeftStick);
        Assert.Equal(3000, clean.StickMouseSpeed);
    }

    [Fact]
    public void EmptyMappingLeavesTheInputAlone()
    {
        var s = new ControllerState { Buttons = Buttons.Cross, LeftX = 3 };
        Assert.Same(s, MappingEngine.Transform(s, new MappingSettings()));
    }
}
