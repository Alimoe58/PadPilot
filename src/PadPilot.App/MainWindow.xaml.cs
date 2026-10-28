using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PadPilot.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly TrayIcon _tray;
    private bool _exiting;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(Dispatcher);
        DataContext = _viewModel;

        _tray = new TrayIcon();
        _tray.OpenRequested += ShowFromTray;
        _tray.ExitRequested += Exit;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.Status) or nameof(MainViewModel.Battery))
                UpdateTrayToolTip();
        };
        UpdateTrayToolTip();

        StateChanged += OnStateChanged;
        IsVisibleChanged += (_, _) => UpdateUiVisible();

        // Start right away (not on Loaded): with "start minimized" the window is never shown.
        _viewModel.Start();
    }

    public void HideToTray()
    {
        Hide();
        _tray.ShowHiddenHint();
    }

    private void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }

    private void Exit()
    {
        _exiting = true;
        Close();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && AppSettings.Current.MinimizeToTray)
            HideToTray();
        UpdateUiVisible();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exiting && AppSettings.Current.CloseToTray)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.Dispose();
        _tray.Dispose();
        Application.Current.Shutdown();
    }

    private void UpdateUiVisible() =>
        _viewModel.IsUiVisible = IsVisible && WindowState != WindowState.Minimized;

    private void UpdateTrayToolTip() =>
        _tray.SetToolTip(_viewModel.Battery is "–" or ""
            ? $"PadPilot · {_viewModel.Status}"
            : $"PadPilot · {_viewModel.Status} · {_viewModel.Battery}");

    private void Swatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Background: SolidColorBrush brush })
            _viewModel.Output.SetColor(brush.Color);
    }

    /// <summary>Enter in the hex box applies the colour right away instead of waiting for focus loss.</summary>
    private void HexBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && sender is TextBox box)
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    private void StopRumble_Click(object sender, RoutedEventArgs e) => _viewModel.Output.StopRumble();
}
