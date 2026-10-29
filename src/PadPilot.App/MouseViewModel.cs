using System.Windows.Threading;
using PadPilot.Core;
using PadPilot.Core.Mouse;

namespace PadPilot.App;

/// <summary>
/// The "Mouse" tab. Sliders use whole numbers (1–100); they are converted to the service's units here.
/// Changes take effect at once; they are written to the settings file when the app closes.
/// </summary>
public sealed class MouseViewModel : ObservableObject
{
    private readonly MouseService _service;
    private readonly Dispatcher _dispatcher;
    private TouchpadMouseSettings _touch;
    private GyroMouseSettings _gyro;
    private bool _isCalibrating;
    private string _calibrationText = "";

    public MouseViewModel(MouseService service, AppSettings settings, Dispatcher dispatcher)
    {
        _service = service;
        _dispatcher = dispatcher;
        _touch = settings.TouchpadMouse;
        _gyro = settings.GyroMouse;
        _service.Touchpad = _touch;
        _service.Gyro = _gyro;
        Loc.Instance.PropertyChanged += (_, _) => UpdateCalibrationText();
        UpdateCalibrationText();
    }

    public static IReadOnlyList<GyroActivation> Activations { get; } = Enum.GetValues<GyroActivation>();

    // Touchpad
    public bool TouchpadEnabled { get => _touch.Enabled; set => SetTouch(_touch with { Enabled = value }); }
    public int TouchpadSensitivity { get => (int)Math.Round(_touch.Sensitivity * 50); set => SetTouch(_touch with { Sensitivity = value / 50.0 }); }
    public int TouchpadAcceleration { get => (int)Math.Round(_touch.Acceleration * 100); set => SetTouch(_touch with { Acceleration = value / 100.0 }); }
    public bool TapToClick { get => _touch.TapToClick; set => SetTouch(_touch with { TapToClick = value }); }
    public bool RightClickOnRightHalf { get => _touch.RightClickOnRightHalf; set => SetTouch(_touch with { RightClickOnRightHalf = value }); }
    public bool TwoFingerScroll { get => _touch.TwoFingerScroll; set => SetTouch(_touch with { TwoFingerScroll = value }); }
    public int ScrollSpeed { get => (int)Math.Round(_touch.ScrollSpeed * 25); set => SetTouch(_touch with { ScrollSpeed = value / 25.0 }); }

    // Gyro
    public bool GyroEnabled { get => _gyro.Enabled; set => SetGyro(_gyro with { Enabled = value }); }
    public GyroActivation Activation { get => _gyro.Activation; set => SetGyro(_gyro with { Activation = value }); }
    public int GyroSensitivity { get => (int)Math.Round(_gyro.PixelsPerDegree * 5); set => SetGyro(_gyro with { PixelsPerDegree = value / 5.0 }); }
    public bool InvertX { get => _gyro.InvertX; set => SetGyro(_gyro with { InvertX = value }); }
    public bool InvertY { get => _gyro.InvertY; set => SetGyro(_gyro with { InvertY = value }); }

    public bool IsCalibrating { get => _isCalibrating; private set => Set(ref _isCalibrating, value); }
    public string CalibrationText { get => _calibrationText; private set => Set(ref _calibrationText, value); }

    /// <summary>Gyro switched off in Settings: the gyro mouse then gets no data.</summary>
    public bool GyroSwitchedOff => AppSettings.Current.GyroDisabled;

    public void RefreshGyroSwitch() => OnPropertyChanged(nameof(GyroSwitchedOff));

    public void Calibrate()
    {
        IsCalibrating = true;
        UpdateCalibrationText();
        _service.StartCalibration(TimeSpan.FromSeconds(2), bias => _dispatcher.BeginInvoke(() =>
        {
            IsCalibrating = false;
            if (bias is Vector3S b)
                SetGyro(_gyro with { Bias = b });
            UpdateCalibrationText(failed: bias == null);
        }));
    }

    public TouchpadMouseSettings TouchpadSettings => _touch;
    public GyroMouseSettings GyroSettings => _gyro;

    /// <summary>Takes over a profile's mouse settings; the gyro calibration stays (it belongs to the controller).</summary>
    public void Apply(TouchpadMouseSettings touch, GyroMouseSettings gyro)
    {
        _touch = touch;
        _gyro = gyro with { Bias = _gyro.Bias };
        _service.Touchpad = _touch;
        _service.Gyro = _gyro;
        OnPropertyChanged(string.Empty); // refresh every binding
        UpdateCalibrationText();
    }

    public void SaveTo(AppSettings settings)
    {
        settings.TouchpadMouse = _touch;
        settings.GyroMouse = _gyro;
    }

    private void SetTouch(TouchpadMouseSettings value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (value == _touch)
            return;
        _touch = value;
        _service.Touchpad = value;
        OnPropertyChanged(name);
    }

    private void SetGyro(GyroMouseSettings value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (value == _gyro)
            return;
        _gyro = value;
        _service.Gyro = value;
        OnPropertyChanged(name);
    }

    private void UpdateCalibrationText(bool failed = false)
    {
        var b = _gyro.Bias;
        CalibrationText = IsCalibrating ? Loc.Get("Gyro_Calibrating")
            : failed ? Loc.Get("Gyro_CalibrationFailed")
            : Loc.Format("Gyro_Calibrated", b.X, b.Y, b.Z);
    }
}
