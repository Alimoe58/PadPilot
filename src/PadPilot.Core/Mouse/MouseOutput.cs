using System.Runtime.InteropServices;

namespace PadPilot.Core.Mouse;

public enum MouseButton
{
    Left,
    Right,
}

/// <summary>Where mouse actions go. The real implementation is <see cref="WindowsMouse"/>; tests use a recorder.</summary>
public interface IMouseOutput
{
    void Move(int dx, int dy);
    void Button(MouseButton button, bool down);

    /// <summary>Wheel movement; 120 is one notch, positive scrolls up.</summary>
    void Wheel(int delta);
}

/// <summary>Sends mouse input to Windows via SendInput (like a real mouse).</summary>
public sealed class WindowsMouse : IMouseOutput
{
    private const uint InputMouse = 0;
    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventWheel = 0x0800;

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    // INPUT is a union; MOUSEINPUT is its largest member, so this layout has the right size on x64.
    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public MouseInput Mouse;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    public void Move(int dx, int dy)
    {
        if (dx != 0 || dy != 0)
            Send(new MouseInput { Dx = dx, Dy = dy, Flags = MouseEventMove });
    }

    public void Button(MouseButton button, bool down) => Send(new MouseInput
    {
        Flags = (button, down) switch
        {
            (MouseButton.Left, true) => MouseEventLeftDown,
            (MouseButton.Left, false) => MouseEventLeftUp,
            (MouseButton.Right, true) => MouseEventRightDown,
            _ => MouseEventRightUp,
        },
    });

    public void Wheel(int delta)
    {
        if (delta != 0)
            Send(new MouseInput { MouseData = unchecked((uint)delta), Flags = MouseEventWheel });
    }

    private static void Send(MouseInput mouse) =>
        SendInput(1, [new Input { Type = InputMouse, Mouse = mouse }], Marshal.SizeOf<Input>());
}
