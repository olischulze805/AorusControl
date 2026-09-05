using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Cursors = System.Windows.Input.Cursors;
using Brushes = System.Windows.Media.Brushes;
using Size = System.Windows.Size;

namespace AorusControl.App.Controls;

/// <summary>
/// The fan curve as temperature °C × fan speed %, drawn at whatever size it is given and -
/// where the running profile allows it - edited in place.
///
/// It is a control rather than window code because the curve appears in several states: the
/// editable one under Dynamic, a flat line under Maximum and Fixed, and Gigabyte's own curve
/// as a flat line under Maximum and Fixed. One implementation means those views cannot drift.
///
/// Editing works on handles, not on the firmware's fifteen points. A fan curve is usually
/// three or four decisions, and fifteen draggable dots is a worse way to express them: points
/// are added by clicking empty plot, removed with a right-click or Delete, dragged, and nudged
/// with the arrow keys once selected - the last being the only way to place a value exactly,
/// since no mouse reliably hits one degree. FanCurveShape turns whatever was drawn into the
/// fifteen points the EC demands.
///
/// Every move is clamped live against the real rules using the neighbours, so the chart cannot
/// even display a shape the firmware would reject: non-decreasing in both axes, and never below
/// the floor for that temperature - zero below 60 °C, where the fans were measured to actually
/// stand still, and 25 % from there upwards.
/// The last handle is not the user's: full speed by 90 °C is required, so it is drawn muted
/// and can be neither moved nor removed.
/// </summary>
public sealed class FanCurveChart : Canvas
{
    private const double TemperatureMin = 0, TemperatureMax = 100;
    private const int PercentMin = 0, PercentMax = 100;
    private const double PointHitRadius = 14;
    private static readonly Color AccentColor = Color.FromRgb(0x35, 0xC7, 0xE6);
    private static readonly Color LockedColor = Color.FromRgb(0x8A, 0x93, 0x9B);

