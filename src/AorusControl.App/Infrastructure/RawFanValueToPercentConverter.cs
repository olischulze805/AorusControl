using System.Globalization;
using System.Windows.Data;
using AorusControl.Core.Features.Cooling;

namespace AorusControl.App.Infrastructure;

/// <summary>Displays a raw fan duty byte as a percent, matching FanSpeedPercent's "out of
/// 229" convention. One-way only: the Fixed dropdown's items are the fixed byte values
/// themselves (SelectedItem stays the byte), this only changes how each one is printed.</summary>
public sealed class RawFanValueToPercentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is byte raw ? $"{FanSpeedPercent.ToPercent(raw)}%" : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
