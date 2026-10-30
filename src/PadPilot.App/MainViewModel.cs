using System.Windows.Threading;
using HidSharp;
using PadPilot.Core;
using PadPilot.Core.Emulation;
using PadPilot.Core.Mapping;
using PadPilot.Core.Power;

namespace PadPilot.App;

/// <summary>Which button view is shown. New controller families get their own value and view.</summary>
public enum ButtonLayout
{
    Generic,
    PlayStation5,
}

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _uiTimer;
    private DualSenseDevice? _device;

    private long _rateCount;
    private DateTime _rateTime = DateTime.UtcNow;
    private byte[] _lastRaw = [];
    private volatile bool _outputDirty = true;
    private DateTime _lastHiddenRefresh;

    private readonly EmulationService _emulation = new();
    private readonly PadPilot.Core.Mouse.MouseService _mouse = new();
    private readonly MappingService _mapping = new();

    // Battery warning, battery lightbar, idle turn-off and the mic LED as mute indicator.
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly IdleTracker _idle = new();
    private BatteryWarning _batteryWarning = new();
    private TimeSpan _blinkUntil;
    private bool _blinking;
    private int _lastBatteryPercent = -1;
    private bool? _micMuted;

    // Mods: commands arrive on network/file threads and are laid over the Outputs tab.
    private readonly PadPilot.Core.Mods.ModOverrides _modOverrides = new();
    private readonly PadPilot.Core.Mods.UdpModServer _modUdp = new();
    private readonly PadPilot.Core.Mods.ModFileWatcher _modFile = new();
    private volatile string _modStatusJson = "{\"ok\":true,\"connected\":false}";
    // Rumble requested by a game through the virtual controller (written on a ViGEm thread).
    private volatile int _gameRumbleLarge;
    private volatile int _gameRumbleSmall;

    // HidHide calls start a short-lived process, so they run in the background, one after another.
    private readonly HidHideController _hidHide = new(AppSettings.Current.HidHideHiddenInstance, AppSettings.Current.HidHideCloakSetByUs);
    private Task _hidHideWork = Task.CompletedTask;
    private string _statusKey = "Status_Searching";

    private ControllerState _state = new();
    private string _status = "";
    private string _connection = "–";
    private string _battery = "–";
    private string _reportRate = "–";
    private string _rawHex = "";
    // PadPilot only supports DualSense controllers, so their drawing is shown even before one connects.
    private ButtonLayout _layout = ButtonLayout.PlayStation5;
    private string _controllerName = "";
    private bool _isBluetooth;
    private string _powerMessage = "";

    public ControllerState State { get => _state; private set => Set(ref _state, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string Connection { get => _connection; private set => Set(ref _connection, value); }
    public string Battery { get => _battery; private set => Set(ref _battery, value); }
    public string ReportRate { get => _reportRate; private set => Set(ref _reportRate, value); }
    public string RawHex { get => _rawHex; private set => Set(ref _rawHex, value); }
    public ButtonLayout Layout { get => _layout; private set => Set(ref _layout, value); }
    public string ControllerName { get => _controllerName; private set => Set(ref _controllerName, value); }

    /// <summary>Connected over Bluetooth: only then can PadPilot turn the controller off.</summary>
    public bool IsBluetooth { get => _isBluetooth; private set => Set(ref _isBluetooth, value); }
    public string PowerMessage { get => _powerMessage; private set => Set(ref _powerMessage, value); }

    /// <summary>Raised on the UI thread when the battery reaches the warning level (percent).</summary>
    public event Action<int>? LowBattery;

    public OutputViewModel Output { get; } = new();
    public UpdateViewModel Update { get; } = new(AppSettings.Current);
    public MappingViewModel Mapping { get; }
    public DeadzonesViewModel Deadzones { get; } = new(AppSettings.Current);
    public SettingsViewModel Settings { get; } = new(AppSettings.Current);
    public EmulationViewModel Emulation { get; }
    public MouseViewModel Mouse { get; }
    public ModsViewModel Mods { get; }
    public ProfilesViewModel Profiles { get; }

    /// <summary>False while the window is hidden in the tray; the live view then only refreshes twice a second.</summary>
    public bool IsUiVisible { get; set; } = true;

    public MainViewModel(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _uiTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Tick(), dispatcher);
        // Sliders fire many changes per second; Tick() sends at most one report per frame.
        Output.Changed += () => _outputDirty = true;
        Settings.Changed += () =>
        {
            if (_device != null)
                _device.MotionDisabled = AppSettings.Current.GyroDisabled;
            Mouse!.RefreshGyroSwitch();
        };
        Mouse = new MouseViewModel(_mouse, AppSettings.Current, dispatcher);
        Mapping = new MappingViewModel(_mapping);
        _emulation.Transform = _mapping.Transform;
        _mapping.Special += action => _dispatcher.BeginInvoke(() => OnSpecialAction(action));

        // The snapshot is refreshed once per second; whether a mod is in control is added live,
        // so the reply to a command already reflects that command.
        _modUdp.StatusJson = () => _modStatusJson[..^1] + $",\"modOverrides\":{(_modOverrides.IsActive ? "true" : "false")}}}";
        _modUdp.Received += (cmd, error) => OnModCommand("UDP", cmd, error);
        _modFile.Received += (cmd, error) => OnModCommand(Loc.Get("Mods_SourceFile"), cmd, error);
        Mods = new ModsViewModel(_modUdp, _modFile, _modOverrides, AppSettings.Current);
        Mods.OverridesCleared += () => _outputDirty = true;

        Loc.Instance.PropertyChanged += (_, _) => UpdateTexts();
        UpdateTexts();

        _emulation.Deadzones = Deadzones.Profile;
        _emulation.Mode = AppSettings.Current.EmulationMode;
        _emulation.StatusChanged += () => _dispatcher.BeginInvoke(() =>
        {
            Emulation!.UpdateStatus();
            UpdateHidHide();
        });
        _emulation.RumbleRequested += (large, small) =>
        {
            _gameRumbleLarge = large;
            _gameRumbleSmall = small;
            _outputDirty = true;
        };
        Emulation = new EmulationViewModel(_emulation, AppSettings.Current);
        Emulation.HideControllerChanged += UpdateHidHide;

        // A previous run may have crashed while the controller was hidden: give it back first.
        UpdateHidHide();

        // Must stay last: capturing and loading a profile touches all the view models above.
        Profiles = new ProfilesViewModel(CaptureProfile, ApplyProfile, AppSettings.Current);
    }

    /// <summary>
    /// Plain-text summary for bug reports: app, Windows, controller, drivers and the main settings.
    /// Leaves out anything personal (no serial number, Bluetooth address, user name or paths).
    /// </summary>
    public string BuildDiagnostics()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"PadPilot {UpdateViewModel.CurrentText}");
        sb.AppendLine($"Windows: {WindowsDescription()}");
        sb.AppendLine($".NET: {Environment.Version}");

        if (_device != null && _device.TryGetLatest(out var state, out _))
        {
            sb.AppendLine($"Controller: {_device.DisplayName} (PID 0x{_device.ProductId:X4}), " +
                          $"{(_device.Connection == ConnectionType.Usb ? "USB" : "Bluetooth")}, " +
                          $"battery {state!.BatteryPercent} % ({state.Charge}), {ReportRate}");
        }
        else
        {
            sb.AppendLine("Controller: not connected");
        }

        sb.AppendLine($"Drivers: {Emulation.ViGEm.Summary} | {Emulation.HidHideDriver.Summary}" +
                      $"{(Emulation.HidHideDriver.Installed && !HidHideController.IsInstalled ? " (HidHideCLI.exe not found)" : "")}");
        sb.AppendLine($"Emulation: {_emulation.Mode}, status {_emulation.Status}" +
                      $"{(_emulation.ErrorMessage is string err ? $" ({err})" : "")}");
        sb.AppendLine($"Hide controller with HidHide: {(Emulation.HideController ? "on" : "off")}, currently hidden: {(_hidHide.HiddenInstance != null ? "yes" : "no")}");
        sb.AppendLine($"Mouse: touchpad {(Mouse.TouchpadEnabled ? "on" : "off")}, gyro {(Mouse.GyroEnabled ? $"on ({Mouse.Activation})" : "off")}, " +
                      $"gyro data {(AppSettings.Current.GyroDisabled ? "disabled" : "enabled")}");
        sb.AppendLine($"Mods: UDP {(AppSettings.Current.ModUdpEnabled ? $"on (port {AppSettings.Current.ModUdpPort}, {(Mods.UdpOk ? "listening" : "not listening")})" : "off")}, " +
                      $"text file {(AppSettings.Current.ModFileEnabled ? "on" : "off")}, mod overrides active: {(_modOverrides.IsActive ? "yes" : "no")}");
        var map = _mapping.Settings;
        sb.AppendLine($"Mapping: {map.Buttons.Count} button(s) changed" +
                      $"{(map.Buttons.Count > 0 ? $" ({string.Join(", ", map.Buttons.Select(b => $"{b.Key}→{b.Value.Kind}"))})" : "")}, " +
                      $"left stick {map.LeftStick}, right stick {map.RightStick}");
        sb.AppendLine($"Battery warning: {(AppSettings.Current.LowBatteryWarning ? $"at {AppSettings.Current.LowBatteryLevel} %" : "off")}, " +
                      $"idle turn-off: {AppSettings.Current.IdleTurnOff}, update check: {(AppSettings.Current.CheckForUpdates ? "on" : "off")}");
        sb.Append($"Language: {Loc.Instance.Language}");
        return sb.ToString();
    }

    private static string WindowsDescription()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            string product = key?.GetValue("ProductName") as string ?? "Windows";
            string build = key?.GetValue("CurrentBuild") as string ?? "?";
            // Windows 11 still reports "Windows 10" as product name; the build number tells them apart.
            if (int.TryParse(build, out int b) && b >= 22000)
                product = product.Replace("Windows 10", "Windows 11");
            string release = key?.GetValue("DisplayVersion") as string ?? "";
            string ubr = key?.GetValue("UBR")?.ToString() ?? "0";
            return $"{product} {release} (build {build}.{ubr}), {(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")}".Replace("  ", " ");
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return Environment.OSVersion.VersionString;
        }
    }

    private Profile CaptureProfile() => new()
    {
        Outputs = Output.ToProfile(),
        LeftStick = Deadzones.LeftStick.Settings,
        RightStick = Deadzones.RightStick.Settings,
        L2 = Deadzones.L2.Settings,
        R2 = Deadzones.R2.Settings,
        EmulationMode = Emulation.Mode,
        HideControllerWithHidHide = Emulation.HideController,
        RumblePassthrough = Emulation.RumblePassthrough,
        TouchpadMouse = Mouse.TouchpadSettings,
        GyroMouse = Mouse.GyroSettings with { Bias = default }, // calibration belongs to the controller, not the profile
        Mapping = Mapping.Settings,
    };

    private void ApplyProfile(Profile p)
    {
        Output.Apply(p.Outputs);
        Deadzones.LeftStick.Apply(p.LeftStick);
        Deadzones.RightStick.Apply(p.RightStick);
        Deadzones.L2.Apply(p.L2);
        Deadzones.R2.Apply(p.R2);
        Emulation.Mode = p.EmulationMode;
        Emulation.HideController = p.HideControllerWithHidHide;
        Emulation.RumblePassthrough = p.RumblePassthrough;
        Mouse.Apply(p.TouchpadMouse, p.GyroMouse);
        Mapping.Apply(p.Mapping);
        _outputDirty = true;
    }

    /// <summary>A controller button mapped to a special action was pressed.</summary>
    private void OnSpecialAction(SpecialAction action)
    {
        switch (action)
        {
            case SpecialAction.ToggleMicMute:
                _micMuted = Microphone.Toggle();
                _outputDirty = true;
                break;
            case SpecialAction.NextProfile:
                int next = (Profiles.Names.IndexOf(Profiles.Active) + 1) % Profiles.Names.Count;
                Profiles.Switch(Profiles.Names[next]);
                break;
            case SpecialAction.TurnOffController:
                TurnOffController();
                break;
        }
    }

    /// <summary>Turns a Bluetooth controller off; over USB this isn't possible.</summary>
    public void TurnOffController()
    {
        if (_device == null)
            return;
        PowerMessage = _device.Connection != ConnectionType.Bluetooth ? Loc.Get("Power_UsbOnly")
            : _device.TurnOff() ? Loc.Get("Power_TurnedOff")
            : Loc.Get("Power_Failed");
    }

    /// <summary>Called on a network or file thread for every mod command.</summary>
    private void OnModCommand(string source, PadPilot.Core.Mods.ModCommand? cmd, string? error)
    {
        if (cmd != null && !cmd.IsStatusRequest)
        {
            _modOverrides.Apply(cmd);
            _outputDirty = true;
        }
        _dispatcher.BeginInvoke(() => Mods.Report(source, cmd, error));
    }

    /// <summary>The reply mods get to every command, refreshed once per second.</summary>
    private void UpdateModStatus()
    {
        bool connected = _device != null && _device.TryGetLatest(out _, out _);
        var status = new Dictionary<string, object?>
        {
            ["ok"] = true,
            ["connected"] = connected,
            ["controller"] = connected ? _device!.DisplayName : null,
            ["connection"] = connected ? (_device!.Connection == ConnectionType.Usb ? "usb" : "bluetooth") : null,
            ["battery"] = connected ? State.BatteryPercent : null,
            ["charging"] = connected ? State.Charge is ChargeStatus.Charging or ChargeStatus.Full : null,
            ["emulation"] = _emulation.Status == EmulationStatus.Active ? _emulation.Mode.ToString().ToLowerInvariant() : "off",
        };
        _modStatusJson = System.Text.Json.JsonSerializer.Serialize(status);
    }

    /// <summary>
    /// Hides the real DualSense while a virtual controller is active (and the user wants that),
    /// otherwise makes sure it is visible again.
    /// </summary>
    private void UpdateHidHide()
    {
        if (!HidHideController.IsInstalled)
            return;

        bool shouldHide = AppSettings.Current.HideControllerWithHidHide
            && _emulation.Status == EmulationStatus.Active
            && _device != null;
        string? devicePath = _device?.DevicePath;
        string appPath = Environment.ProcessPath ?? "";

        _hidHideWork = _hidHideWork.ContinueWith(_ =>
        {
            bool ok = shouldHide && devicePath != null
                ? _hidHide.HiddenInstance != null || _hidHide.Hide(devicePath, appPath)
                : (_hidHide.HiddenInstance == null && !_hidHide.CloakSetByUs) || _hidHide.Release();
            bool hidden = _hidHide.HiddenInstance != null;
            string? error = ok ? null : _hidHide.LastError;
            _dispatcher.BeginInvoke(() =>
            {
                SaveHidHideState();
                Emulation.SetHidHideState(hidden, error);
            });
        }, TaskScheduler.Default);
    }

    private void SaveHidHideState()
    {
        if (AppSettings.Current.HidHideHiddenInstance == _hidHide.HiddenInstance &&
            AppSettings.Current.HidHideCloakSetByUs == _hidHide.CloakSetByUs)
            return;
        AppSettings.Current.HidHideHiddenInstance = _hidHide.HiddenInstance;
        AppSettings.Current.HidHideCloakSetByUs = _hidHide.CloakSetByUs;
        AppSettings.Current.Save();
    }

    public void Start()
    {
        DeviceList.Local.Changed += OnDeviceListChanged;
        TryConnect();
        _uiTimer.Start();

        // Ask GitHub a little later, so the start isn't slowed down.
        _dispatcher.InvokeAsync(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            await Update.CheckAtStartupAsync();
        });
    }

    private void OnDeviceListChanged(object? sender, DeviceListChangedEventArgs e) =>
        _dispatcher.BeginInvoke(() =>
        {
            // Bluetooth disconnects don't always surface as a read error, so also check the device list.
            if (_device != null && DualSenseDevice.FindAll().All(d => d.DevicePath != _device.DevicePath))
                CloseDevice();
            TryConnect();
        });

    private void TryConnect()
    {
        if (_device != null)
            return;

        _device = DualSenseDevice.OpenFirst();
        if (_device == null)
        {
            _statusKey = "Status_NotFound";
            Connection = Battery = ReportRate = "–";
            RawHex = "";
            ControllerName = "";
            State = new ControllerState();
            UpdateTexts();
            return;
        }

        _device.Disconnected += d => _dispatcher.BeginInvoke(() =>
        {
            if (_device == d)
            {
                CloseDevice();
                TryConnect();
            }
        });

        _device.MotionDisabled = AppSettings.Current.GyroDisabled;
        _rateCount = 0;
        _rateTime = DateTime.UtcNow;
        _outputDirty = true;
        _statusKey = "Status_Connected";
        ControllerName = _device.DisplayName;
        Layout = ButtonLayout.PlayStation5; // DualSense and DualSense Edge share the PS5 layout
        _emulation.Attach(_device);
        _mouse.Attach(_device);
        _mapping.Attach(_device);
        _idle.Reset(_clock.Elapsed);
        _batteryWarning = new BatteryWarning();
        _lastBatteryPercent = -1;
        IsBluetooth = _device.Connection == ConnectionType.Bluetooth;
        PowerMessage = "";
        Connection = _device.Connection == ConnectionType.Usb ? "USB" : "Bluetooth";
        UpdateTexts();
    }

    private void CloseDevice()
    {
        // Detach first: the device's read thread must not be inside the emulation while it shuts down.
        _emulation.Detach();
        _mouse.Detach();
        _mapping.Detach();
        _device?.Dispose();
        _device = null;
        IsBluetooth = false;
        UpdateModStatus();
    }

    /// <summary>Rebuilds texts that are composed in code; called on connect changes and language switches.</summary>
    private void UpdateTexts()
    {
        Status = _statusKey == "Status_Connected" ? Loc.Format(_statusKey, ControllerName) : Loc.Get(_statusKey);
        if (_device != null && _device.TryGetLatest(out var state, out _))
            Battery = FormatBattery(state!);
    }

    private static string FormatBattery(ControllerState state) => state.Charge switch
    {
        ChargeStatus.Charging => Loc.Format("Battery_Charging", state.BatteryPercent),
        ChargeStatus.Full => Loc.Get("Battery_Full"),
        ChargeStatus.Unknown => Loc.Get("Battery_Unknown"),
        _ => $"{state.BatteryPercent} %",
    };

    private void Tick()
    {
        if (_device == null)
            return;

        _emulation.Deadzones = Deadzones.Profile;
        Emulation.GameRumbleLarge = _gameRumbleLarge / 255.0 * 100;
        Emulation.GameRumbleSmall = _gameRumbleSmall / 255.0 * 100;

        var clock = _clock.Elapsed;
        if (clock < _blinkUntil || _blinking)
        {
            _outputDirty = true; // low-battery blink runs frame by frame, plus one frame to restore the colour
            _blinking = clock < _blinkUntil;
        }
        if (_outputDirty)
        {
            _outputDirty = false;
            if (!_device.Send(BuildOutput()))
                _outputDirty = true; // retry next frame
        }

        bool hasState = _device.TryGetLatest(out var state, out var raw);
        if (hasState)
            _idle.Update(state!, clock);

        bool refreshView = IsUiVisible || DateTime.UtcNow - _lastHiddenRefresh >= TimeSpan.FromMilliseconds(500);
        if (refreshView && hasState && !ReferenceEquals(raw, _lastRaw))
        {
            _lastHiddenRefresh = DateTime.UtcNow;
            _lastRaw = raw;
            State = state!;
            Deadzones.Update(state!);
            Battery = FormatBattery(state!);
            RawHex = Convert.ToHexString(raw.AsSpan(0, Math.Min(raw.Length, 48)));
        }

        var now = DateTime.UtcNow;
        if (now - _rateTime >= TimeSpan.FromSeconds(1))
        {
            long count = _device.ReportCount;
            ReportRate = Loc.Format("Report_Rate", ((count - _rateCount) / (now - _rateTime).TotalSeconds).ToString("0"));
            UpdateModStatus();
            _rateCount = count;
            _rateTime = now;
            if (hasState)
                EverySecond(state!, clock);
        }
    }

    private void EverySecond(ControllerState state, TimeSpan clock)
    {
        var settings = AppSettings.Current;

        if (state.BatteryPercent != _lastBatteryPercent)
        {
            _lastBatteryPercent = state.BatteryPercent;
            if (Output.LightbarBattery)
                _outputDirty = true;
        }
        if (settings.LowBatteryWarning && _batteryWarning.Update(state.BatteryPercent, state.Charge, settings.LowBatteryLevel))
        {
            if (settings.LowBatteryBlink)
                _blinkUntil = clock + TimeSpan.FromSeconds(3);
            LowBattery?.Invoke(state.BatteryPercent);
        }

        // The mic LED shows whether the Windows microphone is muted, if a button is set up to mute it.
        bool? muted = _mapping.Settings.Uses(SpecialAction.ToggleMicMute) ? Microphone.IsMuted() : null;
        if (muted != _micMuted)
        {
            _micMuted = muted;
            _outputDirty = true;
        }

        if (settings.IdleTurnOff != IdleTimeout.Never && _device!.Connection == ConnectionType.Bluetooth
            && _idle.IdleTime(clock) >= TimeSpan.FromMinutes((int)settings.IdleTurnOff))
        {
            _idle.Reset(clock); // don't try again every second if Windows refuses
            TurnOffController();
        }
    }

    /// <summary>
    /// Settings from the Outputs tab, with mod commands laid on top; game rumble is added
    /// on top of that (the stronger value wins).
    /// </summary>
    private OutputState BuildOutput()
    {
        var output = Output.ToState();
        if (Output.LightbarBattery && _lastBatteryPercent >= 0)
        {
            var (r, g, b) = BatteryColor.For(_lastBatteryPercent, Output.Brightness / 100.0);
            output = output with { LightbarRed = r, LightbarGreen = g, LightbarBlue = b };
        }
        if (_micMuted is bool muted)
            output = output with { MicLed = muted ? MicLed.On : MicLed.Off };
        output = _modOverrides.ApplyTo(output);

        // Low battery: blink red for a moment, over everything else.
        var clock = _clock.Elapsed;
        if (clock < _blinkUntil)
        {
            bool on = (int)((_blinkUntil - clock).TotalMilliseconds / 250) % 2 == 0;
            output = output with { LightbarRed = on ? (byte)255 : (byte)0, LightbarGreen = 0, LightbarBlue = 0 };
        }

        if (!AppSettings.Current.RumblePassthrough)
            return output;
        return output with
        {
            RumbleLeft = (byte)Math.Max(output.RumbleLeft, _gameRumbleLarge),
            RumbleRight = (byte)Math.Max(output.RumbleRight, _gameRumbleSmall),
        };
    }

    public void Dispose()
    {
        // Save the profile first, while emulation and everything else still show the user's settings
        // (shutting the emulation down switches its mode to Off).
        Profiles.SaveActive();
        _uiTimer.Stop();
        DeviceList.Local.Changed -= OnDeviceListChanged;
        CloseDevice();
        _emulation.Dispose();

        // Never leave the controller hidden after PadPilot is gone.
        _hidHideWork.Wait(TimeSpan.FromSeconds(10));
        if (_hidHide.HiddenInstance != null || _hidHide.CloakSetByUs)
            _hidHide.Release();
        AppSettings.Current.HidHideHiddenInstance = _hidHide.HiddenInstance;
        AppSettings.Current.HidHideCloakSetByUs = _hidHide.CloakSetByUs;
        _mouse.Dispose();
        _mapping.Dispose();
        _modUdp.Dispose();
        _modFile.Dispose();
        Deadzones.SaveTo(AppSettings.Current);
        Mouse.SaveTo(AppSettings.Current);
        AppSettings.Current.Save();
    }
}
