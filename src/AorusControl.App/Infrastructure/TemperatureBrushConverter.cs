using System.Globalization;
using System.Windows.Data;
using AorusControl.App.Controls;

namespace AorusControl.App.Infrastructure;

/// <summary>
/// Paints a temperature reading in the colour that temperature means. The dashboard's degree
/// numbers are the one place where the value and its urgency have to arrive together, and a
/// tinted number says it without a second element on the card.
///
/// It takes the live flag as a second value on purpose: when monitoring stops, the last
/// measurement is still in the model, and a number reading "– °C" in alarm orange would be
/// claiming something nobody measured.
/// </summary>
public sealed class TemperatureBrushConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
        ThermalPalette.Brush(ThermalPalette.Tint(
            values.ElementAtOrDefault(0) as double? ?? double.NaN,
            values.ElementAtOrDefault(1) as bool? ?? false));

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
