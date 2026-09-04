using System.Globalization;
using System.Windows.Data;

namespace AorusControl.App.Infrastructure;

/// <summary>
/// True when the bound value equals the converter parameter, as plain strings. Drives
/// the checked state of the profile/mode chips and the effect tiles: they bind one-way
/// against what the device actually reports, so a failed write leaves the highlight on
/// the state that is really active instead of on the one that was clicked.
/// </summary>
public sealed class StringEqualsBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter as string, StringComparison.Ordinal);

    // RadioButton writes back through SetCurrentValue, which keeps the one-way binding
    // intact; nothing should ever push a bool back into the source.
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
