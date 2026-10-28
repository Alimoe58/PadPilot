using System.Buffers.Binary;

namespace PadPilot.Core;

/// <summary>
/// Builds DualSense output reports. Layout of the shared 47-byte block follows the Linux
/// hid-playstation driver; see docs/PLAN.md.
/// </summary>
public static class OutputReportBuilder
{
    public const byte UsbReportId = 0x02;
    public const byte BluetoothReportId = 0x31;
    public const int UsbReportLength = 48;
    public const int BluetoothReportLength = 78;

    private const byte BluetoothTag = 0x10;

    // valid_flag0
    private const byte Flag0CompatibleVibration = 0x01;
    private const byte Flag0HapticsSelect = 0x02;
    private const byte Flag0RightTrigger = 0x04;
    private const byte Flag0LeftTrigger = 0x08;

    // valid_flag1
    private const byte Flag1MicLed = 0x01;
    private const byte Flag1Lightbar = 0x04;
    private const byte Flag1PlayerLeds = 0x10;

    // valid_flag2
    private const byte Flag2LedBrightness = 0x01;
    private const byte Flag2LightbarSetup = 0x02;
    private const byte Flag2CompatibleVibration2 = 0x04;

    private const byte LightbarSetupLightOut = 0x02;

    // Offsets inside the shared block
    private const int MotorRight = 2;
    private const int MotorLeft = 3;
    private const int MicLedOffset = 8;
    private const int RightTriggerOffset = 10;
    private const int LeftTriggerOffset = 21;
    private const int ValidFlag2 = 38;
    private const int LightbarSetup = 41;
    private const int PlayerLedBrightnessOffset = 42;
    private const int PlayerLedsOffset = 43;
    private const int LightbarRgb = 44;

    /// <param name="sequence">Bluetooth only: 4-bit counter, incremented by the caller per report.</param>
    /// <param name="releaseStartupLight">
    /// Fades out the blue light the controller shows after pairing; needed once before the
    /// lightbar colour can be controlled.
    /// </param>
    public static byte[] Build(OutputState state, ConnectionType connection, byte sequence, bool releaseStartupLight, int reportLength = 0)
    {
        byte[] report;
        int offset;
        if (connection == ConnectionType.Usb)
        {
            report = new byte[Math.Max(reportLength, UsbReportLength)];
            report[0] = UsbReportId;
            offset = 1;
        }
        else
        {
            report = new byte[BluetoothReportLength];
            report[0] = BluetoothReportId;
            report[1] = (byte)((sequence & 0x0F) << 4);
            report[2] = BluetoothTag;
            offset = 3;
        }

        var c = report.AsSpan(offset, 47);

        // Rumble: newer firmware listens to the flag2 bit, older to the flag0 bit; setting both works for either.
        c[0] = Flag0CompatibleVibration | Flag0HapticsSelect | Flag0RightTrigger | Flag0LeftTrigger;
        c[1] = Flag1MicLed | Flag1Lightbar | Flag1PlayerLeds;
        c[ValidFlag2] = Flag2LedBrightness | Flag2CompatibleVibration2;

        c[MotorRight] = state.RumbleRight;
        c[MotorLeft] = state.RumbleLeft;
        c[MicLedOffset] = (byte)state.MicLed;

        state.RightTrigger.Bytes.CopyTo(c[RightTriggerOffset..]);
        state.LeftTrigger.Bytes.CopyTo(c[LeftTriggerOffset..]);

        if (releaseStartupLight)
        {
            c[ValidFlag2] |= Flag2LightbarSetup;
            c[LightbarSetup] = LightbarSetupLightOut;
        }

        c[PlayerLedBrightnessOffset] = (byte)state.PlayerLedBrightness;
        c[PlayerLedsOffset] = (byte)state.PlayerLeds;
        c[LightbarRgb] = state.LightbarRed;
        c[LightbarRgb + 1] = state.LightbarGreen;
        c[LightbarRgb + 2] = state.LightbarBlue;

        if (connection == ConnectionType.Bluetooth)
        {
            uint crc = Crc32.Compute(Crc32.OutputSeed, report.AsSpan(0, BluetoothReportLength - 4));
            BinaryPrimitives.WriteUInt32LittleEndian(report.AsSpan(BluetoothReportLength - 4), crc);
        }

        return report;
    }
}
