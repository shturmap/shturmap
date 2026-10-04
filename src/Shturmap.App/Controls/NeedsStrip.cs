using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Shturmap.App.Controls;

/// <summary>Something a quest needs brought on this map: its icon (glyph until it arrives) and name.</summary>
public sealed record NeedChip(string ItemId, string Glyph, string Title)
{
    /// <summary>
    /// Every item the cell stands for when it stands for several ("A or B", gear worn together, a weapon class): it
    /// is each of them for the linked highlight, though it pictures <see cref="ItemId"/>. Null for one item.
    /// </summary>
    public IReadOnlyList<string>? Alternatives { get; init; }
}

/// <summary>
/// What a quest needs brought, as tiny inventory cells beside its name (owner, 2026-10-01: BRING didn't show at a
/// glance which item is for which quest). A quest that needs nothing shows no cell (until 2026-10-04 it showed one
/// empty, dashed cell). Each cell is
/// its item for the linked highlight (its card, its BRING row, its locks and loose spots on the map); leaving it
/// returns to the row's quest.
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
                // Nothing to bring, nothing shown (owner, 2026-10-04: the dashed empty cell stood on most rows and said
                // no more than its absence does).
                return;
        }
        foreach (var need in Needs.Take(Shown))
        {
            var cell = new Picture { ItemId = need.ItemId, Glyph = need.Glyph, Title = need.Title, Size = Cell };
            Linked.SetInline(cell, true);
            Linked.SetItem(cell, need.ItemId);
            if (need.Alternatives is { Count: > 1 } several)
                Linked.SetItems(cell, several);
            Children.Add(cell);
        }
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
