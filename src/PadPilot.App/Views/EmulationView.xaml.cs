using System.Windows;
using System.Windows.Controls;

namespace PadPilot.App.Views;

public partial class EmulationView : UserControl
{
    public EmulationView() => InitializeComponent();

    private EmulationViewModel? ViewModel => DataContext as EmulationViewModel;

    private void InstallViGEm_Click(object sender, RoutedEventArgs e) => ViewModel?.InstallViGEm();
    private void ViGEmPage_Click(object sender, RoutedEventArgs e) => ViewModel?.OpenViGEmPage();
    private void HidHideInstall_Click(object sender, RoutedEventArgs e) => ViewModel?.InstallHidHide();
    private void HidHidePage_Click(object sender, RoutedEventArgs e) => ViewModel?.OpenHidHidePage();
    private void CheckDrivers_Click(object sender, RoutedEventArgs e) => ViewModel?.CheckDrivers();
}
