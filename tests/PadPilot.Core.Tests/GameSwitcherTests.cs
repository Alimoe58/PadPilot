using PadPilot.Core.Games;

namespace PadPilot.Core.Tests;

public class GameSwitcherTests
{
    private readonly GameSwitcher _switcher = new();
    private readonly HashSet<string> _running = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _links = new(StringComparer.OrdinalIgnoreCase)
    {
        ["racer.exe"] = "Racing",
        ["shooter.exe"] = "Shooter",
    };

    private string _active = "Standard";

    /// <summary>One timer tick; applies the switch like the app does.</summary>
    private string? Tick(string? foreground)
    {
        string? target = _switcher.Update(foreground, _active, _links, _running.Contains);
        if (target != null)
            _active = target;
        return target;
    }

    private void Start(string exe) => _running.Add(exe);
    private void Close(string exe) => _running.Remove(exe);

    [Fact]
    public void LinkedGameInForegroundSwitchesToItsProfile()
    {
        Start("racer.exe");
        Assert.Equal("Racing", Tick("RACER.EXE"));
        Assert.Equal("racer.exe", _switcher.CurrentGame, ignoreCase: true);
    }

    [Fact]
    public void UnlinkedProgramsChangeNothing()
    {
        Assert.Null(Tick("explorer.exe"));
        Assert.Null(Tick(null));
        Assert.Equal("Standard", _active);
    }

    [Fact]
    public void AltTabKeepsTheGameProfileUntilTheGameCloses()
    {
        Start("racer.exe");
        Tick("racer.exe");

        Assert.Null(Tick("discord.exe"));
        Assert.Equal("Racing", _active);

        Close("racer.exe");
        Assert.Equal("Standard", Tick("discord.exe"));
        Assert.Null(_switcher.CurrentGame);
    }

    [Fact]
    public void SwitchingBetweenGamesReturnsToTheFirstProfile()
    {
        Start("racer.exe");
        Start("shooter.exe");
        Tick("racer.exe");
        Assert.Equal("Shooter", Tick("shooter.exe"));

        Close("shooter.exe");
        Close("racer.exe");
        Assert.Equal("Standard", Tick(null));
    }

    [Fact]
    public void ManualSwitchIsRespectedUntilTheGameCloses()
    {
        Start("racer.exe");
        Tick("racer.exe");

        _switcher.UserSwitched();
        _active = "Standard";
        Assert.Null(Tick("racer.exe"));

        Close("racer.exe");
        Assert.Null(Tick(null));

        // Started again: switched as usual.
        Start("racer.exe");
        Assert.Equal("Racing", Tick("racer.exe"));
    }

    [Fact]
    public void GameAlreadyOnItsProfileDoesNotSwitchBackAfterwards()
    {
        _active = "Racing";
        Start("racer.exe");
        Assert.Null(Tick("racer.exe"));

        Close("racer.exe");
        Assert.Null(Tick(null));
        Assert.Equal("Racing", _active);
    }

    [Fact]
    public void RenamedReturnProfileIsFollowed()
    {
        Start("racer.exe");
        Tick("racer.exe");
        _switcher.ProfileRenamed("Standard", "Desktop");

        Close("racer.exe");
        Assert.Equal("Desktop", Tick(null));
    }
}
