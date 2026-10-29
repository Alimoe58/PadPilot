using System.Diagnostics;
using System.Text.Json;

namespace PadPilot.Core.Emulation;

/// <summary>
/// Hides the real DualSense from games through the HidHide driver (via its official command line tool),
/// so games only see the virtual controller. PadPilot registers itself as allowed first, otherwise it
/// would lose the controller too. Everything it changes is undone by <see cref="Release"/>.
/// </summary>
public sealed class HidHideController
{
    public static string CliPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "Nefarius Software Solutions", "HidHide", "x64", "HidHideCLI.exe");

    private readonly object _lock = new();

    /// <param name="hiddenInstance">Device PadPilot hid in an earlier run that may not have been released (crash).</param>
    /// <param name="cloakSetByUs">Whether that earlier run switched hiding on.</param>
    public HidHideController(string? hiddenInstance = null, bool cloakSetByUs = false)
    {
        HiddenInstance = hiddenInstance;
        CloakSetByUs = cloakSetByUs;
    }

    public static bool IsInstalled => File.Exists(CliPath);

    /// <summary>Device instance path currently hidden by PadPilot, or null.</summary>
    public string? HiddenInstance { get; private set; }

    /// <summary>True if PadPilot switched HidHide's hiding on (and should switch it off again).</summary>
    public bool CloakSetByUs { get; private set; }

    public string? LastError { get; private set; }

    /// <summary>Hides the controller with the given HID device path from every app except <paramref name="allowedApp"/>.</summary>
    public bool Hide(string devicePath, string allowedApp)
    {
        lock (_lock)
        {
            LastError = null;
            try
            {
                string? instance = FindInstancePath(devicePath);
                if (instance == null)
                {
                    LastError = "Controller not found in HidHide's device list.";
                    return false;
                }
                if (HiddenInstance != null && !HiddenInstance.Equals(instance, StringComparison.OrdinalIgnoreCase))
                    ReleaseLocked();

                bool cloakOn = Run("--cloak-state").Trim() == "--cloak-on";
                // Register first, then hide: PadPilot must never lose access to its own controller.
                Run("--app-reg", allowedApp, "--dev-hide", instance, "--cloak-on");
                HiddenInstance = instance;
                CloakSetByUs |= !cloakOn;
                return true;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException or System.ComponentModel.Win32Exception)
            {
                LastError = ex.Message;
                return false;
            }
        }
    }

    /// <summary>Gives the controller back to all apps and restores HidHide's previous on/off state.</summary>
    public bool Release()
    {
        lock (_lock)
        {
            LastError = null;
            try
            {
                ReleaseLocked();
                return true;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                LastError = ex.Message;
                return false;
            }
        }
    }

    private void ReleaseLocked()
    {
        if (HiddenInstance != null)
        {
            Run("--dev-unhide", HiddenInstance);
            HiddenInstance = null;
        }
        // Only switch hiding off if we switched it on and nothing else (set up by the user) is hidden.
        if (CloakSetByUs && !Run("--dev-list").Contains("--dev-hide", StringComparison.Ordinal))
            Run("--cloak-off");
        CloakSetByUs = false;
    }

    /// <summary>Maps HidSharp's device path (symbolic link) to the device instance path HidHide works with.</summary>
    private static string? FindInstancePath(string devicePath)
    {
        using var doc = JsonDocument.Parse(Run("--dev-all").Trim());
        foreach (var group in doc.RootElement.EnumerateArray())
        {
            foreach (var device in group.GetProperty("devices").EnumerateArray())
            {
                if (device.GetProperty("symbolicLink").GetString() is string link &&
                    link.Equals(devicePath, StringComparison.OrdinalIgnoreCase))
                {
                    return device.GetProperty("deviceInstancePath").GetString();
                }
            }
        }
        return null;
    }

    private static string Run(params string[] args)
    {
        var info = new ProcessStartInfo(CliPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
            info.ArgumentList.Add(arg);

        using var process = Process.Start(info) ?? throw new InvalidOperationException("HidHideCLI could not be started.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(10_000))
        {
            process.Kill();
            throw new InvalidOperationException("HidHideCLI did not respond.");
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"HidHideCLI failed ({process.ExitCode}): {error.Trim()}");
        return output;
    }
}
