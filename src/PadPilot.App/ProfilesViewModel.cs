using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using PadPilot.Core.Games;

namespace PadPilot.App;

/// <summary>
/// Named profiles. All changes in the UI belong to the active profile and are written to it
/// when switching profiles and when PadPilot closes. Profiles can be linked to games and are
/// then switched automatically while the game runs.
/// </summary>
public sealed class ProfilesViewModel : ObservableObject
{
    public const string DefaultName = "Standard";

    private readonly Func<Profile> _capture;
    private readonly Action<Profile> _apply;
    private readonly AppSettings _settings;
    private readonly GameSwitcher _switcher = new();
    private readonly DispatcherTimer _gameTimer;
    private Dictionary<string, string> _links = new(StringComparer.OrdinalIgnoreCase);
    private string _active;
    private string _newName = "";
    private string _message = "";
    private string _gameMessage = "";
    private string _gameStatus = "";
    private IReadOnlyList<RunningApp> _runningApps = [];
    private RunningApp? _selectedApp;

    /// <summary>Raised when the list or the active profile changed (e.g. to update the tray menu).</summary>
    public event Action? ProfilesChanged;

    public ProfilesViewModel(Func<Profile> capture, Action<Profile> apply, AppSettings settings)
    {
        _capture = capture;
        _apply = apply;
        _settings = settings;

        // First start with profiles: the current settings become the "Standard" profile.
        if (ProfileStore.Names().Count == 0)
            ProfileStore.Save(DefaultName, _capture());

        Names = new ObservableCollection<string>(ProfileStore.Names());
        _active = settings.ActiveProfile is string saved && Names.Contains(saved) ? saved : Names[0];
        if (ProfileStore.Load(_active) is Profile profile)
            Load(profile);
        RebuildLinks();
        RememberActive();

        _gameTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => CheckGames(), Dispatcher.CurrentDispatcher);
        _gameTimer.Start();
    }

    public ObservableCollection<string> Names { get; }

    /// <summary>Games linked to the active profile.</summary>
    public ObservableCollection<string> Games { get; } = [];

    public string Active
    {
        get => _active;
        set => Switch(value);
    }

    public string NewName { get => _newName; set => Set(ref _newName, value); }
    public string Message { get => _message; private set => Set(ref _message, value); }
    public string GameMessage { get => _gameMessage; private set => Set(ref _gameMessage, value); }

    /// <summary>Which game currently decides the profile, shown under the game list.</summary>
    public string GameStatus { get => _gameStatus; private set => Set(ref _gameStatus, value); }

    public bool AutoSwitch
    {
        get => _settings.AutoSwitchProfiles;
        set
        {
            if (_settings.AutoSwitchProfiles == value) return;
            _settings.AutoSwitchProfiles = value;
            _settings.Save();
            OnPropertyChanged(nameof(AutoSwitch));
            UpdateGameStatus();
        }
    }

    /// <summary>Programs with a window, offered for linking; refreshed when the list is opened.</summary>
    public IReadOnlyList<RunningApp> RunningApps { get => _runningApps; private set => Set(ref _runningApps, value); }
    public RunningApp? SelectedApp { get => _selectedApp; set => Set(ref _selectedApp, value); }

    /// <summary>Switch chosen by the user (window or tray menu).</summary>
    public void Switch(string? name)
    {
        if (name == null || name == _active || !ProfileStore.Exists(name))
            return;
        _switcher.UserSwitched();
        if (SwitchTo(name))
            Message = "";
        UpdateGameStatus();
    }

    /// <summary>Writes the current settings into the active profile.</summary>
    public void SaveActive() => ProfileStore.Save(_active, _capture() with { Games = Games.ToArray() });

    /// <summary>Saves the current settings as a new profile and switches to it. Linked games stay with the old profile.</summary>
    public void CreateNew()
    {
        if (CheckNewName() is not string name)
            return;
        SaveActive();
        ProfileStore.Save(name, _capture());
        Names.Add(name);
        _active = name;
        Games.Clear();
        NewName = "";
        Message = Loc.Format("Profiles_Created", name);
        RememberActive();
    }

    public void RenameActive()
    {
        if (CheckNewName() is not string name)
            return;
        SaveActive();
        ProfileStore.Rename(_active, name);
        Names[Names.IndexOf(_active)] = name;
        _switcher.ProfileRenamed(_active, name);
        _active = name;
        NewName = "";
        Message = Loc.Format("Profiles_Renamed", name);
        RebuildLinks();
        RememberActive();
    }

    public void DeleteActive()
    {
        if (Names.Count <= 1)
        {
            Message = Loc.Get("Profiles_LastOne");
            return;
        }
        string deleted = _active;
        ProfileStore.Delete(deleted);
        Names.Remove(deleted);
        _active = Names[0];
        if (ProfileStore.Load(_active) is Profile profile)
            Load(profile);
        Message = Loc.Format("Profiles_Deleted", deleted);
        RebuildLinks();
        RememberActive();
    }

    public void RefreshRunningApps() => RunningApps = PadPilot.Core.Games.RunningApps.WithWindows();

    /// <summary>
    /// Links a game (file name or full path of its .exe) to the active profile.
    /// A game belongs to one profile only, so it is taken away from any other profile.
    /// </summary>
    public void AddGame(string? exe)
    {
        exe = Path.GetFileName(exe?.Trim() ?? "");
        if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            GameMessage = Loc.Get("Games_PickFirst");
            return;
        }
        if (Games.Contains(exe, StringComparer.OrdinalIgnoreCase))
        {
            GameMessage = Loc.Format("Games_AlreadyLinked", exe, _active);
            return;
        }

        GameMessage = Loc.Format("Games_Linked", exe, _active);
        if (_links.TryGetValue(exe, out string? other))
        {
            RemoveGameFrom(other, exe);
            GameMessage = Loc.Format("Games_Moved", exe, other, _active);
        }

        Games.Add(exe);
        SaveActive();
        RebuildLinks();
        SelectedApp = null;
    }

    public void RemoveGame(string exe)
    {
        if (!Games.Remove(exe))
            return;
        SaveActive();
        RebuildLinks();
        GameMessage = Loc.Format("Games_Removed", exe);
    }

    /// <summary>Suggested file name when exporting the active profile.</summary>
    public string ExportFileName => _active + ".json";

    /// <summary>Saves the active profile (with its linked games) to a file to share.</summary>
    public void Export(string path)
    {
        try
        {
            ProfileStore.Export(path, _capture() with { Games = Games.ToArray() });
            Message = Loc.Format("Profiles_Exported", _active, Path.GetFileName(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Message = Loc.Format("Profiles_ExportFailed", ex.Message);
        }
    }

    /// <summary>
    /// Adds a shared profile file as a new profile (named after the file) and switches to it.
    /// Its games are taken away from other profiles, since a game can only have one.
    /// </summary>
    public void Import(string path)
    {
        if (ProfileStore.ReadShared(path) is not Profile profile)
        {
            Message = Loc.Get("Profiles_ImportFailed");
            return;
        }
        string name = ProfileStore.UniqueName(
            ProfileStore.CleanName(Path.GetFileNameWithoutExtension(path)) ?? Loc.Get("Profiles_ImportedName"), Names);

        SaveActive();
        var moved = profile.Games.Where(g => _links.ContainsKey(g)).ToList();
        foreach (string game in moved)
            RemoveGameFrom(_links[game], game);

        ProfileStore.Save(name, profile);
        Names.Add(name);
        _switcher.UserSwitched();
        _active = name;
        Load(profile);
        RebuildLinks();
        Message = Loc.Format("Profiles_Imported", name)
                  + (moved.Count > 0 ? " " + Loc.Format("Profiles_ImportGamesMoved", string.Join(", ", moved)) : "");
        RememberActive();
    }

    private void RemoveGameFrom(string profileName, string exe)
    {
        if (profileName == _active)
        {
            if (Games.FirstOrDefault(g => g.Equals(exe, StringComparison.OrdinalIgnoreCase)) is string linked)
                Games.Remove(linked);
            SaveActive();
        }
        else if (ProfileStore.Load(profileName) is Profile other)
        {
            ProfileStore.Save(profileName, other with
            {
                Games = other.Games.Where(g => !g.Equals(exe, StringComparison.OrdinalIgnoreCase)).ToArray(),
            });
        }
    }

    public static void OpenFolder()
    {
        Directory.CreateDirectory(ProfileStore.Folder);
        Process.Start(new ProcessStartInfo(ProfileStore.Folder) { UseShellExecute = true });
    }

    private bool SwitchTo(string name)
    {
        SaveActive();
        if (ProfileStore.Load(name) is not Profile profile)
        {
            Message = Loc.Format("Profiles_LoadFailed", name);
            OnPropertyChanged(nameof(Active));
            return false;
        }
        _active = name;
        Load(profile);
        RememberActive();
        return true;
    }

    private void Load(Profile profile)
    {
        _apply(profile);
        GameMessage = "";
        Games.Clear();
        foreach (string game in profile.Games)
            Games.Add(game);
    }

    private void CheckGames()
    {
        if (!_settings.AutoSwitchProfiles || _links.Count == 0)
        {
            GameStatus = "";
            return;
        }
        string? target = _switcher.Update(PadPilot.Core.Games.RunningApps.ForegroundExe(), _active, _links,
            PadPilot.Core.Games.RunningApps.IsRunning);
        if (target != null && Names.Contains(target))
            SwitchTo(target);
        UpdateGameStatus();
    }

    private void UpdateGameStatus() =>
        GameStatus = !_settings.AutoSwitchProfiles ? ""
            : _switcher.CurrentGame is string game ? Loc.Format("Games_Running", game, _active)
            : "";

    /// <summary>Game → profile for all profiles; the active profile's games come from the UI.</summary>
    private void RebuildLinks()
    {
        var links = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in Names)
        {
            var games = name == _active ? Games.ToArray() : ProfileStore.Load(name)?.Games ?? [];
            foreach (string game in games)
                links[game] = name;
        }
        _links = links;
    }

    private string? CheckNewName()
    {
        string? name = ProfileStore.CleanName(NewName);
        if (name == null)
        {
            Message = Loc.Get("Profiles_NameMissing");
            return null;
        }
        if (Names.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            Message = Loc.Format("Profiles_NameTaken", name);
            return null;
        }
        return name;
    }

    private void RememberActive()
    {
        if (_settings.ActiveProfile != _active)
        {
            _settings.ActiveProfile = _active;
            _settings.Save();
        }
        OnPropertyChanged(nameof(Active));
        ProfilesChanged?.Invoke();
    }
}
