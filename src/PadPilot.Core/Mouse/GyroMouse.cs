namespace PadPilot.Core.Mouse;

public enum GyroActivation
{
    Always,
    HoldL1,
    HoldR1,
    HoldL2,
    TouchpadTouched,
}

public sealed record GyroMouseSettings
{
    public bool Enabled { get; init; }
    public GyroActivation Activation { get; init; } = GyroActivation.HoldL2;

    /// <summary>Mouse pixels per degree the controller is turned.</summary>
    public double PixelsPerDegree { get; init; } = 5;

    public bool InvertX { get; init; }
    public bool InvertY { get; init; }

    /// <summary>Rotation speeds below this (degrees per second) are treated as hand jitter and ignored.</summary>
    public double DeadzoneDegreesPerSecond { get; init; } = 1.0;

    /// <summary>Gyro reading of the controller lying still, subtracted from every sample.</summary>
    public Vector3S Bias { get; init; }
}

/// <summary>Moves the mouse with the controller's rotation: turning left/right moves along X, tilting up/down along Y.</summary>
public sealed class GyroMouse
{
    /// <summary>Approximate raw gyro units per degree per second (DualSense range ±2000 °/s in 16 bit).</summary>
    public const double RawPerDegreePerSecond = 16.4;

    private readonly IMouseOutput _mouse;
    private double _fracX, _fracY;

    public GyroMouse(IMouseOutput mouse) => _mouse = mouse;

    public static bool IsActive(ControllerState s, GyroMouseSettings cfg) => cfg.Enabled && cfg.Activation switch
    {
        GyroActivation.Always => true,
        GyroActivation.HoldL1 => s.IsPressed(Buttons.L1),
        GyroActivation.HoldR1 => s.IsPressed(Buttons.R1),
        GyroActivation.HoldL2 => s.L2 > 30,
        GyroActivation.TouchpadTouched => s.Touch1.Active,
        _ => false,
    };

    /// <param name="seconds">Time since the previous report.</param>
    public void Process(ControllerState s, GyroMouseSettings cfg, double seconds)
    {
        if (!IsActive(s, cfg))
        {
            _fracX = _fracY = 0;
            return;
        }

        // Gyro Y is yaw (turning left/right), X is pitch (tilting up/down).
        double yaw = ToDegreesPerSecond(s.Gyro.Y - cfg.Bias.Y, cfg);
        double pitch = ToDegreesPerSecond(s.Gyro.X - cfg.Bias.X, cfg);

        _fracX += -yaw * seconds * cfg.PixelsPerDegree * (cfg.InvertX ? -1 : 1);
        _fracY += -pitch * seconds * cfg.PixelsPerDegree * (cfg.InvertY ? -1 : 1);
        int mx = (int)_fracX, my = (int)_fracY;
        _mouse.Move(mx, my);
        _fracX -= mx;
        _fracY -= my;
    }

    private static double ToDegreesPerSecond(int raw, GyroMouseSettings cfg)
    {
        double dps = raw / RawPerDegreePerSecond;
        return Math.Abs(dps) < cfg.DeadzoneDegreesPerSecond ? 0 : dps;
    }
}

/// <summary>Averages the gyro while the controller lies still, giving the bias for <see cref="GyroMouseSettings.Bias"/>.</summary>
public sealed class GyroCalibration
{
    private long _sumX, _sumY, _sumZ;
    private int _count;

    public int SampleCount => _count;

    public void Add(Vector3S gyro)
    {
        _sumX += gyro.X;
        _sumY += gyro.Y;
        _sumZ += gyro.Z;
        _count++;
    }

    public Vector3S Result => _count == 0
        ? default
        : new((short)Math.Round(_sumX / (double)_count), (short)Math.Round(_sumY / (double)_count), (short)Math.Round(_sumZ / (double)_count));
}
