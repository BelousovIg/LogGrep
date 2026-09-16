using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LogGrep.Controls;

/// <summary>Shows an element while a flag is off, the counterpart of BooleanToVisibilityConverter.</summary>
public sealed class InverseBoolToVisibility : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}
