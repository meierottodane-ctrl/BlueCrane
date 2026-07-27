using System.Windows;
using System.Windows.Controls;

namespace BlueCrane.Shell;

/// <summary>
/// Lays tabs left-to-right, sharing the strip evenly and shrinking as the count grows —
/// the behaviour every browser has and no stock WPF panel provides. A StackPanel would
/// overflow, a UniformGrid would stretch three tabs across the whole window.
/// </summary>
public sealed class TabStripPanel : Panel
{
    public double MaxTabWidth { get; set; } = 200;
    public double MinTabWidth { get; set; } = 56;

    private double TabWidth(double available, int count)
    {
        if (count == 0) return 0;
        if (double.IsInfinity(available)) return MaxTabWidth;
        return Math.Clamp(available / count, MinTabWidth, MaxTabWidth);
    }

    protected override Size MeasureOverride(Size available)
    {
        var count = InternalChildren.Count;
        if (count == 0) return new Size(0, 0);

        var width = TabWidth(available.Width, count);
        double height = 0;

        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(width, available.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }

        var total = width * count;
        return new Size(double.IsInfinity(available.Width) ? total : Math.Min(total, available.Width), height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var count = InternalChildren.Count;
        if (count == 0) return final;

        var width = TabWidth(final.Width, count);
        double x = 0;

        foreach (UIElement child in InternalChildren)
        {
            child.Arrange(new Rect(x, 0, width, final.Height));
            x += width;
        }

        return final;
    }
}
