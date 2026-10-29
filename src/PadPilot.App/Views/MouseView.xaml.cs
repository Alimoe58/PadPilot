using System.Windows;
using System.Windows.Controls;

namespace PadPilot.App.Views;

public partial class MouseView : UserControl
{
    public MouseView() => InitializeComponent();

    private void Calibrate_Click(object sender, RoutedEventArgs e) => (DataContext as MouseViewModel)?.Calibrate();
}
