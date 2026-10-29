using PadPilot.Core;
using PadPilot.Core.Emulation;

namespace PadPilot.Core.Tests;

public class EmulationTests
{
    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(1.0, 32767)]
    [InlineData(-1.0, -32767)]
    [InlineData(2.0, 32767)]
    [InlineData(0.5, 16384)]
    public void XboxAxis(double value, short expected) => Assert.Equal(expected, InputProcessor.ToXboxAxis(value));

    [Theory]
    [InlineData(0.0, 128)]
    [InlineData(-1.0, 0)]
    [InlineData(1.0, 255)]
    [InlineData(-5.0, 0)]
    public void Ds4Axis(double value, byte expected) => Assert.Equal(expected, InputProcessor.ToDs4Axis(value));

    [Fact]
    public void ProcessAppliesDeadzonesToSticksAndTriggers()
    {
        var state = new ControllerState { LeftX = 130, LeftY = 125, RightX = 255, L2 = 20, R2 = 255, Buttons = Buttons.Cross };
        var profile = new DeadzoneProfile
        {
            LeftStick = new DeadzoneSettings { Inner = 0.1 },
            L2 = new DeadzoneSettings { Inner = 0.1 },
        };

        var p = InputProcessor.Process(state, profile);

        Assert.Equal(new StickVector(0, 0), p.Left);   // tiny offset is inside the deadzone
        Assert.Equal(1, p.Right.X, 2);
        Assert.Equal(0, p.L2);
        Assert.Equal(1, p.R2, 6);
        Assert.Equal(Buttons.Cross, p.Buttons);
    }
}
