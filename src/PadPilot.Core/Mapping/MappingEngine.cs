using PadPilot.Core.Mouse;

namespace PadPilot.Core.Mapping;

/// <summary>
/// Applies a <see cref="MappingSettings"/>: <see cref="Transform"/> rewrites the input for the virtual
/// controller, <see cref="Process"/> turns button presses and stick movement into keys, mouse input and
/// special actions. Several sources can hold the same key; it is released when the last one lets go.
/// </summary>
public sealed class MappingEngine
{
    private const double StickKeyPress = 0.5;    // stick → keys: press beyond half way ...
    private const double StickKeyRelease = 0.4;  // ... release a little earlier, so it doesn't flicker
    private const double StickMouseDeadzone = 0.15;
    private const double StickAxisSnap = 0.05;
    private const int ButtonCount = 19;

    private readonly IKeyboardOutput _keyboard;
    private readonly IMouseOutput _mouse;
    private readonly Dictionary<int, int> _keyHolds = [];
    private readonly Dictionary<MouseButton, int> _mouseHolds = [];
    private readonly bool[] _stickKeys = new bool[8]; // left stick up/down/left/right, then the right stick
    private readonly double[] _fraction = new double[4];
    private Buttons _lastButtons;
    private MappingSettings? _settings;

    public MappingEngine(IKeyboardOutput keyboard, IMouseOutput mouse)
    {
        _keyboard = keyboard;
        _mouse = mouse;
    }

    /// <summary>Raised when a button mapped to a special action is pressed (on the calling thread).</summary>
    public event Action<SpecialAction>? Special;

    /// <summary>The input as the virtual controller should see it.</summary>
    public static ControllerState Transform(ControllerState s, MappingSettings m)
    {
        if (m.IsEmpty)
            return s;

        byte l2 = m.For(Buttons.L2).TakesButton ? (byte)0 : s.L2;
        byte r2 = m.For(Buttons.R2).TakesButton ? (byte)0 : s.R2;
        var buttons = Buttons.None;
        for (int i = 0; i < ButtonCount; i++)
        {
            var button = (Buttons)(1u << i);
            if ((s.Buttons & button) == 0)
                continue;
            var action = m.For(button);
            if (action.Kind == ActionKind.Default)
            {
                buttons |= button;
            }
            else if (action.Kind == ActionKind.Button)
            {
                buttons |= action.Target;
                if (action.Target == Buttons.L2) l2 = 255;
                if (action.Target == Buttons.R2) r2 = 255;
            }
        }

        bool leftFree = m.LeftStick == StickMode.Default, rightFree = m.RightStick == StickMode.Default;
        return s with
        {
            Buttons = buttons,
            L2 = l2,
            R2 = r2,
            LeftX = leftFree ? s.LeftX : (byte)128,
            LeftY = leftFree ? s.LeftY : (byte)128,
            RightX = rightFree ? s.RightX : (byte)128,
            RightY = rightFree ? s.RightY : (byte)128,
        };
    }

    /// <param name="seconds">Time since the previous report, for stick → mouse.</param>
    public void Process(ControllerState s, MappingSettings m, double seconds)
    {
        if (!ReferenceEquals(m, _settings))
        {
            Reset();
            _settings = m;
        }
        if (m.IsEmpty)
            return;

        foreach (var (button, action) in m.Buttons)
        {
            bool down = (s.Buttons & button) != 0;
            if (down == ((_lastButtons & button) != 0))
                continue;
            switch (action.Kind)
            {
                case ActionKind.Key:
                    PressKey(action, down);
                    break;
                case ActionKind.Mouse:
                    MouseAct(action.Mouse, down);
                    break;
                case ActionKind.Special when down:
                    Special?.Invoke(action.Special);
                    break;
            }
        }
        _lastButtons = s.Buttons;

        Stick(Deadzones.NormalizeStick(s.LeftX, s.LeftY), m.LeftStick, 0, m.StickMouseSpeed, seconds);
        Stick(Deadzones.NormalizeStick(s.RightX, s.RightY), m.RightStick, 1, m.StickMouseSpeed, seconds);
    }

    /// <summary>Lets go of every key and mouse button this engine holds.</summary>
    public void Reset()
    {
        foreach (int key in _keyHolds.Keys.ToList())
            _keyboard.Key(key, false);
        foreach (var button in _mouseHolds.Keys.ToList())
            _mouse.Button(button, false);
        _keyHolds.Clear();
        _mouseHolds.Clear();
        Array.Clear(_stickKeys);
        Array.Clear(_fraction);
        _lastButtons = Buttons.None;
        _settings = null;
    }

