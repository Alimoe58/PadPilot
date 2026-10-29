namespace PadPilot.Core.Mods;

/// <summary>
/// What mods have set so far. Laid over the Outputs-tab settings until a mod sends "reset"
/// or the user resets it. Thread-safe: commands arrive on network/file threads.
/// </summary>
public sealed class ModOverrides
{
    private readonly object _lock = new();
    private (byte R, byte G, byte B)? _lightbar;
    private PlayerLeds? _playerLeds;
    private MicLed? _micLed;
    private (byte Left, byte Right)? _rumble;
    private TriggerEffect? _leftTrigger;
    private TriggerEffect? _rightTrigger;

    public bool IsActive
    {
        get
        {
            lock (_lock)
                return _lightbar != null || _playerLeds != null || _micLed != null
                    || _rumble != null || _leftTrigger != null || _rightTrigger != null;
        }
    }

    public void Apply(ModCommand cmd)
    {
        lock (_lock)
        {
            if (cmd.Reset)
                ClearLocked();
            _lightbar = cmd.Lightbar ?? _lightbar;
            _playerLeds = cmd.PlayerLeds ?? _playerLeds;
            _micLed = cmd.MicLed ?? _micLed;
            _rumble = cmd.Rumble ?? _rumble;
            _leftTrigger = cmd.LeftTrigger ?? _leftTrigger;
            _rightTrigger = cmd.RightTrigger ?? _rightTrigger;
        }
    }

    public void Clear()
    {
        lock (_lock)
            ClearLocked();
    }

    /// <summary>The user's output settings with everything a mod has set laid on top.</summary>
    public OutputState ApplyTo(OutputState s)
    {
        lock (_lock)
        {
            return s with
            {
                LightbarRed = _lightbar?.R ?? s.LightbarRed,
                LightbarGreen = _lightbar?.G ?? s.LightbarGreen,
                LightbarBlue = _lightbar?.B ?? s.LightbarBlue,
                PlayerLeds = _playerLeds ?? s.PlayerLeds,
                MicLed = _micLed ?? s.MicLed,
                RumbleLeft = _rumble?.Left ?? s.RumbleLeft,
                RumbleRight = _rumble?.Right ?? s.RumbleRight,
                LeftTrigger = _leftTrigger ?? s.LeftTrigger,
                RightTrigger = _rightTrigger ?? s.RightTrigger,
            };
        }
    }

    private void ClearLocked()
    {
        _lightbar = null;
        _playerLeds = null;
        _micLed = null;
        _rumble = null;
        _leftTrigger = null;
        _rightTrigger = null;
    }
}
