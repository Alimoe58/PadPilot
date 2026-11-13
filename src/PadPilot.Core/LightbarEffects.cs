using PadPilot.Core.Power;

namespace PadPilot.Core;

public enum LightbarEffect
{
    Static,
    Breathing,
    Rainbow,
    Battery,
}

/// <summary>Lightbar colour for an effect at a point in time.</summary>
public static class LightbarEffects
{
    /// <summary>Whether the effect changes over time (and so needs regular updates to the controller).</summary>
    public static bool IsAnimated(LightbarEffect effect, bool charging) =>
        effect is LightbarEffect.Breathing or LightbarEffect.Rainbow || (effect == LightbarEffect.Battery && charging);

    /// <param name="color">The colour chosen in the colour field.</param>
    /// <param name="brightness">Its brightness 0–1, used for rainbow and battery colours.</param>
    /// <param name="seconds">Time since start; only differences matter.</param>
    /// <param name="speed">1 (slow, 10 s per cycle) to 10 (fast, 1 s per cycle).</param>
    public static (byte R, byte G, byte B) Color(LightbarEffect effect, (byte R, byte G, byte B) color, double brightness,
        int batteryPercent, bool charging, double seconds, int speed)
    {
        double period = 11 - Math.Clamp(speed, 1, 10);
        double phase = seconds / period % 1;
        switch (effect)
        {
            case LightbarEffect.Breathing:
                return Scale(color, Breath(phase));
            case LightbarEffect.Rainbow:
                return Hsv(phase * 360, brightness);
            case LightbarEffect.Battery:
                var battery = BatteryColor.For(batteryPercent, brightness);
                return charging ? Scale(battery, Breath(phase)) : battery;
            default:
                return color;
        }
    }

    /// <summary>Smooth in and out, never fully dark (10 %–100 %).</summary>
    private static double Breath(double phase) => 0.1 + 0.9 * (0.5 - 0.5 * Math.Cos(2 * Math.PI * phase));

    private static (byte, byte, byte) Scale((byte R, byte G, byte B) c, double f) =>
        ((byte)Math.Round(c.R * f), (byte)Math.Round(c.G * f), (byte)Math.Round(c.B * f));

    /// <summary>Fully saturated colour of the given hue (0–360) and brightness (0–1).</summary>
    private static (byte, byte, byte) Hsv(double hue, double value)
    {
        value = Math.Clamp(value, 0, 1);
        double h = hue % 360 / 60;
        double x = value * (1 - Math.Abs(h % 2 - 1));
        var (r, g, b) = (int)h switch
        {
            0 => (value, x, 0.0),
            1 => (x, value, 0.0),
            2 => (0.0, value, x),
            3 => (0.0, x, value),
            4 => (x, 0.0, value),
            _ => (value, 0.0, x),
        };
        return ((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
    }
}
