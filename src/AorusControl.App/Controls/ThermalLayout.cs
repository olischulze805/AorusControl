using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Panel = System.Windows.Controls.Panel;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace AorusControl.App.Controls;

/// <summary>
/// This laptop's cooling system, drawn: two blowers, the heat pipes between them, the fin
/// stacks they blow through, and the two chips they are there for.
///
/// It is a redrawing of the AORUS 5 SE4's own thermal layout - two fans in the outer corners,
/// a long pipe across the top, the crossed pair between the fans, and a loop from each fan
/// down to the side exhaust - not a photograph and not a schematic anyone should measure
/// against. What makes it worth the pixels is that it is wired to the real machine: the
/// rotors turn at the measured speed, the light travelling along the pipes moves with how
/// hard each fan is working, and the fins brighten with the temperature. Switching to Leise
/// and watching the flow slow down says more than any number does.
///
/// Everything is drawn in a fixed design space and scaled to whatever width it is given, so
/// the picture keeps its proportions and stays sharp instead of being a bitmap someone has to
/// re-export for every screen.
/// </summary>
public sealed class ThermalLayout : Panel
{
    private const double DesignWidth = 1000, DesignHeight = 380;
    private static readonly Point LeftFan = new(255, 168), RightFan = new(745, 168);
    private const double RotorSize = 208;

    private static readonly Color PipeColor = Color.FromRgb(0x6B, 0x5C, 0x41);
    private static readonly Color PipeEdgeColor = Color.FromRgb(0x9C, 0x85, 0x57);
    private static readonly Color FlowColor = Color.FromRgb(0xF0, 0xD2, 0x8C);
    private static readonly Color BoardColor = Color.FromRgb(0x0B, 0x0D, 0x10);
    private static readonly Color OutlineColor = Color.FromRgb(0x25, 0x2B, 0x33);
    private static readonly Color PartColor = Color.FromRgb(0x14, 0x18, 0x1D);
    private static readonly Color LabelColor = Color.FromRgb(0x6C, 0x76, 0x82);

    private readonly FanRotor _left = new(), _right = new();
    private double _phase;
    private bool _hooked;
    private long _lastTick;

    public static readonly DependencyProperty CpuRpmProperty = Forwarded(nameof(CpuRpm));
    public static readonly DependencyProperty GpuRpmProperty = Forwarded(nameof(GpuRpm));
    public static readonly DependencyProperty CpuDutyProperty = Forwarded(nameof(CpuDuty));
    public static readonly DependencyProperty GpuDutyProperty = Forwarded(nameof(GpuDuty));
    public static readonly DependencyProperty CpuTemperatureProperty = Forwarded(nameof(CpuTemperature), double.NaN);
    public static readonly DependencyProperty GpuTemperatureProperty = Forwarded(nameof(GpuTemperature), double.NaN);

