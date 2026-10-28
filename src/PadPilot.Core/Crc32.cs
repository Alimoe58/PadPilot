namespace PadPilot.Core;

/// <summary>
/// Standard CRC-32 (polynomial 0xEDB88320). Bluetooth reports are checksummed over a
/// one-byte seed (0xA2 for output, 0xA1 for input) followed by the report bytes.
/// </summary>
public static class Crc32
{
    public const byte OutputSeed = 0xA2;

    private static readonly uint[] Table = BuildTable();

    public static uint Compute(byte seed, ReadOnlySpan<byte> data)
    {
        uint crc = Update(0xFFFFFFFF, seed);
        foreach (byte b in data)
            crc = Update(crc, b);
        return ~crc;
    }

    private static uint Update(uint crc, byte b) => Table[(crc ^ b) & 0xFF] ^ (crc >> 8);

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }
}
