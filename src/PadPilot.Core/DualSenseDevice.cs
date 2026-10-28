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

    public ConnectionType Connection { get; }
    public string DisplayName { get; }
    public string DevicePath => _device.DevicePath;
    public long ReportCount => Interlocked.Read(ref _reportCount);

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
                var raw = report.ToArray();
                lock (_lock)
                {
                    _latest = state;
                    _latestRaw = raw;
                }
                Interlocked.Increment(ref _reportCount);
            }
        }

        if (_running)
            Disconnected?.Invoke(this);
    }

    public void Dispose()
    {
        _running = false;
        _stream.Dispose();
        if (Thread.CurrentThread != _readThread)
            _readThread.Join(TimeSpan.FromSeconds(2));
    }
}
