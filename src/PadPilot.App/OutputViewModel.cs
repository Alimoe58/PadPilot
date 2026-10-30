using System.Windows.Media;
using PadPilot.Core;

namespace PadPilot.App;

public enum TriggerMode
{
    Off,
    Resistance,
    Section,
    Weapon,
    Vibration,
}

/// <summary>Ready-made trigger settings. Custom means the current settings match none of them.</summary>
public enum TriggerPreset
{
    Custom,
    Off,
    Light,
    Heavy,
    Brake,
    Bow,
    Click,
    Pistol,
    Shotgun,
    MachineGun,
    Engine,
}

public static class TriggerPresets
{
    public static IReadOnlyList<TriggerPreset> All { get; } = Enum.GetValues<TriggerPreset>();

    public static TriggerProfile? Get(TriggerPreset preset) => preset switch
    {
        TriggerPreset.Off => new() { Mode = TriggerMode.Off },
        TriggerPreset.Light => new() { Mode = TriggerMode.Resistance, Start = 0, Strength = 2 },
        TriggerPreset.Heavy => new() { Mode = TriggerMode.Resistance, Start = 0, Strength = 7 },
        TriggerPreset.Brake => new() { Mode = TriggerMode.Resistance, Start = 3, Strength = 6 },
        TriggerPreset.Bow => new() { Mode = TriggerMode.Section, Start = 2, End = 8, Strength = 6 },
        TriggerPreset.Click => new() { Mode = TriggerMode.Weapon, Start = 2, End = 3, Strength = 3 },
        TriggerPreset.Pistol => new() { Mode = TriggerMode.Weapon, Start = 3, End = 5, Strength = 5 },
        TriggerPreset.Shotgun => new() { Mode = TriggerMode.Weapon, Start = 2, End = 7, Strength = 8 },
        TriggerPreset.MachineGun => new() { Mode = TriggerMode.Vibration, Start = 3, Strength = 6, Frequency = 12 },
        TriggerPreset.Engine => new() { Mode = TriggerMode.Vibration, Start = 1, Strength = 3, Frequency = 30 },
        _ => null,
    };

    /// <summary>The preset these settings correspond to, or Custom.</summary>
    public static TriggerPreset Match(TriggerProfile settings)
    {
        var wanted = Normalize(settings);
        return All.FirstOrDefault(p => Get(p) is TriggerProfile v && Normalize(v) == wanted, TriggerPreset.Custom);
    }

    /// <summary>Clears the values the mode doesn't use, so they don't affect the comparison.</summary>
    private static TriggerProfile Normalize(TriggerProfile p) => p.Mode switch
    {
        TriggerMode.Off => new() { Mode = TriggerMode.Off, Start = 0, End = 0, Strength = 0, Frequency = 0 },
        TriggerMode.Resistance => p with { End = 0, Frequency = 0 },
        TriggerMode.Vibration => p with { End = 0 },
        _ => p with { Frequency = 0 },
    };
}

/// <summary>Settings for one adaptive trigger.</summary>
public sealed class TriggerViewModel : ObservableObject
{
    private TriggerMode _mode;
    private int _start = 2;
    private int _end = 6;
    private int _strength = 5;
    private int _frequency = 20;

    public static IReadOnlyList<TriggerMode> Modes { get; } = Enum.GetValues<TriggerMode>();

    public event Action? Changed;

    public TriggerViewModel() => Changed += () => OnPropertyChanged(nameof(Preset));

    /// <summary>Picking a preset fills in mode and sliders; afterwards they can still be adjusted.</summary>
    public TriggerPreset Preset
    {
        get => TriggerPresets.Match(ToProfile());
        set
        {
            if (TriggerPresets.Get(value) is TriggerProfile p)
                Apply(p);
            else // "Custom" picked: show what the settings actually are (after the ComboBox has finished its update)
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => OnPropertyChanged(nameof(Preset)));
        }
    }

    public TriggerMode Mode
    {
        get => _mode;
        set
        {
            if (!Set(ref _mode, value)) return;
            OnPropertyChanged(nameof(UsesStart));
            OnPropertyChanged(nameof(UsesEnd));
            OnPropertyChanged(nameof(UsesStrength));
            OnPropertyChanged(nameof(UsesFrequency));
            Changed?.Invoke();
        }
    }

    public int Start { get => _start; set { if (Set(ref _start, value)) Changed?.Invoke(); } }
    public int End { get => _end; set { if (Set(ref _end, value)) Changed?.Invoke(); } }
    public int Strength { get => _strength; set { if (Set(ref _strength, value)) Changed?.Invoke(); } }
    public int Frequency { get => _frequency; set { if (Set(ref _frequency, value)) Changed?.Invoke(); } }

    public bool UsesStart => Mode != TriggerMode.Off;
    public bool UsesEnd => Mode is TriggerMode.Section or TriggerMode.Weapon;
    public bool UsesStrength => Mode != TriggerMode.Off;
    public bool UsesFrequency => Mode == TriggerMode.Vibration;

    public TriggerEffect ToEffect() => Mode switch
    {
        TriggerMode.Resistance => TriggerEffect.Resistance(Start, Strength),
        TriggerMode.Section => TriggerEffect.Section(Start, End, Strength),
        TriggerMode.Weapon => TriggerEffect.Weapon(Start, End, Strength),
        TriggerMode.Vibration => TriggerEffect.Vibration(Start, Strength, Frequency),
        _ => TriggerEffect.Off,
    };

    public TriggerProfile ToProfile() => new() { Mode = Mode, Start = Start, End = End, Strength = Strength, Frequency = Frequency };

    public void Apply(TriggerProfile p)
    {
        Start = p.Start;
        End = p.End;
        Strength = p.Strength;
        Frequency = p.Frequency;
        Mode = p.Mode;
    }
}

