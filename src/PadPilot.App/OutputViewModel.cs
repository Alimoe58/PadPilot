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
    private int _rumbleLeft;
    private int _rumbleRight;

    public event Action? Changed;

    public OutputViewModel()
    {
        LeftTrigger.Changed += RaiseChanged;
        RightTrigger.Changed += RaiseChanged;
    }

    public int Red { get => _red; set => SetColor(ref _red, value); }
    public int Green { get => _green; set => SetColor(ref _green, value); }
    public int Blue { get => _blue; set => SetColor(ref _blue, value); }
    public Brush LightbarPreview => new SolidColorBrush(Color.FromRgb((byte)Red, (byte)Green, (byte)Blue));

    public bool Led1 { get => _led1; set => SetAndRaise(ref _led1, value); }
    public bool Led2 { get => _led2; set => SetAndRaise(ref _led2, value); }
    public bool Led3 { get => _led3; set => SetAndRaise(ref _led3, value); }
    public bool Led4 { get => _led4; set => SetAndRaise(ref _led4, value); }
    public bool Led5 { get => _led5; set => SetAndRaise(ref _led5, value); }
    public LedBrightness LedBrightness { get => _ledBrightness; set => SetAndRaise(ref _ledBrightness, value); }

    public MicLed MicLed { get => _micLed; set => SetAndRaise(ref _micLed, value); }

    public int RumbleLeft { get => _rumbleLeft; set => SetAndRaise(ref _rumbleLeft, value); }
    public int RumbleRight { get => _rumbleRight; set => SetAndRaise(ref _rumbleRight, value); }

    public TriggerViewModel LeftTrigger { get; } = new();
    public TriggerViewModel RightTrigger { get; } = new();

    public IReadOnlyList<MicLed> MicLedModes { get; } = Enum.GetValues<MicLed>();
    public IReadOnlyList<LedBrightness> BrightnessLevels { get; } = Enum.GetValues<LedBrightness>();

    public void SetColor(Color color)
    {
        _red = color.R;
        _green = color.G;
        _blue = color.B;
        OnPropertyChanged(nameof(Red));
        OnPropertyChanged(nameof(Green));
        OnPropertyChanged(nameof(Blue));
        OnPropertyChanged(nameof(LightbarPreview));
        RaiseChanged();
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
        RaiseChanged();
    }

    private void SetAndRaise<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (Set(ref field, value, name))
            RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke();
}
