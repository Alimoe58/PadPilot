namespace PadPilot.App;

/// <summary>The "Settings" tab. Every change is written to the settings file right away.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;

    public SettingsViewModel(AppSettings settings) => _settings = settings;

    public bool MinimizeToTray
    {
        get => _settings.MinimizeToTray;
        set => Update(() => _settings.MinimizeToTray = value);
    }

    public bool CloseToTray
    {
        get => _settings.CloseToTray;
        set => Update(() => _settings.CloseToTray = value);
    }

    public bool StartMinimized
    {
        get => _settings.StartMinimized;
        set => Update(() => _settings.StartMinimized = value);
    }

    /// <summary>Switches the controller's motion sensors off; the controller gets the change with the next output report.</summary>
    public bool GyroDisabled
    {
        get => _settings.GyroDisabled;
        set => Update(() => _settings.GyroDisabled = value);
    }

    /// <summary>Start with Windows (minimized in the tray). Stored in Windows' autostart list, not in the settings file.</summary>
    public bool StartWithWindows
    {
        get => Autostart.IsEnabled;
        set
        {
            if (value)
                Autostart.Enable();
            else
                Autostart.Disable();
            OnPropertyChanged(nameof(StartWithWindows));
        }
    }

    /// <summary>Raised after any setting changed.</summary>
    public event Action? Changed;

    private void Update(Action apply, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        apply();
        _settings.Save();
        OnPropertyChanged(name);
        Changed?.Invoke();
    }
}
