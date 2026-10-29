using PadPilot.Core.Emulation;

namespace PadPilot.App;

/// <summary>The "Emulation" tab: virtual controller mode, status, game rumble and hiding the real controller (HidHide).</summary>
public sealed class EmulationViewModel : ObservableObject
{
    private readonly EmulationService _service;
    private readonly AppSettings _settings;
    private string _statusText = "";
    private EmulationStatus _status;
    private double _gameRumbleLarge;
    private double _gameRumbleSmall;

    public EmulationViewModel(EmulationService service, AppSettings settings)
    {
        _service = service;
        _settings = settings;
        CheckDrivers();
        Loc.Instance.PropertyChanged += (_, _) =>
        {
            UpdateStatus();
            UpdateHidHideText();
            OnPropertyChanged(nameof(ViGEmText));
            OnPropertyChanged(nameof(HidHideDriverText));
        };
        UpdateStatus();
        UpdateHidHideText();
    }

    public static IReadOnlyList<EmulationMode> Modes { get; } = Enum.GetValues<EmulationMode>();

    public EmulationMode Mode
    {
        get => _service.Mode;
        set
        {
            if (value == _service.Mode && _status != EmulationStatus.Error)
                return;
            _service.Mode = value;
            _settings.EmulationMode = value;
            _settings.Save();
            OnPropertyChanged(nameof(Mode));
            UpdateStatus();
        }
    }

    public bool RumblePassthrough
    {
        get => _settings.RumblePassthrough;
        set
        {
            _settings.RumblePassthrough = value;
            _settings.Save();
            OnPropertyChanged(nameof(RumblePassthrough));
        }
    }

    public EmulationStatus Status { get => _status; private set => Set(ref _status, value); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public bool IsActive => Status == EmulationStatus.Active;
    public bool HasProblem => Status is EmulationStatus.DriverMissing or EmulationStatus.Error;

    /// <summary>Last rumble requested by a game, in percent (shown as bars).</summary>
    public double GameRumbleLarge { get => _gameRumbleLarge; set => Set(ref _gameRumbleLarge, value); }
    public double GameRumbleSmall { get => _gameRumbleSmall; set => Set(ref _gameRumbleSmall, value); }

    // Drivers: ViGEmBus is required for emulation, HidHide is optional.
    private DriverInfo _vigem = null!;
    private DriverInfo _hidHide = null!;
    private bool _bannerDismissed;

    public DriverInfo ViGEm => _vigem;
    public DriverInfo HidHideDriver => _hidHide;
    public bool ViGEmInstalled => _vigem.Installed;
    public bool ViGEmMissing => !_vigem.Installed;

    /// <summary>HidHide usable: driver installed and its command line tool found.</summary>
    public bool HidHideInstalled => _hidHide.Installed && HidHideController.IsInstalled;
    public bool HidHideMissing => !HidHideInstalled;

    public string ViGEmText => _vigem.Installed
        ? Loc.Format("Driver_Installed", _vigem.Version ?? "?")
        : Loc.Get("Driver_ViGEmMissing");

    public string HidHideDriverText => HidHideInstalled
        ? Loc.Format("Driver_Installed", _hidHide.Version ?? "?")
        : Loc.Get("Driver_HidHideMissing");

    /// <summary>The banner under the status bar: shown at startup when ViGEmBus is missing, until dismissed.</summary>
    public bool ShowDriverBanner => ViGEmMissing && !_bannerDismissed;

    /// <summary>Reads the driver state again, e.g. after the user installed one.</summary>
    public void CheckDrivers()
    {
        _vigem = DriverCheck.ViGEmBus();
        _hidHide = DriverCheck.HidHide();
        OnPropertyChanged(string.Empty); // refresh every binding
        // A driver installed while PadPilot runs: let the emulation try again.
        if (_vigem.Installed && _service.Status == EmulationStatus.DriverMissing)
            _service.Retry();
    }

    public void DismissBanner()
    {
        _bannerDismissed = true;
        OnPropertyChanged(nameof(ShowDriverBanner));
    }

    public void InstallViGEm() => DriverCheck.Install(_vigem);
    public void InstallHidHide() => DriverCheck.Install(_hidHide);
    public void OpenViGEmPage() => DriverCheck.OpenDownloadPage(_vigem);
    public void OpenHidHidePage() => DriverCheck.OpenDownloadPage(_hidHide);

    private string _hidHideStatusText = "";
    private bool _hidHideHidden;

    /// <summary>Raised when the user switches hiding on or off.</summary>
    public event Action? HideControllerChanged;

    public bool HideController
    {
        get => _settings.HideControllerWithHidHide;
        set
        {
            _settings.HideControllerWithHidHide = value;
            _settings.Save();
            OnPropertyChanged(nameof(HideController));
            HideControllerChanged?.Invoke();
        }
    }

    public string HidHideStatusText { get => _hidHideStatusText; private set => Set(ref _hidHideStatusText, value); }
    public bool HidHideHidden { get => _hidHideHidden; private set => Set(ref _hidHideHidden, value); }

    private string? _hidHideError;

    /// <summary>Called on the UI thread after PadPilot hid or released the controller.</summary>
    public void SetHidHideState(bool hidden, string? error)
    {
        HidHideHidden = hidden;
        _hidHideError = error;
        UpdateHidHideText();
    }

    private void UpdateHidHideText() =>
        HidHideStatusText = _hidHideError != null
            ? Loc.Format("HidHideState_Error", _hidHideError)
            : Loc.Get(HidHideHidden ? "HidHideState_Hidden" : "HidHideState_Visible");

    /// <summary>Called on the UI thread when the service reports a change.</summary>
    public void UpdateStatus()
    {
        Status = _service.Status;
        StatusText = Status == EmulationStatus.Error
            ? Loc.Format("EmulationStatus_Error", _service.ErrorMessage ?? "")
            : Loc.Get($"EmulationStatus_{Status}");
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(HasProblem));
    }
}
