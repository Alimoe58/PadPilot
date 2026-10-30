namespace PadPilot.Core.Games;

/// <summary>
/// Decides when to switch profiles for linked games. A linked game in the foreground gets its
/// profile; once that game has closed, the profile from before comes back. Alt-tabbing out of a
/// running game keeps its profile, because switching the emulation mid-game would take the
/// controller away from the game.
/// </summary>
public sealed class GameSwitcher
{
    private string? _game;      // linked game whose profile was switched to
    private string? _returnTo;  // profile that was active before that
    private string? _ignored;   // game whose profile the user left by hand; not switched again until it closes

    /// <summary>The game that currently decides the profile, or null.</summary>
    public string? CurrentGame => _game;

    /// <summary>Called about once per second.</summary>
    /// <param name="foregroundExe">File name of the program in the foreground (e.g. "game.exe"), or null.</param>
    /// <param name="activeProfile">Profile in use right now.</param>
    /// <param name="links">Game file name → profile name, compared without case.</param>
    /// <param name="isRunning">Whether a program with this file name is still running.</param>
    /// <returns>The profile to switch to, or null to keep the current one.</returns>
    public string? Update(string? foregroundExe, string activeProfile,
        IReadOnlyDictionary<string, string> links, Func<string, bool> isRunning)
    {
        if (_ignored != null && !isRunning(_ignored))
            _ignored = null;

        if (foregroundExe != null && links.TryGetValue(foregroundExe, out var profile)
            && !string.Equals(foregroundExe, _ignored, StringComparison.OrdinalIgnoreCase))
        {
            if (_game == null)
                _returnTo = activeProfile;
            _game = foregroundExe;
            return profile == activeProfile ? null : profile;
        }

        if (_game != null && !isRunning(_game))
        {
            string? back = _returnTo;
            _game = null;
            _returnTo = null;
            return back == activeProfile ? null : back;
        }

        return null;
    }

    /// <summary>The user picked a profile by hand: leave the current game alone until it closes.</summary>
    public void UserSwitched()
    {
        if (_game != null)
            _ignored = _game;
        _game = null;
        _returnTo = null;
    }

    public void ProfileRenamed(string oldName, string newName)
    {
        if (_returnTo == oldName)
            _returnTo = newName;
    }
}
