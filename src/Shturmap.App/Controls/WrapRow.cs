using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Shturmap.App.Controls;

/// <summary>
/// Lays its children out in a row that wraps to the next line when the width is used up, like words in a
/// paragraph: for a line of text whose parts are elements of their own (the bosses of a map, each linked to its
/// markers), which one TextBlock can't be. WinUI has no wrapping panel of its own.
/// </summary>
public sealed class WrapRow : Panel
{
    public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(
        nameof(HorizontalSpacing), typeof(double), typeof(WrapRow), new PropertyMetadata(0d, OnSpacingChanged));

    public static readonly DependencyProperty VerticalSpacingProperty = DependencyProperty.Register(
        nameof(VerticalSpacing), typeof(double), typeof(WrapRow), new PropertyMetadata(0d, OnSpacingChanged));

    public double HorizontalSpacing
    {
        get => (double)GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

    public double VerticalSpacing
    {
        get => (double)GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    private static void OnSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((WrapRow)d).InvalidateMeasure();

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        return Arrange(availableSize.Width, place: false);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Arrange(finalSize.Width, place: true);
        return finalSize;
    }

    // One pass for both: where each child goes in a row of this width, and how much room that takes.
    private Size Arrange(double width, bool place)
    {
        double x = 0, y = 0, line = 0, widest = 0;
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            if (size.Width == 0 && size.Height == 0)
                continue;
            if (x > 0 && x + size.Width > width)
            {
                x = 0;
                y += line + VerticalSpacing;
                line = 0;
            }
            if (place)
                child.Arrange(new Rect(x, y, size.Width, size.Height));
            widest = Math.Max(widest, x + size.Width);
            x += size.Width + HorizontalSpacing;
            line = Math.Max(line, size.Height);
        }
        return new Size(double.IsInfinity(width) ? widest : Math.Min(widest, width), y + line);
    }
}
