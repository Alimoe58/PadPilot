using System.Windows;
using System.Windows.Controls;

namespace PadPilot.App.Views;

public partial class EmulationView : UserControl
{
    public EmulationView() => InitializeComponent();

    private void HidHideInstall_Click(object sender, RoutedEventArgs e) => EmulationViewModel.InstallHidHide();
}
