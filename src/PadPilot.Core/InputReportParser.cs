using System.Buffers.Binary;

namespace PadPilot.Core;

/// <summary>
/// Turns raw DualSense input reports into <see cref="ControllerState"/>.
/// Layout follows the Linux hid-playstation driver; see docs/PLAN.md.
/// </summary>
public static class InputReportParser
{
    public const byte UsbReportId = 0x01;
    public const byte BluetoothReportId = 0x31;

    public const int UsbReportLength = 64;
    public const int BluetoothReportLength = 78;

    // Size of the common data block shared by USB and Bluetooth reports.
    private const int DataLength = 63;

    public static bool TryParse(ReadOnlySpan<byte> report, ConnectionType connection, out ControllerState? state)
    {
        state = null;
        if (report.IsEmpty)
            return false;

        int offset;
        if (connection == ConnectionType.Usb && report[0] == UsbReportId)
            offset = 1;
        else if (connection == ConnectionType.Bluetooth && report[0] == BluetoothReportId)
            offset = 2; // byte 1 is a sequence/tag byte
        else
            return false; // e.g. the short Bluetooth report before full mode is enabled

        if (report.Length < offset + DataLength)
            return false;

        state = ParseData(report.Slice(offset, DataLength));
        return true;
    }

    private static ControllerState ParseData(ReadOnlySpan<byte> d)
    {
        byte b0 = d[7], b1 = d[8], b2 = d[9];

        var buttons = HatToDPad(b0 & 0x0F);
        if ((b0 & 0x10) != 0) buttons |= Buttons.Square;
        if ((b0 & 0x20) != 0) buttons |= Buttons.Cross;
        if ((b0 & 0x40) != 0) buttons |= Buttons.Circle;
        if ((b0 & 0x80) != 0) buttons |= Buttons.Triangle;
        if ((b1 & 0x01) != 0) buttons |= Buttons.L1;
        if ((b1 & 0x02) != 0) buttons |= Buttons.R1;
        if ((b1 & 0x04) != 0) buttons |= Buttons.L2;
        if ((b1 & 0x08) != 0) buttons |= Buttons.R2;
        if ((b1 & 0x10) != 0) buttons |= Buttons.Create;
        if ((b1 & 0x20) != 0) buttons |= Buttons.Options;
        if ((b1 & 0x40) != 0) buttons |= Buttons.L3;
        if ((b1 & 0x80) != 0) buttons |= Buttons.R3;
        if ((b2 & 0x01) != 0) buttons |= Buttons.PS;
        if ((b2 & 0x02) != 0) buttons |= Buttons.TouchpadClick;
        if ((b2 & 0x04) != 0) buttons |= Buttons.Mute;

        var (percent, charge) = ParseBattery(d[52]);

        return new ControllerState
        {
            LeftX = d[0],
            LeftY = d[1],
            RightX = d[2],
            RightY = d[3],
            L2 = d[4],
            R2 = d[5],
            Buttons = buttons,
            Gyro = ReadVector(d.Slice(15, 6)),
            Accel = ReadVector(d.Slice(21, 6)),
            Touch1 = ParseTouch(d.Slice(32, 4)),
            Touch2 = ParseTouch(d.Slice(36, 4)),
            BatteryPercent = percent,
            Charge = charge,
        };
    }

    private static Buttons HatToDPad(int hat) => hat switch
    {
        0 => Buttons.DPadUp,
        1 => Buttons.DPadUp | Buttons.DPadRight,
        2 => Buttons.DPadRight,
        3 => Buttons.DPadDown | Buttons.DPadRight,
        4 => Buttons.DPadDown,
        5 => Buttons.DPadDown | Buttons.DPadLeft,
        6 => Buttons.DPadLeft,
        7 => Buttons.DPadUp | Buttons.DPadLeft,
        _ => Buttons.None, // 8 = released
    };

    private static Vector3S ReadVector(ReadOnlySpan<byte> s) => new(
        BinaryPrimitives.ReadInt16LittleEndian(s),
        BinaryPrimitives.ReadInt16LittleEndian(s[2..]),
        BinaryPrimitives.ReadInt16LittleEndian(s[4..]));

    private static TouchPoint ParseTouch(ReadOnlySpan<byte> s)
    {
        // Bit 7 set means the finger is NOT touching.
        bool active = (s[0] & 0x80) == 0;
        byte id = (byte)(s[0] & 0x7F);
        int x = s[1] | ((s[2] & 0x0F) << 8);
        int y = (s[2] >> 4) | (s[3] << 4);
        return new TouchPoint(active, id, x, y);
    }

    private static (int Percent, ChargeStatus Charge) ParseBattery(byte status)
    {
        int level = status & 0x0F;
        return (status >> 4) switch
        {
            0x0 => (Math.Min(level * 10 + 5, 100), ChargeStatus.Discharging),
            0x1 => (Math.Min(level * 10 + 5, 100), ChargeStatus.Charging),
            0x2 => (100, ChargeStatus.Full),
            _ => (0, ChargeStatus.Unknown),
        };
    }
}
