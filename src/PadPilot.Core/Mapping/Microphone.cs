using System.Runtime.InteropServices;

namespace PadPilot.Core.Mapping;

/// <summary>
/// Mutes the default Windows microphone (for normal apps and for calls, if those are different
/// devices) through the Core Audio API, like the mute button in the Windows sound settings.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
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
            return WithEndpoint(Console, v => v.GetMute(out bool muted) == 0 ? muted : (bool?)null);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
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
                WithEndpoint(role, v => v.SetMute(!muted, IntPtr.Zero) == 0 ? true : (bool?)null);
            return !muted;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            return null;
        }
    }

    /// <summary>Runs <paramref name="use"/> on the default microphone's volume control and frees the COM objects right after (this runs every second).</summary>
    private static bool? WithEndpoint(int role, Func<IAudioEndpointVolume, bool?> use)
    {
        object? enumerator = null, device = null, endpoint = null;
        try
        {
            enumerator = new DeviceEnumerator();
            if (((IMMDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(Capture, role, out var d) != 0 || d == null)
                return null;
            device = d;
            var id = EndpointVolumeId;
            if (d.Activate(ref id, ClsctxAll, IntPtr.Zero, out endpoint) != 0 || endpoint is not IAudioEndpointVolume volume)
                return null;
            return use(volume);
        }
        finally
        {
            foreach (var com in new[] { endpoint, device, enumerator })
                if (com != null && Marshal.IsComObject(com))
                    Marshal.ReleaseComObject(com);
        }
    }
}
