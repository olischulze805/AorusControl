using System.Windows;
using System.Windows.Controls;
using Size = System.Windows.Size;
using Panel = System.Windows.Controls.Panel;
using Rect = System.Windows.Rect;

namespace AorusControl.App.Controls;

/// <summary>
/// Lays tiles out in as many equal columns as fit at <see cref="MinTileWidth"/>, so they
/// always fill the row and reflow from four across to one as the window narrows.
///
/// A WrapPanel was the obvious choice and the wrong one: with a fixed ItemWidth it leaves
/// a ragged gap on the right and a lone narrow column on small windows, and computing that
/// width from the panel's own ActualWidth feeds the layout back into itself.
/// </summary>
public sealed class TilePanel : Panel
{
    public static readonly DependencyProperty MinTileWidthProperty = DependencyProperty.Register(
        nameof(MinTileWidth), typeof(double), typeof(TilePanel),
        new FrameworkPropertyMetadata(260.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(TilePanel),
        new FrameworkPropertyMetadata(14.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double MinTileWidth
    {
        get => (double)GetValue(MinTileWidthProperty);
        set => SetValue(MinTileWidthProperty, value);
    }

    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    private int _columns = 1;
    private double _tileWidth, _rowHeight;

    protected override Size MeasureOverride(Size available)
    {
        if (InternalChildren.Count == 0) return new Size(0, 0);
        // An unconstrained width (inside a horizontal StackPanel or ScrollViewer) would
        // otherwise mean "infinite columns"; one row of minimum-width tiles is the honest
        // answer there.
        double width = double.IsInfinity(available.Width) ? MinTileWidth * InternalChildren.Count : available.Width;
        _columns = Math.Max(1, (int)((width + Gap) / (MinTileWidth + Gap)));
        _tileWidth = (width - Gap * (_columns - 1)) / _columns;

        _rowHeight = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(_tileWidth, double.PositiveInfinity));
            _rowHeight = Math.Max(_rowHeight, child.DesiredSize.Height);
        }

        int rows = (InternalChildren.Count + _columns - 1) / _columns;
        return new Size(width, rows * _rowHeight + Gap * (rows - 1));
    }

    protected override Size ArrangeOverride(Size final)
    {
        for (int index = 0; index < InternalChildren.Count; index++)
        {
            int column = index % _columns, row = index / _columns;
            InternalChildren[index].Arrange(new Rect(
                column * (_tileWidth + Gap), row * (_rowHeight + Gap), _tileWidth, _rowHeight));
        }
        return final;
    }
}
