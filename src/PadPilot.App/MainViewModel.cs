using System.Windows.Threading;
using HidSharp;
using PadPilot.Core;

namespace PadPilot.App;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _uiTimer;
    private DualSenseDevice? _device;

    private long _rateCount;
    private DateTime _rateTime = DateTime.UtcNow;
    private byte[] _lastRaw = [];
    private bool _outputDirty = true;

    private ControllerState _state = new();
    private string _status = "Suche Controller …";
    private string _connection = "–";
    private string _battery = "–";
    private string _reportRate = "–";
    private string _rawHex = "";

    public ControllerState State { get => _state; private set => Set(ref _state, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string Connection { get => _connection; private set => Set(ref _connection, value); }
    public string Battery { get => _battery; private set => Set(ref _battery, value); }
    public string ReportRate { get => _reportRate; private set => Set(ref _reportRate, value); }
    public string RawHex { get => _rawHex; private set => Set(ref _rawHex, value); }

    public OutputViewModel Output { get; } = new();

    public MainViewModel(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _uiTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Tick(), dispatcher);
        // Sliders fire many changes per second; Tick() sends at most one report per frame.
        Output.Changed += () => _outputDirty = true;
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
            Status = "Kein DualSense gefunden – per USB-Kabel oder Bluetooth verbinden.";
            Connection = Battery = ReportRate = "–";
            RawHex = "";
            State = new ControllerState();
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

        _rateCount = 0;
        _rateTime = DateTime.UtcNow;
        _outputDirty = true;
        Status = $"{_device.DisplayName} verbunden";
        Connection = _device.Connection == ConnectionType.Usb ? "USB" : "Bluetooth";
    }

    private void CloseDevice()
    {
        _device?.Dispose();
        _device = null;
    }

    private void Tick()
    {
        if (_device == null)
            return;

        if (_outputDirty)
        {
            _outputDirty = false;
            if (!_device.Send(Output.ToState()))
                _outputDirty = true; // retry next frame
        }

        if (_device.TryGetLatest(out var state, out var raw) && !ReferenceEquals(raw, _lastRaw))
        {
            _lastRaw = raw;
            State = state!;
            Battery = state!.Charge switch
            {
                ChargeStatus.Charging => $"{state.BatteryPercent} % (lädt)",
                ChargeStatus.Full => "100 % (voll)",
                ChargeStatus.Unknown => "unbekannt",
                _ => $"{state.BatteryPercent} %",
            };
            RawHex = Convert.ToHexString(raw.AsSpan(0, Math.Min(raw.Length, 48)));
        }

        var now = DateTime.UtcNow;
        if (now - _rateTime >= TimeSpan.FromSeconds(1))
        {
            long count = _device.ReportCount;
            ReportRate = $"{(count - _rateCount) / (now - _rateTime).TotalSeconds:0} Reports/s";
            _rateCount = count;
            _rateTime = now;
        }
    }

    public void Dispose()
    {
        _uiTimer.Stop();
        DeviceList.Local.Changed -= OnDeviceListChanged;
        CloseDevice();
    }
}
