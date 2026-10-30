using System.Windows;
using System.Windows.Controls;

namespace PadPilot.App.Views;

public partial class MappingView : UserControl
{
    public MappingView() => InitializeComponent();

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