/// <summary>Everything on the outputs tab. Raises <see cref="Changed"/> whenever the controller needs an update.</summary>
public sealed class OutputViewModel : ObservableObject
{
    private int _red = 0x3B;
    private int _green = 0x82;
    private int _blue = 0xF6;
    private bool _led1, _led2, _led3 = true, _led4, _led5;
    private LedBrightness _ledBrightness = LedBrightness.High;
    private MicLed _micLed = MicLed.Off;
    private bool _lightbarBattery;
    private int _rumbleLeft;
    private int _rumbleRight;

    public event Action? Changed;

    // The lightbar colour is stored as RGB (what the controller gets); hue / intensity / brightness
    // are kept alongside for the colour field and sliders, and both sides update each other.
    private double _hue;
    private int _saturation;
    private int _brightness;

    public OutputViewModel()
    {
        LeftTrigger.Changed += RaiseChanged;
        RightTrigger.Changed += RaiseChanged;
        UpdateHsvFromRgb();
    }

    public int Red { get => _red; set => SetColor(ref _red, value); }
    public int Green { get => _green; set => SetColor(ref _green, value); }
    public int Blue { get => _blue; set => SetColor(ref _blue, value); }
    public Brush LightbarPreview => new SolidColorBrush(Color.FromRgb((byte)Red, (byte)Green, (byte)Blue));

    /// <summary>Hue in degrees (0–360), picked in the colour field.</summary>
    public double Hue { get => _hue; set { if (Set(ref _hue, Math.Clamp(value, 0, 360))) ApplyHsv(); } }

    /// <summary>Colour intensity (saturation) in percent: 100 = pure colour, 0 = white.</summary>
    public int Saturation { get => _saturation; set { if (Set(ref _saturation, Math.Clamp(value, 0, 100))) ApplyHsv(); } }

    /// <summary>Brightness in percent: 0 = off, 100 = full brightness.</summary>
    public int Brightness { get => _brightness; set { if (Set(ref _brightness, Math.Clamp(value, 0, 100))) ApplyHsv(); } }

    /// <summary>Colour as "#RRGGBB"; invalid input is ignored.</summary>
    public string Hex
    {
        get => $"#{Red:X2}{Green:X2}{Blue:X2}";
        set
        {
            string s = (value ?? "").Trim().TrimStart('#');
            if (s.Length == 6 && int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out int rgb))
                SetColor(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
            else
                OnPropertyChanged(nameof(Hex)); // put the valid value back into the text box
        }
    }

    public bool Led1 { get => _led1; set => SetAndRaise(ref _led1, value); }
    public bool Led2 { get => _led2; set => SetAndRaise(ref _led2, value); }
    public bool Led3 { get => _led3; set => SetAndRaise(ref _led3, value); }
    public bool Led4 { get => _led4; set => SetAndRaise(ref _led4, value); }
    public bool Led5 { get => _led5; set => SetAndRaise(ref _led5, value); }
    public LedBrightness LedBrightness { get => _ledBrightness; set => SetAndRaise(ref _ledBrightness, value); }

    public MicLed MicLed { get => _micLed; set => SetAndRaise(ref _micLed, value); }

    /// <summary>Lightbar shows the battery level (green → red) instead of the chosen colour; brightness still applies.</summary>
    public bool LightbarBattery { get => _lightbarBattery; set => SetAndRaise(ref _lightbarBattery, value); }

    public int RumbleLeft { get => _rumbleLeft; set => SetAndRaise(ref _rumbleLeft, value); }
    public int RumbleRight { get => _rumbleRight; set => SetAndRaise(ref _rumbleRight, value); }

