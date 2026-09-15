using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace AorusControl.App.Controls;

/// <summary>
/// The recent history of one measurement as a small filled line.
///
/// A single number answers "how warm is it"; this answers "and is that going anywhere", which
/// is the question a dashboard is actually for. It takes a plain array and redraws when a new
/// one arrives, so the view model can keep its own ring buffer without any collection
/// bookkeeping in the UI.
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    /// <summary>The samples, oldest first. Fewer than two means there is nothing to say yet.</summary>
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(double[]), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The colour, which the dashboard drives from the current temperature so the
    /// line warms with the machine.</summary>
    public static readonly DependencyProperty TemperatureProperty = DependencyProperty.Register(
        nameof(Temperature), typeof(double), typeof(Sparkline),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>False while nothing is being measured: the line greys out rather than
    /// implying the last shape is current.</summary>
    public static readonly DependencyProperty IsLiveProperty = DependencyProperty.Register(
        nameof(IsLive), typeof(bool), typeof(Sparkline),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The value range the line is drawn against. Fixing it rather than scaling to
    /// the data keeps 40-42 °C looking flat instead of dramatic.</summary>
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(Sparkline),
        new FrameworkPropertyMetadata(30.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(Sparkline),
        new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Overrides the temperature ramp with one fixed colour. Not everything worth drawing is
    /// a temperature: the power card measures watts, where warm and cool mean nothing and a
    /// tint that crept upwards with the number would be inventing a danger level.
    /// </summary>
    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Color?), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public Color? Accent { get => (Color?)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }

    public double[]? Values { get => (double[]?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public double Temperature { get => (double)GetValue(TemperatureProperty); set => SetValue(TemperatureProperty, value); }
    public bool IsLive { get => (bool)GetValue(IsLiveProperty); set => SetValue(IsLiveProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    protected override void OnRender(DrawingContext context)
    {
        double width = ActualWidth, height = ActualHeight;
        if (width <= 1 || height <= 1) return;

        double[] values = Values ?? [];
        Color tint = Accent is { } accent && IsLive ? accent : ThermalPalette.Tint(Temperature, IsLive);

        // The baseline is drawn even with no data: an empty card should still look like a
        // chart waiting for numbers, not like a rendering failure.
        context.DrawLine(new Pen(ThermalPalette.Brush(tint, 0.20), 1),
            new Point(0, height - 0.5), new Point(width, height - 0.5));
        if (values.Length < 2) return;

        double span = Math.Max(1, Maximum - Minimum);
        double step = width / (values.Length - 1);
        Point At(int index) => new(
            index * step,
            height - Math.Clamp((values[index] - Minimum) / span, 0, 1) * (height - 2) - 1);

        var line = new StreamGeometry();
        using (StreamGeometryContext draw = line.Open())
        {
            draw.BeginFigure(At(0), false, false);
            for (int index = 1; index < values.Length; index++) draw.LineTo(At(index), true, false);
        }
        line.Freeze();

        var area = new StreamGeometry();
        using (StreamGeometryContext draw = area.Open())
        {
            draw.BeginFigure(new Point(0, height), true, true);
            for (int index = 0; index < values.Length; index++) draw.LineTo(At(index), true, false);
            draw.LineTo(new Point(width, height), true, false);
        }
        area.Freeze();

        // The fill fades downwards so the line stays the thing being read and the area only
        // gives it weight.
        var fill = new LinearGradientBrush(
            Color.FromArgb(0x4D, tint.R, tint.G, tint.B),
            Color.FromArgb(0x00, tint.R, tint.G, tint.B),
            new Point(0, 0), new Point(0, 1));
        fill.Freeze();
        context.DrawGeometry(fill, null, area);
        context.DrawGeometry(null, new Pen(ThermalPalette.Brush(tint, IsLive ? 1 : 0.5), 1.6)
        {
            LineJoin = PenLineJoin.Round
        }, line);

        // The newest sample gets a dot: on a line this small it is the difference between
        // "here is a history" and "here is where it is now".
        if (IsLive) context.DrawEllipse(ThermalPalette.Brush(tint), null, At(values.Length - 1), 2.4, 2.4);
    }
}
