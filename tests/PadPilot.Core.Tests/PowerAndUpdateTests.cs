using PadPilot.Core.Power;
using PadPilot.Core.Updates;

namespace PadPilot.Core.Tests;

public class PowerAndUpdateTests
{
    [Fact]
    public void BatteryWarnsOncePerDischarge()
    {
        var w = new BatteryWarning();
        Assert.False(w.Update(45, ChargeStatus.Discharging, 20));
        Assert.True(w.Update(15, ChargeStatus.Discharging, 20));
        Assert.False(w.Update(15, ChargeStatus.Discharging, 20));
        Assert.False(w.Update(5, ChargeStatus.Discharging, 20));

        Assert.False(w.Update(15, ChargeStatus.Charging, 20));
        Assert.True(w.Update(15, ChargeStatus.Discharging, 20));
    }

    [Fact]
    public void BatteryUnknownNeverWarns()
    {
        Assert.False(new BatteryWarning().Update(0, ChargeStatus.Unknown, 20));
    }

    [Fact]
    public void BatteryColourGoesFromGreenToRed()
    {
        Assert.Equal(((byte)0, (byte)255, (byte)0), BatteryColor.For(100));
        Assert.Equal(((byte)255, (byte)255, (byte)0), BatteryColor.For(50));
        Assert.Equal(((byte)255, (byte)0, (byte)0), BatteryColor.For(0));
        Assert.Equal(((byte)0, (byte)128, (byte)0), BatteryColor.For(100, 0.5));
    }

    [Fact]
    public void IdleTimeCountsFromTheLastRealInput()
    {
        var idle = new IdleTracker();
        var t0 = TimeSpan.FromMinutes(1);
        idle.Reset(t0);
        idle.Update(new ControllerState(), t0);
        // Small stick drift and a still controller don't count.
        idle.Update(new ControllerState { LeftX = 135, RightY = 120, Gyro = new Vector3S(20, -15, 5) }, t0 + TimeSpan.FromMinutes(4));
        Assert.Equal(TimeSpan.FromMinutes(4), idle.IdleTime(t0 + TimeSpan.FromMinutes(4)));

        idle.Update(new ControllerState { Buttons = Buttons.Cross }, t0 + TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.Zero, idle.IdleTime(t0 + TimeSpan.FromMinutes(5)));

        idle.Update(new ControllerState { RightX = 250 }, t0 + TimeSpan.FromMinutes(7));
        Assert.Equal(TimeSpan.FromMinutes(1), idle.IdleTime(t0 + TimeSpan.FromMinutes(8)));
    }

    [Theory]
    [InlineData("v1.2.0", "1.2.0")]
    [InlineData("1.2", "1.2.0")]
    [InlineData("V2.0.1-beta", "2.0.1")]
    public void VersionTagsAreParsed(string tag, string expected) =>
        Assert.Equal(Version.Parse(expected), UpdateChecker.ParseVersion(tag));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    public void InvalidTagsGiveNull(string? tag) => Assert.Null(UpdateChecker.ParseVersion(tag));

    [Fact]
    public void OnlyHigherVersionsAreNewer()
    {
        Assert.True(UpdateChecker.IsNewer(new Version(1, 2, 0), new Version(1, 1, 0)));
        Assert.False(UpdateChecker.IsNewer(new Version(1, 1, 0), new Version(1, 1, 0)));
        Assert.False(UpdateChecker.IsNewer(new Version(1, 1), new Version(1, 1, 0)));
        Assert.False(UpdateChecker.IsNewer(new Version(1, 0, 9), new Version(1, 1, 0)));
    }
}
