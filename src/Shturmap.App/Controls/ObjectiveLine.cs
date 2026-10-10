using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Shturmap.App.Rules;
using Windows.Foundation;

namespace Shturmap.App.Controls;

/// <summary>
/// An objective's line in the raid card: its words, then what it hands over (the item's cell and the mark, when it has
/// one), then where it is (the distance over its direction tag), <see cref="Spacing"/> apart, the last two at the right.
/// The words keep at least half of the line (<see cref="LineRoom"/>): where the other two would take more, where it is
/// is measured narrower, and its tag (<see cref="TagLines"/>) puts its parts one under the other. Otherwise it lays out
/// as a grid of a star column and two auto ones would.
/// </summary>
public sealed class ObjectiveLine : Panel
{
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(ObjectiveLine), new PropertyMetadata(10d, (d, _) => ((ObjectiveLine)d).InvalidateMeasure()));

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    // Its three children: the words, the hand-over, where it is.
    private (UIElement? Words, UIElement? Handover, UIElement? Where) Parts =>
        (Children.Count > 0 ? Children[0] : null, Children.Count > 1 ? Children[1] : null, Children.Count > 2 ? Children[2] : null);

    // What a part takes across: nothing for one that is collapsed or shows nothing.
    private static double Across(UIElement? e) => e is { Visibility: Visibility.Visible } ? e.DesiredSize.Width : 0;

    // The width a part takes with the spacing before it.
    private double Side(UIElement? e) => Across(e) > 0 ? Across(e) + Spacing : 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        var (words, handover, where) = Parts;
        var line = availableSize.Width;
        var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);
        handover?.Measure(unbounded);
        where?.Measure(unbounded);
        if (where is not null && Across(where) > 0 && LineRoom.Where(line, Across(handover), Across(where), Spacing) is { } room)
            where.Measure(new Size(room, double.PositiveInfinity));
        var rest = double.IsInfinity(line) ? double.PositiveInfinity : Math.Max(0, line - Side(handover) - Side(where));
        words?.Measure(new Size(rest, double.PositiveInfinity));
        var height = new[] { words, handover, where }.Where(e => e is { Visibility: Visibility.Visible }).Select(e => e!.DesiredSize.Height).DefaultIfEmpty(0).Max();
        var width = double.IsInfinity(line) ? (words?.DesiredSize.Width ?? 0) + Side(handover) + Side(where) : line;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (words, handover, where) = Parts;
        var right = finalSize.Width;
        // Where it is, at the right, as tall as the line; the hand-over before it, at the top; the words in the rest.
        foreach (var (part, tall) in new[] { (where, true), (handover, false) })
        {
            var across = Across(part);
            right -= across;
            part?.Arrange(new Rect(right, 0, across, across > 0 ? (tall ? finalSize.Height : part.DesiredSize.Height) : 0));
            if (across > 0)
                right -= Spacing;
        }
        words?.Arrange(new Rect(0, 0, Math.Max(0, right), finalSize.Height));
        return finalSize;
    }
}
