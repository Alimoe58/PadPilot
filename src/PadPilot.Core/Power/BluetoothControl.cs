using System.Runtime.InteropServices;

namespace PadPilot.Core.Power;

/// <summary>Ends a Bluetooth connection, which makes the DualSense turn itself off.</summary>
public static class BluetoothControl
{
    private const uint IoctlBthDisconnectDevice = 0x41000C;

    [StructLayout(LayoutKind.Sequential)]
    private struct FindRadioParams
    {
        public uint Size;
    }

    [DllImport("BluetoothApis.dll", SetLastError = true)]
    private static extern IntPtr BluetoothFindFirstRadio(ref FindRadioParams p, out IntPtr radio);

    [DllImport("BluetoothApis.dll", SetLastError = true)]
    private static extern bool BluetoothFindNextRadio(IntPtr find, out IntPtr radio);

    [DllImport("BluetoothApis.dll")]
    private static extern bool BluetoothFindRadioClose(IntPtr find);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(IntPtr device, uint code, ref ulong input, int inputSize,
        IntPtr output, int outputSize, out int returned, IntPtr overlapped);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    /// <summary>Asks every Bluetooth radio to drop the device with this address. True if one of them did.</summary>
    public static bool Disconnect(ulong address)
    {
        var p = new FindRadioParams { Size = (uint)Marshal.SizeOf<FindRadioParams>() };
        IntPtr find;
        try
        {
            find = BluetoothFindFirstRadio(ref p, out IntPtr radio);
            if (find == IntPtr.Zero)
                return false;
            bool done = false;
            do
            {
                done |= DeviceIoControl(radio, IoctlBthDisconnectDevice, ref address, sizeof(ulong), IntPtr.Zero, 0, out _, IntPtr.Zero);
                CloseHandle(radio);
            }
            while (!done && BluetoothFindNextRadio(find, out radio));
            BluetoothFindRadioClose(find);
            return done;
        }
        catch (DllNotFoundException)
        {
            return false; // no Bluetooth stack
        }
    }
}
