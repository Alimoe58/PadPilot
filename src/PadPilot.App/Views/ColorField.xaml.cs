using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PadPilot.App.Views;

public partial class ColorField : UserControl
{
    /// <summary>Hue in degrees, 0–360 (left to right).</summary>
    public static readonly DependencyProperty HueProperty = DependencyProperty.Register(
        nameof(Hue), typeof(double), typeof(ColorField),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ColorField)d).PlaceMarker()));

    /// <summary>Saturation ("intensity") in percent, 100 at the top, 0 at the bottom.</summary>
    public static readonly DependencyProperty SaturationProperty = DependencyProperty.Register(
        nameof(Saturation), typeof(int), typeof(ColorField),
        new FrameworkPropertyMetadata(100, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ColorField)d).PlaceMarker()));

    public ColorField()
    {
        InitializeComponent();
        SizeChanged += (_, _) => PlaceMarker();
    }

    public double Hue { get => (double)GetValue(HueProperty); set => SetValue(HueProperty, value); }
    public int Saturation { get => (int)GetValue(SaturationProperty); set => SetValue(SaturationProperty, value); }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Root.CaptureMouse();
        Pick(e.GetPosition(Root));
    }

    private void Root_MouseMove(object sender, MouseEventArgs e)
    {
        if (Root.IsMouseCaptured)
            Pick(e.GetPosition(Root));
    }

    private void Root_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => Root.ReleaseMouseCapture();

    private void Pick(Point p)
    {
        if (Root.ActualWidth <= 0 || Root.ActualHeight <= 0)
            return;
        Hue = Math.Clamp(p.X / Root.ActualWidth, 0, 1) * 360;
        Saturation = (int)Math.Round((1 - Math.Clamp(p.Y / Root.ActualHeight, 0, 1)) * 100);
    }

    private void PlaceMarker()
    {
        Canvas.SetLeft(Marker, Hue / 360 * Root.ActualWidth - Marker.Width / 2);
        Canvas.SetTop(Marker, (1 - Saturation / 100.0) * Root.ActualHeight - Marker.Height / 2);
    }
}
