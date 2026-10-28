using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PadPilot.Core;

namespace PadPilot.App;

/// <summary>Deadzones, live view and circularity test for one stick. The view is a 220 x 220 canvas.</summary>
public sealed class StickDeadzoneViewModel : ObservableObject
{
    public const double ViewSize = 220;
    private const double Center = ViewSize / 2;
    private const double Radius = 100;

    private readonly CircularityTest _circularity = new();

    private int _inner;
    private int _outer;
    private DeadzoneShape _shape;
    private DeadzoneSettings _settings;

    private Point _rawDot = new(Center, Center);
    private Point _outputDot = new(Center, Center);
    private string _rawText = "";
    private string _outputText = "";
    private string _driftText = "";
    private bool _isTesting;
    private PointCollection _circularityPoints = new();
    private string _circularityResult = "";

    public StickDeadzoneViewModel(DeadzoneSettings settings)
    {
        _settings = settings;
        _inner = (int)Math.Round(settings.Inner * 100);
        _outer = (int)Math.Round(settings.Outer * 100);
        _shape = settings.Shape;
        Loc.Instance.PropertyChanged += (_, _) => UpdateCircularityText();
    }

    public DeadzoneSettings Settings => _settings;

    public static IReadOnlyList<DeadzoneShape> Shapes { get; } = Enum.GetValues<DeadzoneShape>();

    /// <summary>Inner deadzone in percent (0–50).</summary>
    public int Inner { get => _inner; set { if (Set(ref _inner, value)) SettingsChanged(); } }

    /// <summary>Outer deadzone in percent (0–30).</summary>
    public int Outer { get => _outer; set { if (Set(ref _outer, value)) SettingsChanged(); } }

    public DeadzoneShape Shape { get => _shape; set { if (Set(ref _shape, value)) SettingsChanged(); } }

    public bool IsRadial => Shape == DeadzoneShape.Radial;
    public bool IsAxial => Shape == DeadzoneShape.Axial;

    // Deadzone outlines in canvas coordinates
    public double InnerSize => Inner / 100.0 * 2 * Radius;
    public double InnerOffset => Center - InnerSize / 2;
    public double OuterSize => (1 - Outer / 100.0) * 2 * Radius;
    public double OuterOffset => Center - OuterSize / 2;

    public Point RawDot { get => _rawDot; private set => Set(ref _rawDot, value); }
    public Point OutputDot { get => _outputDot; private set => Set(ref _outputDot, value); }
    public string RawText { get => _rawText; private set => Set(ref _rawText, value); }
    public string OutputText { get => _outputText; private set => Set(ref _outputText, value); }
    public string DriftText { get => _driftText; private set => Set(ref _driftText, value); }

    public bool IsTesting { get => _isTesting; private set => Set(ref _isTesting, value); }
    public PointCollection CircularityPoints { get => _circularityPoints; private set => Set(ref _circularityPoints, value); }
    public string CircularityResult { get => _circularityResult; private set => Set(ref _circularityResult, value); }

    public void ToggleTest()
    {
        if (!IsTesting)
        {
            _circularity.Reset();
            CircularityPoints = new PointCollection();
        }
        IsTesting = !IsTesting;
        UpdateCircularityText();
    }

    public void ResetDefaults()
    {
        var defaults = new DeadzoneSettings();
        Inner = (int)Math.Round(defaults.Inner * 100);
        Outer = (int)Math.Round(defaults.Outer * 100);
        Shape = defaults.Shape;
    }

    public void Update(byte x, byte y)
    {
        var raw = Deadzones.NormalizeStick(x, y);
        var output = Deadzones.ApplyStick(raw, _settings);

        RawDot = ToCanvas(raw);
        OutputDot = ToCanvas(output);
        RawText = $"X {raw.X,6:0.00}   Y {raw.Y,6:0.00}";
        OutputText = $"X {output.X,6:0.00}   Y {output.Y,6:0.00}";
        DriftText = $"{raw.Length * 100:0.0} %";

        if (IsTesting)
        {
            _circularity.AddSample(raw);
            CircularityPoints = BuildCircularityPoints();
            UpdateCircularityText();
        }
    }

