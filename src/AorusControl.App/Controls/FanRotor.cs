using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace AorusControl.App.Controls;

/// <summary>
/// One of the two fans, turning at the speed it is really turning at.
///
/// The point is feedback while something is being changed: a number that goes from 2400 to
/// 3100 says little at a glance, a rotor that visibly speeds up says it immediately, and a
/// stopped rotor is the only honest way to show that a curve really did switch the fans off.
///
/// Two things make it read as motion rather than as a spinning icon. The speed is eased
/// towards the measured one instead of jumping to it, so a new reading every two seconds
/// still looks like a fan spooling up; and the blur grows with the speed, the way a real
/// fan stops being individual blades. Both are driven from the per-frame rendering tick and
/// are unhooked the moment the control leaves the tree, so an invisible section costs
/// nothing.
/// </summary>
public sealed class FanRotor : FrameworkElement
{
    /// <summary>Above this the rotor is drawn at its fastest; this laptop's fans top out
    /// around 6000 RPM, and mapping the real speed linearly onto a visible one keeps the two
    /// fans comparable to each other.</summary>
    private const double FastestRpm = 6000;
    private const double FastestDegreesPerSecond = 500;
    private const int Blades = 11;

    private static readonly Color CoolColor = Color.FromRgb(0x35, 0xC7, 0xE6);
    private static readonly Color WarmColor = Color.FromRgb(0xF2, 0x9A, 0x3C);
    private static readonly Color HubColor = Color.FromRgb(0x1A, 0x1E, 0x24);

    private readonly Stopwatch _clock = new();
    private double _angle;
    private double _speed;
    private bool _hooked;

    public static readonly DependencyProperty RpmProperty = DependencyProperty.Register(
        nameof(Rpm), typeof(double), typeof(FanRotor),
        new PropertyMetadata(0.0, (rotor, _) => ((FanRotor)rotor).OnSpeedChanged()));

