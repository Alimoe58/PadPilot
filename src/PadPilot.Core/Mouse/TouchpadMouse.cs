namespace PadPilot.Core.Mouse;

public sealed record TouchpadMouseSettings
{
    public bool Enabled { get; init; }

    /// <summary>Mouse pixels per touchpad unit (the pad is 1920 units wide).</summary>
    public double Sensitivity { get; init; } = 0.6;

    /// <summary>0 = none; higher values make fast swipes travel further.</summary>
    public double Acceleration { get; init; } = 0.5;

    public bool TapToClick { get; init; }
    public bool RightClickOnRightHalf { get; init; } = true;
    public bool TwoFingerScroll { get; init; } = true;

    /// <summary>Wheel units per touchpad unit of two-finger movement (120 = one notch).</summary>
    public double ScrollSpeed { get; init; } = 2.0;
}

/// <summary>
/// Turns touchpad input into mouse actions: one finger moves, two fingers scroll, pressing the pad clicks
/// (right half = right click), optionally a short tap clicks. Feed it every input report.
/// </summary>
public sealed class TouchpadMouse
{
    public const int PadWidth = 1920;

    private static readonly TimeSpan MaxTapDuration = TimeSpan.FromMilliseconds(180);
    private const double MaxTapMovement = 40;

    private readonly IMouseOutput _mouse;

    private TouchPoint _last1;
    private TouchPoint _last2;
    private double _fracX, _fracY, _scroll;
    private MouseButton? _held;
    private bool _wasPressed;

    // Tap detection for finger 1
    private TimeSpan _touchStart;
    private double _touchMoved;
    private bool _touchDisqualified;

    public TouchpadMouse(IMouseOutput mouse) => _mouse = mouse;

    public void Process(ControllerState s, TouchpadMouseSettings cfg, TimeSpan now)
    {
        if (!cfg.Enabled)
        {
            Reset();
            return;
        }

        TouchPoint t1 = s.Touch1, t2 = s.Touch2;
        bool twoFingers = t1.Active && t2.Active;
        bool sameFinger1 = t1.Active && _last1.Active && t1.Id == _last1.Id;

        if (twoFingers && cfg.TwoFingerScroll)
        {
            if (sameFinger1 && _last2.Active && t2.Id == _last2.Id)
            {
                double dy = ((t1.Y - _last1.Y) + (t2.Y - _last2.Y)) / 2.0;
                // Fingers moving down scroll up, so the content follows the fingers like on a laptop touchpad.
                _scroll += dy * cfg.ScrollSpeed;
                int whole = (int)_scroll;
                if (Math.Abs(whole) >= 20)
                {
                    _mouse.Wheel(whole);
                    _scroll -= whole;
                }
            }
            _touchDisqualified = true;
        }
        else if (sameFinger1 && !_last2.Active)
        {
            double dx = t1.X - _last1.X, dy = t1.Y - _last1.Y;
            double speed = Math.Sqrt(dx * dx + dy * dy);
            double factor = cfg.Sensitivity * (1 + cfg.Acceleration * Math.Min(speed / 20, 2));
            _fracX += dx * factor;
            _fracY += dy * factor;
            int mx = (int)_fracX, my = (int)_fracY;
            _mouse.Move(mx, my);
            _fracX -= mx;
            _fracY -= my;
            _touchMoved += speed;
        }

        UpdateTap(t1, cfg, now);
        UpdateClick(s, t1, twoFingers, cfg);

        _last1 = t1;
        _last2 = t2;
    }

    /// <summary>Lets go of any held button; call when the controller disconnects or the feature is turned off.</summary>
    public void Reset()
    {
        if (_held is MouseButton b)
            _mouse.Button(b, false);
        _held = null;
        _wasPressed = false;
        _last1 = _last2 = default;
        _fracX = _fracY = _scroll = 0;
    }

    private void UpdateClick(ControllerState s, TouchPoint t1, bool twoFingers, TouchpadMouseSettings cfg)
    {
        bool pressed = s.IsPressed(Buttons.TouchpadClick);
        if (pressed && !_wasPressed)
        {
            bool right = twoFingers || (cfg.RightClickOnRightHalf && t1.Active && t1.X >= PadWidth / 2);
            _held = right ? MouseButton.Right : MouseButton.Left;
            _mouse.Button(_held.Value, true);
            _touchDisqualified = true; // a physical click is not also a tap
        }
        else if (!pressed && _wasPressed && _held is MouseButton b)
        {
            _mouse.Button(b, false);
            _held = null;
        }
        _wasPressed = pressed;
    }

    private void UpdateTap(TouchPoint t1, TouchpadMouseSettings cfg, TimeSpan now)
    {
        if (t1.Active && !_last1.Active)
        {
            _touchStart = now;
            _touchMoved = 0;
            _touchDisqualified = false;
        }
        else if (!t1.Active && _last1.Active)
        {
            bool tap = cfg.TapToClick && !_touchDisqualified
                && now - _touchStart <= MaxTapDuration && _touchMoved <= MaxTapMovement;
            if (tap)
            {
                _mouse.Button(MouseButton.Left, true);
                _mouse.Button(MouseButton.Left, false);
            }
        }
    }
}
