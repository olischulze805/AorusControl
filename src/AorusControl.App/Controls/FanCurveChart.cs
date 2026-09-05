using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using AorusControl.App.ViewModels;
using AorusControl.Core.Features.Cooling;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Cursors = System.Windows.Input.Cursors;
using Brushes = System.Windows.Media.Brushes;
using Size = System.Windows.Size;

namespace AorusControl.App.Controls;

/// <summary>
/// The fan curve as temperature °C × fan speed %, drawn at whatever size it is given.
///
/// It is a control rather than window code because the curve is shown twice: editable in
/// the Cooling section, and read-only under the power modes, where the point is to see at
/// a glance what the fans will actually do. One implementation means the read-only view
/// can never drift from the editor.
///
/// While <see cref="IsEditable"/> is set, every drag is clamped live against
/// FanCurveValidation's real rules (25-100%, non-decreasing temperature and speed) using
/// the point's immediate neighbours, so the chart cannot even display an invalid shape.
/// The last point is not draggable at all - the firmware requires it fixed - and is drawn
/// muted rather than merely being hard to hit.
/// </summary>
public sealed class FanCurveChart : Canvas
{
    private const double TemperatureMin = 0, TemperatureMax = 100;
    private const int PercentMin = 0, PercentMax = 100;
    private const double PointHitRadius = 14;
    private static readonly Color AccentColor = Color.FromRgb(0x35, 0xC7, 0xE6);
    private static readonly Color LockedColor = Color.FromRgb(0x8A, 0x93, 0x9B);
    private static readonly Color ReferenceColor = Color.FromRgb(0xF2, 0xB1, 0x4C);

