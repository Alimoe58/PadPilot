namespace PadPilot.Core.Power;

/// <summary>Decides when to warn about a low battery: once per discharge, again only after charging.</summary>
public sealed class BatteryWarning
{
    private bool _warned;

    /// <summary>True exactly once when the battery drops to <paramref name="threshold"/> percent or below.</summary>
    public bool Update(int percent, ChargeStatus charge, int threshold)
    {
        if (charge is ChargeStatus.Charging or ChargeStatus.Full)
        {
            _warned = false;
            return false;
        }
        if (charge == ChargeStatus.Unknown || _warned || percent > threshold)
            return false;
        _warned = true;
        return true;
    }
}

public static class BatteryColor
{
    /// <summary>Green when full, yellow at half, red when empty; <paramref name="brightness"/> 0–1 scales it.</summary>
    public static (byte R, byte G, byte B) For(int percent, double brightness = 1)
    {
        double p = Math.Clamp(percent, 0, 100) / 100.0;
        double r = p > 0.5 ? (1 - p) * 2 : 1;
        double g = p < 0.5 ? p * 2 : 1;
        brightness = Math.Clamp(brightness, 0, 1);
        return ((byte)Math.Round(r * 255 * brightness), (byte)Math.Round(g * 255 * brightness), 0);
    }
}
