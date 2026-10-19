using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Shturmap.App.Rules;
using Shturmap.Core.Desk;

namespace Shturmap.App.Controls;

/// <summary>
/// The in-raid glance's plate (owner, 2026-10-19: from its panel, Variant 1, with the looted container's or body's item
/// layout mirrored; docs/DESIGN.md §4, "Screen anatomy", *The in-raid glance*): the cue pattern's band, the ground at 90 %
/// between two amber hairlines, at the top of the map's column; on it FROM YOUR SCREENSHOT, "LOOT 1.87 M+ ₽" large with
/// the total in amber, "ON YOU 1.01 M+ ₽ · 2 unsure" under it, then the LOOT pane's regions in the desk's language: each
/// under its own head, a body's slots as a strip, its parts' grids with the desk's cells (<see cref="DeskCellView"/>,
/// without a board: nothing on it is pointed at or clicked), a part not searched a closed region with its word and "+",
/// and where the room ends, the line that names the rest (<see cref="GlancePlateRules"/>). Built in code, as the desk's
/// page is, anew for each reading and each new room; it takes no input.
/// </summary>
public sealed partial class GlancePlate : Grid
{
    private readonly StackPanel _stack;

    public GlancePlate()
    {
        IsHitTestVisible = false;
        // The cue's band: the ground at 90 % between two amber hairlines (the panel's plate).
        Children.Add(new Border
        {
            Background = new SolidColorBrush((Windows.UI.Color)Application.Current.Resources["GroundColor"]) { Opacity = 0.9 },
            BorderBrush = DeskBoard.Brush("AmberBrush"),
            BorderThickness = new Thickness(0, 1, 0, 1),
        });
        _stack = new StackPanel
        {
            Padding = new Thickness(GlancePlateRules.PadSide, GlancePlateRules.PadTop, GlancePlateRules.PadSide, GlancePlateRules.PadBottom),
        };
        Children.Add(_stack);
    }

    /// <summary>The reading on the plate, for the layout check and the developer view.</summary>
    public RaidGlance? Glance { get; private set; }

    /// <summary>How its regions were laid out (the cell's size, the regions cut), for the app log and the developer view.</summary>
    public GlancePlateRules.Arrangement? Arrangement { get; private set; }

