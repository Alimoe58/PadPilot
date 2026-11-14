using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PadPilot.App.Views;

/// <summary>How strongly a trigger setting pushes back at each of the 10 positions (0 = released, 9 = fully pressed).</summary>
public static class TriggerCurve
{
    public const int Zones = 10;

    /// <summary>Resistance per zone, 0–1. Vibrating modes return their strength; <see cref="Vibrates"/> tells them apart.</summary>
    public static double[] Levels(TriggerProfile? p)
    {
        var levels = new double[Zones];
        if (p == null)
            return levels;
        int start = Math.Clamp(p.Start, 0, 9);
        int end = Math.Clamp(p.End, start, 9);
        double strength = Math.Clamp(p.Strength, 1, 8) / 8.0;
        double strength2 = Math.Clamp(p.Strength2, 1, 8) / 8.0;
        for (int z = 0; z < Zones; z++)
        {
            levels[z] = p.Mode switch
            {
                TriggerMode.Resistance or TriggerMode.Vibration => z >= start ? strength : 0,
                TriggerMode.Section or TriggerMode.Bow => z >= start && z <= end ? strength : 0,
                // Builds up towards the break point, then gives way.
                TriggerMode.Weapon => z >= start && z <= end ? strength * (z - start + 1) / (end - start + 1) : 0,
                TriggerMode.Slope => z < start ? 0
                    : z >= end ? strength2
                    : strength + (strength2 - strength) * (z - start) / Math.Max(1, end - start),
                TriggerMode.Galloping => z >= start && z <= end ? 0.5 : 0,
                TriggerMode.Machine => z >= start && z <= end ? Math.Max(strength, strength2) : 0,
                _ => 0,
            };
        }
        return levels;
    }

    public static bool Vibrates(TriggerProfile? p) =>
        p?.Mode is TriggerMode.Vibration or TriggerMode.Galloping or TriggerMode.Machine;
}

/// <summary>
/// Bar chart of a trigger setting: one bar per position, blue for resistance, amber for vibration,
/// and optionally a line where the trigger is pressed right now.
/// </summary>
public sealed class TriggerGraph : FrameworkElement
{
    public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(
        nameof(Profile), typeof(TriggerProfile), typeof(TriggerGraph),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Live trigger value 0–255; negative hides the line.</summary>
    public static readonly DependencyProperty LiveProperty = DependencyProperty.Register(
        nameof(Live), typeof(double), typeof(TriggerGraph),
        new FrameworkPropertyMetadata(-1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public TriggerProfile? Profile { get => (TriggerProfile?)GetValue(ProfileProperty); set => SetValue(ProfileProperty, value); }
    public double Live { get => (double)GetValue(LiveProperty); set => SetValue(LiveProperty, value); }

    private static readonly Brush Track = Frozen(Color.FromArgb(0x40, 0x9A, 0xA0, 0xAA));
    private static readonly Brush Resist = Frozen(Color.FromRgb(0x3B, 0x82, 0xF6));
    private static readonly Brush Vibrate = Frozen(Color.FromRgb(0xF5, 0x9E, 0x0B));
    private static readonly Pen LivePen = FrozenPen(Color.FromRgb(0xF0, 0x6A, 0x4A), 2);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 20 || h < 8)
            return;
        var levels = TriggerCurve.Levels(Profile);
        var fill = TriggerCurve.Vibrates(Profile) ? Vibrate : Resist;
        double gap = Math.Max(1, w * 0.02);
        double bar = (w - gap * (TriggerCurve.Zones - 1)) / TriggerCurve.Zones;
        double radius = Math.Min(4, bar / 4);

        for (int z = 0; z < TriggerCurve.Zones; z++)
        {
            double x = z * (bar + gap);
            dc.DrawRoundedRectangle(Track, null, new Rect(x, 0, bar, h), radius, radius);
            if (levels[z] > 0)
            {
                double bh = Math.Max(2, levels[z] * h);
                dc.DrawRoundedRectangle(fill, null, new Rect(x, h - bh, bar, bh), radius, radius);
            }
        }

        if (Live >= 0)
        {
            double x = Math.Clamp(Live / 255.0, 0, 1) * w;
            dc.DrawLine(LivePen, new Point(x, -2), new Point(x, h + 2));
        }
    }

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Pen FrozenPen(Color c, double thickness)
    {
        var p = new Pen(Frozen(c), thickness);
        p.Freeze();
        return p;
    }
}

/// <summary>Preset → its trigger settings, for the mini graph on a preset card.</summary>
public sealed class PresetProfileConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is TriggerPreset preset ? TriggerPresets.Get(preset) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
