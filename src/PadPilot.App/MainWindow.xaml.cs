using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PadPilot.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(Dispatcher);
        DataContext = _viewModel;
        Loaded += (_, _) => _viewModel.Start();
        Closed += (_, _) => _viewModel.Dispose();
    }

    private void Swatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Background: SolidColorBrush brush })
            _viewModel.Output.SetColor(brush.Color);
    }

    private void StopRumble_Click(object sender, RoutedEventArgs e) => _viewModel.Output.StopRumble();
}
