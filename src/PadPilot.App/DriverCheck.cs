using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace PadPilot.App;

/// <summary>State of one of the drivers PadPilot can use.</summary>
public sealed record DriverInfo(string Name, bool Installed, string? Version, string WingetId, string Url)
{
    public string Summary => Installed ? $"{Name} {Version ?? "(version unknown)"}" : $"{Name} not installed";
}

/// <summary>
/// Looks up the drivers emulation depends on: ViGEmBus (virtual controllers, required for emulation)
/// and HidHide (hides the real controller, optional).
/// </summary>
public static class DriverCheck
{
    public static DriverInfo ViGEmBus() => Detect("ViGEmBus", "ViGEm.ViGEmBus", "https://github.com/nefarius/ViGEmBus/releases");
    public static DriverInfo HidHide() => Detect("HidHide", "Nefarius.HidHide", "https://github.com/nefarius/HidHide/releases");

    /// <summary>Installs a driver through winget in a visible console; Windows asks for admin rights.</summary>
    public static void Install(DriverInfo driver) =>
        Process.Start(new ProcessStartInfo("winget",
            $"install --id {driver.WingetId} --exact --accept-source-agreements --accept-package-agreements")
        { UseShellExecute = true });

    public static void OpenDownloadPage(DriverInfo driver) =>
        Process.Start(new ProcessStartInfo(driver.Url) { UseShellExecute = true });

    private static DriverInfo Detect(string service, string wingetId, string url)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{service}");
            if (key?.GetValue("ImagePath") is not string imagePath)
                return new DriverInfo(service, false, null, wingetId, url);

            // "\SystemRoot\System32\drivers\X.sys" → C:\Windows\System32\drivers\X.sys
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string path = imagePath
                .Replace(@"\SystemRoot", windows, StringComparison.OrdinalIgnoreCase)
                .Replace(@"\??\", "", StringComparison.Ordinal);
            if (path.StartsWith(@"System32\", StringComparison.OrdinalIgnoreCase))
                path = Path.Combine(windows, path);

            string? version = File.Exists(path) ? FileVersionInfo.GetVersionInfo(path).FileVersion : null;
            return new DriverInfo(service, true, version, wingetId, url);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return new DriverInfo(service, false, null, wingetId, url);
        }
    }
}