    public static readonly DependencyProperty IsLiveProperty = DependencyProperty.Register(
        nameof(IsLive), typeof(bool), typeof(ThermalLayout),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender,
            (layout, _) => ((ThermalLayout)layout).OnLiveChanged()));

    private static DependencyProperty Forwarded(string name, double fallback = 0) =>
        DependencyProperty.Register(name, typeof(double), typeof(ThermalLayout),
            new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender,
                (layout, args) => ((ThermalLayout)layout).Forward(name, args.NewValue)));

    public double CpuRpm { get => (double)GetValue(CpuRpmProperty); set => SetValue(CpuRpmProperty, value); }
    public double GpuRpm { get => (double)GetValue(GpuRpmProperty); set => SetValue(GpuRpmProperty, value); }
    public double CpuDuty { get => (double)GetValue(CpuDutyProperty); set => SetValue(CpuDutyProperty, value); }
    public double GpuDuty { get => (double)GetValue(GpuDutyProperty); set => SetValue(GpuDutyProperty, value); }
    public double CpuTemperature { get => (double)GetValue(CpuTemperatureProperty); set => SetValue(CpuTemperatureProperty, value); }
    public double GpuTemperature { get => (double)GetValue(GpuTemperatureProperty); set => SetValue(GpuTemperatureProperty, value); }
    public bool IsLive { get => (bool)GetValue(IsLiveProperty); set => SetValue(IsLiveProperty, value); }

    public ThermalLayout()
    {
        Children.Add(_left);
        Children.Add(_right);
        Loaded += (_, _) => Hook(true);
        Unloaded += (_, _) => Hook(false);
        IsVisibleChanged += (_, _) => Hook(IsVisible);
    }

    /// <summary>The rotors are the same control the tiles use, so the two fans in the picture
    /// are not a second implementation of "a turning fan" that could drift from the first.</summary>
    private void Forward(string name, object value)
    {
        FanRotor rotor = name.StartsWith("Cpu", StringComparison.Ordinal) ? _left : _right;
        DependencyProperty property = name[3..] switch
        {
            "Rpm" => FanRotor.RpmProperty,
            "Duty" => FanRotor.DutyProperty,
            _ => FanRotor.TemperatureProperty
        };
        rotor.SetValue(property, value);
    }

    private void OnLiveChanged()
    {
        _left.IsLive = _right.IsLive = IsLive;
        Hook(IsVisible && IsLoaded);
    }

    // ---- the flow -------------------------------------------------------------------
    private double FlowSpeed => IsLive ? Math.Clamp(Math.Max(CpuDuty, GpuDuty), 0, 100) / 100 : 0;

    private void Hook(bool wanted)
    {
        bool needed = wanted && FlowSpeed > 0.01;
        if (needed == _hooked) return;
        _hooked = needed;
        if (needed)
        {
            _lastTick = Environment.TickCount64;
            CompositionTarget.Rendering += OnFrame;
        }
        else
        {
            CompositionTarget.Rendering -= OnFrame;
        }
    }

    private void OnFrame(object? sender, EventArgs eventArgs)
    {
        long now = Environment.TickCount64;
        double elapsed = Math.Clamp((now - _lastTick) / 1000.0, 0, 0.25);
        _lastTick = now;
        // One pattern length per second at full duty, a crawl at the bottom of the range.
        _phase = (_phase + elapsed * (0.25 + FlowSpeed * 1.6)) % 1;
        InvalidateVisual();
    }

    // ---- layout ---------------------------------------------------------------------
    private double Scale => ActualWidth > 0 ? ActualWidth / DesignWidth : 1;

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? DesignWidth : availableSize.Width;
        double scale = width / DesignWidth;
        foreach (UIElement child in InternalChildren) child.Measure(new Size(RotorSize * scale, RotorSize * scale));
        return new Size(width, DesignHeight * scale);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double scale = finalSize.Width / DesignWidth, size = RotorSize * scale;
        foreach ((UIElement child, Point centre) in new[] { ((UIElement)_left, LeftFan), (_right, RightFan) })
            child.Arrange(new Rect(centre.X * scale - size / 2, centre.Y * scale - size / 2, size, size));
        return finalSize;
    }

    // ---- drawing --------------------------------------------------------------------
    protected override void OnRender(DrawingContext context)
    {
        if (ActualWidth <= 0) return;
        context.PushTransform(new ScaleTransform(Scale, Scale));

        // The chassis and the board it holds: everything else is drawn on top of this, which
        // is what stops the pipes from floating in mid-air.
        context.DrawRoundedRectangle(new SolidColorBrush(BoardColor), new Pen(new SolidColorBrush(OutlineColor), 1.5),
            new Rect(12, 12, DesignWidth - 24, DesignHeight - 24), 20, 20);

        DrawParts(context);
        DrawFins(context);
        foreach ((PathGeometry pipe, bool flows) in Pipes) DrawPipe(context, pipe, flows);
        DrawFanWells(context);
        DrawChips(context);

        context.Pop();
    }

    /// <summary>The parts that place the picture: the memory and the drive sit where they
    /// really do, which is what makes it read as this laptop rather than as a logo.</summary>
    private static void DrawParts(DrawingContext context)
    {
        var part = new SolidColorBrush(PartColor);
        var edge = new Pen(new SolidColorBrush(OutlineColor), 1);
        foreach (Rect slot in new[] { new Rect(600, 292, 280, 26), new Rect(600, 328, 280, 26) })
        {
            context.DrawRoundedRectangle(part, edge, slot, 4, 4);
            for (double x = slot.X + 14; x < slot.Right - 10; x += 13)
                context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0x6C, 0x76, 0x82)), 1),
                    new Point(x, slot.Y + 6), new Point(x, slot.Bottom - 6));
        }
        context.DrawRoundedRectangle(part, edge, new Rect(120, 300, 210, 24), 4, 4);
        Label(context, "RAM", new Point(890, 300), 13);
        Label(context, "SSD", new Point(134, 304), 13);
    }

    /// <summary>The four fin stacks: two at the back, one behind each side vent. They warm
    /// with the machine, so the picture says where the heat is going out.</summary>
    private void DrawFins(DrawingContext context)
    {
        double warmth = IsLive ? Math.Clamp((Math.Max(CpuTemperature, GpuTemperature) - 50) / 35.0, 0, 1) : 0;
        byte alpha = (byte)(0x40 + warmth * 0xA0);
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, FlowColor.R, FlowColor.G, FlowColor.B)), 3)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };

        foreach (double left in new[] { 172.0, 655.0 })
            for (double x = left; x < left + 176; x += 11)
                context.DrawLine(pen, new Point(x, 30), new Point(x, 48));

        foreach (double x in new[] { 34.0, 966.0 })
            for (double y = 96; y < 248; y += 11)
                context.DrawLine(pen, new Point(x - 9, y), new Point(x + 9, y));
    }

    /// <summary>
    /// A pipe is three strokes: the body, a lighter edge along it for the brushed-metal look,
    /// and - where the machine is actually moving heat - a dashed overlay whose offset moves,
    /// which is the flow. The dashes are what tie the picture to the live readings.
    /// </summary>
    private void DrawPipe(DrawingContext context, Geometry pipe, bool flows)
    {
        // Without a reading the whole assembly is dimmed rather than left looking lit: the
        // picture must never suggest it knows something it has not been told.
        context.DrawGeometry(null, RoundPen(PipeColor, IsLive ? (byte)0xFF : (byte)0xA0, 13), pipe);
        context.DrawGeometry(null, RoundPen(PipeEdgeColor, IsLive ? (byte)0x99 : (byte)0x44, 4), pipe);
        if (!flows || FlowSpeed <= 0.01) return;

        Pen flow = RoundPen(FlowColor, (byte)(0x60 + FlowSpeed * 0x8F), 5);
        flow.DashStyle = new DashStyle([0.5, 4.5], -_phase * 5);
        flow.DashCap = PenLineCap.Round;
        context.DrawGeometry(null, flow, pipe);
    }

    private static Pen RoundPen(Color color, byte alpha, double thickness) =>
        new(new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)), thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

    /// <summary>The blower housings. The rotors themselves are child controls arranged into
    /// these, so what turns in the picture is the same control the tiles use.</summary>
    private static void DrawFanWells(DrawingContext context)
    {
        var fill = new SolidColorBrush(Color.FromRgb(0x0E, 0x11, 0x15));
        var edge = new Pen(new SolidColorBrush(OutlineColor), 1.5);
        foreach (Point centre in new[] { LeftFan, RightFan })
            context.DrawRoundedRectangle(fill, edge,
                new Rect(centre.X - 112, centre.Y - 112, 224, 224), 38, 38);
    }

    /// <summary>The two chips everything else exists for, named, so the pipes visibly start
    /// somewhere rather than being decoration.</summary>
    private void DrawChips(DrawingContext context)
    {
        double warmth = IsLive ? 1 : 0;
        foreach ((string name, Rect area, double temperature) in new[]
        {
            ("CPU", new Rect(432, 212, 62, 54), CpuTemperature),
            ("GPU", new Rect(508, 212, 62, 54), GpuTemperature)
        })
        {
            double heat = IsLive && !double.IsNaN(temperature) ? Math.Clamp((temperature - 50) / 35.0, 0, 1) : 0;
            context.DrawRoundedRectangle(
                new SolidColorBrush(PartColor),
                RoundPen(FlowColor, (byte)(0x30 + warmth * heat * 0xB0), 1.5),
                area, 6, 6);
            Label(context, name, new Point(area.X + 12, area.Y + 18), 12);
        }
    }

    private static void Label(DrawingContext context, string text, Point at, double size) =>
        context.DrawText(
            new FormattedText(text, CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                size, new SolidColorBrush(LabelColor), 96) { TextAlignment = TextAlignment.Left },
            at);

    // ---- the pipe routing -----------------------------------------------------------
    // Built once and frozen: the shape never changes, only what is drawn along it.
    private static readonly (PathGeometry Pipe, bool Flows)[] Pipes = BuildPipes();

    private static (PathGeometry, bool)[] BuildPipes() =>
    [
        // The long pipe across the back, dipping between the two fans - the one that gives
        // this layout its silhouette. Drawn as two halves running outwards from the middle,
        // because that is the direction the heat travels and the flow has to say so.
        (Path(new Point(500, 104),
            Curve(new Point(430, 104), new Point(430, 46), new Point(352, 46)),
            Line(new Point(186, 46))), true),
        (Path(new Point(500, 104),
            Curve(new Point(570, 104), new Point(570, 46), new Point(648, 46)),
            Line(new Point(814, 46))), true),

        // The crossed pair between the fans. They cross because each chip is served by the
        // fan on the far side - drawn as two near-straight diagonals so the crossing reads as
        // a crossing, and flowing in opposite directions because that is what they do.
        (Path(new Point(548, 210),
            Curve(new Point(516, 202), new Point(404, 158), new Point(358, 142))), true),
        (Path(new Point(452, 210),
            Curve(new Point(484, 202), new Point(596, 158), new Point(642, 142))), true),

        // And the loop from each fan down and out to the side vent.
        (Path(new Point(255, 284),
            Curve(new Point(150, 300), new Point(64, 288), new Point(46, 230))), true),
        (Path(new Point(745, 284),
            Curve(new Point(850, 300), new Point(936, 288), new Point(954, 230))), true),

        // Short stubs from the chips up into the crossing, so the heat visibly comes from
        // somewhere.
        (Path(new Point(463, 212), Line(new Point(463, 176))), false),
        (Path(new Point(539, 212), Line(new Point(539, 176))), false)
    ];

    private static PathGeometry Path(Point start, params PathSegment[] segments)
    {
        var figure = new PathFigure { StartPoint = start };
        foreach (PathSegment segment in segments) figure.Segments.Add(segment);
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    private static LineSegment Line(Point to) => new(to, true);

    private static BezierSegment Curve(Point first, Point second, Point to) => new(first, second, to, true);
}
