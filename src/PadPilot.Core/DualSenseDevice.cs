using HidSharp;

namespace PadPilot.Core;

/// <summary>
/// An open DualSense controller. Reads input reports on a background thread and keeps
/// the latest state, which the UI picks up at its own pace via <see cref="TryGetLatest"/>.
/// </summary>
public sealed class DualSenseDevice : IDisposable
{
    public const int SonyVendorId = 0x054C;
    public const int DualSenseProductId = 0x0CE6;
    public const int DualSenseEdgeProductId = 0x0DF2;

    private const byte CalibrationFeatureReportId = 0x05;

    private readonly HidDevice _device;
    private readonly HidStream _stream;
    private readonly Thread _readThread;
    private readonly object _lock = new();
    private volatile bool _running = true;

    private ControllerState? _latest;
    private byte[] _latestRaw = [];
    private long _reportCount;

    private readonly object _writeLock = new();
    private OutputState? _lastOutput;
    private byte _outputSequence;
    private bool _startupLightReleased;

    /// <summary>
    /// When set, gyro and accelerometer values are zeroed right after parsing, so nothing in the app
    /// (display, emulation, gyro mouse) receives motion data.
    /// </summary>
    public bool MotionDisabled { get => _motionDisabled; set => _motionDisabled = value; }
    private volatile bool _motionDisabled;

    public ConnectionType Connection { get; }
    public string DisplayName { get; }
    public string DevicePath => _device.DevicePath;
    public int ProductId => _device.ProductID;
    public long ReportCount => Interlocked.Read(ref _reportCount);

    /// <summary>Raised on the read thread for every input report, for low-latency consumers like emulation.</summary>
    public event Action<ControllerState>? StateReceived;

    /// <summary>Raised on the read thread when the controller is unplugged or the connection drops.</summary>
    public event Action<DualSenseDevice>? Disconnected;

    private DualSenseDevice(HidDevice device, HidStream stream)
    {
        _device = device;
        _stream = stream;
        Connection = device.GetMaxInputReportLength() == InputReportParser.UsbReportLength
            ? ConnectionType.Usb
            : ConnectionType.Bluetooth;
        DisplayName = device.ProductID == DualSenseEdgeProductId ? "DualSense Edge" : "DualSense";

        if (Connection == ConnectionType.Bluetooth)
            EnableBluetoothFullReports();

        _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "DualSense input" };
        _readThread.Start();
    }

    public static IEnumerable<HidDevice> FindAll() =>
        DeviceList.Local.GetHidDevices(SonyVendorId)
            .Where(d => d.ProductID is DualSenseProductId or DualSenseEdgeProductId);

    /// <summary>Opens the first DualSense that can be opened, or returns null.</summary>
    public static DualSenseDevice? OpenFirst()
    {
        foreach (var device in FindAll())
        {
            if (device.TryOpen(out HidStream stream))
                return new DualSenseDevice(device, stream);
        }
        return null;
    }

    public bool TryGetLatest(out ControllerState? state, out byte[] raw)
    {
        lock (_lock)
        {
            state = _latest;
            raw = _latestRaw;
        }
        return state != null;
    }

    /// <summary>Sends lightbar, LEDs, rumble and trigger effects. Returns false if the write failed.</summary>
    public bool Send(OutputState state)
    {
        lock (_writeLock)
        {
            var report = OutputReportBuilder.Build(
                state, Connection, _outputSequence, releaseStartupLight: !_startupLightReleased,
                reportLength: _device.GetMaxOutputReportLength());
            try
            {
                _stream.Write(report);
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or ObjectDisposedException)
            {
                return false;
            }

            _outputSequence = (byte)((_outputSequence + 1) & 0x0F);
            _startupLightReleased = true;
            _lastOutput = state;
            return true;
        }
    }

    /// <summary>
    /// Over Bluetooth the controller only sends a short report (0x01) until the calibration
    /// feature report is read; after that it switches to the full report 0x31.
    /// </summary>
    private void EnableBluetoothFullReports()
    {
        try
        {
            var buffer = new byte[_device.GetMaxFeatureReportLength()];
            buffer[0] = CalibrationFeatureReportId;
            _stream.GetFeature(buffer);
        }
        catch (IOException)
        {
            // Not fatal: we will simply not see full reports. Shows up as "0 reports" in the UI.
        }
    }

    private void ReadLoop()
    {
        var buffer = new byte[_device.GetMaxInputReportLength()];
        _stream.ReadTimeout = 1000;

        while (_running)
        {
            int count;
            try
            {
                count = _stream.Read(buffer, 0, buffer.Length);
            }
            catch (TimeoutException)
            {
                continue;
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                break;
            }

            var report = buffer.AsSpan(0, count);
            if (InputReportParser.TryParse(report, Connection, out var state))
            {
                if (_motionDisabled)
                    state = state! with { Gyro = default, Accel = default };
                var raw = report.ToArray();
                lock (_lock)
                {
                    _latest = state;
                    _latestRaw = raw;
                }
                Interlocked.Increment(ref _reportCount);
                StateReceived?.Invoke(state!);
            }
        }

        if (_running)
            Disconnected?.Invoke(this);
    }

    public void Dispose()
    {
        // Don't leave the triggers stiff or the motors running after we let go of the controller.
        if (_running && _lastOutput != null)
            Send(_lastOutput.Released());

        _running = false;
        _stream.Dispose();
        if (Thread.CurrentThread != _readThread)
            _readThread.Join(TimeSpan.FromSeconds(2));
    }
}
