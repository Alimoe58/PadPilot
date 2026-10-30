using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PadPilot.Core.Mapping;

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
        _viewModel.LowBattery += percent => _tray.ShowWarning(Loc.Format("Battery_Low", percent));
        _viewModel.Profiles.ProfilesChanged += UpdateTrayProfiles;
        UpdateTrayProfiles();

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

    private void UpdateTrayProfiles() =>
        _tray.SetProfiles(_viewModel.Profiles.Names, _viewModel.Profiles.Active, name => _viewModel.Profiles.Switch(name));

    public const string IssuesUrl = "https://github.com/Alimoe58/PadPilot/issues/new";

    private void CopyDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        string text = _viewModel.BuildDiagnostics();
        DiagnosticsBox.Text = text;
        DiagnosticsBox.Visibility = Visibility.Visible;
        try
        {
            Clipboard.SetText(text);
            DiagnosticsCopied.Text = Loc.Get("Support_Copied");
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Another program is holding the clipboard; the text is still shown to copy by hand.
            DiagnosticsCopied.Text = Loc.Get("Support_CopyFailed");
        }
    }

    private void ReportProblem_Click(object sender, RoutedEventArgs e) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(IssuesUrl) { UseShellExecute = true });

    private void CheckUpdate_Click(object sender, RoutedEventArgs e) => _ = _viewModel.Update.CheckAsync();
    private void DownloadUpdate_Click(object sender, RoutedEventArgs e) => _viewModel.Update.Download();
    private void SkipUpdate_Click(object sender, RoutedEventArgs e) => _viewModel.Update.Skip();
    private void DismissUpdate_Click(object sender, RoutedEventArgs e) => _viewModel.Update.Dismiss();
    private void TurnOff_Click(object sender, RoutedEventArgs e) => _viewModel.TurnOffController();

    // Key capture for the "Buttons" tab: the next key goes to the row waiting for it. A modifier
    // pressed on its own (e.g. Shift for sprinting) is taken when it is released without another key.
    private Key? _pendingModifier;

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (_viewModel.Mapping.IsCapturing)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (IsModifier(key))
                _pendingModifier = key;
            else
            {
                _pendingModifier = null;
                _viewModel.Mapping.CaptureKey(KeyInterop.VirtualKeyFromKey(key), CurrentModifiers());
            }
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (_viewModel.Mapping.IsCapturing && _pendingModifier == key)
        {
            _pendingModifier = null;
            _viewModel.Mapping.CaptureKey(KeyInterop.VirtualKeyFromKey(key), KeyModifiers.None);
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyUp(e);
    }

    private static bool IsModifier(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
        or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;

    private static KeyModifiers CurrentModifiers()
    {
        var m = Keyboard.Modifiers;
        return (m.HasFlag(ModifierKeys.Control) ? KeyModifiers.Ctrl : 0)
             | (m.HasFlag(ModifierKeys.Shift) ? KeyModifiers.Shift : 0)
             | (m.HasFlag(ModifierKeys.Alt) ? KeyModifiers.Alt : 0)
             | (m.HasFlag(ModifierKeys.Windows) ? KeyModifiers.Win : 0);
    }

    private void InstallViGEm_Click(object sender, RoutedEventArgs e) => _viewModel.Emulation.InstallViGEm();
    private void CheckDrivers_Click(object sender, RoutedEventArgs e) => _viewModel.Emulation.CheckDrivers();
    private void DismissBanner_Click(object sender, RoutedEventArgs e) => _viewModel.Emulation.DismissBanner();

    private void NewProfile_Click(object sender, RoutedEventArgs e) => _viewModel.Profiles.CreateNew();
    private void RenameProfile_Click(object sender, RoutedEventArgs e) => _viewModel.Profiles.RenameActive();
    private void DeleteProfile_Click(object sender, RoutedEventArgs e) => _viewModel.Profiles.DeleteActive();
    private void ProfileFolder_Click(object sender, RoutedEventArgs e) => ProfilesViewModel.OpenFolder();

    private void RunningApps_DropDownOpened(object sender, EventArgs e) => _viewModel.Profiles.RefreshRunningApps();
    private void LinkRunningGame_Click(object sender, RoutedEventArgs e) => _viewModel.Profiles.AddGame(_viewModel.Profiles.SelectedApp?.Exe);

    private void BrowseGame_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.Get("Games_BrowseTitle"),
            Filter = $"{Loc.Get("Games_BrowseFilter")} (*.exe)|*.exe",
        };
        if (dialog.ShowDialog(this) == true)
            _viewModel.Profiles.AddGame(dialog.FileName);
    }

    private void RemoveGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string exe })
            _viewModel.Profiles.RemoveGame(exe);
    }

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
