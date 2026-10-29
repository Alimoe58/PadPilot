using PadPilot.Core;
using PadPilot.Core.Mouse;

namespace PadPilot.Core.Tests;

public class MouseTests
{
    private sealed class Recorder : IMouseOutput
    {
        public int X, Y, WheelTotal;
        public List<string> Clicks { get; } = [];
        public void Move(int dx, int dy) { X += dx; Y += dy; }
        public void Button(MouseButton button, bool down) => Clicks.Add($"{button}{(down ? "Down" : "Up")}");
        public void Wheel(int delta) => WheelTotal += delta;
    }

    private static readonly TouchpadMouseSettings Touch = new()
    {
        Enabled = true, Sensitivity = 1, Acceleration = 0, TapToClick = true,
    };

    private static ControllerState Finger(int x, int y, byte id = 1, Buttons buttons = Buttons.None) =>
        new() { Touch1 = new TouchPoint(true, id, x, y), Buttons = buttons };

    private static ControllerState TwoFingers(int y) =>
        new() { Touch1 = new TouchPoint(true, 1, 500, y), Touch2 = new TouchPoint(true, 2, 900, y) };

    [Fact]
    public void OneFingerMovesTheCursor()
    {
        var mouse = new Recorder();
        var pad = new TouchpadMouse(mouse);

        pad.Process(Finger(100, 100), Touch, TimeSpan.Zero);
        pad.Process(Finger(130, 90), Touch, TimeSpan.FromMilliseconds(4));
        pad.Process(Finger(160, 80), Touch, TimeSpan.FromMilliseconds(8));

        Assert.Equal((60, -20), (mouse.X, mouse.Y));
    }

    [Fact]
    public void NewFingerDoesNotJump()
    {
        var mouse = new Recorder();
        var pad = new TouchpadMouse(mouse);

        pad.Process(Finger(100, 100, id: 1), Touch, TimeSpan.Zero);
        pad.Process(Finger(1500, 900, id: 2), Touch, TimeSpan.FromMilliseconds(4));

        Assert.Equal((0, 0), (mouse.X, mouse.Y));
    }

    [Fact]
    public void TwoFingersScrollInsteadOfMoving()
    {
        var mouse = new Recorder();
        var pad = new TouchpadMouse(mouse);

        pad.Process(TwoFingers(300), Touch, TimeSpan.Zero);
        pad.Process(TwoFingers(360), Touch, TimeSpan.FromMilliseconds(4));

        Assert.Equal(120, mouse.WheelTotal);
        Assert.Equal((0, 0), (mouse.X, mouse.Y));
    }

    [Theory]
    [InlineData(400, "LeftDown", "LeftUp")]
    [InlineData(1500, "RightDown", "RightUp")]
    public void PressingThePadClicks(int x, string down, string up)
    {
        var mouse = new Recorder();
        var pad = new TouchpadMouse(mouse);

        pad.Process(Finger(x, 500), Touch, TimeSpan.Zero);
        pad.Process(Finger(x, 500, buttons: Buttons.TouchpadClick), Touch, TimeSpan.FromMilliseconds(50));
        pad.Process(Finger(x, 500), Touch, TimeSpan.FromMilliseconds(100));

        Assert.Equal([down, up], mouse.Clicks);
    }

    [Fact]
    public void ShortTapClicksButLongTouchDoesNot()
    {
        var mouse = new Recorder();
        var pad = new TouchpadMouse(mouse);
        var lifted = new ControllerState();

        pad.Process(Finger(800, 500), Touch, TimeSpan.Zero);
        pad.Process(lifted, Touch, TimeSpan.FromMilliseconds(100));
        Assert.Equal(["LeftDown", "LeftUp"], mouse.Clicks);

        pad.Process(Finger(800, 500, id: 2), Touch, TimeSpan.FromMilliseconds(500));
        pad.Process(lifted, Touch, TimeSpan.FromMilliseconds(1000));
        Assert.Equal(2, mouse.Clicks.Count);
    }

    [Fact]
    public void ResetReleasesAHeldButton()
    {
        var mouse = new Recorder();
        var pad = new TouchpadMouse(mouse);

        pad.Process(Finger(400, 500), Touch, TimeSpan.Zero);
        pad.Process(Finger(400, 500, buttons: Buttons.TouchpadClick), Touch, TimeSpan.FromMilliseconds(50));
        pad.Reset();

        Assert.Equal(["LeftDown", "LeftUp"], mouse.Clicks);
    }

    private static readonly GyroMouseSettings Gyro = new()
    {
        Enabled = true, Activation = GyroActivation.Always, PixelsPerDegree = 10, Bias = new Vector3S(-10, 5, 0),
    };

    [Fact]
    public void GyroIgnoresBiasAtRest()
    {
        var mouse = new Recorder();
        var gyro = new GyroMouse(mouse);

        for (int i = 0; i < 250; i++)
            gyro.Process(new ControllerState { Gyro = new Vector3S(-10, 5, 0) }, Gyro, 0.004);

        Assert.Equal((0, 0), (mouse.X, mouse.Y));
    }

    [Fact]
    public void GyroTurnMovesCursor()
    {
        var mouse = new Recorder();
        var gyro = new GyroMouse(mouse);

        // Yaw of 164 raw units above bias = 10 °/s, for one second at 10 px per degree = 100 px.
        for (int i = 0; i < 250; i++)
            gyro.Process(new ControllerState { Gyro = new Vector3S(-10, 5 + 164, 0) }, Gyro, 0.004);

        Assert.InRange(mouse.X, -101, -99);
        Assert.Equal(0, mouse.Y);
    }

    [Fact]
    public void GyroOnlyWhileActivationHeld()
    {
        var mouse = new Recorder();
        var gyro = new GyroMouse(mouse);
        var holdR1 = Gyro with { Activation = GyroActivation.HoldR1 };

        gyro.Process(new ControllerState { Gyro = new Vector3S(2000, 2000, 0) }, holdR1, 0.1);
        Assert.Equal((0, 0), (mouse.X, mouse.Y));

        gyro.Process(new ControllerState { Gyro = new Vector3S(2000, 2000, 0), Buttons = Buttons.R1 }, holdR1, 0.1);
        Assert.NotEqual((0, 0), (mouse.X, mouse.Y));
    }

    [Fact]
    public void CalibrationAverages()
    {
        var c = new GyroCalibration();
        c.Add(new Vector3S(-10, 4, 1));
        c.Add(new Vector3S(-8, 6, 1));
        Assert.Equal(new Vector3S(-9, 5, 1), c.Result);
    }
}
