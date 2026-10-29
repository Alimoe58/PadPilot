using System.Diagnostics;

namespace PadPilot.Core.Mouse;

/// <summary>
/// Runs touchpad mouse and gyro mouse on the controller's read thread (no UI delay), and gyro calibration.
/// </summary>
public sealed class MouseService : IDisposable
{
    private readonly object _lock = new();
    private readonly TouchpadMouse _touchpad;
    private readonly GyroMouse _gyro;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _lastSample;
    private DualSenseDevice? _device;

    private volatile TouchpadMouseSettings _touchpadSettings = new();
    private volatile GyroMouseSettings _gyroSettings = new();

    private GyroCalibration? _calibration;
    private TimeSpan _calibrationEnd;
    private Action<Vector3S?>? _calibrationDone;

    public MouseService(IMouseOutput? output = null)
    {
        output ??= new WindowsMouse();
        _touchpad = new TouchpadMouse(output);
        _gyro = new GyroMouse(output);
    }

    public TouchpadMouseSettings Touchpad { get => _touchpadSettings; set => _touchpadSettings = value; }
    public GyroMouseSettings Gyro { get => _gyroSettings; set => _gyroSettings = value; }

    public void Attach(DualSenseDevice device)
    {
        lock (_lock)
        {
            _device = device;
            device.StateReceived += OnStateReceived;
        }
    }

    /// <summary>Stops listening and lets go of any held mouse button.</summary>
    public void Detach()
    {
        lock (_lock)
        {
            if (_device != null)
                _device.StateReceived -= OnStateReceived;
            _device = null;
            _touchpad.Reset();
            FinishCalibration(null);
        }
    }

    /// <summary>
    /// Measures the gyro's resting value. The controller should lie still for <paramref name="duration"/>.
    /// <paramref name="done"/> gets the bias (or null without controller data) on the read thread.
    /// </summary>
    public void StartCalibration(TimeSpan duration, Action<Vector3S?> done)
    {
        lock (_lock)
        {
            FinishCalibration(null);
            _calibration = new GyroCalibration();
            _calibrationEnd = _clock.Elapsed + duration;
            _calibrationDone = done;
        }
    }

    private void OnStateReceived(ControllerState state)
    {
        lock (_lock)
        {
            var now = _clock.Elapsed;
            double seconds = Math.Clamp((now - _lastSample).TotalSeconds, 0, 0.05);
            _lastSample = now;

            if (_calibration != null)
            {
                _calibration.Add(state.Gyro);
                if (now >= _calibrationEnd)
                    FinishCalibration(_calibration.Result);
            }

            _touchpad.Process(state, _touchpadSettings, now);
            if (_calibration == null)
                _gyro.Process(state, _gyroSettings, seconds);
        }
    }

    // Call with _lock held.
    private void FinishCalibration(Vector3S? result)
    {
        var done = _calibrationDone;
        bool hadSamples = _calibration?.SampleCount > 0;
        _calibration = null;
        _calibrationDone = null;
        done?.Invoke(hadSamples ? result : null);
    }

    public void Dispose() => Detach();
}
