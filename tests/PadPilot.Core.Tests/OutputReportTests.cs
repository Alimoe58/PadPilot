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
    public void SlopeRisesEvenlyAndHoldsTheEndStrength()
    {
        var b = TriggerEffect.Slope(2, 5, 1, 4).Bytes.ToArray();

        Assert.Equal(0x21, b[0]);
        uint active = (uint)(b[1] | b[2] << 8);
        Assert.Equal(0b11_1111_1100u, active);             // zones 2–9
        uint forces = (uint)(b[3] | b[4] << 8 | b[5] << 16 | b[6] << 24);
        int Force(int zone) => (int)((forces >> (3 * zone)) & 7) + 1;
        Assert.Equal([1, 2, 3, 4, 4, 4, 4, 4], Enumerable.Range(2, 8).Select(Force));
    }

    [Fact]
    public void BowPacksZonesAndForces()
    {
        var b = TriggerEffect.Bow(1, 6, 5, 8).Bytes.ToArray();
        // zones 1 and 6; strength 5 → 4, snap 8 → 7 in bits 3–5
        Assert.Equal(new byte[] { 0x22, 0x42, 0x00, 4 | 7 << 3, 0, 0, 0, 0, 0, 0, 0 }, b);
    }

    [Fact]
    public void GallopingPacksFeetAndFrequency()
    {
        var b = TriggerEffect.Galloping(0, 9, 2, 5, 3).Bytes.ToArray();
        Assert.Equal(new byte[] { 0x23, 0x01, 0x02, 5 | 2 << 3, 3, 0, 0, 0, 0, 0, 0 }, b);
    }

    [Fact]
    public void MachinePacksBothStrengthsFrequencyAndPeriod()
    {
        var b = TriggerEffect.Machine(1, 9, 8, 3, 12, 4).Bytes.ToArray();
        Assert.Equal(new byte[] { 0x27, 0x02, 0x02, 7 | 2 << 3, 12, 4, 0, 0, 0, 0, 0 }, b);
    }

    [Fact]
    public void NewEffectsKeepTheirRules()
    {
        // End is always after start; feet stay in order.
        var bow = TriggerEffect.Bow(8, 3, 1, 1).Bytes.ToArray();
        Assert.Equal((1 << 7) | (1 << 8), bow[1] | bow[2] << 8);
        var gallop = TriggerEffect.Galloping(0, 9, 6, 2, 0).Bytes.ToArray();
        Assert.Equal(7 | 6 << 3, gallop[3]);
        Assert.Equal(1, gallop[4]);
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
