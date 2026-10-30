using System.Diagnostics;
using PadPilot.Core.Mouse;

namespace PadPilot.Core.Mapping;

/// <summary>Runs the button and stick mapping on the controller's read thread (no UI delay).</summary>
public sealed class MappingService : IDisposable
{
    private readonly object _lock = new();
    private readonly MappingEngine _engine;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _lastSample;
    private DualSenseDevice? _device;
    private volatile MappingSettings _settings = new();

    public MappingService(IKeyboardOutput? keyboard = null, IMouseOutput? mouse = null)
    {
        _engine = new MappingEngine(keyboard ?? new WindowsKeyboard(), mouse ?? new WindowsMouse());
        _engine.Special += a => Special?.Invoke(a);
    }

    public MappingSettings Settings { get => _settings; set => _settings = value; }

    /// <summary>Raised on the read thread when a button mapped to a special action is pressed.</summary>
    public event Action<SpecialAction>? Special;

    /// <summary>The input as the virtual controller should see it; called by the emulation.</summary>
    public ControllerState Transform(ControllerState state) => MappingEngine.Transform(state, _settings);

    public void Attach(DualSenseDevice device)
    {
        lock (_lock)
        {
            _device = device;
            device.StateReceived += OnStateReceived;
        }
    }

    /// <summary>Stops listening and lets go of every held key and mouse button.</summary>
    public void Detach()
    {
        lock (_lock)
        {
            if (_device != null)
                _device.StateReceived -= OnStateReceived;
            _device = null;
            _engine.Reset();
        }
    }

    private void OnStateReceived(ControllerState state)
    {
        lock (_lock)
        {
            var now = _clock.Elapsed;
            double seconds = Math.Clamp((now - _lastSample).TotalSeconds, 0, 0.05);
            _lastSample = now;
            _engine.Process(state, _settings, seconds);
        }
    }

    public void Dispose() => Detach();
}
