using System.Windows;
using AorusControl.App.Infrastructure;
using System.Windows.Controls;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;
using Panel = System.Windows.Controls.Panel;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;

namespace AorusControl.App.Controls;

/// <summary>
/// The scale under a slider: tick marks and their labels, both placed by the same
/// calculation the slider itself uses to place its thumb.
///
/// This exists because the two obvious approaches are both wrong. WPF's own
/// <see cref="TickBar"/> spreads its ticks across the full width unless it is told the thumb
/// width, so they drift away from the thumb towards the ends; and labels laid out in equal
/// grid columns sit at the centre of each column (12.5%, 37.5%, …) rather than at the values
/// they name (0%, 33%, …). Together that produced a scale where nothing lined up with
/// anything.
///
/// The thumb travels over <c>width - thumbWidth</c>, starting half a thumb in, so a value's
/// position is <c>thumbWidth/2 + fraction * (width - thumbWidth)</c>. One formula here, for
/// both the ticks and the labels, means they cannot disagree - and it has to be given the
/// same <see cref="ThumbWidth"/> the slider's own template uses.
/// </summary>
public sealed class SliderScale : Panel
{
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(SliderScale),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(SliderScale),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Must match the slider's thumb. WPF-UI's Fluent thumb is 20 wide.</summary>
    public static readonly DependencyProperty ThumbWidthProperty = DependencyProperty.Register(
        nameof(ThumbWidth), typeof(double), typeof(SliderScale),
        new FrameworkPropertyMetadata(20.0, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Tick values. Left unset, every label gets a tick - which is what a scale with
    /// named steps wants; the fan slider sets them explicitly because its tested steps are
    /// not evenly spaced.</summary>
    public static readonly DependencyProperty TicksProperty = DependencyProperty.Register(
        nameof(Ticks), typeof(DoubleCollection), typeof(SliderScale),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TickBrushProperty = DependencyProperty.Register(
        nameof(TickBrush), typeof(Brush), typeof(SliderScale),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Where a label sits, when the labels do not simply divide the range evenly.</summary>
    public static readonly DependencyProperty AtValueProperty = DependencyProperty.RegisterAttached(
        "AtValue", typeof(double), typeof(SliderScale),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsParentArrange));

    public static void SetAtValue(UIElement element, double value) => element.SetValue(AtValueProperty, value);
    public static double GetAtValue(UIElement element) => (double)element.GetValue(AtValueProperty);

    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double ThumbWidth { get => (double)GetValue(ThumbWidthProperty); set => SetValue(ThumbWidthProperty, value); }
    public DoubleCollection? Ticks { get => (DoubleCollection?)GetValue(TicksProperty); set => SetValue(TicksProperty, value); }
    public Brush TickBrush { get => (Brush)GetValue(TickBrushProperty); set => SetValue(TickBrushProperty, value); }

    private const double TickHeight = 5, TickGap = 4, LabelGap = 10;

    protected override Size MeasureOverride(Size available)
    {
        double height = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(
            double.IsInfinity(available.Width) ? 0 : available.Width,
            height + TickHeight + TickGap);
    }

    protected override Size ArrangeOverride(Size final)
    {
        int count = InternalChildren.Count;
        var left = new double[count];
        for (int index = 0; index < count; index++)
        {
            double width = InternalChildren[index].DesiredSize.Width;
            // Centred on its own value, then kept inside the panel: the first and last labels
            // would otherwise hang over the edges by half their width.
            left[index] = Math.Clamp(PositionOf(ValueOf(index), final.Width) - width / 2, 0, Math.Max(0, final.Width - width));
        }

        // Narrow window: drop the labels that would collide rather than printing them over
        // each other. The ends always survive, because they are what gives the scale meaning;
        // the current step is named in full by the readout beside the slider anyway. The ticks
        // stay put, so nothing about the scale itself changes - only how much of it is named.
        double keptRight = double.NegativeInfinity;
        double lastLeft = count > 1 ? left[count - 1] : double.PositiveInfinity;
        for (int index = 0; index < count; index++)
        {
            UIElement child = InternalChildren[index];
            double width = child.DesiredSize.Width;
            bool isEnd = index == 0 || index == count - 1;
            bool fits = left[index] >= keptRight + LabelGap
                && (isEnd || left[index] + width + LabelGap <= lastLeft);
            if (!fits)
            {
                child.Arrange(new Rect(0, 0, 0, 0));
                continue;
            }

            child.Arrange(new Rect(left[index], TickHeight + TickGap, width, child.DesiredSize.Height));
            keptRight = left[index] + width;
        }
        return final;
    }

    protected override void OnRender(DrawingContext context)
    {
        var pen = new Pen(TickBrush, 1);
        pen.Freeze();
        foreach (double value in TickValues())
        {
            // +0.5 keeps a one-pixel line on the pixel rather than across two of them.
            double x = Math.Round(PositionOf(value, ActualWidth)) + 0.5;
            context.DrawLine(pen, new Point(x, 0), new Point(x, TickHeight));
        }
    }

    private IEnumerable<double> TickValues()
    {
        if (Ticks is { Count: > 0 } ticks) return ticks;
        return Enumerable.Range(0, InternalChildren.Count).Select(ValueOf);
    }

    /// <summary>A label's value: its own if it was given one, otherwise an even share of the
    /// range, which is what a scale of named steps is.</summary>
    private double ValueOf(int index)
    {
        double explicitValue = GetAtValue(InternalChildren[index]);
        if (!double.IsNaN(explicitValue)) return explicitValue;
        int count = InternalChildren.Count;
        return count <= 1 ? Minimum : Minimum + index * (Maximum - Minimum) / (count - 1);
    }

    private double PositionOf(double value, double width) =>
        SliderGeometry.PositionOf(value, Minimum, Maximum, width, ThumbWidth);
}
