using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using PadPilot.Core;
using PadPilot.Core.Mapping;
using MouseAction = PadPilot.Core.Mapping.MouseAction;

namespace PadPilot.App;

/// <summary>One controller button on the "Buttons" tab.</summary>
public sealed class ButtonMappingRow : ObservableObject
{
    private ActionKind _kind;
    private Buttons _target;
    private int _key;
    private KeyModifiers _modifiers;
    private MouseAction _mouse;
    private SpecialAction _special;
    private bool _isCapturing;

    public ButtonMappingRow(Buttons button)
    {
        Button = button;
        _target = button;
    }

    public Buttons Button { get; }

    /// <summary>Raised when the row's action changed.</summary>
    public event Action? Changed;

    public ActionKind Kind
    {
        get => _kind;
        set
        {
            if (!Set(ref _kind, value)) return;
            OnPropertyChanged(nameof(UsesTarget));
            OnPropertyChanged(nameof(UsesKey));
            OnPropertyChanged(nameof(UsesMouse));
            OnPropertyChanged(nameof(UsesSpecial));
            OnPropertyChanged(nameof(IsChanged));
            Changed?.Invoke();
        }
    }

    public Buttons Target { get => _target; set { if (Set(ref _target, value)) Changed?.Invoke(); } }
    public MouseAction Mouse { get => _mouse; set { if (Set(ref _mouse, value)) Changed?.Invoke(); } }
    public SpecialAction Special { get => _special; set { if (Set(ref _special, value)) Changed?.Invoke(); } }

    public bool UsesTarget => Kind == ActionKind.Button;
    public bool UsesKey => Kind == ActionKind.Key;
    public bool UsesMouse => Kind == ActionKind.Mouse;
    public bool UsesSpecial => Kind == ActionKind.Special;
    public bool IsChanged => Kind != ActionKind.Default;

    public bool IsCapturing
    {
        get => _isCapturing;
        set { if (Set(ref _isCapturing, value)) OnPropertyChanged(nameof(KeyText)); }
    }

    public string KeyText => IsCapturing ? Loc.Get("Mapping_PressKey")
        : _key == 0 ? Loc.Get("Mapping_ChooseKey")
        : KeyNames.Format(_key, _modifiers);

    public void SetKey(int key, KeyModifiers modifiers)
    {
        _key = key;
        _modifiers = modifiers;
        IsCapturing = false;
        OnPropertyChanged(nameof(KeyText));
        Changed?.Invoke();
    }

    public void RefreshTexts() => OnPropertyChanged(nameof(KeyText));

    /// <summary>The action, or null when the button is unchanged.</summary>
    public ButtonAction? ToAction() => Kind switch
    {
        ActionKind.Default => null,
        ActionKind.Key when _key == 0 => null, // no key chosen yet
        ActionKind.Button when Target == Button => null,
        _ => new ButtonAction { Kind = Kind, Target = Target, Key = _key, Modifiers = _modifiers, Mouse = Mouse, Special = Special },
    };

    public void Apply(ButtonAction? a)
    {
        _target = a?.Kind == ActionKind.Button ? a.Target : Button;
        _key = a?.Key ?? 0;
        _modifiers = a?.Modifiers ?? KeyModifiers.None;
        _mouse = a?.Mouse ?? MouseAction.LeftClick;
        _special = a?.Special ?? SpecialAction.ToggleMicMute;
        _isCapturing = false;
        _kind = a?.Kind ?? ActionKind.Default;
        OnPropertyChanged(string.Empty);
    }
}

/// <summary>The "Buttons" tab: what each button and stick does. Part of a profile.</summary>
public sealed class MappingViewModel : ObservableObject
{
    private readonly MappingService _service;
    private StickMode _leftStick;
    private StickMode _rightStick;
    private int _stickSpeed = 40;
    private bool _applying;
    private ButtonMappingRow? _capturing;

    // Order on the tab: face buttons, shoulders, sticks, d-pad, centre buttons.
    private static readonly Buttons[] Order =
    [
        Buttons.Cross, Buttons.Circle, Buttons.Square, Buttons.Triangle,
        Buttons.L1, Buttons.R1, Buttons.L2, Buttons.R2, Buttons.L3, Buttons.R3,
        Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight,
        Buttons.Options, Buttons.Create, Buttons.PS, Buttons.TouchpadClick, Buttons.Mute,
    ];

    public MappingViewModel(MappingService service)
    {
        _service = service;
        Rows = new ObservableCollection<ButtonMappingRow>(Order.Select(b => new ButtonMappingRow(b)));
        foreach (var row in Rows)
            row.Changed += Push;
        Loc.Instance.PropertyChanged += (_, _) =>
        {
            foreach (var row in Rows)
                row.RefreshTexts();
        };
    }

