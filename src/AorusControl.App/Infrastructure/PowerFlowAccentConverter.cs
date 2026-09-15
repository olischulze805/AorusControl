using System.Globalization;
using System.Windows.Data;
using AorusControl.App.Controls;
using Color = System.Windows.Media.Color;

namespace AorusControl.App.Infrastructure;

/// <summary>
/// Green while the battery fills, the app's own cyan while it empties.
///
/// Deliberately not the temperature ramp: watts are not degrees, and a figure that drifted
/// towards alarm orange as it rose would be inventing a danger level where there is none -
/// 60 W out of the battery is a busy machine, not a hot one. Two states, two colours, and
/// the direction is readable without reading the label.
/// </summary>
public sealed class PowerFlowAccentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? ThermalPalette.Charge : (Color?)ThermalPalette.Cool;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
