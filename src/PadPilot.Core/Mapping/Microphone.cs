using System.Runtime.InteropServices;

namespace PadPilot.Core.Mapping;

/// <summary>
/// Mutes the default Windows microphone (for normal apps and for calls, if those are different
/// devices) through the Core Audio API, like the mute button in the Windows sound settings.
/// </summary>
public static class Microphone
{
    private const int Capture = 1;                 // EDataFlow.eCapture
    private const int Console = 0, Communications = 2;  // ERole
    private const int ClsctxAll = 0x17;
    private static readonly Guid EndpointVolumeId = new("5CDF2C82-841E-4546-9722-0CF74078229A");

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class DeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid id, int context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object endpoint);
    }

    // Only the methods up to GetMute are declared; the order must match the real interface.
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float level, IntPtr context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, IntPtr context);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, IntPtr context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, IntPtr context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, IntPtr context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    /// <summary>Whether the default microphone is muted; null without a microphone.</summary>
    public static bool? IsMuted()
    {
        try
        {
            return Endpoint(Console) is IAudioEndpointVolume volume && volume.GetMute(out bool muted) == 0 ? muted : null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    /// <summary>Mutes or unmutes; returns the new state, or null without a microphone.</summary>
    public static bool? Toggle()
    {
        if (IsMuted() is not bool muted)
            return null;
        try
        {
            foreach (int role in new[] { Console, Communications })
                (Endpoint(role) as IAudioEndpointVolume)?.SetMute(!muted, IntPtr.Zero);
            return !muted;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static object? Endpoint(int role)
    {
        var enumerator = (IMMDeviceEnumerator)new DeviceEnumerator();
        if (enumerator.GetDefaultAudioEndpoint(Capture, role, out var device) != 0 || device == null)
            return null;
        var id = EndpointVolumeId;
        return device.Activate(ref id, ClsctxAll, IntPtr.Zero, out object endpoint) == 0 ? endpoint : null;
    }
}