    /// <summary>
    /// Builds the plate for a reading in a room this wide and high, in DIP (the map column's width less its margins, about
    /// half its height): the head, then the regions at the largest cell size that fits the room left under it.
    /// </summary>
    public void Show(RaidGlance glance, double width, double height)
    {
        Glance = glance;
        _stack.Children.Clear();
        var inner = Math.Max(1, width - 2 * GlancePlateRules.PadSide);
        MaxWidth = width;
        var head = GlancePlateRules.HeadOf(glance);
        _stack.Children.Add(new TextBlock
        {
            Text = head.Eyebrow,
            FontFamily = (FontFamily)Application.Current.Resources["UiFont"],
            FontSize = GlancePlateRules.EyebrowSize,
            FontWeight = FontWeights.SemiBold,
            FontStretch = Windows.UI.Text.FontStretch.SemiCondensed,
            CharacterSpacing = 140,
            Foreground = DeskBoard.Brush("MutedBrush"),
        });
        // The title in one line: the word in ink, the total in amber; drawn smaller only where the plate is too narrow for it.
        var title = new TextBlock
        {
            FontFamily = (FontFamily)Application.Current.Resources["UiFont"],
            FontSize = GlancePlateRules.TitleSize,
            FontWeight = FontWeights.SemiBold,
            FontStretch = Windows.UI.Text.FontStretch.SemiCondensed,
            Foreground = DeskBoard.Brush("InkBrush"),
        };
        title.Inlines.Add(new Run { Text = head.Word + " " });
        title.Inlines.Add(new Run { Text = head.Total, Foreground = DeskBoard.Brush("AmberBrush") });
        _stack.Children.Add(new Viewbox { Child = title, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxWidth = inner, HorizontalAlignment = HorizontalAlignment.Left });
        var line = new TextBlock
        {
            FontFamily = (FontFamily)Application.Current.Resources["UiFont"],
            FontSize = GlancePlateRules.LineSize,
            FontStretch = Windows.UI.Text.FontStretch.SemiCondensed,
            Foreground = DeskBoard.Brush(glance.Loot is null ? "MutedBrush" : "InkBrush"),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = inner,
            Margin = new Thickness(0, 2, 0, 0),
        };
        line.Inlines.Add(new Run { Text = head.Line });
        if (head.Unsure is { } unsure)
            line.Inlines.Add(new Run { Text = " · " + unsure, Foreground = DeskBoard.Brush("MutedBrush") });
        _stack.Children.Add(line);

        var regions = GlancePlateRules.Drawn(glance);
        if (regions.Count == 0)
        {
            Arrangement = null;
            return;
        }
        // The room the regions have under the head: the plate's height less the head as it is laid out (Windows' text size
        // in it; settings' step comes with the window's next text pass, TextScale, so it is added here), its gap and padding.
        _stack.Measure(new Windows.Foundation.Size(width, double.PositiveInfinity));
        var room = height - _stack.DesiredSize.Height * Math.Max(1, TextScale.Own) - GlancePlateRules.HeadGap;
        var scale = TextScale.Drawn(12) / 12;
        var arrangement = GlancePlateRules.Layout(regions, glance.Sheet.Page, inner, room, scale);
        Arrangement = arrangement;
        var size = arrangement.Cell;
        var columns = new Grid { ColumnSpacing = GlancePlateRules.ColumnGap, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, GlancePlateRules.HeadGap, 0, 0) };
        var panels = new List<StackPanel>();
        for (var c = 0; c < arrangement.ColumnWidths.Count; c++)
        {
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(arrangement.ColumnWidths[c]) });
            var panel = new StackPanel { Spacing = GlancePlateRules.BlockGap };
            SetColumn(panel, c);
            columns.Children.Add(panel);
            panels.Add(panel);
        }
        for (var i = 0; i < arrangement.Shown.Count; i++)
        {
            var c = arrangement.Columns[i];
            panels[c].Children.Add(Region(arrangement.Shown[i], glance.Sheet.Page, size, arrangement.ColumnWidths[c]));
        }
        if (arrangement.Shown.Count > 0)
            _stack.Children.Add(columns);
        if (arrangement.Cut.Count > 0)
            _stack.Children.Add(CutLine(arrangement.Cut, glance.Sheet.Page, size, inner, arrangement.Shown.Count > 0));
    }

    // A region under its head: its slots in a strip (the body's, in the game's order; a part's own container), its grid with
    // the desk's compartments and cells, or where it has no grid and its contents went unseen, a closed region with its word.
    private static FrameworkElement Region(DeskRegionSheet sheet, DeskPage page, double size, double column)
    {
        var region = sheet.Region;
        var block = new StackPanel { Spacing = 6 };
        block.Children.Add(new TextBlock
        {
            Style = (Style)Application.Current.Resources["EyebrowText"],
            Margin = new Thickness(0),
            Text = GlancePlateRules.HeadLine(region, page),
            FontSize = DeskPageRules.HeadSize(size),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = column,
        });
        var slots = sheet.Cells.Where(c => c.Cell.IsSlot).OrderBy(c => DeskBoard.SlotOrder(c.Cell.Slot)).ToList();
        if (slots.Count > 0)
        {
            var strip = new WrapRow { HorizontalSpacing = 4, VerticalSpacing = 4, MaxWidth = column };
            foreach (var slot in slots)
                strip.Children.Add(new DeskCellView(region, slot, size, null));
            block.Children.Add(strip);
        }
        if (GlancePlateRules.HasGrid(region))
        {
            var (cols, rows) = DeskPageRules.Grid(region);
            var grid = new Canvas { Width = cols * size + 1, Height = rows * size + 1, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (var part in region.Parts.Count > 0 ? region.Parts : [new DeskRect(0, 0, cols, rows)])
                grid.Children.Add(DeskBoard.PartOf(part, size, DeskBoard.Thickness(DeskPageRules.Inset(region, part, size)), region.Parts.Count > 1));
            foreach (var item in sheet.Cells.Where(c => !c.Cell.IsSlot).OrderBy(c => c.Cell.J).ThenBy(c => c.Cell.I))
            {
                var inset = DeskBoard.Thickness(DeskPageRules.Inset(region, item.Cell.Rect, size));
                var cell = new DeskCellView(region, item, size, null, inset);
                Canvas.SetLeft(cell, item.Cell.I * size + inset.Left);
                Canvas.SetTop(cell, item.Cell.J * size + inset.Top);
                grid.Children.Add(cell);
            }
            block.Children.Add(grid);
        }
        else if (GlancePlateRules.State(region) is { } state)
            block.Children.Add(Closed(state, size, column));
        return block;
    }

    // A part whose contents weren't shown: a closed region, the cells' ground in a strong frame, its word and "+" in its
    // middle, as wide as the column and three quarters of a cell tall.
    private static FrameworkElement Closed(string state, double size, double column) => new Border
    {
        Width = column,
        Height = GlancePlateRules.ClosedHeight(size),
        HorizontalAlignment = HorizontalAlignment.Left,
        Background = DeskBoard.Brush("CardBrush"),
        BorderBrush = DeskBoard.Brush("LineStrongBrush"),
        BorderThickness = new Thickness(1),
        Child = new TextBlock
        {
            Style = (Style)Application.Current.Resources["StatusText"],
            FontSize = DeskPageRules.HeadSize(size),
            Foreground = DeskBoard.Brush("MutedBrush"),
            Text = state,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };

    // "ALSO · BACKPACK 412k+ ₽ · POCKETS 23k ₽": the regions there was no room for, each with its total; each "·" goes with
    // the name after it, so a line that wraps never ends in one (the desk's fold line).
    private static FrameworkElement CutLine(IReadOnlyList<DeskRegionSheet> cut, DeskPage page, double size, double width, bool under)
    {
        var (word, names) = GlancePlateRules.CutLine(cut, page);
        var px = DeskPageRules.HeadSize(size);
        var line = new WrapRow { HorizontalSpacing = 6, VerticalSpacing = 2, MaxWidth = width, Margin = new Thickness(0, under ? GlancePlateRules.BlockGap : GlancePlateRules.HeadGap, 0, 0) };
        line.Children.Add(Word(word, "MutedBrush", px));
        foreach (var name in names)
        {
            var named = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            named.Children.Add(Word("·", "MutedBrush", px));
            named.Children.Add(Word(name, "InkBrush", px));
            line.Children.Add(named);
        }
        return line;
    }

    private static TextBlock Word(string text, string brush, double size) => new()
    {
        Style = (Style)Application.Current.Resources["StatusText"],
        FontSize = size,
        Text = text,
        Foreground = DeskBoard.Brush(brush),
    };
}
