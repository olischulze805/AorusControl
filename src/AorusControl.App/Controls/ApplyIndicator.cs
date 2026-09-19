using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using AorusControl.App.Infrastructure;

namespace AorusControl.App.Controls;


/// <summary>
/// A sixteen-pixel mark beside a value that applies itself: turning while it is on its way,
/// a tick when the device has confirmed it, a dot when it has not.
///
/// It exists to delete a sentence. The charge-limit card used to explain in words that the
/// slider applies itself shortly after you let go, then say "80 % is being applied", then
/// say it had been applied and read back - three lines of prose for something the reader can
/// simply be shown. Explaining a mechanism in a permanent label is what you do when the
/// interface cannot demonstrate it.
///
/// Drawn rather than templated, like the other marks in this app, so the whole thing is one
/// file and the animation is a number rather than a storyboard hidden in a dictionary.
/// </summary>
public sealed class ApplyIndicator : FrameworkElement
{
    private const double Box = 16;
    private const double Radius = 6;
    private const double Thickness = 2;

    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(ApplyState), typeof(ApplyIndicator),
        new FrameworkPropertyMetadata(ApplyState.Idle, FrameworkPropertyMetadataOptions.AffectsRender, OnStateChanged));

    /// <summary>The rotating gap's position, in degrees. A plain animated number instead of a
    /// RotateTransform, because this element renders itself and has no child to transform.</summary>
    private static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        nameof(Phase), typeof(double), typeof(ApplyIndicator),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// How much of the tick is still there. It fades on its own so nobody has to start a
    /// timer in a view model to take it away again.
    ///
    /// The default is full rather than nothing, so the tick is also there when no animation
    /// clock is running - which is the case in the offscreen renders that check the layout.
    /// A mark that only exists while animating cannot be checked by a picture.
    /// </summary>
    private static readonly DependencyProperty FadeProperty = DependencyProperty.Register(
        nameof(Fade), typeof(double), typeof(ApplyIndicator),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public ApplyState State
    {
        get => (ApplyState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    private double Phase => (double)GetValue(PhaseProperty);
    private double Fade => (double)GetValue(FadeProperty);

    protected override Size MeasureOverride(Size availableSize) => new(Box, Box);

    private static void OnStateChanged(DependencyObject element, DependencyPropertyChangedEventArgs args) =>
        ((ApplyIndicator)element).Animate((ApplyState)args.NewValue);

    private void Animate(ApplyState state)
    {
        BeginAnimation(PhaseProperty, null);
        BeginAnimation(FadeProperty, null);

        if (state == ApplyState.Applying)
        {
            BeginAnimation(PhaseProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.1))
            {
                RepeatBehavior = RepeatBehavior.Forever
            });
            return;
        }

        if (state != ApplyState.Confirmed) return;
        // Held for a moment at full strength, then gone. Long enough to be noticed by someone
        // who was looking at the slider, short enough not to become furniture.
        BeginAnimation(FadeProperty, new DoubleAnimation(1, 0, TimeSpan.FromSeconds(1.1))
        {
            BeginTime = TimeSpan.FromSeconds(0.9)
        });
    }

    protected override void OnRender(DrawingContext context)
    {
        var centre = new Point(Box / 2, Box / 2);
        switch (State)
        {
            case ApplyState.Applying:
                Draw(context, Arc(centre, Phase, 110), ThermalPalette.Cool, 1);
                // The rest of the ring, faint: without it the turning arc reads as a stray
                // scratch rather than as something going round.
                Draw(context, Arc(centre, Phase + 120, 240), ThermalPalette.Idle, 0.25);
                break;

            case ApplyState.Confirmed when Fade > 0:
                Draw(context, Tick(), ThermalPalette.Charge, Fade);
                break;

            case ApplyState.Failed:
                var dot = new SolidColorBrush(ThermalPalette.Hot);
                dot.Freeze();
                context.DrawEllipse(dot, null, centre, 3.5, 3.5);
                break;
        }
    }

    private static void Draw(DrawingContext context, Geometry geometry, Color color, double opacity)
    {
        var pen = new Pen(new SolidColorBrush(color) { Opacity = opacity }, Thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        pen.Freeze();
        context.DrawGeometry(null, pen, geometry);
    }

    private static Geometry Arc(Point centre, double startDegrees, double sweepDegrees)
    {
        Point from = OnCircle(centre, startDegrees);
        Point to = OnCircle(centre, startDegrees + sweepDegrees);
        var figure = new PathFigure { StartPoint = from };
        figure.Segments.Add(new ArcSegment(to, new Size(Radius, Radius), 0,
            sweepDegrees > 180, SweepDirection.Clockwise, isStroked: true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    private static Point OnCircle(Point centre, double degrees)
    {
        double radians = degrees * Math.PI / 180;
        return new Point(centre.X + Radius * Math.Cos(radians), centre.Y + Radius * Math.Sin(radians));
    }

    private static Geometry Tick()
    {
        var figure = new PathFigure { StartPoint = new Point(4, 8.4) };
        figure.Segments.Add(new PolyLineSegment([new Point(6.8, 11.2), new Point(12, 5.2)], isStroked: true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }
}
