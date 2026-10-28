using System.Windows;

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
}