    /// <summary>Tints the rotor from cool to warm. It is the temperature this fan is answering
    /// to, so the colour and the movement tell the same story.</summary>
    public static readonly DependencyProperty TemperatureProperty = DependencyProperty.Register(
        nameof(Temperature), typeof(double), typeof(FanRotor),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>How hard this fan is working, 0-100. Drawn as an arc around the housing, so
    /// speed and effort are two separate things the tile can say at once - a fan at 3000 RPM
    /// means something different at 40 % than at 90 %.</summary>
    public static readonly DependencyProperty DutyProperty = DependencyProperty.Register(
        nameof(Duty), typeof(double), typeof(FanRotor),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>False while nothing is being measured: the rotor then stands still and fades,
    /// rather than showing a plausible speed nobody read.</summary>
    public static readonly DependencyProperty IsLiveProperty = DependencyProperty.Register(
        nameof(IsLive), typeof(bool), typeof(FanRotor),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender,
            (rotor, _) => ((FanRotor)rotor).OnSpeedChanged()));

    public double Rpm
    {
        get => (double)GetValue(RpmProperty);
        set => SetValue(RpmProperty, value);
    }

    public double Temperature
    {
        get => (double)GetValue(TemperatureProperty);
        set => SetValue(TemperatureProperty, value);
    }

    public double Duty
    {
        get => (double)GetValue(DutyProperty);
        set => SetValue(DutyProperty, value);
    }

    public bool IsLive
    {
        get => (bool)GetValue(IsLiveProperty);
        set => SetValue(IsLiveProperty, value);
    }

    public FanRotor()
    {
        Loaded += (_, _) => Hook(true);
        Unloaded += (_, _) => Hook(false);
        IsVisibleChanged += (_, _) => Hook(IsVisible);
    }

    private double TargetDegreesPerSecond =>
        IsLive ? Math.Clamp(Rpm, 0, FastestRpm) / FastestRpm * FastestDegreesPerSecond : 0;

    private void OnSpeedChanged()
    {
        // A change in the target is exactly when the rotor needs the frame tick back: it may
        // have parked itself after coming to a standstill.
        Hook(IsVisible && IsLoaded);
        InvalidateVisual();
    }

    private void Hook(bool wanted)
    {
        // Nothing to animate once it stands still and is meant to: without this the tick would
        // keep running for a stopped fan on a section nobody is looking at.
        bool needed = wanted && (TargetDegreesPerSecond > 0 || _speed > 0.5);
        if (needed == _hooked) return;
        _hooked = needed;
        if (needed)
        {
            _clock.Restart();
            CompositionTarget.Rendering += OnFrame;
        }
        else
        {
            CompositionTarget.Rendering -= OnFrame;
            _clock.Stop();
        }
    }

    private void OnFrame(object? sender, EventArgs eventArgs)
    {
        double elapsed = _clock.Elapsed.TotalSeconds;
        _clock.Restart();
        if (elapsed <= 0 || elapsed > 0.25) elapsed = 1.0 / 60;

        // Exponential approach: fast enough to react to a reading, slow enough that the
        // change is visible as spooling up rather than as a jump.
        double target = TargetDegreesPerSecond;
        _speed += (target - _speed) * Math.Min(1, elapsed * 2.2);
        if (Math.Abs(target - _speed) < 0.5) _speed = target;

        _angle = (_angle + _speed * elapsed) % 360;
        InvalidateVisual();
        if (_speed == 0 && target == 0) Hook(false);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double side = Math.Min(
            double.IsInfinity(availableSize.Width) ? 96 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 96 : availableSize.Height);
        return new Size(side, side);
    }

    protected override void OnRender(DrawingContext context)
    {
        double side = Math.Min(ActualWidth, ActualHeight);
        if (side <= 0) return;

        var centre = new Point(ActualWidth / 2, ActualHeight / 2);
        // The housing keeps clear of the outer edge, because the effort arc lives out there
        // and an arc sitting exactly on the ring is invisible.
        double outer = side / 2 - 8;
        double inner = outer * 0.30;
        Color tint = Tint();

        // The housing: a soft filled disc and a ring, so the blades have something to turn in
        // and the tile does not look like a floating icon.
        context.DrawEllipse(
            new RadialGradientBrush(
                Color.FromArgb(0x22, tint.R, tint.G, tint.B),
                Color.FromArgb(0x00, tint.R, tint.G, tint.B)),
            new Pen(new SolidColorBrush(Color.FromArgb(0x33, tint.R, tint.G, tint.B)), 1.5),
            centre, outer, outer);

        DrawDutyArc(context, centre, side / 2 - 2.5, tint);

        context.PushTransform(new RotateTransform(_angle, centre.X, centre.Y));
        var bladePen = new Pen(new SolidColorBrush(Color.FromArgb(IsLive ? (byte)0xEE : (byte)0x66, tint.R, tint.G, tint.B)), Math.Max(1.5, side / 34))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        for (int blade = 0; blade < Blades; blade++)
        {
            double start = blade * 2 * Math.PI / Blades;
            // Each blade sweeps backwards as it goes outwards, which is what makes a still
            // frame read as a fan rather than as a star.
            context.DrawGeometry(null, bladePen, Blade(centre, inner, outer * 0.94, start));
        }
        context.Pop();

        context.DrawEllipse(new SolidColorBrush(HubColor), new Pen(new SolidColorBrush(Color.FromArgb(0x55, tint.R, tint.G, tint.B)), 1), centre, inner, inner);

        // Blur instead of more geometry: a fast fan is a smear, and this is both truer to look
        // at and cheaper than drawing motion trails.
        double share = Math.Clamp(_speed / FastestDegreesPerSecond, 0, 1);
        Effect = share < 0.08 ? null : new BlurEffect { Radius = share * 2.6, KernelType = KernelType.Gaussian };
    }

    /// <summary>The effort arc: from the top, clockwise, one full turn at 100 %. Drawn on the
    /// housing ring rather than inside, so it never competes with the blades.</summary>
    private void DrawDutyArc(DrawingContext context, Point centre, double radius, Color tint)
    {
        double share = IsLive ? Math.Clamp(Duty, 0, 100) / 100 : 0;
        if (share <= 0.002) return;

        double sweep = share * 2 * Math.PI;
        Point At(double angle) => new(
            centre.X + radius * Math.Sin(angle),
            centre.Y - radius * Math.Cos(angle));

        var figure = new PathFigure { StartPoint = At(0) };
        // One arc segment cannot express more than half a turn unambiguously, so it is drawn
        // in halves - the flag alone would leave 51 % and 99 % looking identical.
        foreach (double end in sweep <= Math.PI ? [sweep] : new[] { Math.PI, sweep })
            figure.Segments.Add(new ArcSegment(At(end), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(0xCC, tint.R, tint.G, tint.B)), 3)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        }, geometry);
    }

    private static PathGeometry Blade(Point centre, double inner, double outer, double startAngle)
    {
        const double Sweep = 0.62;
        Point At(double radius, double angle) =>
            new(centre.X + radius * Math.Cos(angle), centre.Y + radius * Math.Sin(angle));

        var figure = new PathFigure { StartPoint = At(inner, startAngle) };
        figure.Segments.Add(new QuadraticBezierSegment(
            At((inner + outer) / 2, startAngle + Sweep * 0.35),
            At(outer, startAngle + Sweep), true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    /// <summary>
    /// The tile's colour, from the app's own cyan when the machine is cool to a warm amber
    /// when it is hot.
    ///
    /// It stays cyan well past idle and only warms from about 65 °C, because that is where
    /// the temperature starts meaning something on this laptop - and because a colour that
    /// drifts on every reading tells you nothing. The two ends are blended directly rather
    /// than walked round the colour wheel: the wheel's shortest path from cyan to amber runs
    /// through a loud green that belongs to no other part of this app.
    /// </summary>
    private Color Tint()
    {
        if (double.IsNaN(Temperature) || !IsLive) return Color.FromRgb(0x8A, 0x93, 0x9B);
        double share = Math.Clamp((Temperature - 55) / 30.0, 0, 1);
        return Color.FromRgb(
            (byte)(CoolColor.R + (WarmColor.R - CoolColor.R) * share),
            (byte)(CoolColor.G + (WarmColor.G - CoolColor.G) * share),
            (byte)(CoolColor.B + (WarmColor.B - CoolColor.B) * share));
    }
}
