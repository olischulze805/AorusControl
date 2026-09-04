using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using AorusControl.App.ViewModels;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Cursors = System.Windows.Input.Cursors;
using Brushes = System.Windows.Media.Brushes;
using Size = System.Windows.Size;

namespace AorusControl.App;

/// <summary>
/// Draws and drives the draggable fan-curve chart (temperature °C vs. fan speed %) shown
/// in the Cooling section. Kept in its own partial-class file, separate from the window's
/// general lifecycle code in MainWindow.xaml.cs, since it is a self-contained visual
/// component with its own coordinate math and mouse handling.
///
/// The chart is the only editor for the curve - no numeric grid - so every point must
/// stay visibly valid while dragging rather than only being checked at Apply time: each
/// drag is clamped live against FanCurveValidation's actual rules (57-229 raw / 25-100%,
/// non-decreasing temperature and speed) using the point's immediate neighbors, and the
/// last point is not draggable at all because the firmware requires it fixed at 100% by
/// 90 °C at the latest.
/// </summary>
public partial class MainWindow
{
    private const double ChartWidth = 660;
    private const double ChartHeight = 300;
    private const double PlotLeft = 46, PlotRight = ChartWidth - 14;
    private const double PlotTop = 14, PlotBottom = ChartHeight - 32;
    private const double TemperatureMin = 0, TemperatureMax = 100;
    private const int PercentMin = 0, PercentMax = 100;
    private const double PointHitRadius = 14;
    private static readonly Color AccentColor = Color.FromRgb(0x35, 0xC7, 0xE6);
    private static readonly Color LockedColor = Color.FromRgb(0x8A, 0x93, 0x9B);

    private int? _draggingFanCurveIndex;

    private static double ToCanvasX(double temperature) =>
        PlotLeft + (Math.Clamp(temperature, TemperatureMin, TemperatureMax) - TemperatureMin)
            / (TemperatureMax - TemperatureMin) * (PlotRight - PlotLeft);

    private static double ToCanvasY(double percent) =>
        PlotBottom - (Math.Clamp(percent, PercentMin, PercentMax) - PercentMin)
            / (PercentMax - PercentMin) * (PlotBottom - PlotTop);

    private static double TemperatureFromCanvasX(double x) =>
        TemperatureMin + Math.Clamp((x - PlotLeft) / (PlotRight - PlotLeft), 0, 1) * (TemperatureMax - TemperatureMin);

    private static int PercentFromCanvasY(double y) =>
        (int)Math.Round(PercentMin + Math.Clamp((PlotBottom - y) / (PlotBottom - PlotTop), 0, 1) * (PercentMax - PercentMin));

