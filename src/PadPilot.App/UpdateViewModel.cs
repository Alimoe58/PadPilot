using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using PadPilot.Core.Updates;

namespace PadPilot.App;

/// <summary>
/// Update check against the GitHub releases (banner at the top and the "Updates" panel in Settings),
/// and installing a new version with one click.
/// </summary>
public sealed class UpdateViewModel : ObservableObject
{
    /// <summary>Passed to the new version after an update, so it waits for the old one to close.</summary>
    public const string UpdatedArgument = "--updated";

    private readonly AppSettings _settings;
    private UpdateInfo? _available;
    private bool _dismissed;
    private bool _justUpdated;
    private bool _isChecking;
    private bool _isInstalling;
    private double _progress;
    private string _status = "";

    public UpdateViewModel(AppSettings settings) => _settings = settings;

    /// <summary>The running version, e.g. "1.2.0".</summary>
    public static string CurrentText { get; } =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    public static Version Current { get; } = UpdateChecker.ParseVersion(CurrentText) ?? new Version(0, 0, 0);

    private static string ExePath => Environment.ProcessPath ?? "";

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

    /// <summary>A new version is available and can be installed right here (otherwise: download page only).</summary>
    public bool CanInstall => _available?.Download != null && !IsInstalling && Updater.CanReplace(ExePath);
    public bool ManualOnly => _available != null && !CanInstall && !IsInstalling;

    public bool IsInstalling
    {
        get => _isInstalling;
        private set
        {
            if (!Set(ref _isInstalling, value)) return;
            OnPropertyChanged(nameof(CanInstall));
            OnPropertyChanged(nameof(ManualOnly));
        }
    }

    /// <summary>Download progress 0–100.</summary>
    public double Progress { get => _progress; private set => Set(ref _progress, value); }

    /// <summary>Version and approximate size for the confirmation question, e.g. ("1.3.0", 64).</summary>
    public (string Version, long Megabytes) Pending =>
        (_available?.Version.ToString(3) ?? "", (_available?.Download?.Size ?? 0) / (1024 * 1024));

    /// <summary>At startup: quietly, and versions the user chose to skip are not offered again.</summary>
    public Task CheckAtStartupAsync() => CheckAtStartup ? CheckAsync(manual: false) : Task.CompletedTask;

    public async Task CheckAsync(bool manual = true)
    {
        if (IsChecking || IsInstalling)
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
            Status = info == null ? Loc.Format(_justUpdated ? "Update_Done" : "Update_UpToDate", CurrentText)
                : info.Download != null && !Updater.CanReplace(ExePath) && !File.Exists(Path.Combine(Path.GetDirectoryName(ExePath) ?? "", "PadPilot.dll"))
                    ? Loc.Format("Update_Available", info.Version.ToString(3), CurrentText) + " " + Loc.Get("Update_NoWriteAccess")
                    : Loc.Format("Update_Available", info.Version.ToString(3), CurrentText);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            Status = Loc.Get("Update_Failed");
        }
        finally
        {
            IsChecking = false;
            RefreshAll();
        }
    }

    /// <summary>
    /// Downloads and checks the new version, puts it in place of the running exe and starts it.
    /// Returns true when the new version is running and this one should close now.
    /// </summary>
    public async Task<bool> InstallAsync()
    {
        if (_available?.Download is not ReleaseFile file || !CanInstall)
            return false;
        string exe = ExePath;
        IsInstalling = true;
        Progress = 0;
        Status = Loc.Format("Update_Downloading", _available.Version.ToString(3));
        try
        {
            string downloaded = await Updater.DownloadAsync(file, exe, Current, new Progress<double>(p => Progress = p * 100));
            Status = Loc.Get("Update_Restarting");
            Updater.Swap(exe, downloaded);
            try
            {
                Process.Start(new ProcessStartInfo(exe, UpdatedArgument) { UseShellExecute = false });
            }
            catch (Win32Exception)
            {
                // The new file won't start: put the old one back, so PadPilot keeps working.
                File.Delete(exe);
                File.Move(exe + Updater.OldSuffix, exe);
                throw;
            }
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException
                                   or UnauthorizedAccessException or UpdateFailedException or Win32Exception)
        {
            Status = Loc.Format("Update_InstallFailed", ex.Message);
            IsInstalling = false;
            RefreshAll();
            return false;
        }
    }

    /// <summary>Called in the new version right after an update: say so and remove the old file.</summary>
    public void FinishUpdate()
    {
        _justUpdated = true; // the startup check that follows keeps saying so instead of "latest version"
        Status = Loc.Format("Update_Done", CurrentText);
        _ = Updater.CleanUpAsync(ExePath);
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

    private void RefreshAll()
    {
        OnPropertyChanged(nameof(ShowBanner));
        OnPropertyChanged(nameof(BannerText));
        OnPropertyChanged(nameof(CanInstall));
        OnPropertyChanged(nameof(ManualOnly));
    }
}
