using Shturmap.Core;
using Shturmap.Core.Desk;

namespace Shturmap.App.Rules;

/// <summary>
/// The desk page's layout and words (owner, 2026-10-19, the addendum's A(2) and B1; docs/DESIGN.md §4, *Its place*,
/// *What it mirrors*, *The head line and the fold*): the cell's size and the columns by the desk's width (owner,
/// 2026-10-20: "We should utilize the whole screen"), the regions' heads, the fold line's names, the "Not attached" line
/// and the head line.
/// </summary>
public static class DeskPageRules
{
    /// <summary>The cell's sizes, in DIP, smallest first: the item cell's 40 up to 128 (twice the art's own 64 px), in steps
    /// of 8, each a whole number of pixels at every Windows scale from 100 to 200 % in quarters, so the grid's hairlines and
    /// the art stay crisp.</summary>
    public static readonly IReadOnlyList<double> Sizes = [40, 48, 56, 64, 72, 80, 88, 96, 104, 112, 120, 128];

    /// <summary>The space between the two columns, between regions side by side in a row, and the page's margin on
    /// either side, in DIP.</summary>
    public const double ColumnGap = 24;

    public const double RowGap = 24;

    public const double Margin = 18;

    /// <summary>The space over a block of the page after the first in its column (DeskBoard), and under the page.</summary>
    public const double BlockGap = 20;

    public const double PageBottom = 24;

    /// <summary>
    /// The page's cell size and columns for a desk this wide and this high (owner, 2026-10-19, after trying step C: "there
    /// is a lot of wasted screen space … Find the optimal solution"; the coordinator's rule): the largest size of
    /// <see cref="Sizes"/> at which the page's widest row fits the desk's width and its tallest column the desk's height.
    /// At each size the page flows into columns (<see cref="Arrange"/>): the gear's regions in reading order down a column
    /// and on into the next where the desk's height ends, the stash heading a column of its own right of the gear, what
    /// follows the stash (its windows, the windows attached to none) in the shortest column beside it; else everything in
    /// one column. What is folded counts as its fold line (<paramref name="open"/>; null, every region open). Where even 40
    /// doesn't fit the height the page scrolls at 40, the gear's one column left of the stash's where both fit the width.
    /// Until 2026-10-19: the width alone, 40 to 80, the gear and the stash two columns from 56.
    /// </summary>
    /// <param name="deskHeight">The page's room under the head line; infinity for the width alone.</param>
    /// <param name="textScale">The size text is drawn at against the size written (the heads' and fold lines' height).</param>
    public static Arrangement Layout(IReadOnlyList<DeskRegion> regions, double deskWidth, double deskHeight = double.PositiveInfinity,
        IReadOnlySet<string>? open = null, double textScale = 1)
    {
        foreach (var size in Sizes.Reverse())
        {
            if (Arrange(regions, open, size, deskWidth, deskHeight, textScale) is var flowed && flowed.Fits(deskWidth, deskHeight))
                return flowed;
            if (Arrange(regions, open, size, deskWidth, deskHeight, textScale, oneColumn: true) is var one && one.Fits(deskWidth, deskHeight))
                return one;
        }
        var scrolling = Arrange(regions, open, Sizes[0], deskWidth, double.PositiveInfinity, textScale);
        return scrolling.Width + 2 * Margin <= deskWidth ? scrolling
            : Arrange(regions, open, Sizes[0], deskWidth, double.PositiveInfinity, textScale, oneColumn: true);
    }

    /// <summary>The cell's size for these regions on a desk this wide and high (<see cref="Layout"/>).</summary>
    public static double Cell(IReadOnlyList<DeskRegion> regions, double deskWidth, double deskHeight = double.PositiveInfinity) =>
        Layout(regions, deskWidth, deskHeight).Cell;