    private int? _dragging;

    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows), typeof(IEnumerable<FanCurveRowViewModel>), typeof(FanCurveChart),
        new PropertyMetadata(null, (chart, args) => ((FanCurveChart)chart).OnRowsChanged(args)));

    /// <summary>A second curve drawn for comparison - Gigabyte's own, so the edited one can be
    /// judged against it. Temperature in °C, speed in percent.</summary>
    public static readonly DependencyProperty ReferenceProperty = DependencyProperty.Register(
        nameof(Reference), typeof(IEnumerable<(byte TemperatureCelsius, byte Percent)>), typeof(FanCurveChart),
        new PropertyMetadata(null, (chart, _) => ((FanCurveChart)chart).Redraw()));

    public static readonly DependencyProperty IsEditableProperty = DependencyProperty.Register(
        nameof(IsEditable), typeof(bool), typeof(FanCurveChart),
        new PropertyMetadata(false, (chart, _) => ((FanCurveChart)chart).Redraw()));

    public IEnumerable<FanCurveRowViewModel>? Rows
    {
        get => (IEnumerable<FanCurveRowViewModel>?)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public IEnumerable<(byte TemperatureCelsius, byte Percent)>? Reference
    {
        get => (IEnumerable<(byte TemperatureCelsius, byte Percent)>?)GetValue(ReferenceProperty);
        set => SetValue(ReferenceProperty, value);
    }

    public bool IsEditable
    {
        get => (bool)GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    /// <summary>Raised when a drag ends, so the owner can schedule the device write. The
    /// chart deliberately does not write anything itself.</summary>
    public event EventHandler? CurveEdited;

    public FanCurveChart()
    {
        Background = Brushes.Transparent;
        ClipToBounds = true;
        SizeChanged += (_, _) => Redraw();
    }

    private void OnRowsChanged(DependencyPropertyChangedEventArgs args)
    {
        // Follow the collection AND each row: the curve is repopulated on load and edited
        // in place afterwards, and both have to reach the drawing.
        if (args.OldValue is INotifyCollectionChanged oldCollection) oldCollection.CollectionChanged -= OnCollectionChanged;
        foreach (FanCurveRowViewModel row in Enumerate(args.OldValue)) row.PropertyChanged -= OnRowPropertyChanged;
        if (args.NewValue is INotifyCollectionChanged newCollection) newCollection.CollectionChanged += OnCollectionChanged;
        foreach (FanCurveRowViewModel row in Enumerate(args.NewValue)) row.PropertyChanged += OnRowPropertyChanged;
        Redraw();
    }

    private static IEnumerable<FanCurveRowViewModel> Enumerate(object? value) =>
        value as IEnumerable<FanCurveRowViewModel> ?? [];

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        foreach (FanCurveRowViewModel row in args.OldItems?.OfType<FanCurveRowViewModel>() ?? []) row.PropertyChanged -= OnRowPropertyChanged;
        foreach (FanCurveRowViewModel row in args.NewItems?.OfType<FanCurveRowViewModel>() ?? []) row.PropertyChanged += OnRowPropertyChanged;
        Redraw();
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs args) => Redraw();

    // ---- coordinates ---------------------------------------------------------------
    // Derived from the control's real size rather than a fixed canvas scaled by a Viewbox:
    // scaling makes the labels fuzzy and the dots grow with the window.
    private double PlotLeft => 46;
    private double PlotRight => Math.Max(PlotLeft + 1, ActualWidth - 14);
    private double PlotTop => 14;
    private double PlotBottom => Math.Max(PlotTop + 1, ActualHeight - 28);

    private double ToCanvasX(double temperature) =>
        PlotLeft + (Math.Clamp(temperature, TemperatureMin, TemperatureMax) - TemperatureMin)
            / (TemperatureMax - TemperatureMin) * (PlotRight - PlotLeft);

    private double ToCanvasY(double percent) =>
        PlotBottom - (Math.Clamp(percent, PercentMin, PercentMax) - PercentMin)
            / (PercentMax - PercentMin) * (PlotBottom - PlotTop);

    private double TemperatureFromCanvasX(double x) =>
        TemperatureMin + Math.Clamp((x - PlotLeft) / (PlotRight - PlotLeft), 0, 1) * (TemperatureMax - TemperatureMin);

    private int PercentFromCanvasY(double y) =>
        (int)Math.Round(PercentMin + Math.Clamp((PlotBottom - y) / (PlotBottom - PlotTop), 0, 1) * (PercentMax - PercentMin));

    // ---- dragging ------------------------------------------------------------------
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs eventArgs)
    {
        base.OnMouseLeftButtonDown(eventArgs);
        if (!IsEditable) return;
        var rows = Rows?.ToList();
        if (rows is null || rows.Count == 0) return;

        Point position = eventArgs.GetPosition(this);
        // The last point is excluded: the firmware pins it, so offering it for dragging
        // would be offering an edit that is going to be rejected.
        for (int index = 0; index < rows.Count - 1; index++)
        {
            var center = new Point(ToCanvasX(rows[index].TemperatureNumber), ToCanvasY(rows[index].Percent));
            if ((position - center).Length > PointHitRadius) continue;
            _dragging = index;
            CaptureMouse();
            UpdatePoint(rows, index, position);
            return;
        }
    }

    protected override void OnMouseMove(MouseEventArgs eventArgs)
    {
        base.OnMouseMove(eventArgs);
        if (_dragging is not { } index) return;
        var rows = Rows?.ToList();
        if (rows is not null) UpdatePoint(rows, index, eventArgs.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs eventArgs)
    {
        base.OnMouseLeftButtonUp(eventArgs);
        bool wasDragging = _dragging is not null;
        _dragging = null;
        ReleaseMouseCapture();
        // Raised on release rather than on every move: a user who pauses mid-drag is still
        // shaping the curve, and writing there would switch the fan mode underneath the
        // gesture. The owner's debouncer still collapses several quick drags into one.
        if (wasDragging) CurveEdited?.Invoke(this, EventArgs.Empty);
    }

    private void UpdatePoint(IReadOnlyList<FanCurveRowViewModel> rows, int index, Point position)
    {
        if (index < 0 || index >= rows.Count - 1) return;

        // Clamp against the immediate neighbours so the curve is always valid while
        // dragging - and never below the firmware's tested floor (25% / raw 57), which
        // every point must respect, not only the first.
        double minTemperature = index == 0 ? TemperatureMin : rows[index - 1].TemperatureNumber;
        double maxTemperature = rows[index + 1].TemperatureNumber;
        int minPercent = Math.Max(25, index == 0 ? 25 : rows[index - 1].Percent);
        int maxPercent = rows[index + 1].Percent;

        rows[index].TemperatureNumber = Math.Clamp(TemperatureFromCanvasX(position.X), minTemperature, maxTemperature);
        rows[index].Percent = Math.Clamp(PercentFromCanvasY(position.Y), minPercent, maxPercent);
    }

    // ---- drawing -------------------------------------------------------------------
    private void Redraw()
    {
        Children.Clear();
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var rows = Rows?.ToList() ?? [];
        var reference = Reference?.OrderBy(point => point.TemperatureCelsius).ToList() ?? [];

        Brush gridBrush = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
        Brush labelBrush = (Brush)(TryFindResource("TextFillColorSecondaryBrush") ?? Brushes.Gray);
        // A short chart has no room for five labelled gridlines without them colliding.
        int step = ActualHeight < 190 ? 50 : 20;

        for (int temperature = 0; temperature <= 100; temperature += step)
        {
            double x = ToCanvasX(temperature);
            AddLine(x, PlotTop, x, PlotBottom, gridBrush);
            AddLabel($"{temperature}°", x, PlotBottom + 6, labelBrush, centerHorizontally: true);
        }
        for (int percent = 0; percent <= 100; percent += step)
        {
            double y = ToCanvasY(percent);
            AddLine(PlotLeft, y, PlotRight, y, gridBrush);
            AddLabel($"{percent}%", PlotLeft - 8, y - 8, labelBrush, rightAlign: true);
        }

        DrawReference(reference);
        // An empty chart still shows its axes: a blank box reads as broken, while an empty
        // grid reads as "nothing measured yet", which is what it is.
        if (rows.Count == 0) return;

        var points = new PointCollection(rows.Select(row => new Point(ToCanvasX(row.TemperatureNumber), ToCanvasY(row.Percent))));

        // Carry the curve out to both edges of the plot. Below the first point and above
        // the last one the fan does not stop existing - the firmware holds those values -
        // so drawing the line only between the points left the chart ending in mid-air.
        points.Insert(0, new Point(PlotLeft, points[0].Y));
        points.Add(new Point(ToCanvasX(TemperatureMax), points[^1].Y));

        var areaPoints = new PointCollection { new(points[0].X, PlotBottom) };
        foreach (Point point in points) areaPoints.Add(point);
        areaPoints.Add(new Point(points[^1].X, PlotBottom));
        Children.Add(new Polygon
        {
            Points = areaPoints,
            Fill = new LinearGradientBrush(
                Color.FromArgb(0x55, AccentColor.R, AccentColor.G, AccentColor.B),
                Color.FromArgb(0x00, AccentColor.R, AccentColor.G, AccentColor.B),
                new Point(0, 0), new Point(0, 1))
        });

        // A soft blurred duplicate underneath gives the line a gentle glow rather than a
        // flat, static-looking stroke.
        Children.Add(Line(points, 7, 0.35, new BlurEffect { Radius = 8 }));
        Children.Add(Line(points, 3, 1.0, null));

        // Read-only: the line and its fill say everything, and fifteen dots on a small
        // chart read as a dotted line rather than as points.
        if (!IsEditable) return;

        for (int index = 0; index < rows.Count; index++)
        {
            bool locked = index == rows.Count - 1;
            // +1: points[0] is the synthetic left edge, not a real curve point.
            Point center = points[index + 1];
            Color color = locked ? LockedColor : AccentColor;
            double radius = locked ? 6 : 7;
            var dot = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(Color.FromRgb(0x0E, 0x10, 0x13)),
                StrokeThickness = 2,
                Cursor = locked ? Cursors.Arrow : Cursors.Hand,
                Effect = new DropShadowEffect { Color = color, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.7 },
                ToolTip = locked
                    ? $"Fest: {rows[index].TemperatureNumber:0} °C / {rows[index].Percent}% (Firmware-Vorgabe)"
                    : $"{rows[index].TemperatureNumber:0} °C / {rows[index].Percent}%"
            };
            SetLeft(dot, center.X - radius);
            SetTop(dot, center.Y - radius);
            Children.Add(dot);
        }
    }

    /// <summary>
    /// The comparison curve: dashed, in its own colour, and thinner than the edited one. It is
    /// somebody else's setting rather than this device's state, and must not read as the same
    /// kind of claim.
    /// </summary>
    private void DrawReference(IReadOnlyList<(byte TemperatureCelsius, byte Percent)> reference)
    {
        if (reference.Count < 2) return;
        var brush = new SolidColorBrush(ReferenceColor);
        brush.Freeze();
        Children.Add(new Polyline
        {
            Points = new PointCollection(reference.Select(point => new Point(ToCanvasX(point.TemperatureCelsius), ToCanvasY(point.Percent)))),
            Stroke = brush,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeDashArray = [4, 3],
            Opacity = 0.9,
            ToolTip = "Kurve aus Gigabytes Control Center"
        });
    }

    private static Polyline Line(PointCollection points, double thickness, double opacity, Effect? effect) => new()
    {
        Points = points,
        Stroke = new SolidColorBrush(AccentColor),
        StrokeThickness = thickness,
        StrokeLineJoin = PenLineJoin.Round,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        Opacity = opacity,
        Effect = effect
    };

    private void AddLine(double x1, double y1, double x2, double y2, Brush brush) =>
        Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush, StrokeThickness = 1, StrokeDashArray = [2, 3] });

    private void AddLabel(string text, double x, double y, Brush brush, bool centerHorizontally = false, bool rightAlign = false)
    {
        var label = new TextBlock { Text = text, Foreground = brush, FontSize = 11 };
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        SetLeft(label, centerHorizontally ? x - label.DesiredSize.Width / 2 : rightAlign ? x - label.DesiredSize.Width : x);
        SetTop(label, y);
        Children.Add(label);
    }
}
