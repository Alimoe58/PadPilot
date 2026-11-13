using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace PadPilot.App;

public partial class App : Application
{
    // One PadPilot per Windows user session: two copies would fight over the controller, HidHide and the mod port.
    private const string InstanceName = @"Local\PadPilot.SingleInstance";
    private const string ShowWindowSignal = @"Local\PadPilot.ShowWindow";

    private Mutex? _instance;
    private EventWaitHandle? _showWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnUiException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                CrashLog.Write(ex, "background thread");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashLog.Write(args.Exception, "task");
            args.SetObserved();
        };

        bool fromAutostart = e.Args.Contains(Autostart.Argument);
        _instance = new Mutex(true, InstanceName, out bool firstInstance);
        if (!firstInstance)
        {
            // Already running: bring that window up (unless Windows is just starting us automatically) and leave.
            if (!fromAutostart && EventWaitHandle.TryOpenExisting(ShowWindowSignal, out var signal))
            {
                signal.Set();
                signal.Dispose();
            }
            _instance.Dispose();
            _instance = null;
            Shutdown();
            return;
        }

        // The window may live hidden in the tray, so the app ends only when MainWindow says so.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Autostart.RefreshPath();

        var window = new MainWindow();
        MainWindow = window;
        if (AppSettings.Current.StartMinimized || fromAutostart)
            window.HideToTray();
        else
            window.Show();

        _showWindow = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowSignal);
        var waiter = new Thread(() =>
        {
            try
            {
                while (_showWindow.WaitOne())
                    Dispatcher.BeginInvoke(window.ShowFromTray);
            }
            catch (ObjectDisposedException)
            {
                // PadPilot is closing.
            }
        }) { IsBackground = true, Name = "Second start" };
        waiter.Start();
    }

    /// <summary>
    /// An error on the UI thread: write it to the crash log, tell the user, and close cleanly so the
    /// controller is released (triggers off, HidHide undone) instead of the process just vanishing.
    /// </summary>
    private void OnUiException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        CrashLog.Write(e.Exception, "UI");
        DispatcherUnhandledException -= OnUiException; // one report is enough if closing fails too
        try
        {
            MessageBox.Show(Loc.Format("Crash_Message", CrashLog.FilePath), "PadPilot", MessageBoxButton.OK, MessageBoxImage.Error);
            (MainWindow as MainWindow)?.Exit();
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "closing after an error");
        }
        Shutdown(1);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showWindow?.Dispose();
        if (_instance != null)
        {
            _instance.ReleaseMutex();
            _instance.Dispose();
        }
        base.OnExit(e);
    }
}