    private void SettingsChanged()
    {
        _settings = new DeadzoneSettings { Inner = Inner / 100.0, Outer = Outer / 100.0, Shape = Shape };
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(IsRadial));
        OnPropertyChanged(nameof(IsAxial));
        OnPropertyChanged(nameof(InnerSize));
        OnPropertyChanged(nameof(InnerOffset));
        OnPropertyChanged(nameof(OuterSize));
        OnPropertyChanged(nameof(OuterOffset));
    }

    private static Point ToCanvas(StickVector v) => new(Center + v.X * Radius, Center + v.Y * Radius);

    private PointCollection BuildCircularityPoints()
    {
        var points = new PointCollection();
        for (int i = 0; i < CircularityTest.Sectors; i++)
        {
            double r = _circularity.MaxRadius[i];
            if (r <= 0)
                continue;
            double a = CircularityTest.SectorAngle(i);
            points.Add(new Point(Center + Math.Cos(a) * r * Radius, Center + Math.Sin(a) * r * Radius));
        }
        points.Freeze();
        return points;
    }

    private void UpdateCircularityText()
    {
        if (_circularity.Coverage == 0)
        {
            CircularityResult = IsTesting ? Loc.Get("Circ_Hint") : "";
            return;
        }
        CircularityResult = string.Format(CultureInfo.CurrentCulture, Loc.Get("Circ_Result"),
            _circularity.AverageError * 100, _circularity.Coverage * 100);
    }
}

/// <summary>Deadzones and live bars for one trigger.</summary>
public sealed class TriggerDeadzoneViewModel : ObservableObject
{
    private int _inner;
    private int _outer;
    private DeadzoneSettings _settings;
    private double _rawPercent;
    private double _outputPercent;

    public TriggerDeadzoneViewModel(string name, DeadzoneSettings settings)
    {
        Name = name;
        _settings = settings;
        _inner = (int)Math.Round(settings.Inner * 100);
        _outer = (int)Math.Round(settings.Outer * 100);
    }

    public string Name { get; }
    public DeadzoneSettings Settings => _settings;

    public int Inner { get => _inner; set { if (Set(ref _inner, value)) SettingsChanged(); } }
    public int Outer { get => _outer; set { if (Set(ref _outer, value)) SettingsChanged(); } }

    public double RawPercent { get => _rawPercent; private set => Set(ref _rawPercent, value); }
    public double OutputPercent { get => _outputPercent; private set => Set(ref _outputPercent, value); }

    public void ResetDefaults()
    {
        var defaults = new DeadzoneSettings { Inner = 0 };
        Inner = (int)Math.Round(defaults.Inner * 100);
        Outer = (int)Math.Round(defaults.Outer * 100);
    }

    public void Update(byte raw)
    {
        RawPercent = raw / 255.0 * 100;
        OutputPercent = Deadzones.ApplyTrigger(raw, _settings) * 100;
    }

    private void SettingsChanged() =>
        _settings = new DeadzoneSettings { Inner = Inner / 100.0, Outer = Outer / 100.0 };
}

/// <summary>Everything on the "Sticks &amp; deadzones" tab.</summary>
public sealed class DeadzonesViewModel
{
    public DeadzonesViewModel(AppSettings settings)
    {
        LeftStick = new StickDeadzoneViewModel(settings.LeftStick);
        RightStick = new StickDeadzoneViewModel(settings.RightStick);
        L2 = new TriggerDeadzoneViewModel("L2", settings.L2);
        R2 = new TriggerDeadzoneViewModel("R2", settings.R2);
    }

    public StickDeadzoneViewModel LeftStick { get; }
    public StickDeadzoneViewModel RightStick { get; }
    public TriggerDeadzoneViewModel L2 { get; }
    public TriggerDeadzoneViewModel R2 { get; }

    public void Update(ControllerState state)
    {
        LeftStick.Update(state.LeftX, state.LeftY);
        RightStick.Update(state.RightX, state.RightY);
        L2.Update(state.L2);
        R2.Update(state.R2);
    }

    public void SaveTo(AppSettings settings)
    {
        settings.LeftStick = LeftStick.Settings;
        settings.RightStick = RightStick.Settings;
        settings.L2 = L2.Settings;
        settings.R2 = R2.Settings;
    }
}
