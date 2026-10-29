using System.Windows.Threading;
using HidSharp;
using PadPilot.Core;
using PadPilot.Core.Emulation;

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
    private ButtonLayout _layout = ButtonLayout.Generic;
    private string _controllerName = "";

    public ControllerState State { get => _state; private set => Set(ref _state, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string Connection { get => _connection; private set => Set(ref _connection, value); }
    public string Battery { get => _battery; private set => Set(ref _battery, value); }
    public string ReportRate { get => _reportRate; private set => Set(ref _reportRate, value); }
    public string RawHex { get => _rawHex; private set => Set(ref _rawHex, value); }
    public ButtonLayout Layout { get => _layout; private set => Set(ref _layout, value); }
    public string ControllerName { get => _controllerName; private set => Set(ref _controllerName, value); }

    public OutputViewModel Output { get; } = new();
    public DeadzonesViewModel Deadzones { get; } = new(AppSettings.Current);
    public SettingsViewModel Settings { get; } = new(AppSettings.Current);
    public EmulationViewModel Emulation { get; }

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
        };
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
        Connection = _device.Connection == ConnectionType.Usb ? "USB" : "Bluetooth";
        UpdateTexts();
    }

    private void CloseDevice()
    {
        // Detach first: the device's read thread must not be inside the emulation while it shuts down.
        _emulation.Detach();
        _device?.Dispose();
        _device = null;
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

        if (_outputDirty)
        {
            _outputDirty = false;
            if (!_device.Send(BuildOutput()))
                _outputDirty = true; // retry next frame
        }

        bool refreshView = IsUiVisible || DateTime.UtcNow - _lastHiddenRefresh >= TimeSpan.FromMilliseconds(500);
        if (refreshView && _device.TryGetLatest(out var state, out var raw) && !ReferenceEquals(raw, _lastRaw))
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
            _rateCount = count;
            _rateTime = now;
        }
    }

    /// <summary>Settings from the Outputs tab; game rumble is added on top (the stronger value wins).</summary>
    private OutputState BuildOutput()
    {
        var output = Output.ToState();
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
        Deadzones.SaveTo(AppSettings.Current);
        AppSettings.Current.Save();
    }
}
