using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;

namespace PadPilot.Core.Emulation;

public enum EmulationStatus
{
    Off,
    WaitingForController,
    Active,
    DriverMissing,
    Error,
}

/// <summary>
/// Feeds the DualSense input into a virtual Xbox 360 or DualShock 4 controller. Input is forwarded
/// on the controller's read thread (no UI delay); rumble from games is reported back via <see cref="RumbleRequested"/>.
/// </summary>
public sealed class EmulationService : IDisposable
{
    private readonly object _lock = new();
    private ViGEmClient? _client;
    private IVirtualPad? _pad;
    private DualSenseDevice? _device;
    private EmulationMode _mode;
    private volatile DeadzoneProfile _deadzones = new();

    public EmulationStatus Status { get; private set; } = EmulationStatus.Off;
    public string? ErrorMessage { get; private set; }

    /// <summary>Raised when <see cref="Status"/> changes (on the calling thread).</summary>
    public event Action? StatusChanged;

    /// <summary>Rumble requested by a game (large, small), raised on a ViGEm thread.</summary>
    public event Action<byte, byte>? RumbleRequested;

    public DeadzoneProfile Deadzones
    {
        get => _deadzones;
        set => _deadzones = value;
    }

    public EmulationMode Mode
    {
        get => _mode;
        set
        {
            lock (_lock)
            {
                if (_mode == value && Status != EmulationStatus.Error)
                    return;
                _mode = value;
                RecreatePad();
            }
            StatusChanged?.Invoke();
        }
    }

    /// <summary>Tries to create the virtual controller again, e.g. after the driver was installed.</summary>
    public void Retry()
    {
        lock (_lock)
            RecreatePad();
        StatusChanged?.Invoke();
    }

    public void Attach(DualSenseDevice device)
    {
        lock (_lock)
        {
            _device = device;
            device.StateReceived += OnStateReceived;
            RecreatePad();
        }
        StatusChanged?.Invoke();
    }

    /// <summary>Unplugs the virtual controller too, so games don't keep a stuck pad.</summary>
    public void Detach()
    {
        lock (_lock)
        {
            if (_device != null)
                _device.StateReceived -= OnStateReceived;
            _device = null;
            RecreatePad();
        }
        StatusChanged?.Invoke();
    }

    private void OnStateReceived(ControllerState state)
    {
        lock (_lock)
        {
            if (_pad == null)
                return;
            try
            {
                _pad.Submit(InputProcessor.Process(state, _deadzones));
            }
            catch (VigemTargetNotPluggedInException)
            {
                // The pad is being removed; the next RecreatePad will sort it out.
            }
        }
    }

    // Call with _lock held.
    private void RecreatePad()
    {
        if (_pad != null)
        {
            _pad.RumbleRequested -= OnRumble;
            try { _pad.Dispose(); } catch (Exception ex) when (ex is VigemTargetNotPluggedInException or VigemRemovalFailedException) { }
            _pad = null;
            RumbleRequested?.Invoke(0, 0);
        }

        ErrorMessage = null;
        if (_mode == EmulationMode.Off)
        {
            Status = EmulationStatus.Off;
            return;
        }
        if (_device == null)
        {
            Status = EmulationStatus.WaitingForController;
            return;
        }

        try
        {
            _client ??= new ViGEmClient();
            _pad = _mode == EmulationMode.Xbox360 ? new Xbox360Pad(_client) : new DualShock4Pad(_client);
            _pad.RumbleRequested += OnRumble;
            Status = EmulationStatus.Active;
        }
        catch (VigemBusNotFoundException)
        {
            Status = EmulationStatus.DriverMissing;
        }
        catch (Exception ex) when (ex.GetType().Namespace == typeof(VigemBusNotFoundException).Namespace)
        {
            Status = EmulationStatus.Error;
            ErrorMessage = ex.Message;
        }
    }

    private void OnRumble(byte large, byte small) => RumbleRequested?.Invoke(large, small);

    public void Dispose()
    {
        lock (_lock)
        {
            _mode = EmulationMode.Off;
            if (_device != null)
                _device.StateReceived -= OnStateReceived;
            _device = null;
            RecreatePad();
            _client?.Dispose();
            _client = null;
        }
    }
}
