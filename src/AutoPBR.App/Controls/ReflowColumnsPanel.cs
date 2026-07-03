using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace AutoPBR.App.Controls;

public class ReflowColumnsPanel : Panel
{
    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<ReflowColumnsPanel, double>(nameof(MinColumnWidth), 240d);

    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<ReflowColumnsPanel, int>(nameof(MaxColumns), 0);

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<ReflowColumnsPanel, double>(nameof(ColumnSpacing), 16d);

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<ReflowColumnsPanel, double>(nameof(RowSpacing), 0d);

    private const string WrappedRowClass = "reflow-wrapped-row";

    private int _columnCount;
    private IReadOnlyList<Control> _layoutChildren = Array.Empty<Control>();

    static ReflowColumnsPanel()
    {
        AffectsMeasure<ReflowColumnsPanel>(
            MinColumnWidthProperty,
            MaxColumnsProperty,
            ColumnSpacingProperty,
            RowSpacingProperty);
    }

    public double MinColumnWidth
    {
        get => GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    public int MaxColumns
    {
        get => GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }

    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    public double RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _layoutChildren = CollectVisibleChildren();

        if (_layoutChildren.Count == 0)
        {
            _columnCount = 0;
            return default;
        }

        var width = availableSize.Width;
        if (double.IsInfinity(width) || double.IsNaN(width))
            width = 0;

        _columnCount = ComputeColumnCount(width, _layoutChildren.Count);
        var columnWidth = ComputeColumnWidth(width, _columnCount);
        var columnConstraint = new Size(columnWidth, availableSize.Height);

        var rows = (_layoutChildren.Count + _columnCount - 1) / _columnCount;
        var rowHeights = new double[rows];
        var measuredWidth = 0d;

        for (var i = 0; i < _layoutChildren.Count; i++)
        {
            var child = _layoutChildren[i];
            child.Measure(columnConstraint);

            var row = i / _columnCount;
            rowHeights[row] = Math.Max(rowHeights[row], child.DesiredSize.Height);
        }

        if (double.IsPositiveInfinity(availableSize.Width))
        {
            measuredWidth = _columnCount * columnWidth + ColumnSpacing * Math.Max(0, _columnCount - 1);
        }
        else
        {
            measuredWidth = width;
        }

        var totalHeight = 0d;
        for (var row = 0; row < rows; row++)
        {
            totalHeight += rowHeights[row];
            if (row > 0)
                totalHeight += RowSpacing;
        }

        return new Size(measuredWidth, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_layoutChildren.Count == 0 || _columnCount <= 0)
            return finalSize;

        var columnWidth = ComputeColumnWidth(finalSize.Width, _columnCount);
        var rows = (_layoutChildren.Count + _columnCount - 1) / _columnCount;
        var rowHeights = new double[rows];

        for (var i = 0; i < _layoutChildren.Count; i++)
        {
            var row = i / _columnCount;
            rowHeights[row] = Math.Max(rowHeights[row], _layoutChildren[i].DesiredSize.Height);
        }

        var layoutSet = new HashSet<Control>(_layoutChildren);
        foreach (var child in Children)
        {
            if (child is Control control)
                control.Classes.Remove(WrappedRowClass);
        }

        var y = 0d;
        for (var row = 0; row < rows; row++)
        {
            var x = 0d;
            for (var col = 0; col < _columnCount; col++)
            {
                var index = row * _columnCount + col;
                if (index >= _layoutChildren.Count)
                    break;

                var child = _layoutChildren[index];
                if (row > 0 && col == 0)
                    child.Classes.Add(WrappedRowClass);

                child.Arrange(new Rect(x, y, columnWidth, rowHeights[row]));
                x += columnWidth + ColumnSpacing;
            }

            y += rowHeights[row];
            if (row < rows - 1)
                y += RowSpacing;
        }

        foreach (var child in Children)
        {
            if (child is Control control && !layoutSet.Contains(control))
                control.Classes.Remove(WrappedRowClass);
        }

        return finalSize;
    }

    private List<Control> CollectVisibleChildren()
    {
        var visibleChildren = new List<Control>();
        foreach (var child in Children)
        {
            if (child is Control control && control.IsVisible)
                visibleChildren.Add(control);
        }

        return visibleChildren;
    }

    private int ComputeColumnCount(double width, int childCount)
    {
        if (childCount <= 0)
            return 0;

        var gap = ColumnSpacing;
        var minCol = Math.Max(1d, MinColumnWidth);

        int cols;
        if (width <= 0 || double.IsPositiveInfinity(width))
        {
            cols = MaxColumns > 0 ? Math.Min(MaxColumns, childCount) : childCount;
        }
        else
        {
            cols = (int)Math.Floor((width + gap) / (minCol + gap));
            cols = Math.Max(1, Math.Min(cols, childCount));
            if (MaxColumns > 0)
                cols = Math.Min(cols, MaxColumns);
        }

        return cols;
    }

    private double ComputeColumnWidth(double totalWidth, int columnCount)
    {
        if (columnCount <= 0)
            return 0;

        if (totalWidth <= 0 || double.IsPositiveInfinity(totalWidth))
            return MinColumnWidth;

        var gaps = ColumnSpacing * Math.Max(0, columnCount - 1);
        return Math.Max(0, (totalWidth - gaps) / columnCount);
    }
}
