using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using PadPilot.Core;

namespace PadPilot.App;

/// <summary>Multiplies a number by the factor given as ConverterParameter (used for stick and touch positions).</summary>
public sealed class ScaleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Convert.ToDouble(value) * double.Parse((string)parameter, CultureInfo.InvariantCulture);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Returns the "pressed" brush when the button named in ConverterParameter is held.</summary>
public sealed class ButtonBrushConverter : IValueConverter
{
    private static readonly Brush Pressed = Freeze(new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)));
    private static readonly Brush Released = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x3D, 0x46)));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var button = Enum.Parse<Buttons>((string)parameter);
        return value is Buttons b && (b & button) == button ? Pressed : Released;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
