using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PadPilot.Core.Games;

/// <summary>A program with a visible window, for picking a game to link.</summary>
public sealed record RunningApp(string Exe, string Title)
{
    public override string ToString() => $"{Exe}  –  {Title}";
}

/// <summary>Which programs run and which one is in the foreground, identified by their file name.</summary>
public static class RunningApps
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref uint size);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    /// <summary>File name of the program that owns the foreground window (e.g. "game.exe"); null for PadPilot itself.</summary>
    public static string? ForegroundExe()
    {
        IntPtr window = GetForegroundWindow();
        if (window == IntPtr.Zero)
            return null;
        GetWindowThreadProcessId(window, out uint pid);
        return pid == 0 || pid == Environment.ProcessId ? null : ExeOf(pid);
    }

    public static bool IsRunning(string exe)
    {
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe));
        foreach (var p in processes)
            p.Dispose();
        return processes.Length > 0;
    }

    /// <summary>Programs with a titled main window, except PadPilot, sorted by file name.</summary>
    public static IReadOnlyList<RunningApp> WithWindows()
    {
        var apps = new List<RunningApp>();
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    if (p.Id == Environment.ProcessId || p.MainWindowHandle == IntPtr.Zero || p.MainWindowTitle.Length == 0)
                        continue;
                    if (ExeOf((uint)p.Id) is string exe)
                        apps.Add(new RunningApp(exe, p.MainWindowTitle));
                }
                catch (InvalidOperationException)
                {
                    // The process ended while we looked at it.
                }
            }
        }
        return apps
            .DistinctBy(a => a.Exe, StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a.Exe, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>File name of a process; limited query rights also work for elevated programs.</summary>
    private static string? ExeOf(uint pid)
    {
        IntPtr handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero)
            return null;
        try
        {
            var name = new StringBuilder(1024);
            uint size = (uint)name.Capacity;
            return QueryFullProcessImageName(handle, 0, name, ref size) ? Path.GetFileName(name.ToString()) : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }
}
