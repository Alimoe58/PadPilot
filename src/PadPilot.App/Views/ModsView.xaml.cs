using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PadPilot.App.Views;

public partial class ModsView : UserControl
{
    public ModsView() => InitializeComponent();

    private ModsViewModel? ViewModel => DataContext as ModsViewModel;

    private void SendTest_Click(object sender, RoutedEventArgs e) => ViewModel?.SendTest();
    private void Reset_Click(object sender, RoutedEventArgs e) => ViewModel?.ResetOverrides();
    private void OpenFolder_Click(object sender, RoutedEventArgs e) => ViewModel?.OpenFileFolder();

    /// <summary>Enter in the port box applies the new port right away.</summary>
    private void Port_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox box)
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }
}