    private int? _dragging;

    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows), typeof(IEnumerable<FanCurveRowViewModel>), typeof(FanCurveChart),
        new PropertyMetadata(null, (chart, args) => ((FanCurveChart)chart).OnRowsChanged(args)));

    /// <summary>A single speed held at every temperature - what Maximum and a fixed value
    /// really are. NaN draws nothing.</summary>
    public static readonly DependencyProperty ConstantPercentProperty = DependencyProperty.Register(
        nameof(ConstantPercent), typeof(double), typeof(FanCurveChart),
        new PropertyMetadata(double.NaN, (chart, _) => ((FanCurveChart)chart).Redraw()));

    /// <summary>Where the machine is right now: the temperature it is running at and the duty
    /// the fans are really at. NaN draws nothing.</summary>
    public static readonly DependencyProperty LiveTemperatureProperty = DependencyProperty.Register(
        nameof(LiveTemperature), typeof(double), typeof(FanCurveChart),
        new PropertyMetadata(double.NaN, (chart, _) => ((FanCurveChart)chart).Redraw()));

    public static readonly DependencyProperty LivePercentProperty = DependencyProperty.Register(
        nameof(LivePercent), typeof(double), typeof(FanCurveChart),
        new PropertyMetadata(double.NaN, (chart, _) => ((FanCurveChart)chart).Redraw()));

    public static readonly DependencyProperty IsEditableProperty = DependencyProperty.Register(
        nameof(IsEditable), typeof(bool), typeof(FanCurveChart),
        new PropertyMetadata(false, (chart, _) => ((FanCurveChart)chart).OnEditableChanged()));

    /// <summary>Which handle is selected, or -1. Selection is what makes the keyboard useful,
    /// and what a right-click or Delete acts on.</summary>
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex), typeof(int), typeof(FanCurveChart),
        new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (chart, _) => ((FanCurveChart)chart).Redraw()));

    public IEnumerable<FanCurveRowViewModel>? Rows
    {
        get => (IEnumerable<FanCurveRowViewModel>?)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public double ConstantPercent
    {
        get => (double)GetValue(ConstantPercentProperty);
        set => SetValue(ConstantPercentProperty, value);
    }

    public double LiveTemperature
    {
        get => (double)GetValue(LiveTemperatureProperty);
        set => SetValue(LiveTemperatureProperty, value);
    }

    public double LivePercent
    {
        get => (double)GetValue(LivePercentProperty);
        set => SetValue(LivePercentProperty, value);
    }

    public bool IsEditable
    {
        get => (bool)GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>Raised whenever the curve changed - moved, added or removed. The chart writes
    /// nothing itself; the owner decides when a change reaches the device.</summary>
    public event EventHandler? CurveEdited;

    public FanCurveChart()
    {
        Background = Brushes.Transparent;
        ClipToBounds = true;
        Focusable = true;
        FocusVisualStyle = null;
        SizeChanged += (_, _) => Redraw();
    }

    private void OnEditableChanged()
    {
        if (!IsEditable) SelectedIndex = -1;
        Redraw();
    }

    private void OnRowsChanged(DependencyPropertyChangedEventArgs args)
    {
        // Follow the collection AND each row: the curve is repopulated on load and edited in
        // place afterwards, and both have to reach the drawing.
        if (args.OldValue is INotifyCollectionChanged oldCollection) oldCollection.CollectionChanged -= OnCollectionChanged;
        foreach (FanCurveRowViewModel row in Enumerate(args.OldValue)) row.PropertyChanged -= OnRowPropertyChanged;
        if (args.NewValue is INotifyCollectionChanged newCollection) newCollection.CollectionChanged += OnCollectionChanged;
        foreach (FanCurveRowViewModel row in Enumerate(args.NewValue)) row.PropertyChanged += OnRowPropertyChanged;
        SelectedIndex = -1;
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

    // ---- editing -------------------------------------------------------------------
    private List<FanCurveRowViewModel> Handles => Rows?.ToList() ?? [];
    private ObservableCollection<FanCurveRowViewModel>? Editable => Rows as ObservableCollection<FanCurveRowViewModel>;

    /// <summary>The last handle belongs to the firmware, not the user: full speed by 90 °C is
    /// required, so it cannot be moved or removed.</summary>
    private static bool IsLocked(int index, int count) => index == count - 1;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs eventArgs)
    {
        base.OnMouseLeftButtonDown(eventArgs);
        if (!IsEditable) return;
        Focus();
        List<FanCurveRowViewModel> handles = Handles;
        if (handles.Count == 0) return;

        Point position = eventArgs.GetPosition(this);
        int hit = HandleAt(handles, position);
        if (hit >= 0)
        {
            SelectedIndex = hit;
            if (IsLocked(hit, handles.Count)) return;
            _dragging = hit;
            CaptureMouse();
            Set(handles, hit, TemperatureFromCanvasX(position.X), PercentFromCanvasY(position.Y));
            return;
        }

        // A click on empty plot adds a point there. Without it the editor could only ever lose
        // handles, which would make removing one a decision nobody dares take.
        Add(position);
    }

    protected override void OnMouseMove(MouseEventArgs eventArgs)
    {
        base.OnMouseMove(eventArgs);
        if (_dragging is not { } index) return;
        Point position = eventArgs.GetPosition(this);
        Set(Handles, index, TemperatureFromCanvasX(position.X), PercentFromCanvasY(position.Y));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs eventArgs)
    {
        base.OnMouseLeftButtonUp(eventArgs);
        bool wasDragging = _dragging is not null;
        _dragging = null;
        ReleaseMouseCapture();
        if (wasDragging) CurveEdited?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs eventArgs)
    {
        base.OnMouseRightButtonUp(eventArgs);
        if (!IsEditable) return;
        List<FanCurveRowViewModel> handles = Handles;
        int hit = HandleAt(handles, eventArgs.GetPosition(this));
        if (hit < 0) return;
        SelectedIndex = hit;
        Remove(handles, hit);
        eventArgs.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (!IsEditable) return;
        List<FanCurveRowViewModel> handles = Handles;
        if (handles.Count == 0) return;

        // Home and End reach the ends without the mouse; the arrow keys then walk from there.
        if (eventArgs.Key is Key.Home or Key.End)
        {
            SelectedIndex = eventArgs.Key == Key.Home ? 0 : handles.Count - 1;
            eventArgs.Handled = true;
            return;
        }

        if (SelectedIndex < 0 || SelectedIndex >= handles.Count) return;

        if (eventArgs.Key is Key.Delete or Key.Back)
        {
            Remove(handles, SelectedIndex);
            eventArgs.Handled = true;
            return;
        }

        // The arrow keys are the only way to place a value exactly - no mouse reliably hits one
        // degree. Ctrl moves in fives, for crossing the chart without forty keystrokes.
        int step = (Keyboard.Modifiers & ModifierKeys.Control) != 0 ? 5 : 1;
        (double temperature, int percent) = eventArgs.Key switch
        {
            Key.Left => (-step, 0),
            Key.Right => (step, 0),
            Key.Up => (0, step),
            Key.Down => (0, -step),
            _ => (0.0, 0)
        };
        if (temperature == 0 && percent == 0) return;

        FanCurveRowViewModel handle = handles[SelectedIndex];
        Set(handles, SelectedIndex, handle.TemperatureNumber + temperature, handle.Percent + percent);
        CurveEdited?.Invoke(this, EventArgs.Empty);
        eventArgs.Handled = true;
    }

    private int HandleAt(IReadOnlyList<FanCurveRowViewModel> handles, Point position)
    {
        for (int index = 0; index < handles.Count; index++)
        {
            var center = new Point(ToCanvasX(handles[index].TemperatureNumber), ToCanvasY(handles[index].Percent));
            if ((position - center).Length <= PointHitRadius) return index;
        }
        return -1;
    }

    private void Add(Point position)
    {
        if (Editable is not { } collection || collection.Count >= FanCurveShape.FirmwarePoints) return;

        double temperature = Math.Round(TemperatureFromCanvasX(position.X));
        // Only between existing handles: a point past the last one would sit beyond the
        // firmware's own end, which is fixed.
        int at = -1;
        for (int index = 0; index < collection.Count; index++)
        {
            if (collection[index].TemperatureNumber <= temperature) continue;
            at = index;
            break;
        }
        if (at <= 0) return;

        collection.Insert(at, new FanCurveRowViewModel(at + 1)
        {
            TemperatureNumber = temperature,
            Percent = PercentFromCanvasY(position.Y)
        });
        Renumber(collection);
        SelectedIndex = at;
        // Clamped like any other move, so an added point cannot break the shape either.
        Set(Handles, at, temperature, PercentFromCanvasY(position.Y));
        CurveEdited?.Invoke(this, EventArgs.Empty);
    }

    private void Remove(List<FanCurveRowViewModel> handles, int index)
    {
        if (Editable is not { } collection) return;
        // The ends anchor the curve, and the firmware owns the last one.
        if (handles.Count <= FanCurveShape.MinimumHandles || index <= 0 || IsLocked(index, handles.Count)) return;

        collection.RemoveAt(index);
        Renumber(collection);
        SelectedIndex = Math.Min(index, collection.Count - 1);
        CurveEdited?.Invoke(this, EventArgs.Empty);
    }

    private static void Renumber(ObservableCollection<FanCurveRowViewModel> collection)
    {
        for (int index = 0; index < collection.Count; index++) collection[index].Number = index + 1;
    }

    /// <summary>
    /// Places a handle, clamped against its neighbours and the firmware's floor, so the chart
    /// can never display a shape that would be rejected on write.
    /// </summary>
    private void Set(List<FanCurveRowViewModel> handles, int index, double temperature, double percent)
    {
        if (index < 0 || index >= handles.Count || IsLocked(index, handles.Count)) return;

        double minTemperature = index == 0 ? TemperatureMin : handles[index - 1].TemperatureNumber + 1;
        double maxTemperature = handles[index + 1].TemperatureNumber - 1;
        handles[index].TemperatureNumber = Math.Clamp(Math.Round(temperature), minTemperature, Math.Max(minTemperature, maxTemperature));

        // The floor follows the temperature the handle actually ended up at, so it has to be
        // taken after the clamp above: zero while the machine is cool enough for the fans to
        // stand still, the verified 25 % from 60 °C upwards.
        int floor = FanCurveShape.MinimumPercentAt(handles[index].TemperatureNumber);
        int minPercent = index == 0 ? floor : Math.Max(floor, handles[index - 1].Percent);
        int maxPercent = handles[index + 1].Percent;
        handles[index].Percent = Math.Clamp((int)Math.Round(percent), minPercent, Math.Max(minPercent, maxPercent));
    }

    // ---- drawing -------------------------------------------------------------------
    private void Redraw()
    {
        Children.Clear();
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        List<FanCurveRowViewModel> handles = Handles;

        // Two densities rather than one: five-degree lines to actually read a value off the
        // chart, twenty-degree lines to keep it navigable, and labels only on the latter -
        // twenty-one of them would collide into a grey band. On a short chart the fine grid
        // would be a hatch pattern instead of a grid, so it is dropped and the labels thin out.
        bool roomy = ActualHeight >= 190;
        Brush minorBrush = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
        Brush majorBrush = new SolidColorBrush(Color.FromArgb(0x32, 0xFF, 0xFF, 0xFF));
        Brush labelBrush = (Brush)(TryFindResource("TextFillColorSecondaryBrush") ?? Brushes.Gray);
        int labelStep = roomy ? 20 : 50;

        if (roomy)
        {
            for (int temperature = 5; temperature < 100; temperature += 5)
            {
                if (temperature % 20 == 0) continue;
                AddLine(ToCanvasX(temperature), PlotTop, ToCanvasX(temperature), PlotBottom, minorBrush, dashed: false);
            }
            for (int percent = 5; percent < 100; percent += 5)
            {
                if (percent % 20 == 0) continue;
                AddLine(PlotLeft, ToCanvasY(percent), PlotRight, ToCanvasY(percent), minorBrush, dashed: false);
            }
        }

        for (int temperature = 0; temperature <= 100; temperature += labelStep)
        {
            double x = ToCanvasX(temperature);
            AddLine(x, PlotTop, x, PlotBottom, majorBrush);
            AddLabel($"{temperature}°", x, PlotBottom + 6, labelBrush, centerHorizontally: true);
        }
        for (int percent = 0; percent <= 100; percent += labelStep)
        {
            double y = ToCanvasY(percent);
            AddLine(PlotLeft, y, PlotRight, y, majorBrush);
            AddLabel($"{percent}%", PlotLeft - 8, y - 8, labelBrush, rightAlign: true);
        }

        DrawConstant();
        DrawLivePoint();
        // An empty chart still shows its axes: a blank box reads as broken, while an empty grid
        // reads as "nothing to show here", which is what it is.
        if (handles.Count == 0) return;

        var points = new PointCollection(handles.Select(handle => new Point(ToCanvasX(handle.TemperatureNumber), ToCanvasY(handle.Percent))));

        // Carry the curve out to both edges of the plot. Below the first handle and above the
        // last one the fan does not stop existing - the firmware holds those values - so
        // drawing only between them left the chart ending in mid-air.
        points.Insert(0, new Point(PlotLeft, points[0].Y));
        points.Add(new Point(ToCanvasX(TemperatureMax), points[^1].Y));

        // A curve that is stored but not running is drawn in the muted colour: it is still the
        // real curve, with its real points, but the accent belongs to whatever is actually
        // driving the fans right now - under Fixed and Maximum that is the flat line above.
        Color curveColor = IsEditable ? AccentColor : LockedColor;

        var areaPoints = new PointCollection { new(points[0].X, PlotBottom) };
        foreach (Point point in points) areaPoints.Add(point);
        areaPoints.Add(new Point(points[^1].X, PlotBottom));
        Children.Add(new Polygon
        {
            Points = areaPoints,
            Fill = new LinearGradientBrush(
                Color.FromArgb(IsEditable ? (byte)0x55 : (byte)0x28, curveColor.R, curveColor.G, curveColor.B),
                Color.FromArgb(0x00, curveColor.R, curveColor.G, curveColor.B),
                new Point(0, 0), new Point(0, 1))
        });

        // A soft blurred duplicate underneath gives the line a gentle glow rather than a flat,
        // static-looking stroke.
        Children.Add(Line(points, curveColor, 7, 0.35, new BlurEffect { Radius = 8 }));
        Children.Add(Line(points, curveColor, 3, 1.0, null));

        // The points are drawn either way. Seeing where the curve bends is half of reading it,
        // and a locked curve says so by its colour and its cursor rather than by hiding them.
        for (int index = 0; index < handles.Count; index++) DrawHandle(handles, index, points[index + 1]);
    }

    private void DrawHandle(IReadOnlyList<FanCurveRowViewModel> handles, int index, Point center)
    {
        bool locked = !IsEditable || IsLocked(index, handles.Count);
        bool selected = IsEditable && index == SelectedIndex;
        Color color = locked ? LockedColor : AccentColor;
        double radius = locked ? 6 : 7;

        // The selection is a ring around the handle rather than a bigger handle: a dot that
        // changes size when you touch it is harder to place, not easier to see.
        if (selected)
        {
            var ring = new Ellipse
            {
                Width = radius * 2 + 12,
                Height = radius * 2 + 12,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2,
                Opacity = 0.6
            };
            SetLeft(ring, center.X - radius - 6);
            SetTop(ring, center.Y - radius - 6);
            Children.Add(ring);
        }

        var dot = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = new SolidColorBrush(color),
            Stroke = new SolidColorBrush(Color.FromRgb(0x0E, 0x10, 0x13)),
            StrokeThickness = 2,
            Cursor = locked ? Cursors.Arrow : Cursors.Hand,
            Effect = new DropShadowEffect { Color = color, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.7 },
            ToolTip = !IsEditable
                ? $"{handles[index].TemperatureNumber:0} °C / {handles[index].Percent} % · gespeicherte Kurve, unter diesem Modus nur zur Ansicht"
                : locked
                ? $"Fest: {handles[index].TemperatureNumber:0} °C / {handles[index].Percent} % - die Firmware verlangt volle Drehzahl spätestens hier"
                : $"{handles[index].TemperatureNumber:0} °C / {handles[index].Percent} % · Ziehen, Pfeiltasten verschieben, Rechtsklick entfernt"
        };
        SetLeft(dot, center.X - radius);
        SetTop(dot, center.Y - radius);
        Children.Add(dot);
    }

    /// <summary>
    /// Where the machine actually is: a dot at the current temperature and fan duty, with a
    /// dropped line to the axis. It is what turns the chart from a drawing into feedback -
    /// while a curve is being shaped, this dot says what the fans are doing about it.
    /// </summary>
    private void DrawLivePoint()
    {
        if (double.IsNaN(LiveTemperature) || double.IsNaN(LivePercent)) return;
        double x = ToCanvasX(LiveTemperature), y = ToCanvasY(LivePercent);
        var brush = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        brush.Freeze();

        Children.Add(new Line
        {
            X1 = x, Y1 = y, X2 = x, Y2 = PlotBottom,
            Stroke = brush, StrokeThickness = 1, Opacity = 0.25, StrokeDashArray = [2, 3]
        });

        var dot = new Ellipse
        {
            Width = 11,
            Height = 11,
            Fill = brush,
            Stroke = new SolidColorBrush(Color.FromRgb(0x0E, 0x10, 0x13)),
            StrokeThickness = 2,
            Effect = new DropShadowEffect { Color = Colors.White, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.8 },
            ToolTip = $"Jetzt: {LiveTemperature:0} °C / {LivePercent:0} %"
        };
        SetLeft(dot, x - 5.5);
        SetTop(dot, y - 5.5);
        Children.Add(dot);
    }

    /// <summary>A profile that holds one speed is one straight line, and drawing it is more
    /// honest than leaving the chart empty as if nothing were known.</summary>
    private void DrawConstant()
    {
        if (double.IsNaN(ConstantPercent)) return;
        double y = ToCanvasY(ConstantPercent);
        var brush = new SolidColorBrush(AccentColor);
        brush.Freeze();
        Children.Add(new Polyline
        {
            Points = [new Point(PlotLeft, y), new Point(ToCanvasX(TemperatureMax), y)],
            Stroke = brush,
            StrokeThickness = 3,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
    }

    private static Polyline Line(PointCollection points, Color color, double thickness, double opacity, Effect? effect) => new()
    {
        Points = points,
        Stroke = new SolidColorBrush(color),
        StrokeThickness = thickness,
        StrokeLineJoin = PenLineJoin.Round,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        Opacity = opacity,
        Effect = effect
    };

    private void AddLine(double x1, double y1, double x2, double y2, Brush brush, bool dashed = true) =>
        Children.Add(new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = brush,
            StrokeThickness = 1,
            // The fine grid stays solid: dashes at five-degree spacing read as noise rather
            // than as lines.
            StrokeDashArray = dashed ? [2, 3] : null,
            SnapsToDevicePixels = true
        });

    private void AddLabel(string text, double x, double y, Brush brush, bool centerHorizontally = false, bool rightAlign = false)
    {
        var label = new TextBlock { Text = text, Foreground = brush, FontSize = 11 };
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        SetLeft(label, centerHorizontally ? x - label.DesiredSize.Width / 2 : rightAlign ? x - label.DesiredSize.Width : x);
        SetTop(label, y);
        Children.Add(label);
    }
}