    public TriggerViewModel LeftTrigger { get; } = new();
    public TriggerViewModel RightTrigger { get; } = new();

    public IReadOnlyList<MicLed> MicLedModes { get; } = Enum.GetValues<MicLed>();
    public IReadOnlyList<LedBrightness> BrightnessLevels { get; } = Enum.GetValues<LedBrightness>();

    /// <summary>Current settings for a profile (the rumble test sliders are not included).</summary>
    public OutputProfile ToProfile() => new()
    {
        Lightbar = Hex,
        PlayerLeds = [Led1, Led2, Led3, Led4, Led5],
        LedBrightness = LedBrightness,
        MicLed = MicLed,
        LeftTrigger = LeftTrigger.ToProfile(),
        RightTrigger = RightTrigger.ToProfile(),
        LightbarShowsBattery = LightbarBattery,
    };

    public void Apply(OutputProfile p)
    {
        Hex = p.Lightbar;
        bool Led(int i) => i < p.PlayerLeds.Length && p.PlayerLeds[i];
        Led1 = Led(0);
        Led2 = Led(1);
        Led3 = Led(2);
        Led4 = Led(3);
        Led5 = Led(4);
        LedBrightness = p.LedBrightness;
        MicLed = p.MicLed;
        LeftTrigger.Apply(p.LeftTrigger);
        RightTrigger.Apply(p.RightTrigger);
        LightbarBattery = p.LightbarShowsBattery;
    }

    public void SetColor(Color color)
    {
        SetRgb(color.R, color.G, color.B);
        UpdateHsvFromRgb();
    }

    public void StopRumble()
    {
        RumbleLeft = 0;
        RumbleRight = 0;
    }

    public OutputState ToState()
    {
        var leds = PlayerLeds.None;
        if (Led1) leds |= PlayerLeds.Led1;
        if (Led2) leds |= PlayerLeds.Led2;
        if (Led3) leds |= PlayerLeds.Led3;
        if (Led4) leds |= PlayerLeds.Led4;
        if (Led5) leds |= PlayerLeds.Led5;

        return new OutputState
        {
            LightbarRed = (byte)Red,
            LightbarGreen = (byte)Green,
            LightbarBlue = (byte)Blue,
            PlayerLeds = leds,
            PlayerLedBrightness = LedBrightness,
            MicLed = MicLed,
            RumbleLeft = (byte)RumbleLeft,
            RumbleRight = (byte)RumbleRight,
            LeftTrigger = LeftTrigger.ToEffect(),
            RightTrigger = RightTrigger.ToEffect(),
        };
    }

    private void SetColor(ref int field, int value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (!Set(ref field, Math.Clamp(value, 0, 255), name)) return;
        OnPropertyChanged(nameof(LightbarPreview));
        OnPropertyChanged(nameof(Hex));
        UpdateHsvFromRgb();
        RaiseChanged();
    }

    private void SetRgb(int r, int g, int b)
    {
        _red = r;
        _green = g;
        _blue = b;
        OnPropertyChanged(nameof(Red));
        OnPropertyChanged(nameof(Green));
        OnPropertyChanged(nameof(Blue));
        OnPropertyChanged(nameof(LightbarPreview));
        OnPropertyChanged(nameof(Hex));
        RaiseChanged();
    }

    /// <summary>Hue / intensity / brightness changed: recompute the RGB colour.</summary>
    private void ApplyHsv()
    {
        double s = Saturation / 100.0, v = Brightness / 100.0;
        double c = v * s;
        double h = Hue % 360 / 60;
        double x = c * (1 - Math.Abs(h % 2 - 1));
        var (r, g, b) = (int)h switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        double m = v - c;
        SetRgb((int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
    }

    /// <summary>RGB changed: recompute hue / intensity / brightness. Hue and intensity are kept when they are undefined (black or grey).</summary>
    private void UpdateHsvFromRgb()
    {
        double r = Red / 255.0, g = Green / 255.0, b = Blue / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        _brightness = (int)Math.Round(max * 100);
        if (max > 0)
            _saturation = (int)Math.Round(delta / max * 100);
        if (delta > 0)
        {
            double h = max == r ? (g - b) / delta % 6
                     : max == g ? (b - r) / delta + 2
                     : (r - g) / delta + 4;
            _hue = (h * 60 + 360) % 360;
        }

        OnPropertyChanged(nameof(Hue));
        OnPropertyChanged(nameof(Saturation));
        OnPropertyChanged(nameof(Brightness));
    }

    private void SetAndRaise<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (Set(ref field, value, name))
            RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke();
}