    private void PressKey(ButtonAction a, bool down)
    {
        (KeyModifiers Flag, int Key)[] modifiers =
            [(KeyModifiers.Ctrl, VirtualKeys.Ctrl), (KeyModifiers.Shift, VirtualKeys.Shift), (KeyModifiers.Alt, VirtualKeys.Alt), (KeyModifiers.Win, VirtualKeys.LeftWin)];
        if (down)
        {
            foreach (var (flag, key) in modifiers)
                if (a.Modifiers.HasFlag(flag))
                    Hold(key, true);
            Hold(a.Key, true);
        }
        else
        {
            Hold(a.Key, false);
            foreach (var (flag, key) in modifiers.Reverse())
                if (a.Modifiers.HasFlag(flag))
                    Hold(key, false);
        }
    }

    private void Hold(int key, bool down)
    {
        if (key == 0)
            return;
        _keyHolds.TryGetValue(key, out int count);
        if (down)
        {
            _keyHolds[key] = count + 1;
            if (count == 0)
                _keyboard.Key(key, true);
        }
        else if (count > 0)
        {
            if (count == 1)
            {
                _keyHolds.Remove(key);
                _keyboard.Key(key, false);
            }
            else
            {
                _keyHolds[key] = count - 1;
            }
        }
    }

    private void MouseAct(MouseAction action, bool down)
    {
        if (action is MouseAction.WheelUp or MouseAction.WheelDown)
        {
            if (down)
                _mouse.Wheel(action == MouseAction.WheelUp ? 120 : -120);
            return;
        }
        var button = action switch
        {
            MouseAction.RightClick => MouseButton.Right,
            MouseAction.MiddleClick => MouseButton.Middle,
            _ => MouseButton.Left,
        };
        _mouseHolds.TryGetValue(button, out int count);
        if (down)
        {
            _mouseHolds[button] = count + 1;
            if (count == 0)
                _mouse.Button(button, true);
        }
        else if (count == 1)
        {
            _mouseHolds.Remove(button);
            _mouse.Button(button, false);
        }
        else if (count > 1)
        {
            _mouseHolds[button] = count - 1;
        }
    }

    private void Stick(StickVector v, StickMode mode, int index, double speed, double seconds)
    {
        switch (mode)
        {
            case StickMode.Wasd:
                StickKeys(v, index, VirtualKeys.W, VirtualKeys.S, VirtualKeys.A, VirtualKeys.D);
                break;
            case StickMode.ArrowKeys:
                StickKeys(v, index, VirtualKeys.Up, VirtualKeys.Down, VirtualKeys.Left, VirtualKeys.Right);
                break;
            case StickMode.Mouse:
                StickMouse(v, index, speed, seconds);
                break;
        }
    }

    private void StickKeys(StickVector v, int index, int up, int down, int left, int right)
    {
        // Stick Y grows downwards, like on the DualSense.
        (double Value, int Key)[] directions = [(-v.Y, up), (v.Y, down), (-v.X, left), (v.X, right)];
        for (int i = 0; i < 4; i++)
        {
            ref bool held = ref _stickKeys[index * 4 + i];
            bool press = directions[i].Value > (held ? StickKeyRelease : StickKeyPress);
            if (press != held)
            {
                held = press;
                Hold(directions[i].Key, press);
            }
        }
    }

    private void StickMouse(StickVector v, int index, double speed, double seconds)
    {
        double length = v.Length;
        if (length < StickMouseDeadzone)
            return;
        double amount = Math.Min(1, (length - StickMouseDeadzone) / (1 - StickMouseDeadzone));
        double pixels = speed * amount * amount * seconds;   // squared: fine aiming near the centre

        // An axis that barely moves (the resting value 128 isn't exactly centre) is treated as 0,
        // so pushing straight sideways doesn't creep up or down.
        double x = Math.Abs(v.X) < StickAxisSnap ? 0 : v.X;
        double y = Math.Abs(v.Y) < StickAxisSnap ? 0 : v.Y;
        ref double fx = ref _fraction[index * 2];
        ref double fy = ref _fraction[index * 2 + 1];
        fx += x / length * pixels;
        fy += y / length * pixels;
        int dx = (int)fx, dy = (int)fy;
        fx -= dx;
        fy -= dy;
        _mouse.Move(dx, dy);
    }
}
