using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AorusControl.App.Infrastructure;

/// <summary>Negates a bound bool. Used to disable a busy-triggering button while its own command runs.</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool flag && !flag;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool flag && !flag;
}

/// <summary>Shows the element only when the bound bool is true.</summary>
public sealed class BoolVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}


/// <summary>
/// Full opacity when the bound bool is true, dimmed when false. Used for controls that
/// stay usable but currently have no effect - dimming says "this changes nothing right
/// now" without taking the control away, since the value it sets is still stored.
/// </summary>
public sealed class ActiveOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 1.0 : 0.35;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
