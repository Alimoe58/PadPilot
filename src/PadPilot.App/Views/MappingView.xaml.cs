using System.Windows;
using System.Windows.Controls;

namespace PadPilot.App.Views;

public partial class MappingView : UserControl
{
    public MappingView()
    {
        InitializeComponent();
        // Leaving the tab ends a waiting key capture, so typing elsewhere (e.g. a profile name) isn't swallowed.
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is false)
                ViewModel?.StopCapture();
        };
    }

    private MappingViewModel? ViewModel => DataContext as MappingViewModel;

    /// <summary>The main window passes the next key press to the row (see MainWindow.OnPreviewKeyDown).</summary>
    private void CaptureKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ButtonMappingRow row })
            ViewModel?.StartCapture(row);
    }

    private void MicMute_Click(object sender, RoutedEventArgs e) => ViewModel?.UseMicButtonForMute();
    private void ResetAll_Click(object sender, RoutedEventArgs e) => ViewModel?.ResetAll();
}
