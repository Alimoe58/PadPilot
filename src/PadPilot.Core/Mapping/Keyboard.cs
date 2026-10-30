using System.Runtime.InteropServices;

namespace PadPilot.Core.Mapping;

/// <summary>Where key presses go. The real implementation is <see cref="WindowsKeyboard"/>; tests use a recorder.</summary>
public interface IKeyboardOutput
{
    /// <summary>Presses or releases a key, given as Windows virtual-key code.</summary>
    void Key(int virtualKey, bool down);
}

/// <summary>Virtual-key codes PadPilot uses itself.</summary>
public static class VirtualKeys
{
    public const int Shift = 0x10, Ctrl = 0x11, Alt = 0x12, LeftWin = 0x5B;
    public const int Left = 0x25, Up = 0x26, Right = 0x27, Down = 0x28;
    public const int A = 0x41, D = 0x44, S = 0x53, W = 0x57;
}

/// <summary>
/// Sends keys to Windows via SendInput. Keys go out as scan codes, which games that read the
/// keyboard directly also understand.
/// </summary>
public sealed class WindowsKeyboard : IKeyboardOutput
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventScanCode = 0x0008;
    private const uint MapVkToVsc = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    // INPUT is a union whose largest member (MOUSEINPUT) is 8 bytes longer than KEYBDINPUT on x64.
    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public KeyboardInput Keyboard;
        public long Padding;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    // Keys that sit in the "extended" block (arrows, navigation, right-hand modifiers, Windows keys).
    private static readonly HashSet<int> Extended =
        [0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x2D, 0x2E, 0x5B, 0x5C, 0x5D, 0x6F, 0x90, 0xA3, 0xA5];

    public void Key(int virtualKey, bool down)
    {
        ushort scan = (ushort)MapVirtualKey((uint)virtualKey, MapVkToVsc);
        uint flags = (down ? 0 : KeyEventKeyUp) | (Extended.Contains(virtualKey) ? KeyEventExtendedKey : 0);
        var input = new KeyboardInput { VirtualKey = (ushort)virtualKey, Flags = flags };
        if (scan != 0)
        {
            input.VirtualKey = 0;
            input.ScanCode = scan;
            input.Flags |= KeyEventScanCode;
        }
        SendInput(1, [new Input { Type = InputKeyboard, Keyboard = input }], Marshal.SizeOf<Input>());
    }
}
