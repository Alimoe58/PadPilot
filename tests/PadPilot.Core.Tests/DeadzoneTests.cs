using PadPilot.Core;

namespace PadPilot.Core.Tests;

public class DeadzoneTests
{
    private static readonly DeadzoneSettings Radial = new() { Inner = 0.2, Outer = 0.1 };
    private static readonly DeadzoneSettings Axial = Radial with { Shape = DeadzoneShape.Axial };

    [Fact]
    public void NormalizeCoversFullRange()
    {
        Assert.Equal(-1, Deadzones.Normalize(0), 6);
        Assert.Equal(1, Deadzones.Normalize(255), 6);
        Assert.Equal(0, Deadzones.Normalize(128), 1);
    }

    [Fact]
    public void RadialInsideInnerDeadzoneIsZero()
    {
        var result = Deadzones.ApplyStick(new StickVector(0.1, 0.1), Radial);
        Assert.Equal(new StickVector(0, 0), result);
    }

    [Fact]
    public void RadialRescalesAndKeepsDirection()
    {
        // Length 0.55 is halfway between inner (0.2) and 1 - outer (0.9).
        var result = Deadzones.ApplyStick(new StickVector(0.55, 0), Radial);
        Assert.Equal(0.5, result.X, 6);
        Assert.Equal(0, result.Y, 6);

        var diagonal = Deadzones.ApplyStick(new StickVector(-0.6, 0.8), Radial);
        Assert.Equal(1, diagonal.Length, 6);
        Assert.Equal(-0.6, diagonal.X, 6);
    }

    [Fact]
    public void OuterDeadzoneReachesFullOutputEarly()
    {
        Assert.Equal(1, Deadzones.ApplyStick(new StickVector(0, -0.92), Radial).Length, 6);
    }

    [Fact]
    public void AxialTreatsAxesSeparately()
    {
        // Y is inside the deadzone on its own axis, X is not.
        var result = Deadzones.ApplyStick(new StickVector(0.55, 0.15), Axial);
        Assert.Equal(0.5, result.X, 6);
        Assert.Equal(0, result.Y, 6);
    }

    [Fact]
    public void TriggerDeadzones()
    {
        var s = new DeadzoneSettings { Inner = 0.1, Outer = 0.1 };
        Assert.Equal(0, Deadzones.ApplyTrigger(20, s));
        Assert.Equal(1, Deadzones.ApplyTrigger(240, s));
        Assert.Equal(0.5, Deadzones.ApplyTrigger(128, s), 2);
    }

    [Fact]
    public void CircularityOfPerfectCircleIsZero()
    {
        var test = new CircularityTest();
        for (int i = 0; i < 720; i++)
        {
            double a = i * Math.PI / 360;
            test.AddSample(new StickVector(Math.Cos(a), Math.Sin(a)));
        }

        Assert.Equal(1, test.Coverage, 6);
        Assert.Equal(0, test.AverageError, 6);
    }

    [Fact]
    public void CircularityMeasuresSquareCorners()
    {
        var test = new CircularityTest();
        test.AddSample(new StickVector(1, 1)); // corner of a square: 1.414
        test.AddSample(new StickVector(1, 0.001));
        test.AddSample(new StickVector(0.2, 0.2)); // too close to centre, ignored

        Assert.Equal(2.0 / CircularityTest.Sectors, test.Coverage, 6);
        Assert.Equal((Math.Sqrt(2) - 1) / 2, test.AverageError, 3);
    }
}
