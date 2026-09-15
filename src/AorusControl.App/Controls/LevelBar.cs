using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;

namespace AorusControl.App.Controls;

/// <summary>
/// A thin filled track: how much of something is in use, next to the number that says how
/// much. The dashboard uses it for fan duty, where "45 %" alone leaves the reader doing the
/// comparison that a bar does for free.
/// </summary>
public sealed class LevelBar : FrameworkElement
{
    /// <summary>The filled share, 0-100.</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(LevelBar),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Tints the fill from the shared thermal ramp, so a hard-working fan on a hot
    /// machine reads warm here as well.</summary>
    public static readonly DependencyProperty TemperatureProperty = DependencyProperty.Register(
        nameof(Temperature), typeof(double), typeof(LevelBar),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsLiveProperty = DependencyProperty.Register(
        nameof(IsLive), typeof(bool), typeof(LevelBar),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Overrides the temperature ramp with one fixed colour. Not everything worth drawing is
    /// a temperature: the power card measures watts, where warm and cool mean nothing and a
    /// tint that crept upwards with the number would be inventing a danger level.
    /// </summary>
    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Color?), typeof(LevelBar),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public Color? Accent { get => (Color?)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Temperature { get => (double)GetValue(TemperatureProperty); set => SetValue(TemperatureProperty, value); }
    public bool IsLive { get => (bool)GetValue(IsLiveProperty); set => SetValue(IsLiveProperty, value); }

    protected override Size MeasureOverride(Size available) => new(double.IsInfinity(available.Width) ? 80 : available.Width, 4);

    protected override void OnRender(DrawingContext context)
    {
        double width = ActualWidth, height = ActualHeight;
        if (width <= 1 || height <= 0) return;

        var radius = new CornerRadius(height / 2);
        context.DrawRoundedRectangle(ThermalPalette.Brush(Colors.White, 0.08), null,
            new Rect(0, 0, width, height), radius.TopLeft, radius.TopLeft);

        double share = Math.Clamp(Value / 100.0, 0, 1);
        // A stopped fan draws nothing at all: a sliver of fill would read as "barely
        // turning", which is a different fact from "not turning".
        if (!IsLive || share <= 0) return;
        Color fill = Accent ?? ThermalPalette.Tint(Temperature, IsLive);
        context.DrawRoundedRectangle(ThermalPalette.Brush(fill), null,
            new Rect(0, 0, Math.Max(height, width * share), height), radius.TopLeft, radius.TopLeft);
    }
}
