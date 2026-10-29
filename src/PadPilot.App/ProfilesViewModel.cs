using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

namespace PadPilot.App;

/// <summary>
/// Named profiles. All changes in the UI belong to the active profile and are written to it
/// when switching profiles and when PadPilot closes.
/// </summary>
public sealed class ProfilesViewModel : ObservableObject
{
    public const string DefaultName = "Standard";

    private readonly Func<Profile> _capture;
    private readonly Action<Profile> _apply;
    private readonly AppSettings _settings;
    private string _active;
    private string _newName = "";
    private string _message = "";

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
            _apply(profile);
        RememberActive();
    }

    public ObservableCollection<string> Names { get; }

    public string Active
    {
        get => _active;
        set => Switch(value);
    }

    public string NewName { get => _newName; set => Set(ref _newName, value); }
    public string Message { get => _message; private set => Set(ref _message, value); }

    public void Switch(string? name)
    {
        if (name == null || name == _active || !ProfileStore.Exists(name))
            return;
        SaveActive();
        if (ProfileStore.Load(name) is not Profile profile)
        {
            Message = Loc.Format("Profiles_LoadFailed", name);
            OnPropertyChanged(nameof(Active));
            return;
        }
        _active = name;
        _apply(profile);
        Message = "";
        RememberActive();
    }

    /// <summary>Writes the current settings into the active profile.</summary>
    public void SaveActive() => ProfileStore.Save(_active, _capture());

    /// <summary>Saves the current settings as a new profile and switches to it.</summary>
    public void CreateNew()
    {
        if (CheckNewName() is not string name)
            return;
        SaveActive();
        ProfileStore.Save(name, _capture());
        Names.Add(name);
        _active = name;
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
        _active = name;
        NewName = "";
        Message = Loc.Format("Profiles_Renamed", name);
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
            _apply(profile);
        Message = Loc.Format("Profiles_Deleted", deleted);
        RememberActive();
    }

    public static void OpenFolder()
    {
        Directory.CreateDirectory(ProfileStore.Folder);
        Process.Start(new ProcessStartInfo(ProfileStore.Folder) { UseShellExecute = true });
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
