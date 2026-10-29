using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace PadPilot.Core.Emulation;

public enum EmulationMode
{
    Off,
    Xbox360,
    DualShock4,
}

/// <summary>A virtual controller (via the ViGEmBus driver) that games see instead of the DualSense.</summary>
public interface IVirtualPad : IDisposable
{
    void Submit(ProcessedInput input);

    /// <summary>Rumble requested by a game: large (strong, left) and small (fine, right) motor, 0–255.</summary>
    event Action<byte, byte>? RumbleRequested;
}

public sealed class Xbox360Pad : IVirtualPad
{
    private readonly IXbox360Controller _pad;

    public event Action<byte, byte>? RumbleRequested;

    public Xbox360Pad(ViGEmClient client)
    {
        _pad = client.CreateXbox360Controller();
        _pad.AutoSubmitReport = false;
        _pad.FeedbackReceived += (_, e) => RumbleRequested?.Invoke(e.LargeMotor, e.SmallMotor);
        _pad.Connect();

        // A freshly connected pad reports a random state until it receives a report that differs
        // from the previous one; an all-zero report (sticks inside the deadzone) doesn't count.
        // So send one slightly different report, then the real neutral one.
        _pad.SetSliderValue(Xbox360Slider.LeftTrigger, 1);
        _pad.SubmitReport();
        _pad.SetSliderValue(Xbox360Slider.LeftTrigger, 0);
        _pad.SubmitReport();
    }

    public void Submit(ProcessedInput input)
    {
        var b = input.Buttons;
        _pad.SetButtonState(Xbox360Button.A, b.HasFlag(Buttons.Cross));
        _pad.SetButtonState(Xbox360Button.B, b.HasFlag(Buttons.Circle));
        _pad.SetButtonState(Xbox360Button.X, b.HasFlag(Buttons.Square));
        _pad.SetButtonState(Xbox360Button.Y, b.HasFlag(Buttons.Triangle));
        _pad.SetButtonState(Xbox360Button.LeftShoulder, b.HasFlag(Buttons.L1));
        _pad.SetButtonState(Xbox360Button.RightShoulder, b.HasFlag(Buttons.R1));
        _pad.SetButtonState(Xbox360Button.LeftThumb, b.HasFlag(Buttons.L3));
        _pad.SetButtonState(Xbox360Button.RightThumb, b.HasFlag(Buttons.R3));
        // Create and a touchpad click both act as Back, so games that want "View/Back" can use either.
        _pad.SetButtonState(Xbox360Button.Back, b.HasFlag(Buttons.Create) || b.HasFlag(Buttons.TouchpadClick));
        _pad.SetButtonState(Xbox360Button.Start, b.HasFlag(Buttons.Options));
        _pad.SetButtonState(Xbox360Button.Guide, b.HasFlag(Buttons.PS));
        _pad.SetButtonState(Xbox360Button.Up, b.HasFlag(Buttons.DPadUp));
        _pad.SetButtonState(Xbox360Button.Down, b.HasFlag(Buttons.DPadDown));
        _pad.SetButtonState(Xbox360Button.Left, b.HasFlag(Buttons.DPadLeft));
        _pad.SetButtonState(Xbox360Button.Right, b.HasFlag(Buttons.DPadRight));

        _pad.SetAxisValue(Xbox360Axis.LeftThumbX, InputProcessor.ToXboxAxis(input.Left.X));
        _pad.SetAxisValue(Xbox360Axis.LeftThumbY, InputProcessor.ToXboxAxis(-input.Left.Y));
        _pad.SetAxisValue(Xbox360Axis.RightThumbX, InputProcessor.ToXboxAxis(input.Right.X));
        _pad.SetAxisValue(Xbox360Axis.RightThumbY, InputProcessor.ToXboxAxis(-input.Right.Y));
        _pad.SetSliderValue(Xbox360Slider.LeftTrigger, InputProcessor.ToTrigger(input.L2));
        _pad.SetSliderValue(Xbox360Slider.RightTrigger, InputProcessor.ToTrigger(input.R2));

        _pad.SubmitReport();
    }

    public void Dispose() => _pad.Disconnect();
}

public sealed class DualShock4Pad : IVirtualPad
{
    private readonly IDualShock4Controller _pad;

