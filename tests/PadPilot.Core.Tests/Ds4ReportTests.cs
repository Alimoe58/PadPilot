using PadPilot.Core.Emulation;

namespace PadPilot.Core.Tests;

public class Ds4ReportTests
{
    private static ProcessedInput Input(Buttons buttons = Buttons.None) =>
        new(new StickVector(0, 0), new StickVector(0, 0), 0, 0, buttons);

    [Fact]
    public void ReportHasTheSizeViGEmExpects()
    {
        Assert.Equal(63, Ds4Report.Build(Input(), 0, 0).Length);
    }

    [Fact]
    public void SticksTriggersAndNeutralDPad()
    {
        var input = new ProcessedInput(new StickVector(-1, 1), new StickVector(1, -1), 1, 0.5, Buttons.None);
        var r = Ds4Report.Build(input, 0, 0);
        Assert.Equal(new byte[] { 0, 255, 255, 0 }, r[..4]);
        Assert.Equal(8, r[4] & 0x0F);       // d-pad released
        Assert.Equal(255, r[7]);
        Assert.Equal(128, r[8]);
    }

    [Fact]
    public void ButtonsLandOnTheirBits()
    {
        var r = Ds4Report.Build(Input(Buttons.Cross | Buttons.Triangle | Buttons.R1 | Buttons.Options | Buttons.R3
                                      | Buttons.DPadUp | Buttons.DPadRight | Buttons.PS | Buttons.TouchpadClick), 0, 0);
        int word = r[4] | r[5] << 8;
        Assert.Equal(1, word & 0x0F);                       // up-right
        Assert.Equal((1 << 5) | (1 << 7) | (1 << 9) | (1 << 13) | (1 << 15), word & ~0x0F);
        Assert.Equal(0b11, r[6]);
    }

    [Fact]
    public void MotionAndTimestampArePassedThrough()
    {
        var input = Input() with { Gyro = new Vector3S(-2, 300, 4), Accel = new Vector3S(5, -8192, 7) };
        var r = Ds4Report.Build(input, 0x1234, 0);
        Assert.Equal(new byte[] { 0x34, 0x12 }, r[9..11]);
        Assert.Equal(-2, BitConverter.ToInt16(r, 12));
        Assert.Equal(300, BitConverter.ToInt16(r, 14));
        Assert.Equal(-8192, BitConverter.ToInt16(r, 20));
    }

    [Fact]
    public void TouchIsScaledToTheDs4Pad()
    {
        var input = Input() with
        {
            Touch1 = new TouchPoint(true, 5, 1919, 1079),
            Touch2 = new TouchPoint(false, 6, 100, 0),
        };
        var r = Ds4Report.Build(input, 0, 42);
        Assert.Equal(1, r[32]);
        Assert.Equal(42, r[33]);

        Assert.Equal(5, r[34]);                                        // touching, id 5
        int x = r[35] | (r[36] & 0x0F) << 8, y = r[36] >> 4 | r[37] << 4;
        Assert.Equal(1919, x);
        Assert.Equal(941, y);

        Assert.Equal(0x80 | 6, r[38]);                                // second finger lifted
    }

    [Fact]
    public void BatteryLevelAndCable()
    {
        var r = Ds4Report.Build(Input() with { BatteryPercent = 75, Charging = true }, 0, 0);
        Assert.Equal(7 | 0x10, r[29]);
    }
}
