using System.Windows.Media;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Colors = System.Windows.Media.Colors;

namespace AorusControl.App.Controls;

/// <summary>
/// One temperature-to-colour ramp for the whole app: the rotor arcs on the cooling page, the
/// dashboard's degree numbers and its trend lines all warm at the same rate, so a colour
/// means the same thing wherever it appears.
/// </summary>
public static class ThermalPalette
{
    public static readonly Color Cool = Color.FromRgb(0x35, 0xC7, 0xE6);
    public static readonly Color Warm = Color.FromRgb(0xF2, 0x9A, 0x3C);
    public static readonly Color Hot = Color.FromRgb(0xEF, 0x5F, 0x5F);
    public static readonly Color Idle = Color.FromRgb(0x8A, 0x93, 0x9B);

    /// <summary>
    /// The app's own cyan while the machine is cool, warming towards amber from about 55 °C
    /// and reaching red at 90 °C - where this laptop's own firmware starts to throttle.
    ///
    /// The stops are blended directly rather than walked round the colour wheel: the wheel's
    /// short path from cyan to amber runs through a loud green that belongs to no other part
    /// of this app.
    /// </summary>
    public static Color Tint(double celsius, bool isLive = true)
    {
        if (!isLive || double.IsNaN(celsius)) return Idle;
        return celsius <= 85
            ? Mix(Cool, Warm, Share(celsius, 55, 85))
            : Mix(Warm, Hot, Share(celsius, 85, 95));
    }

    public static Color Mix(Color from, Color to, double share) => Color.FromRgb(
        (byte)(from.R + (to.R - from.R) * share),
        (byte)(from.G + (to.G - from.G) * share),
        (byte)(from.B + (to.B - from.B) * share));

    public static SolidColorBrush Brush(Color color, double opacity = 1)
    {
        var brush = new SolidColorBrush(color) { Opacity = opacity };
        brush.Freeze();
        return brush;
    }

    private static double Share(double value, double from, double to) =>
        Math.Clamp((value - from) / (to - from), 0, 1);
}