    private void OnFanCurveMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (!_viewModel.FanControlsEnabled) return;
        var rows = _viewModel.FanCurveRows;
        Point position = eventArgs.GetPosition(FanCurveCanvas);
        // The last point is fixed (firmware requires 100% by 90 °C at the latest) - it is
        // deliberately excluded here rather than merely discouraged, so a drag can never
        // even start on it.
        for (int index = 0; index < rows.Count - 1; index++)
        {
            var center = new Point(ToCanvasX(rows[index].TemperatureNumber), ToCanvasY(rows[index].Percent));
            if ((center - position).Length <= PointHitRadius)
            {
                _draggingFanCurveIndex = index;
                FanCurveCanvas.CaptureMouse();
                return;
            }
        }
    }

    private void OnFanCurveMouseMove(object sender, MouseEventArgs eventArgs)
    {
        if (_draggingFanCurveIndex is not { } index) return;
        UpdateFanCurvePoint(index, eventArgs.GetPosition(FanCurveCanvas));
    }

    private void OnFanCurveMouseUp(object sender, MouseButtonEventArgs eventArgs)
    {
        _draggingFanCurveIndex = null;
        FanCurveCanvas.ReleaseMouseCapture();
    }

    private void UpdateFanCurvePoint(int index, Point canvasPosition)
    {
        var rows = _viewModel.FanCurveRows;
        if (index < 0 || index >= rows.Count - 1) return;

        // Clamp against immediate neighbors so the curve is always valid while dragging,
        // not just rejected after the fact at Apply time - and never below the firmware's
        // tested floor (25% / raw 57), which every point must respect, not only the first.
        double minTemperature = index == 0 ? TemperatureMin : rows[index - 1].TemperatureNumber;
        double maxTemperature = rows[index + 1].TemperatureNumber;
        int minPercent = Math.Max(25, index == 0 ? 25 : rows[index - 1].Percent);
        int maxPercent = rows[index + 1].Percent;

        double temperature = Math.Clamp(TemperatureFromCanvasX(canvasPosition.X), minTemperature, maxTemperature);
        int percent = Math.Clamp(PercentFromCanvasY(canvasPosition.Y), minPercent, maxPercent);

        rows[index].TemperatureNumber = temperature;
        rows[index].Percent = percent;
        DrawFanCurveChart();
    }

    private void DrawFanCurveChart()
    {
        if (FanCurveCanvas is null) return;
        FanCurveCanvas.Children.Clear();
        var rows = _viewModel.FanCurveRows;
        if (rows.Count == 0) return;

        Brush gridBrush = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
        Brush labelBrush = (Brush)(TryFindResource("TextFillColorSecondaryBrush") ?? Brushes.Gray);

        for (int temperature = 0; temperature <= 100; temperature += 20)
        {
            double x = ToCanvasX(temperature);
            AddLine(x, PlotTop, x, PlotBottom, gridBrush);
            AddLabel($"{temperature}°", x, PlotBottom + 6, labelBrush, centerHorizontally: true);
        }
        for (int percent = 0; percent <= 100; percent += 20)
        {
            double y = ToCanvasY(percent);
            AddLine(PlotLeft, y, PlotRight, y, gridBrush);
            AddLabel($"{percent}%", PlotLeft - 8, y - 8, labelBrush, rightAlign: true);
        }

        var points = new PointCollection(rows.Select(row => new Point(ToCanvasX(row.TemperatureNumber), ToCanvasY(row.Percent))));

        // Soft area fill under the curve for a modern "area chart" look.
        var areaPoints = new PointCollection { new(points[0].X, PlotBottom) };
        foreach (Point point in points) areaPoints.Add(point);
        areaPoints.Add(new Point(points[^1].X, PlotBottom));
        var areaFill = new LinearGradientBrush(
            Color.FromArgb(0x55, AccentColor.R, AccentColor.G, AccentColor.B),
            Color.FromArgb(0x00, AccentColor.R, AccentColor.G, AccentColor.B),
            new Point(0, 0), new Point(0, 1));
        FanCurveCanvas.Children.Add(new Polygon { Points = areaPoints, Fill = areaFill });

        // A soft blurred duplicate of the line underneath gives it a gentle glow rather
        // than a flat, static-looking stroke.
        FanCurveCanvas.Children.Add(new Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(AccentColor),
            StrokeThickness = 7,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Opacity = 0.35,
            Effect = new BlurEffect { Radius = 8 }
        });
        FanCurveCanvas.Children.Add(new Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(AccentColor),
            StrokeThickness = 3,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });

        for (int index = 0; index < rows.Count; index++)
        {
            bool locked = index == rows.Count - 1;
            Point center = points[index];
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
            Canvas.SetLeft(dot, center.X - radius);
            Canvas.SetTop(dot, center.Y - radius);
            FanCurveCanvas.Children.Add(dot);
        }
    }

    private void AddLine(double x1, double y1, double x2, double y2, Brush brush)
    {
        FanCurveCanvas.Children.Add(new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = brush,
            StrokeThickness = 1,
            StrokeDashArray = [2, 3]
        });
    }

    private void AddLabel(string text, double x, double y, Brush brush, bool centerHorizontally = false, bool rightAlign = false)
    {
        var label = new TextBlock { Text = text, Foreground = brush, FontSize = 11 };
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double left = centerHorizontally ? x - label.DesiredSize.Width / 2 : rightAlign ? x - label.DesiredSize.Width : x;
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, y);
        FanCurveCanvas.Children.Add(label);
    }
}
