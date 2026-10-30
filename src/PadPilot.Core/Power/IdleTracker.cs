namespace PadPilot.Core.Power;

public enum IdleTimeout
{
    Never = 0,
    Minutes5 = 5,
    Minutes10 = 10,
    Minutes15 = 15,
    Minutes30 = 30,
    Minutes60 = 60,
}

/// <summary>
/// Measures how long nobody has used the controller. Pressed buttons, sticks pushed clearly past
/// their resting drift, pulled triggers, touches and turning the controller count as use.
/// </summary>
public sealed class IdleTracker
{
    private const int StickActivity = 30;     // of 128: well beyond normal stick drift
    private const int StickMovement = 12;     // a stick that moves this much since the last check
    private const int TriggerActivity = 25;
    private const int GyroActivity = 800;     // raw units, about 50 °/s

    private ControllerState? _last;
    private TimeSpan _lastActivity;

    public TimeSpan IdleTime(TimeSpan now) => now - _lastActivity;

    public void Reset(TimeSpan now)
    {
        _last = null;
        _lastActivity = now;
    }

    public void Update(ControllerState s, TimeSpan now)
    {
        if (IsActive(s, _last))
            _lastActivity = now;
        _last = s;
    }

    private static bool IsActive(ControllerState s, ControllerState? last) =>
        s.Buttons != Buttons.None
        || last == null
        || Far(s.LeftX) || Far(s.LeftY) || Far(s.RightX) || Far(s.RightY)
        || Moved(s.LeftX, last.LeftX) || Moved(s.LeftY, last.LeftY) || Moved(s.RightX, last.RightX) || Moved(s.RightY, last.RightY)
        || s.L2 > TriggerActivity || s.R2 > TriggerActivity
        || s.Touch1.Active || s.Touch2.Active
        || Math.Abs((int)s.Gyro.X) > GyroActivity || Math.Abs((int)s.Gyro.Y) > GyroActivity || Math.Abs((int)s.Gyro.Z) > GyroActivity;

    private static bool Far(byte axis) => Math.Abs(axis - 128) > StickActivity;
    private static bool Moved(byte axis, byte before) => Math.Abs(axis - before) > StickMovement;
}
