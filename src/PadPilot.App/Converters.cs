using System.Globalization;
using System.Windows;
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

/// <summary>Maps a stick axis (0–255, centre 128) to an offset of ±ConverterParameter pixels.</summary>
public sealed class StickOffsetConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (System.Convert.ToDouble(value) - 128) / 128 * double.Parse((string)parameter, CultureInfo.InvariantCulture);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Visible when the bound value's name equals ConverterParameter (used to pick the button layout).</summary>
public sealed class EqualsVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == (string)parameter ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Translated name of an enum value, looked up as "{EnumType}_{Value}" in the string resources.
/// Used as a MultiBinding with Loc.Language as second input so it refreshes when the language changes.
/// </summary>
public sealed class EnumLabelConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values[0] is Enum e ? Loc.Get($"{e.GetType().Name}_{e}") : values[0]?.ToString() ?? "";

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Returns the "pressed" brush when the button named in ConverterParameter is held.</summary>
public sealed class ButtonBrushConverter : IValueConverter
{
    public Brush Pressed { get; set; } = Freeze(new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)));
    public Brush Released { get; set; } = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x3D, 0x46)));

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
