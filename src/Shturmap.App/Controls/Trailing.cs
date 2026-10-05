using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Windows.Foundation;

namespace Shturmap.App.Controls;

/// <summary>
/// A text and a small mark right after its last word, where one more word would stand: the hand-over mark after a
/// raid card line ("Obtain hard drive" and the handshake). The first child is the text, wrapped to leave the mark
/// room on every line; the second, the mark, follows the end of the last line, centred on it. A collapsed mark takes no
/// room. A TextBlock can't hold an element inline, and a mark in a column of its own would stand by the distance
/// instead of the words.
/// </summary>
public sealed class Trailing : Panel
{
    private const double Gap = 3;

    private double MarkWidth => Children.Count > 1 && Children[1].Visibility == Visibility.Visible ? Children[1].DesiredSize.Width + Gap : 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count == 0)
            return new Size(0, 0);
        if (Children.Count > 1)
            Children[1].Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var mark = MarkWidth;
        var text = Children[0];
        text.Measure(new Size(Math.Max(0, availableSize.Width - mark), availableSize.Height));
        var height = Math.Max(text.DesiredSize.Height, Children.Count > 1 ? Children[1].DesiredSize.Height : 0);
        return new Size(text.DesiredSize.Width + mark, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0)
            return finalSize;
        var text = Children[0];
        // The width it was measured in, so it wraps where it did; its words stand at the left either way.
        text.Arrange(new Rect(0, 0, Math.Max(text.DesiredSize.Width, finalSize.Width - MarkWidth), text.DesiredSize.Height));
        if (Children.Count > 1)
        {
            var mark = Children[1];
            var size = mark.DesiredSize;
            // After the last character of the last line; every line left the mark its room. Without a layout to ask
            // (no TextBlock), after the widest line.
            var at = new Point(text.DesiredSize.Width + Gap, 0);
            if (text is TextBlock block && block.ContentEnd.GetCharacterRect(LogicalDirection.Backward) is var end &&
                !double.IsNaN(end.X) && !double.IsInfinity(end.X) && end.Height > 0)
                at = new Point(end.X + end.Width + Gap, Math.Max(0, end.Y + (end.Height - size.Height) / 2));
            mark.Arrange(new Rect(at, size));
        }
        return finalSize;
    }
}