    public event Action<byte, byte>? RumbleRequested;

    public DualShock4Pad(ViGEmClient client)
    {
        _pad = client.CreateDualShock4Controller();
        _pad.AutoSubmitReport = false;
        // Marked obsolete in favour of AwaitRawOutputReport(), which also carries lightbar data.
        // For rumble the event is enough; switch over if games' DS4 rumble turns out unreliable.
#pragma warning disable CS0618
        _pad.FeedbackReceived += (_, e) => RumbleRequested?.Invoke(e.LargeMotor, e.SmallMotor);
#pragma warning restore CS0618
        _pad.Connect();
    }

    public void Submit(ProcessedInput input)
    {
        var b = input.Buttons;
        _pad.SetButtonState(DualShock4Button.Cross, b.HasFlag(Buttons.Cross));
        _pad.SetButtonState(DualShock4Button.Circle, b.HasFlag(Buttons.Circle));
        _pad.SetButtonState(DualShock4Button.Square, b.HasFlag(Buttons.Square));
        _pad.SetButtonState(DualShock4Button.Triangle, b.HasFlag(Buttons.Triangle));
        _pad.SetButtonState(DualShock4Button.ShoulderLeft, b.HasFlag(Buttons.L1));
        _pad.SetButtonState(DualShock4Button.ShoulderRight, b.HasFlag(Buttons.R1));
        _pad.SetButtonState(DualShock4Button.TriggerLeft, b.HasFlag(Buttons.L2));
        _pad.SetButtonState(DualShock4Button.TriggerRight, b.HasFlag(Buttons.R2));
        _pad.SetButtonState(DualShock4Button.ThumbLeft, b.HasFlag(Buttons.L3));
        _pad.SetButtonState(DualShock4Button.ThumbRight, b.HasFlag(Buttons.R3));
        _pad.SetButtonState(DualShock4Button.Share, b.HasFlag(Buttons.Create));
        _pad.SetButtonState(DualShock4Button.Options, b.HasFlag(Buttons.Options));
        _pad.SetSpecialButtonsFull((byte)(
            (b.HasFlag(Buttons.PS) ? DualShock4SpecialButton.Ps.Value : 0) |
            (b.HasFlag(Buttons.TouchpadClick) ? DualShock4SpecialButton.Touchpad.Value : 0)));
        _pad.SetDPadDirection(ToDPad(b));

        _pad.SetAxisValue(DualShock4Axis.LeftThumbX, InputProcessor.ToDs4Axis(input.Left.X));
        _pad.SetAxisValue(DualShock4Axis.LeftThumbY, InputProcessor.ToDs4Axis(input.Left.Y));
        _pad.SetAxisValue(DualShock4Axis.RightThumbX, InputProcessor.ToDs4Axis(input.Right.X));
        _pad.SetAxisValue(DualShock4Axis.RightThumbY, InputProcessor.ToDs4Axis(input.Right.Y));
        _pad.SetSliderValue(DualShock4Slider.LeftTrigger, InputProcessor.ToTrigger(input.L2));
        _pad.SetSliderValue(DualShock4Slider.RightTrigger, InputProcessor.ToTrigger(input.R2));

        _pad.SubmitReport();
    }

    private static DualShock4DPadDirection ToDPad(Buttons b)
    {
        bool up = b.HasFlag(Buttons.DPadUp), down = b.HasFlag(Buttons.DPadDown);
        bool left = b.HasFlag(Buttons.DPadLeft), right = b.HasFlag(Buttons.DPadRight);
        return (up, down, left, right) switch
        {
            (true, _, true, _) => DualShock4DPadDirection.Northwest,
            (true, _, _, true) => DualShock4DPadDirection.Northeast,
            (_, true, true, _) => DualShock4DPadDirection.Southwest,
            (_, true, _, true) => DualShock4DPadDirection.Southeast,
            (true, _, _, _) => DualShock4DPadDirection.North,
            (_, true, _, _) => DualShock4DPadDirection.South,
            (_, _, true, _) => DualShock4DPadDirection.West,
            (_, _, _, true) => DualShock4DPadDirection.East,
            _ => DualShock4DPadDirection.None,
        };
    }

    public void Dispose() => _pad.Disconnect();
}
