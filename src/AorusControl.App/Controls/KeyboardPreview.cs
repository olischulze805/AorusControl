using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Control = System.Windows.Controls.Control;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Size = System.Windows.Size;

namespace AorusControl.App.Controls;

/// <summary>
/// A picture of this exact laptop's keyboard - full layout including the numeric pad -
/// whose keys light up per RGB zone. The three zones are vertical bands across one
/// keyboard, not three separate pads, and the keycaps stay black while the legend and a
/// rim of spill light carry the colour: that is how the hardware actually looks
/// (research/APP-KEYBOARD-RGB.md, and the owner's photo of the device).
///
/// Built in code rather than 90 hand-written XAML elements: the layout below is the
/// single description of the keyboard, and the control turns it into a grid whose key
/// widths and zone assignment come straight from that table.
/// </summary>
public sealed class KeyboardPreview : Control
{
    // One grid column per quarter key unit, so a 1.25u or 2.25u key is still whole
    // columns. Every row totals 76 columns, which keeps the right edge flush.
    private const int ColumnsPerUnit = 4;
    private const int TotalColumns = 76;

    private readonly Grid _grid = new();
    private readonly List<(TextBlock Legend, Border Cap, DropShadowEffect Glow, int Zone)> _keys = [];
    private bool _appearancePending;

    public static readonly DependencyProperty Zone1BrushProperty = DependencyProperty.Register(
        nameof(Zone1Brush), typeof(Brush), typeof(KeyboardPreview),
        new PropertyMetadata(Brushes.Transparent, OnAppearanceChanged));

    public static readonly DependencyProperty Zone2BrushProperty = DependencyProperty.Register(
        nameof(Zone2Brush), typeof(Brush), typeof(KeyboardPreview),
        new PropertyMetadata(Brushes.Transparent, OnAppearanceChanged));

    public static readonly DependencyProperty Zone3BrushProperty = DependencyProperty.Register(
        nameof(Zone3Brush), typeof(Brush), typeof(KeyboardPreview),
        new PropertyMetadata(Brushes.Transparent, OnAppearanceChanged));

    /// <summary>Perceived brightness of the lighting, 0-1. Applied to the legends and
    /// their glow only - the keycaps themselves never change.</summary>
    public static readonly DependencyProperty LightingOpacityProperty = DependencyProperty.Register(
        nameof(LightingOpacity), typeof(double), typeof(KeyboardPreview),
        new PropertyMetadata(1.0, OnAppearanceChanged));

    public Brush Zone1Brush { get => (Brush)GetValue(Zone1BrushProperty); set => SetValue(Zone1BrushProperty, value); }
    public Brush Zone2Brush { get => (Brush)GetValue(Zone2BrushProperty); set => SetValue(Zone2BrushProperty, value); }
    public Brush Zone3Brush { get => (Brush)GetValue(Zone3BrushProperty); set => SetValue(Zone3BrushProperty, value); }
    public double LightingOpacity { get => (double)GetValue(LightingOpacityProperty); set => SetValue(LightingOpacityProperty, value); }

    public KeyboardPreview()
    {
        Focusable = false;
        BuildLayout();
        AddVisualChild(_grid);
        AddLogicalChild(_grid);
        ApplyAppearance();
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _grid;

    protected override Size MeasureOverride(Size availableSize)
    {
        _grid.Measure(availableSize);
        return _grid.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _grid.Arrange(new Rect(finalSize));
        return finalSize;
    }

    private static void OnAppearanceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((KeyboardPreview)sender).InvalidateAppearance();

    /// <summary>
    /// Coalesces the four appearance properties into one repaint. A preview frame sets
    /// three zone brushes and the opacity, and doing the ~90-key walk once per property
    /// meant four full passes per frame, twenty times a second.
    /// </summary>
    private void InvalidateAppearance()
    {
        if (_appearancePending) return;
        _appearancePending = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _appearancePending = false;
            ApplyAppearance();
        }), System.Windows.Threading.DispatcherPriority.Render);
    }

    private void ApplyAppearance()
    {
        double opacity = Math.Clamp(LightingOpacity, 0, 1);
        foreach ((TextBlock legend, Border cap, DropShadowEffect glow, int zone) in _keys)
        {
            Brush brush = zone switch { 1 => Zone1Brush, 2 => Zone2Brush, _ => Zone3Brush };
            legend.Foreground = brush;
            legend.Opacity = opacity;
            // The rim of light around each key is the same colour as its legend; on the
            // real keyboard that spill is most of what you see. The effect instance is
            // created once per key and only mutated here.
            if (brush is SolidColorBrush { Color: var color })
            {
                glow.Color = color;
                glow.Opacity = 0.55 * opacity;
                cap.Effect = glow;
            }
            else
            {
                cap.Effect = null;
            }
        }
    }

    private void BuildLayout()
    {
        _grid.Background = Brushes.Transparent;
        for (int column = 0; column < TotalColumns; column++)
        {
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        for (int row = 0; row < KeyboardLayout.Rows.Count; row++)
        {
            _grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (int row = 0; row < KeyboardLayout.Rows.Count; row++)
        {
            int column = 0;
            foreach (KeyboardLayout.Key key in KeyboardLayout.Rows[row])
            {
                int span = (int)Math.Round(key.Units * ColumnsPerUnit);
                // A gap reserves its columns without drawing anything.
                if (!key.IsGap) _grid.Children.Add(CreateKey(key, span, row, column));
                column += span;
            }
        }
    }

    private FrameworkElement CreateKey(KeyboardLayout.Key key, int span, int row, int column)
    {
        var legend = new TextBlock
        {
            Text = key.Legend,
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            TextTrimming = TextTrimming.None
        };

        var glow = new DropShadowEffect { BlurRadius = 12, ShadowDepth = 0, Opacity = 0 };
        var cap = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x15, 0x17, 0x1A)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Height = key.RowSpan > 1 ? double.NaN : 30,
            Margin = new Thickness(2),
            Child = legend
        };

        Grid.SetRow(cap, row);
        Grid.SetColumn(cap, column);
        Grid.SetColumnSpan(cap, span);
        if (key.RowSpan > 1) Grid.SetRowSpan(cap, key.RowSpan);

        _keys.Add((legend, cap, glow, key.Zone));
        return cap;
    }
}
