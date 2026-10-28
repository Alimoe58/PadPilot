using PadPilot.Core;

namespace PadPilot.Core.Tests;

public class InputReportParserTests
{
    // Builds a report with neutral sticks and released D-pad; `data` edits the 63-byte data block.
    private static byte[] MakeReport(ConnectionType connection, Action<byte[]>? data = null)
    {
        var block = new byte[63];
        block[0] = block[1] = block[2] = block[3] = 128;
        block[7] = 0x08;                 // D-pad released
        block[32] = block[36] = 0x80;    // no fingers
        data?.Invoke(block);

        if (connection == ConnectionType.Usb)
        {
            var usb = new byte[InputReportParser.UsbReportLength];
            usb[0] = InputReportParser.UsbReportId;
            block.CopyTo(usb, 1);
            return usb;
        }

        var bt = new byte[InputReportParser.BluetoothReportLength];
        bt[0] = InputReportParser.BluetoothReportId;
        bt[1] = 0x10;
        block.CopyTo(bt, 2);
        return bt;
    }

    [Theory]
    [InlineData(ConnectionType.Usb)]
    [InlineData(ConnectionType.Bluetooth)]
    public void ParsesSticksAndTriggers(ConnectionType connection)
    {
        var report = MakeReport(connection, d =>
        {
            d[0] = 0; d[1] = 255; d[2] = 10; d[3] = 20;
            d[4] = 100; d[5] = 200;
        });

        Assert.True(InputReportParser.TryParse(report, connection, out var s));
        Assert.Equal((0, 255, 10, 20, 100, 200), ((int)s!.LeftX, (int)s.LeftY, (int)s.RightX, (int)s.RightY, (int)s.L2, (int)s.R2));
        Assert.Equal(Buttons.None, s.Buttons);
    }

    [Fact]
    public void ParsesButtonsAndDiagonalDPad()
    {
        var report = MakeReport(ConnectionType.Usb, d =>
        {
            d[7] = 0x20 | 0x80 | 0x03;   // Cross, Triangle, D-pad down-right
            d[8] = 0x01 | 0x20 | 0x80;   // L1, Options, R3
            d[9] = 0x01 | 0x04;          // PS, Mute
        });

        Assert.True(InputReportParser.TryParse(report, ConnectionType.Usb, out var s));
        Assert.Equal(
            Buttons.Cross | Buttons.Triangle | Buttons.DPadDown | Buttons.DPadRight |
            Buttons.L1 | Buttons.Options | Buttons.R3 | Buttons.PS | Buttons.Mute,
            s!.Buttons);
    }

    [Fact]
    public void ParsesTouchPoints()
    {
        // Finger 1 at (1919, 1079), id 5; finger 2 lifted.
        var report = MakeReport(ConnectionType.Usb, d =>
        {
            d[32] = 0x05;
            d[33] = 0x7F;                // x low byte
            d[34] = 0x77;                // x high nibble 7, y low nibble 7
            d[35] = 0x43;                // y high byte
        });

        Assert.True(InputReportParser.TryParse(report, ConnectionType.Usb, out var s));
        Assert.Equal(new TouchPoint(true, 5, 1919, 1079), s!.Touch1);
        Assert.False(s.Touch2.Active);
    }

    [Fact]
    public void ParsesMotionAsSignedLittleEndian()
    {
        var report = MakeReport(ConnectionType.Usb, d =>
        {
            d[15] = 0xFF; d[16] = 0xFF;  // gyro X = -1
            d[21] = 0x00; d[22] = 0x20;  // accel X = 8192
        });

        Assert.True(InputReportParser.TryParse(report, ConnectionType.Usb, out var s));
        Assert.Equal(-1, s!.Gyro.X);
        Assert.Equal(8192, s.Accel.X);
    }

    [Theory]
    [InlineData(0x07, 75, ChargeStatus.Discharging)]
    [InlineData(0x1A, 100, ChargeStatus.Charging)]
    [InlineData(0x13, 35, ChargeStatus.Charging)]
    [InlineData(0x20, 100, ChargeStatus.Full)]
    [InlineData(0xF0, 0, ChargeStatus.Unknown)]
    public void ParsesBattery(byte status, int percent, ChargeStatus charge)
    {
        var report = MakeReport(ConnectionType.Usb, d => d[52] = status);

        Assert.True(InputReportParser.TryParse(report, ConnectionType.Usb, out var s));
        Assert.Equal(percent, s!.BatteryPercent);
        Assert.Equal(charge, s.Charge);
    }

    [Fact]
    public void IgnoresShortBluetoothReport()
    {
        var shortReport = new byte[InputReportParser.BluetoothReportLength];
        shortReport[0] = 0x01;

        Assert.False(InputReportParser.TryParse(shortReport, ConnectionType.Bluetooth, out _));
    }
}
