using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AorusControl.App.Infrastructure;

/// <summary>
/// Collapses an element whose text is empty.
///
/// Several lines in this app exist only when there is something to say: a remaining-time
/// figure the rate does not support, a note about leftover registry entries, a Fn+Space
/// failure. Bound plainly, an empty one still occupies a full line of height plus its own
/// margin, so the card keeps a gap where the sentence would have been - which reads as a
/// layout mistake rather than as silence.
/// </summary>
public sealed class TextVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
