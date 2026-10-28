using System.Buffers.Binary;
using PadPilot.Core;

namespace PadPilot.Core.Tests;

public class OutputReportTests
{
    [Fact]
    public void Crc32MatchesStandardCheckValue()
    {
        // CRC-32 of "123456789" is 0xCBF43926; the first byte doubles as the seed.
        Assert.Equal(0xCBF43926u, Crc32.Compute((byte)'1', "23456789"u8));
    }

    private static readonly OutputState Sample = new()
    {
        LightbarRed = 0x11,
        LightbarGreen = 0x22,
        LightbarBlue = 0x33,
        PlayerLeds = PlayerLeds.Led3,
        PlayerLedBrightness = LedBrightness.Low,
        MicLed = MicLed.Pulse,
        RumbleLeft = 200,
        RumbleRight = 100,
        LeftTrigger = TriggerEffect.Weapon(2, 5, 8),
        RightTrigger = TriggerEffect.Resistance(0, 8),
    };

    [Fact]
    public void UsbReportHasExpectedLayout()
    {
        var r = OutputReportBuilder.Build(Sample, ConnectionType.Usb, 0, releaseStartupLight: false);

        Assert.Equal(48, r.Length);
        Assert.Equal(0x02, r[0]);
        Assert.Equal(100, r[1 + 2]);   // right motor
        Assert.Equal(200, r[1 + 3]);   // left motor
        Assert.Equal(2, r[1 + 8]);     // mic LED pulse
        Assert.Equal(0x21, r[1 + 10]); // right trigger mode
        Assert.Equal(0x25, r[1 + 21]); // left trigger mode
        Assert.Equal(2, r[1 + 42]);    // LED brightness low
        Assert.Equal(0x04, r[1 + 43]); // player LED 3
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33 }, r[(1 + 44)..(1 + 47)]);
    }

    [Fact]
    public void BluetoothReportHasHeaderSequenceAndValidCrc()
    {
        var r = OutputReportBuilder.Build(Sample, ConnectionType.Bluetooth, 5, releaseStartupLight: true);

        Assert.Equal(78, r.Length);
        Assert.Equal(0x31, r[0]);
        Assert.Equal(0x50, r[1]);      // sequence 5 in the upper nibble
        Assert.Equal(0x10, r[2]);
        Assert.Equal(0x11, r[3 + 44]); // lightbar red, shifted by the 3-byte BT header
        Assert.Equal(0x02, r[3 + 41]); // fade out startup light

        uint expected = Crc32.Compute(0xA2, r.AsSpan(0, 74));
        Assert.Equal(expected, BinaryPrimitives.ReadUInt32LittleEndian(r.AsSpan(74)));
    }

    [Fact]
    public void ResistanceCoversZonesFromStart()
    {
        var b = TriggerEffect.Resistance(7, 8).Bytes.ToArray();

        // Zones 7, 8, 9 active, each with force 7 (3 bits per zone).
        Assert.Equal(new byte[] { 0x21, 0x80, 0x03, 0x00, 0x00, 0xE0, 0x3F, 0, 0, 0, 0 }, b);
    }

    [Fact]
    public void WeaponEncodesStartEndAndStrength()
    {
        var b = TriggerEffect.Weapon(2, 5, 8).Bytes.ToArray();

        Assert.Equal(new byte[] { 0x25, 0x24, 0x00, 0x07, 0, 0, 0, 0, 0, 0, 0 }, b);
    }

    [Fact]
    public void VibrationSetsFrequency()
    {
        var b = TriggerEffect.Vibration(0, 8, 30).Bytes.ToArray();

        Assert.Equal(0x26, b[0]);
        Assert.Equal(0xFF, b[1]);
        Assert.Equal(0x03, b[2]);
        Assert.Equal(30, b[9]);
    }

    [Fact]
    public void ReleasedTurnsOffMotorsAndTriggersButKeepsLights()
    {
        var released = Sample.Released();

        Assert.Equal(0, released.RumbleLeft);
        Assert.Same(TriggerEffect.Off, released.RightTrigger);
        Assert.Equal(Sample.LightbarRed, released.LightbarRed);
    }
}