    // The stash's place in reading order, −1 without one.
    private static int StashAt(IReadOnlyList<DeskRegion> regions)
    {
        for (var i = 0; i < regions.Count; i++)
        {
            if (regions[i].Kind == DeskRegionKind.Stash)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// The page laid out at a cell size: its blocks in reading order (<see cref="DeskFold.Blocks"/>) and the column each
    /// stands in, each column's width in DIP, the columns' width with the gaps between, the tallest column's height with
    /// the room under the page. <see cref="Break"/> is the stash's place in reading order where it heads a column of its
    /// own, else −1.
    /// </summary>
    public sealed record Arrangement(double Cell, IReadOnlyList<DeskBlock> Blocks, IReadOnlyList<int> Columns, IReadOnlyList<double> ColumnWidths,
        double Width, double Height, int Break)
    {
        /// <summary>Whether it fits a desk this wide (with the page's margins) and this high.</summary>
        public bool Fits(double deskWidth, double deskHeight) => Width + 2 * Margin <= deskWidth && Height <= deskHeight;

        /// <summary>What the page's shape is: the size and each block's column (a layout anew only when it changes).</summary>
        public string Shape => Cell.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + string.Join(",", Columns);
    }

    // A column of the page as it is filled: its open regions in order, its height, how many blocks, its least cells (the
    // gear's six, the stash's ten).
    private sealed class Column(int least)
    {
        public List<DeskRegion> Members { get; } = [];
        public double Height { get; set; }
        public int Count { get; set; }
        public int Least { get; set; } = least;
    }

    // The width of a column that holds only fold lines: they wrap within it.
    private const double FoldColumn = 240;

    /// <summary>
    /// The page at a cell size (owner, 2026-10-19: the cells fill the desk). Flowed: the gear's blocks in reading order
    /// down a column and on into the next where the desk's height ends (a row of POCKETS and SPECIAL SLOTS kept whole); the
    /// stash heads a column of its own; each block after it (its windows, the windows attached to none) in the shortest of
    /// the columns beside it (the gear's last, the stash's, or a new one after) where it fits the height and the width (the
    /// owner's page of 2026-10-19: the stash and the Keys window under it stood twenty rows tall beside a folded gear; the
    /// window beside the stash lets the stash fill the height at about twice the size). <paramref name="oneColumn"/>:
    /// everything in reading order in one column, as wide as the stash's ten cells. Each block stands under the one before
    /// by <see cref="BlockGap"/>: a region its head, its "Not attached" line, its strip of slots (wrapping at the column's
    /// width) and its grid, 6 DIP apart; a run of folded regions one fold line; <see cref="PageBottom"/> under the page. The
    /// heads' and lines' heights are estimates of the drawn text (an eyebrow about 1.4 of its size), a little over rather
    /// than under, so a page said to fit doesn't scroll.
    /// </summary>
    public static Arrangement Arrange(IReadOnlyList<DeskRegion> regions, IReadOnlySet<string>? open, double size, double deskWidth = double.PositiveInfinity,
        double deskHeight = double.PositiveInfinity, double textScale = 1, bool oneColumn = false)
    {
        var keys = regions.Select(r => r.Key).ToList();
        open ??= keys.ToHashSet(StringComparer.Ordinal);
        var stash = StashAt(regions);
        var blocks = DeskFold.Blocks(keys, open, oneColumn ? -1 : stash);
        var head = Math.Ceiling(HeadSize(size) * textScale * 1.4);
        var fold = Math.Max(head, Math.Ceiling(20 * textScale));
        var room = deskHeight - PageBottom;
        var columns = new List<Column> { new(oneColumn ? 10 : 6) };
        double WidthOf(Column column, DeskRegion? adding = null) =>
            column.Members.Count == 0 && adding is null ? Math.Min(column.Least * size + 1, FoldColumn)
            : ColumnWidth(adding is null ? column.Members : [.. column.Members, adding], size, column.Least);
        double Total(Column? changed = null, DeskRegion? adding = null) =>
            columns.Sum(c => ReferenceEquals(c, changed) ? WidthOf(c, adding) : WidthOf(c)) + (columns.Count - 1) * ColumnGap;
        double HeightOf(DeskBlock block, DeskRegion region, Column column) =>
            block is DeskBlock.Region ? RegionHeight(region, size, WidthOf(column, region), head, textScale) : fold;
        var stashColumn = -1;
        var placed = new List<int>();
        DeskRegionKind? before = null;
        var rowHeight = 0.0;
        var last = -1;
        foreach (var block in blocks)
        {
            var first = block switch
            {
                DeskBlock.Region r => keys.IndexOf(r.Key),
                DeskBlock.Fold f => keys.IndexOf(f.Keys[0]),
                _ => 0,
            };
            var region = regions[first];
            var isRegion = block is DeskBlock.Region;
            var holdsStash = block is DeskBlock.Fold f2 ? f2.Keys.Any(k => regions[keys.IndexOf(k)].Kind == DeskRegionKind.Stash)
                : region.Kind == DeskRegionKind.Stash;
            var partner = isRegion && before is { } kind && SameRow(kind, region.Kind);
            int c;
            if (oneColumn || partner)
                c = oneColumn ? 0 : last;
            else if (stashColumn < 0 && !holdsStash)
            {
                // The gear: down its column, and on into a new one where the desk's height ends.
                c = columns.Count - 1;
                if (columns[c].Count > 0 && columns[c].Height + BlockGap + HeightOf(block, region, columns[c]) > room)
                {
                    columns.Add(new Column(6));
                    c = columns.Count - 1;
                }
            }
            else if (stashColumn < 0)
            {
                // The stash heads a column of its own (the first, without gear before it).
                if (columns[^1].Count > 0)
                    columns.Add(new Column(10));
                columns[^1].Least = 10;
                c = stashColumn = columns.Count - 1;
            }
            else
            {
                // After the stash: the shortest column beside it where the block fits the height and the page the width;
                // else a new column after the last, where it fits the width; else the shortest (the page then scrolls).
                var from = Math.Max(0, stashColumn - 1);
                var fitting = Enumerable.Range(from, columns.Count - from)
                    .Where(i => columns[i].Height + (columns[i].Count > 0 ? BlockGap : 0) + HeightOf(block, region, columns[i]) <= room &&
                                Total(columns[i], isRegion ? region : null) + 2 * Margin <= deskWidth)
                    .OrderBy(i => columns[i].Height).ToList();
                if (fitting.Count > 0)
                    c = fitting[0];
                else
                {
                    var fresh = new Column(0);
                    columns.Add(fresh);
                    if (Total(fresh, isRegion ? region : null) + 2 * Margin <= deskWidth)
                        c = columns.Count - 1;
                    else
                    {
                        columns.RemoveAt(columns.Count - 1);
                        c = Enumerable.Range(from, columns.Count - from).OrderBy(i => columns[i].Height).First();
                    }
                }
            }
            var height = HeightOf(block, region, columns[c]);
            if (partner && last == c)
            {
                // Beside the one before it: the row as tall as the taller.
                columns[c].Height += Math.Max(0, height - rowHeight);
                rowHeight = Math.Max(rowHeight, height);
            }
            else
            {
                columns[c].Height += (columns[c].Count++ > 0 ? BlockGap : 0) + height;
                rowHeight = height;
            }
            if (isRegion)
                columns[c].Members.Add(region);
            before = isRegion ? region.Kind : null;
            last = c;
            placed.Add(c);
        }
        var widths = columns.Select(c => WidthOf(c)).ToList();
        return new Arrangement(size, blocks, placed, widths, widths.Sum() + (widths.Count - 1) * ColumnGap,
            columns.Max(c => c.Height) + PageBottom, stashColumn > 0 ? stash : -1);
    }

    /// <summary>The page's height at a cell size, its tallest column's, in DIP, all in one column (<see cref="Arrange"/>),
    /// or flowed on a desk this high.</summary>
    public static double PageHeight(IReadOnlyList<DeskRegion> regions, IReadOnlySet<string>? open, double size, bool oneColumn,
        double deskHeight = double.PositiveInfinity, double textScale = 1) =>
        Arrange(regions, open, size, double.PositiveInfinity, deskHeight, textScale, oneColumn).Height;

    // A region's block: its head, the "Not attached" line of a window attached to none, its strip of slots, its grid.
    private static double RegionHeight(DeskRegion region, double size, double column, double head, double textScale)
    {
        var height = head;
        if (region is { Kind: DeskRegionKind.ContainerWindow, AttachedTo: null })
            height += 6 + Math.Ceiling(12.5 * textScale * 1.4);
        var slots = region.Cells.Where(c => c.IsSlot).ToList();
        if (slots.Count > 0)
        {
            var (lines, x) = (1, 0.0);
            foreach (var slot in slots)
            {
                var w = DeskCells.Width(slot, size);
                if (x > 0 && x + 4 + w > column)
                    (lines, x) = (lines + 1, w);
                else
                    x += (x > 0 ? 4 : 0) + w;
            }
            height += 6 + lines * size + (lines - 1) * 4;
        }
        var (cols, rows) = Grid(region);
        if (cols > 0 && rows > 0)
            height += 6 + rows * size + 1;
        return height;
    }

    /// <summary>The regions' heads and the fold lines at a cell size: the eyebrows' 11 px at 40 and 48, 12 at 56 and 64,
    /// 13 at 72 and 80, a pixel more every two steps up to 16 at 120 and 128, so a head stays in scale with its grid.</summary>
    public static double HeadSize(double cell) => 11 + Math.Floor((cell - Sizes[0]) / 16);

    /// <summary>A region's grid as the page draws it, in cells: none for LOADOUT, a strip of slot cells whatever a reading
    /// says (QA pass of 2026-10-19, F1 and F2: a reading's loadout of eleven by one drew a row of empty cells under the
    /// strip and widened the gear's column until the stash ran past the window).</summary>
    public static (int Cols, int Rows) Grid(DeskRegion region) =>
        region.Kind == DeskRegionKind.Loadout ? (0, 0) : (Math.Max(0, region.Cols), Math.Max(0, region.Rows));

    /// <summary>A column's width in DIP: its widest grid, POCKETS and SPECIAL SLOTS side by side where both stand in it
    /// (as a fold may leave them), and never under <paramref name="least"/> cells (the gear's six; the stash's ten, which
    /// one column takes too).</summary>
    public static double ColumnWidth(IReadOnlyList<DeskRegion> regions, double size, int least)
    {
        var widest = least * size;
        for (var i = 0; i < regions.Count; i++)
        {
            var width = Grid(regions[i]).Cols * size;
            if (i > 0 && SameRow(regions[i - 1].Kind, regions[i].Kind))
                width += Grid(regions[i - 1]).Cols * size + RowGap;
            widest = Math.Max(widest, width);
        }
        return widest + 1;
    }

    /// <summary>Where the stash's column starts in reading order, where it heads a column of its own; −1 for one column
    /// (<see cref="Layout"/>).</summary>
    public static int ColumnBreak(IReadOnlyList<DeskRegion> regions, double deskWidth, double deskHeight = double.PositiveInfinity) =>
        Layout(regions, deskWidth, deskHeight).Break;

    /// <summary>The space between a grid's compartments, in DIP, as the game sets a rig's pockets a few pixels apart: 2 at
    /// 40 DIP, 4 at 56.</summary>
    public static double PartGap(double size) => size >= DeskCells.Large ? 4 : 2;

    /// <summary>
    /// How far a cell or a compartment stands in from its place in a grid of several compartments, so they stand apart as
    /// the game draws them (QA pass of 2026-10-19, F3: the Zhuk's ten pockets of one by two read as twenty equal cells):
    /// half <see cref="PartGap"/> on each side that runs along its compartment's edge; nothing in a grid of one.
    /// </summary>
    public static (double Left, double Top, double Right, double Bottom) Inset(DeskRegion region, DeskRect rect, double size)
    {
        if (region.Parts.Count < 2 || region.Parts.FirstOrDefault(p => p.Contains(rect)) is not { W: > 0 } part)
            return (0, 0, 0, 0);
        var half = PartGap(size) / 2;
        return (rect.I == part.I ? half : 0, rect.J == part.J ? half : 0, rect.I + rect.W == part.I + part.W ? half : 0,
            rect.J + rect.H == part.J + part.H ? half : 0);
    }

    /// <summary>POCKETS and SPECIAL SLOTS stand on one row, as the game has them, each under its own head.</summary>
    public static bool SameRow(DeskRegionKind first, DeskRegionKind second) =>
        first == DeskRegionKind.Pockets && second == DeskRegionKind.SpecialSlots;

    /// <summary>A region's name as its head and the fold line say it: LOADOUT, TACTICAL RIG, POCKETS, SPECIAL SLOTS,
    /// BACKPACK, POUCH, STASH; a window by its title ("ZHUK").</summary>
    public static string Name(DeskRegion region) => region.Kind switch
    {
        DeskRegionKind.Loadout => RuleTexts.DeskLoadout,
        DeskRegionKind.Rig => RuleTexts.DeskRig,
        DeskRegionKind.Pockets => RuleTexts.DeskPockets,
        DeskRegionKind.SpecialSlots => RuleTexts.DeskSpecialSlots,
        DeskRegionKind.Backpack => RuleTexts.DeskBackpack,
        DeskRegionKind.Pouch => RuleTexts.DeskPouch,
        DeskRegionKind.Stash => RuleTexts.DeskStash,
        _ => UiLanguage.Upper(region.Title ?? ""),
    };

    /// <summary>A region's head: its name; an attached window's "ZHUK · IN THE BACKPACK" (the region its container stands
    /// in, or the window it stands in); a window attached to none its title alone.</summary>
    public static string Head(DeskRegion region, DeskPage page)
    {
        if (region.Kind != DeskRegionKind.ContainerWindow || region.AttachedTo is not { } anchor || page.Region(anchor.Region) is not { } holder)
            return Name(region);
        var title = UiLanguage.Upper(region.Title ?? "");
        return holder.Kind switch
        {
            DeskRegionKind.Loadout => RuleTexts.DeskWindowInLoadout(title: title),
            DeskRegionKind.Rig => RuleTexts.DeskWindowInRig(title: title),
            DeskRegionKind.Pockets => RuleTexts.DeskWindowInPockets(title: title),
            DeskRegionKind.SpecialSlots => RuleTexts.DeskWindowInSpecialSlots(title: title),
            DeskRegionKind.Backpack => RuleTexts.DeskWindowInBackpack(title: title),
            DeskRegionKind.Pouch => RuleTexts.DeskWindowInPouch(title: title),
            DeskRegionKind.Stash => RuleTexts.DeskWindowInStash(title: title),
            _ => RuleTexts.DeskWindowInWindow(title: title, container: UiLanguage.Upper(holder.Title ?? "")),
        };
    }

    /// <summary>A window attached to none says so under its head, muted: how many containers of its name the page holds
    /// ("Not attached: WZ ×3 on the page"), and where they are split between ON YOU and the stash, that it counts in
    /// neither. Null for any other region.</summary>
    public static string? NotAttached(DeskRegionSheet sheet)
    {
        var region = sheet.Region;
        if (region.Kind != DeskRegionKind.ContainerWindow || region.AttachedTo is not null)
            return null;
        var title = region.Title ?? "";
        return sheet.Part == DeskPart.Neither && region.SameName > 1
            ? RuleTexts.DeskNotAttachedSplit(count: region.SameName, name: title)
            : RuleTexts.DeskNotAttached(count: region.SameName, name: title);
    }

    /// <summary>The head line over the page, in muted capitals: "ON YOU 1.01 M ₽ · STASH 6.84 M ₽ · KEEP 3 · 2 · 4", a
    /// total with an unseen container in it ending in "+" ("1.01 M+ ₽"). The totals are given apart from the rest, so
    /// they can count up.</summary>
    public static string HeadLine(DeskTotals totals, double onYou, double stash) =>
        RuleTexts.DeskHeadLine(onYou: DeskNumbers.Total(onYou, totals.OnYouPlus), stash: DeskNumbers.Total(stash, totals.StashPlus),
            now: totals.KeepNow, next: totals.KeepNext, later: totals.KeepLater);

    /// <summary>A fold line's words before its names: "UNCHANGED".</summary>
    public static string Unchanged => RuleTexts.DeskUnchanged;

    /// <summary>A name in a fold line, as a link that opens its region: "POUCH ▸".</summary>
    public static string FoldName(DeskRegion region) => Name(region) + " ▸";
}
