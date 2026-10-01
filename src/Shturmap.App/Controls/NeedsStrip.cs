using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Shturmap.App.Controls;

/// <summary>Something a quest needs brought on this map: its icon (glyph until it arrives) and name.</summary>
public sealed record NeedChip(string ItemId, string Glyph, string Title);

/// <summary>
/// What a quest needs brought, as tiny inventory cells beside its name (owner, 2026-10-01: BRING didn't show at a
/// glance which item is for which quest). A quest that needs nothing shows one empty cell, the inventory's own way of
/// saying "nothing here". No list at all (null) shows nothing, for lists where bringing doesn't apply.
/// </summary>
public sealed partial class NeedsStrip : StackPanel
{
    public static readonly DependencyProperty NeedsProperty = DependencyProperty.Register(
        nameof(Needs), typeof(IReadOnlyList<NeedChip>), typeof(NeedsStrip), new PropertyMetadata(null, (d, _) => ((NeedsStrip)d).Build()));

    public static readonly DependencyProperty ShownProperty = DependencyProperty.Register(
        nameof(Shown), typeof(int), typeof(NeedsStrip), new PropertyMetadata(3, (d, _) => ((NeedsStrip)d).Build()));

    /// <summary>How many cells at most; the rest is "+N".</summary>
    public int Shown
    {
        get => (int)GetValue(ShownProperty);
        set => SetValue(ShownProperty, value);
    }

    private const double Cell = 18;

    public NeedsStrip()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 2;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(0, 1, 0, 0);
    }

    public IReadOnlyList<NeedChip>? Needs
    {
        get => (IReadOnlyList<NeedChip>?)GetValue(NeedsProperty);
        set => SetValue(NeedsProperty, value);
    }

    private void Build()
    {
        Children.Clear();
        switch (Needs)
        {
            case null:
                return;
            case { Count: 0 }:
                // An empty cell, dashed like an empty slot.
                var empty = new Rectangle
                {
                    Width = Cell,
                    Height = Cell,
                    Stroke = (Brush)Application.Current.Resources["LineStrongBrush"],
                    StrokeThickness = 1,
                    StrokeDashArray = [2, 2],
                };
                ToolTipService.SetToolTip(empty, "Nothing to bring");
                Children.Add(empty);
                return;
        }
        foreach (var need in Needs.Take(Shown))
            Children.Add(new Picture { ItemId = need.ItemId, Glyph = need.Glyph, Title = need.Title, Size = Cell });
        if (Needs.Count > Shown)
        {
            var more = new TextBlock
            {
                Text = $"+{Needs.Count - Shown}",
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["MutedBrush"],
                Margin = new Thickness(2, 0, 0, 0),
            };
            ToolTipService.SetToolTip(more, string.Join("\n", Needs.Skip(Shown).Select(n => n.Title)));
            Children.Add(more);
        }
    }
}
