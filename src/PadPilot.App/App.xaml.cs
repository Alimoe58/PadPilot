using System.Windows;

namespace PadPilot.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // The window may live hidden in the tray, so the app ends only when MainWindow says so.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Autostart.RefreshPath();
        bool fromAutostart = e.Args.Contains(Autostart.Argument);

        var window = new MainWindow();
        MainWindow = window;
        if (AppSettings.Current.StartMinimized || fromAutostart)
            window.HideToTray();
        else
            window.Show();
    }
}
