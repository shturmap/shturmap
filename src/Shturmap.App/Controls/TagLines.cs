using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Shturmap.App.Controls;

/// <summary>
/// The tag under an objective's distance in the raid card ("AHEAD-LEFT · 5 M DOWN"), right-aligned: its parts on one
/// line, or, given less room than that line takes, one under the other, each whole (the height under the direction;
/// Rules.LineRoom). It is never narrower than its widest part.
/// </summary>
public sealed class TagLines : Panel
{
    public static readonly DependencyProperty PartsProperty = DependencyProperty.Register(
        nameof(Parts), typeof(IReadOnlyList<string>), typeof(TagLines), new PropertyMetadata(null, (d, _) => ((TagLines)d).Build()));

    /// <summary>The parts, in capitals: the direction, then the height, where there is one.</summary>
    public IReadOnlyList<string>? Parts
    {
        get => (IReadOnlyList<string>?)GetValue(PartsProperty);
        set => SetValue(PartsProperty, value);
    }

    // The whole tag on one line first, then each part on its own; only one of the two forms is given room.
    private TextBlock? _line;
    private readonly List<TextBlock> _parts = [];
    private bool _stacked;

    // Its words in place: the text blocks stay and take the new words (a template's binding sets the parts again with
    // the same words often), so what is on screen keeps its place until the next layout.
    private void Build()
    {
        var parts = Parts ?? [];
        if (_line is null)
        {
            _line = Text();
            Children.Add(_line);
        }
        _line.Text = string.Join(" · ", parts);
        var stacked = parts.Count > 1 ? parts : [];
        while (_parts.Count > stacked.Count)
        {
            Children.Remove(_parts[^1]);
            _parts.RemoveAt(_parts.Count - 1);
        }
        while (_parts.Count < stacked.Count)
        {
            var block = Text();
            // The whole tag says it once to a screen reader.
            AutomationProperties.SetAccessibilityView(block, AccessibilityView.Raw);
            _parts.Add(block);
            Children.Add(block);
        }
        for (var i = 0; i < stacked.Count; i++)
            _parts[i].Text = stacked[i];
        InvalidateMeasure();
    }

    private static TextBlock Text() => new()
    {
        Style = (Style)Application.Current.Resources["StatusText"], FontSize = 11, TextAlignment = TextAlignment.Right,
        Foreground = (Brush)Application.Current.Resources["MutedBrush"],
    };

    protected override Size MeasureOverride(Size availableSize)
    {
        var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);
        foreach (var child in Children)
            child.Measure(unbounded);
        if (_line is null)
            return new Size(0, 0);
        _stacked = _parts.Count > 1 && _line.DesiredSize.Width > availableSize.Width;
        return _stacked
            ? new Size(_parts.Max(p => p.DesiredSize.Width), _parts.Sum(p => p.DesiredSize.Height))
            : _line.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_line is null)
            return finalSize;
        // The form not shown gets no room and no ink.
        _line.Opacity = _stacked ? 0 : 1;
        foreach (var part in _parts)
            part.Opacity = _stacked ? 1 : 0;
        _line.Arrange(_stacked ? new Rect(finalSize.Width, 0, 0, 0) : new Rect(finalSize.Width - _line.DesiredSize.Width, 0, _line.DesiredSize.Width, _line.DesiredSize.Height));
        var y = 0.0;
        foreach (var part in _parts)
        {
            var size = part.DesiredSize;
            part.Arrange(_stacked ? new Rect(finalSize.Width - size.Width, y, size.Width, size.Height) : new Rect(finalSize.Width, 0, 0, 0));
            y += _stacked ? size.Height : 0;
        }
        return finalSize;
    }
}
