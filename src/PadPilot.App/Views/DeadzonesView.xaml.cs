using System.Windows;
using System.Windows.Controls;

namespace PadPilot.App.Views;

public partial class DeadzonesView : UserControl
{
    public DeadzonesView() => InitializeComponent();

    private void ToggleTest_Click(object sender, RoutedEventArgs e) =>
        ((sender as FrameworkElement)?.DataContext as StickDeadzoneViewModel)?.ToggleTest();

    private void ResetStick_Click(object sender, RoutedEventArgs e) =>
        ((sender as FrameworkElement)?.DataContext as StickDeadzoneViewModel)?.ResetDefaults();

    private void ResetTrigger_Click(object sender, RoutedEventArgs e) =>
        ((sender as FrameworkElement)?.DataContext as TriggerDeadzoneViewModel)?.ResetDefaults();
}
