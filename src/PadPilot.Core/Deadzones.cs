namespace PadPilot.Core;

public enum DeadzoneShape
{
    /// <summary>Deadzone is a circle around the centre; direction is preserved.</summary>
    Radial,

    /// <summary>Each axis has its own deadzone (a cross shape); makes pure up/down/left/right easier.</summary>
    Axial,
}

public readonly record struct StickVector(double X, double Y)
{
    public double Length => Math.Sqrt(X * X + Y * Y);
}

/// <summary>
/// Deadzone settings for one stick or trigger. Values are fractions of the full range:
/// <see cref="Inner"/> = movement near rest that is ignored, <see cref="Outer"/> = movement
/// near the edge that already counts as 100 %.
/// </summary>
public sealed record DeadzoneSettings
{
    public const double MaxInner = 0.5;
    public const double MaxOuter = 0.3;

    public double Inner { get; init; } = 0.05;
    public double Outer { get; init; } = 0.0;
    public DeadzoneShape Shape { get; init; } = DeadzoneShape.Radial;
}

public static class Deadzones
{
    /// <summary>Raw stick byte (0–255) to −1…1. 0 is left/up, 255 is right/down.</summary>
    public static double Normalize(byte raw) => (raw - 127.5) / 127.5;

    public static StickVector NormalizeStick(byte x, byte y) => new(Normalize(x), Normalize(y));

    public static StickVector ApplyStick(StickVector raw, DeadzoneSettings s)
    {
        var (inner, outer) = Clamp(s);

        if (s.Shape == DeadzoneShape.Axial)
            return new StickVector(Rescale(raw.X, inner, outer), Rescale(raw.Y, inner, outer));

        double length = raw.Length;
        if (length <= inner)
            return new StickVector(0, 0);

        double scaled = Math.Abs(Rescale(length, inner, outer));
        return new StickVector(raw.X / length * scaled, raw.Y / length * scaled);
    }

    /// <summary>Trigger byte (0–255) to 0…1 with deadzones applied.</summary>
    public static double ApplyTrigger(byte raw, DeadzoneSettings s)
    {
        var (inner, outer) = Clamp(s);
        return Rescale(raw / 255.0, inner, outer);
    }

    /// <summary>Maps |value| from [inner, 1 − outer] to [0, 1], keeping the sign.</summary>
    private static double Rescale(double value, double inner, double outer)
    {
        double magnitude = Math.Abs(value);
        if (magnitude <= inner)
            return 0;
        double result = Math.Min(1, (magnitude - inner) / (1 - outer - inner));
        return Math.CopySign(result, value);
    }

    private static (double Inner, double Outer) Clamp(DeadzoneSettings s) =>
        (Math.Clamp(s.Inner, 0, DeadzoneSettings.MaxInner), Math.Clamp(s.Outer, 0, DeadzoneSettings.MaxOuter));
}

/// <summary>
/// Measures how round a stick's outer range is. Rotate the stick along its edge; for each
/// 5° sector the furthest reach is kept. A perfect stick reaches exactly 1.0 everywhere.
/// </summary>
public sealed class CircularityTest
{
    public const int Sectors = 72;

    private readonly double[] _maxRadius = new double[Sectors];

    // Ignore samples near the centre; only the edge matters.
    private const double MinRadius = 0.5;

    public void Reset() => Array.Clear(_maxRadius);

    public void AddSample(StickVector raw)
    {
        double r = raw.Length;
        if (r < MinRadius)
            return;
        int sector = SectorOf(raw);
        if (r > _maxRadius[sector])
            _maxRadius[sector] = r;
    }

    /// <summary>Share of sectors that have been reached (0–1).</summary>
    public double Coverage => _maxRadius.Count(r => r > 0) / (double)Sectors;

    /// <summary>Average deviation from the unit circle over the sectors reached, as a fraction.</summary>
    public double AverageError
    {
        get
        {
            var reached = _maxRadius.Where(r => r > 0).ToArray();
            return reached.Length == 0 ? 0 : reached.Average(r => Math.Abs(r - 1));
        }
    }

    /// <summary>Furthest reach per sector (0 = not reached yet), sector 0 pointing right, clockwise on screen.</summary>
    public IReadOnlyList<double> MaxRadius => _maxRadius;

    public static double SectorAngle(int sector) => (sector + 0.5) * (2 * Math.PI / Sectors);

    private static int SectorOf(StickVector v)
    {
        double angle = Math.Atan2(v.Y, v.X);
        if (angle < 0)
            angle += 2 * Math.PI;
        return Math.Min(Sectors - 1, (int)(angle / (2 * Math.PI) * Sectors));
    }
}