    public ObservableCollection<ButtonMappingRow> Rows { get; }

    public static IReadOnlyList<ActionKind> Kinds { get; } = Enum.GetValues<ActionKind>();
    public static IReadOnlyList<Buttons> Targets { get; } = Order.Where(b => b != Buttons.Mute).ToList();
    public static IReadOnlyList<MouseAction> MouseActions { get; } = Enum.GetValues<MouseAction>();
    public static IReadOnlyList<SpecialAction> SpecialActions { get; } = Enum.GetValues<SpecialAction>();
    public static IReadOnlyList<StickMode> StickModes { get; } = Enum.GetValues<StickMode>();

    public StickMode LeftStick { get => _leftStick; set { if (Set(ref _leftStick, value)) Push(); } }
    public StickMode RightStick { get => _rightStick; set { if (Set(ref _rightStick, value)) Push(); } }

    /// <summary>Stick-as-mouse speed, 1–100 (× 30 pixels per second at full deflection).</summary>
    public int StickSpeed { get => _stickSpeed; set { if (Set(ref _stickSpeed, Math.Clamp(value, 1, 100))) Push(); } }

    public MappingSettings Settings => _service.Settings;

    public bool IsCapturing => _capturing != null;

    /// <summary>The next key pressed in the window goes to this row; clicking the row again cancels.</summary>
    public void StartCapture(ButtonMappingRow row)
    {
        bool cancel = _capturing == row;
        StopCapture();
        if (cancel)
            return;
        _capturing = row;
        row.IsCapturing = true;
    }

    public void CaptureKey(int virtualKey, KeyModifiers modifiers)
    {
        var row = _capturing;
        _capturing = null;
        row?.SetKey(virtualKey, modifiers);
    }

    public void StopCapture()
    {
        if (_capturing != null)
            _capturing.IsCapturing = false;
        _capturing = null;
    }

    /// <summary>Quick setup: the Mic button mutes the Windows microphone, the mic LED shows the state.</summary>
    public void UseMicButtonForMute()
    {
        var row = Rows.First(r => r.Button == Buttons.Mute);
        row.Special = SpecialAction.ToggleMicMute;
        row.Kind = ActionKind.Special;
    }

    public void ResetAll() => Apply(new MappingSettings());

    public void Apply(MappingSettings settings)
    {
        _applying = true;
        StopCapture();
        foreach (var row in Rows)
            row.Apply(settings.Buttons.TryGetValue(row.Button, out var action) ? action : null);
        _leftStick = settings.LeftStick;
        _rightStick = settings.RightStick;
        _stickSpeed = (int)Math.Clamp(Math.Round(settings.StickMouseSpeed / 30), 1, 100);
        _applying = false;
        OnPropertyChanged(string.Empty);
        _service.Settings = settings;
    }

    private void Push()
    {
        if (_applying)
            return;
        var buttons = new Dictionary<Buttons, ButtonAction>();
        foreach (var row in Rows)
            if (row.ToAction() is ButtonAction action)
                buttons[row.Button] = action;
        _service.Settings = new MappingSettings
        {
            Buttons = buttons,
            LeftStick = LeftStick,
            RightStick = RightStick,
            StickMouseSpeed = StickSpeed * 30,
        };
        OnPropertyChanged(nameof(Settings));
    }
}

/// <summary>Readable names for keys, e.g. "Ctrl+Shift+M" or "Space".</summary>
public static class KeyNames
{
    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    public static string Format(int virtualKey, KeyModifiers modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(KeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(KeyModifiers.Win)) parts.Add("Win");
        parts.Add(Name(virtualKey));
        return string.Join("+", parts);
    }

    private static string Name(int virtualKey)
    {
        var key = KeyInterop.KeyFromVirtualKey(virtualKey);
        string name = key.ToString();
        if (name.StartsWith("Oem", StringComparison.Ordinal) || name.StartsWith("Abnt", StringComparison.Ordinal))
        {
            // Punctuation keys: show the character printed on the key in the current layout.
            char c = (char)(MapVirtualKey((uint)virtualKey, 2) & 0x7FFF);
            if (c > ' ')
                return c.ToString().ToUpperInvariant();
        }
        return key switch
        {
            >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
            Key.Return => "Enter",
            Key.Escape => "Esc",
            Key.Back => "Backspace",
            Key.Capital => "Caps Lock",
            Key.Prior => "Page Up",
            Key.Next => "Page Down",
            Key.LeftCtrl or Key.RightCtrl => "Ctrl",
            Key.LeftShift or Key.RightShift => "Shift",
            Key.LeftAlt or Key.RightAlt => "Alt",
            Key.LWin or Key.RWin => "Win",
            _ => name,
        };
    }
}
