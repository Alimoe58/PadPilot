namespace PadPilot.Core.Tests;

public class LightbarEffectTests
{
    private static readonly (byte, byte, byte) Orange = (255, 128, 0);

    [Fact]
    public void StaticKeepsTheColour()
    {
        Assert.Equal(Orange, LightbarEffects.Color(LightbarEffect.Static, Orange, 1, 50, false, 123.4, 5));
        Assert.False(LightbarEffects.IsAnimated(LightbarEffect.Static, charging: true));
    }

    [Fact]
    public void BreathingGoesFromDimToFullAndBack()
    {
        // Speed 5 → 6 s per breath: dim at 0 s, full at 3 s, dim again at 6 s.
        Assert.Equal(((byte)26, (byte)13, (byte)0), LightbarEffects.Color(LightbarEffect.Breathing, Orange, 1, 0, false, 0, 5));
        Assert.Equal(Orange, LightbarEffects.Color(LightbarEffect.Breathing, Orange, 1, 0, false, 3, 5));
        Assert.Equal(((byte)26, (byte)13, (byte)0), LightbarEffects.Color(LightbarEffect.Breathing, Orange, 1, 0, false, 6, 5));
    }

    [Fact]
    public void RainbowWalksThroughTheHues()
    {
        // Speed 10 → 1 s per round: red, then green after a third, blue after two thirds.
        Assert.Equal(((byte)255, (byte)0, (byte)0), LightbarEffects.Color(LightbarEffect.Rainbow, Orange, 1, 0, false, 0, 10));
        Assert.Equal(((byte)0, (byte)255, (byte)0), LightbarEffects.Color(LightbarEffect.Rainbow, Orange, 1, 0, false, 1 / 3.0, 10));
        Assert.Equal(((byte)0, (byte)0, (byte)128), LightbarEffects.Color(LightbarEffect.Rainbow, Orange, 0.5, 0, false, 2 / 3.0, 10));
    }

    [Fact]
    public void BatteryBreathesOnlyWhileCharging()
    {
        Assert.Equal(((byte)255, (byte)0, (byte)0), LightbarEffects.Color(LightbarEffect.Battery, Orange, 1, 0, false, 0, 5));
        Assert.False(LightbarEffects.IsAnimated(LightbarEffect.Battery, charging: false));
        Assert.True(LightbarEffects.IsAnimated(LightbarEffect.Battery, charging: true));
        var dim = LightbarEffects.Color(LightbarEffect.Battery, Orange, 1, 0, true, 0, 5);
        Assert.True(dim.R < 255);
    }
}
