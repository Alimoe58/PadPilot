namespace PadPilot.Core.Emulation;

/// <summary>
/// Builds the extended DualShock 4 input report that ViGEmBus accepts (63 bytes, layout of its
/// DS4_REPORT_EX): buttons, sticks, triggers, gyro, accelerometer, touchpad and battery.
/// </summary>
public static class Ds4Report
{
    public const int Size = 63;

    /// <summary>The DS4 touchpad is 1920 × 942 units; the DualSense one 1920 × 1080.</summary>
    public const int TouchHeight = 942;

    /// <param name="timestamp">Running counter in units of 16/3 µs (the DS4's clock), for gyro timing.</param>
    /// <param name="touchCounter">Running touch packet number.</param>
    public static byte[] Build(ProcessedInput input, ushort timestamp, byte touchCounter)
    {
        var r = new byte[Size];
        r[0] = InputProcessor.ToDs4Axis(input.Left.X);
        r[1] = InputProcessor.ToDs4Axis(input.Left.Y);
        r[2] = InputProcessor.ToDs4Axis(input.Right.X);
        r[3] = InputProcessor.ToDs4Axis(input.Right.Y);

        var b = input.Buttons;
        int buttons = DPad(b)
            | Bit(b, Buttons.Square, 4) | Bit(b, Buttons.Cross, 5) | Bit(b, Buttons.Circle, 6) | Bit(b, Buttons.Triangle, 7)
            | Bit(b, Buttons.L1, 8) | Bit(b, Buttons.R1, 9) | Bit(b, Buttons.L2, 10) | Bit(b, Buttons.R2, 11)
            | Bit(b, Buttons.Create, 12) | Bit(b, Buttons.Options, 13) | Bit(b, Buttons.L3, 14) | Bit(b, Buttons.R3, 15);
        r[4] = (byte)buttons;
        r[5] = (byte)(buttons >> 8);
        r[6] = (byte)(Bit(b, Buttons.PS, 0) | Bit(b, Buttons.TouchpadClick, 1));
        r[7] = InputProcessor.ToTrigger(input.L2);
        r[8] = InputProcessor.ToTrigger(input.R2);
        r[9] = (byte)timestamp;
        r[10] = (byte)(timestamp >> 8);
        r[11] = 0xFF; // sensor temperature on a real DS4; unused by games

        // Both controllers use the same motion units (about 16.4 per °/s and 8192 per g) and axis order.
        WriteShort(r, 12, input.Gyro.X);
        WriteShort(r, 14, input.Gyro.Y);
        WriteShort(r, 16, input.Gyro.Z);
        WriteShort(r, 18, input.Accel.X);
        WriteShort(r, 20, input.Accel.Y);
        WriteShort(r, 22, input.Accel.Z);

        // Battery 0–10, plus bit 4 while the cable is connected.
        r[29] = (byte)(Math.Clamp(input.BatteryPercent / 10, 0, 10) | (input.Charging ? 0x10 : 0));

        r[32] = 1; // one touch packet
        r[33] = touchCounter;
        WriteTouch(r, 34, input.Touch1);
        WriteTouch(r, 38, input.Touch2);
        return r;
    }

    /// <summary>One finger: tracking byte (bit 7 set = not touching) and 12-bit X / Y.</summary>
    private static void WriteTouch(byte[] r, int offset, TouchPoint t)
    {
        r[offset] = (byte)((t.Active ? 0 : 0x80) | (t.Id & 0x7F));
        int x = Math.Clamp(t.X, 0, 1919);
        int y = Math.Clamp((int)Math.Round(t.Y * (TouchHeight - 1) / 1079.0), 0, TouchHeight - 1);
        r[offset + 1] = (byte)x;
        r[offset + 2] = (byte)(((x >> 8) & 0x0F) | ((y & 0x0F) << 4));
        r[offset + 3] = (byte)(y >> 4);
    }

    private static void WriteShort(byte[] r, int offset, short value)
    {
        r[offset] = (byte)value;
        r[offset + 1] = (byte)(value >> 8);
    }

    private static int Bit(Buttons b, Buttons button, int bit) => (b & button) != 0 ? 1 << bit : 0;

    /// <summary>D-pad as hat value: 0 = up, clockwise to 7 = up-left, 8 = released.</summary>
    private static int DPad(Buttons b)
    {
        bool up = (b & Buttons.DPadUp) != 0, down = (b & Buttons.DPadDown) != 0;
        bool left = (b & Buttons.DPadLeft) != 0, right = (b & Buttons.DPadRight) != 0;
        return (up, down, left, right) switch
        {
            (true, _, true, _) => 7,
            (true, _, _, true) => 1,
            (_, true, true, _) => 5,
            (_, true, _, true) => 3,
            (true, _, _, _) => 0,
            (_, true, _, _) => 4,
            (_, _, true, _) => 6,
            (_, _, _, true) => 2,
            _ => 8,
        };
    }
}
