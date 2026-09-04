using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AorusControl.App.Infrastructure;

/// <summary>Shows the element when the bound value equals the converter parameter,
/// as plain strings. Used to switch between navigation sections without a
/// separate ViewModel per page or a Frame/page-service navigation stack.</summary>
public sealed class StringEqualsVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value as string, parameter as string, StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
