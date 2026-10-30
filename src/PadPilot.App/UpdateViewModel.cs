using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using PadPilot.Core.Updates;

namespace PadPilot.App;

/// <summary>Update check against the GitHub releases: banner at the top and the "Updates" panel in Settings.</summary>
public sealed class UpdateViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private UpdateInfo? _available;
    private bool _dismissed;
    private bool _isChecking;
    private string _status = "";

    public UpdateViewModel(AppSettings settings) => _settings = settings;

    /// <summary>The running version, e.g. "1.2.0".</summary>
    public static string CurrentText { get; } =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    public static Version Current { get; } = UpdateChecker.ParseVersion(CurrentText) ?? new Version(0, 0, 0);

    public bool CheckAtStartup
    {
        get => _settings.CheckForUpdates;
        set
        {
            _settings.CheckForUpdates = value;
            _settings.Save();
            OnPropertyChanged(nameof(CheckAtStartup));
        }
    }

    public bool IsChecking { get => _isChecking; private set => Set(ref _isChecking, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }

    public bool ShowBanner => _available != null && !_dismissed;
    public string BannerText => _available == null ? "" : Loc.Format("Update_Available", _available.Version.ToString(3), CurrentText);

    /// <summary>At startup: quietly, and versions the user chose to skip are not offered again.</summary>
    public Task CheckAtStartupAsync() => CheckAtStartup ? CheckAsync(manual: false) : Task.CompletedTask;

    public async Task CheckAsync(bool manual = true)
    {
        if (IsChecking)
            return;
        IsChecking = true;
        Status = Loc.Get("Update_Checking");
        try
        {
            var info = await UpdateChecker.CheckAsync(Current);
            if (info != null && !manual && _settings.SkippedUpdate == info.Version.ToString(3))
                info = null;
            _available = info;
            _dismissed = false;
            Status = info != null ? Loc.Format("Update_Available", info.Version.ToString(3), CurrentText)
                : Loc.Format("Update_UpToDate", CurrentText);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            Status = Loc.Get("Update_Failed");
        }
        finally
        {
            IsChecking = false;
            OnPropertyChanged(nameof(ShowBanner));
            OnPropertyChanged(nameof(BannerText));
        }
    }

    public void Download() =>
        Process.Start(new ProcessStartInfo(_available?.Url ?? UpdateChecker.ReleasesPage) { UseShellExecute = true });

    /// <summary>Don't offer this version again at startup.</summary>
    public void Skip()
    {
        if (_available != null)
        {
            _settings.SkippedUpdate = _available.Version.ToString(3);
            _settings.Save();
        }
        Dismiss();
    }

    /// <summary>Hide the banner until the next start.</summary>
    public void Dismiss()
    {
        _dismissed = true;
        OnPropertyChanged(nameof(ShowBanner));
    }
}
