using System.Globalization;
using Shturmap.Core;
using Shturmap.Core.Desk;

namespace Shturmap.App.Rules;

/// <summary>One block of the desk's page in its place (owner, 2026-10-25: the GEAR tab mirrored and fixed): a fixed
/// region (LOADOUT, TACTICAL RIG, POCKETS, SPECIAL SLOTS, BACKPACK, POUCH, STASH) or a container's window, with its
/// region as the page in hand has it; <see cref="Region"/> null for a fixed region no reading of the mode has shown (its
/// head over a dashed frame, "NOT IN A SCREENSHOT YET"), or one a reading made from its heading alone
/// (<see cref="DeskPageRules.HeadingAlone"/>).</summary>
public sealed record DeskBlock(string Key, DeskRegionKind Kind, DeskRegion? Region)
{
    /// <summary>A fixed region never read: its head and a dashed frame stand in its place.</summary>
    public bool Placeholder => Region is null;
}

/// <summary>
/// The desk page's layout and words (owner, 2026-10-25, decision 1 of the layout round: A, the GEAR tab mirrored and fixed;
/// docs/DESIGN.md §4, *Its place*, *What it mirrors*, *The head line*): the game's GEAR tab's shape in three columns at a
/// fixed cell size (LOADOUT as the game's slot grid; TACTICAL RIG, POCKETS and SPECIAL SLOTS, BACKPACK and POUCH; STASH),
/// every region in its place whether or not the last screenshot showed it, the windows right of the stash or under it;
/// the cell's size by the desk's size alone; the regions' heads, dated where the last screenshot didn't show them; the
/// "Not attached" line and the head line. Until 2026-10-25 the page folded what a reading didn't change and flowed into
/// as many columns as filled the desk (owner, 2026-10-19).
/// </summary>
public static class DeskPageRules
{
    /// <summary>The cell's sizes, in DIP, smallest first: the item cell's 40 up to 128 (twice the art's own 64 px), in steps
    /// of 8, each a whole number of pixels at every Windows scale from 100 to 200 % in quarters, so the grid's hairlines and
    /// the art stay crisp.</summary>
    public static readonly IReadOnlyList<double> Sizes = [40, 48, 56, 64, 72, 80, 88, 96, 104, 112, 120, 128];

    /// <summary>The space between the columns, between POCKETS and SPECIAL SLOTS on their row, and the page's margin on
    /// either side, in DIP.</summary>
    public const double ColumnGap = 24;

    public const double RowGap = 24;

    public const double Margin = 18;

    /// <summary>The space over a block of the page after the first in its column (DeskBoard), and under the page.</summary>
    public const double BlockGap = 20;

    public const double PageBottom = 24;

    /// <summary>The room over the columns, the page's top padding: a stem from a cell to a window two or more columns right
    /// of it runs there, over the heads between (DeskBoard).</summary>
    public const double Lane = 12;

    /// <summary>The space under a head, before what stands under it (a block's StackPanel spacing).</summary>
    public const double HeadGap = 6;

    /// <summary>LOADOUT's width in cells: the game's three columns of slot boxes two cells wide, a cell apart, as the game
    /// sets them; a weapon box spans the first two and the cell between, five.</summary>
    public const int LoadoutCells = 8;

    /// <summary>The stash's column: ten cells, as the game's stash is wide.</summary>
    public const int StashCells = 10;

    /// <summary>A grid region never read: a dashed frame five cells wide and two high.</summary>
    public const int PlaceholderCols = 5, PlaceholderRows = 2;

    /// <summary>
    /// The dashed frame of a grid region never read, in cells: five by two (owner, 2026-10-25), but POCKETS and SPECIAL
    /// SLOTS, whose grids the game sets the same for everyone, as wide as theirs (four and three) and two high, as the
    /// owner's of 2026-10-19 are. Five each would take their row to ten cells and a gap, the middle column's widest by
    /// three cells, so that a page of the stash alone stood in one column at 1600×900 (1232 DIP of 1216) and the size
    /// changed again when the pockets were read.
    /// </summary>
    public static (int Cols, int Rows) Placeholder(DeskRegionKind kind) => kind switch
    {
        DeskRegionKind.Pockets => (4, PlaceholderRows),
        DeskRegionKind.SpecialSlots => (3, PlaceholderRows),
        _ => (PlaceholderCols, PlaceholderRows),
    };

    /// <summary>The fixed regions in the game's order: the left column, the middle column top to bottom, the stash.</summary>
    public static readonly IReadOnlyList<DeskRegionKind> Fixed =
    [
        DeskRegionKind.Loadout, DeskRegionKind.Rig, DeskRegionKind.Pockets, DeskRegionKind.SpecialSlots, DeskRegionKind.Backpack,
        DeskRegionKind.Pouch, DeskRegionKind.Stash,
    ];

    // Whole pixels at every Windows scale from 100 to 200 % in quarters: a multiple of 4 DIP.
    private static double Quarter(double dip) => Math.Max(4, Math.Round(dip / 4) * 4);

    /// <summary>The space between a container's slot box and its grid (TACTICAL RIG, BACKPACK, POUCH, on one row as the
    /// game draws them), and between the loadout's rows of boxes: a fifth of a cell in whole pixels, 8 DIP at 40, 12 at 56.</summary>
    public static double BoxGap(double size) => Quarter(size / 5);

    /// <summary>A container region's slot box and the loadout's boxes: two cells square.</summary>
    public static double Box(double size) => 2 * size;

    /// <summary>The space between the armband's box and the dog tag's name, which share a slot box's room.</summary>
    public const double ShortGap = 4;

    /// <summary>Whether a region's own container's slot box stands beside its grid: TACTICAL RIG, BACKPACK, POUCH.</summary>
    public static bool HasBox(DeskRegionKind kind) => DeskRegion.IsContainer(kind);

    /// <summary>The page's blocks in their places: the seven fixed regions in the game's order, each as the page in hand has
    /// it or a placeholder where no reading showed it (or one made from its heading alone, <see cref="HeadingAlone"/>), then
    /// the windows in reading order (each after the region its container is in, those attached to none at the end;
    /// <see cref="DeskPage.InReadingOrder"/>).</summary>
    public static IReadOnlyList<DeskBlock> Blocks(IReadOnlyList<DeskRegion> regions)
    {
        var blocks = Fixed.Select(kind => Read(regions, kind) is { } region
            ? new DeskBlock(region.Key, kind, region)
            : new DeskBlock(DeskRegion.KeyOf(kind), kind, null)).ToList();
        blocks.AddRange(regions.Where(r => r.Kind == DeskRegionKind.ContainerWindow).Select(r => new DeskBlock(r.Key, r.Kind, r)));
        return blocks;
    }

    /// <summary>The page's region of a fixed kind, or null where it stands as never read: none of that kind on the page, or
    /// one a reading made from its heading alone (<see cref="HeadingAlone"/>). The one rule of what was read, for the dashed
    /// frame (<see cref="Blocks"/>) and the head line (<see cref="PartsRead"/>).</summary>
    public static DeskRegion? Read(IReadOnlyList<DeskRegion> regions, DeskRegionKind kind) =>
        regions.FirstOrDefault(r => r.Kind == kind) is { } region && !HeadingAlone(region) ? region : null;

    /// <summary>Whether the page holds a gear page read (LOADOUT or a region of the middle column, read at least once) and
    /// whether it holds a stash read: the regions <see cref="Read(IReadOnlyList{DeskRegion}, DeskRegionKind)"/> doesn't draw
    /// as never read. A window counts in the part of the region its container stands in, so it makes no part read alone.</summary>
    public static (bool Gear, bool Stash) PartsRead(IReadOnlyList<DeskRegion> regions) =>
        (Fixed.Any(kind => kind != DeskRegionKind.Stash && Read(regions, kind) is not null), Read(regions, DeskRegionKind.Stash) is not null);

    /// <summary>
    /// Whether a region on the page is one a reading made from its heading alone, no grid and no cell in it: POCKETS,
    /// SPECIAL SLOTS or STASH of no columns or no rows, as the layout gives one on a first reading when the region's frames
    /// are all under a window or under no heading (code review of 2026-10-25: drawn as a bare head over nothing, POCKETS and
    /// SPECIAL SLOTS a DIP wide, their heads wrapping a letter a line). It stands as a region never read: the placeholder,
    /// counted as one in the layout; it counts nothing in the totals, having no cell. Not TACTICAL RIG, BACKPACK or POUCH,
    /// whose grid goes with a slot seen empty (docs/DESIGN.md §4, *A region read in part keeps the page's rest*): such a
    /// region is its empty slot box with no grid, as the game shows it. Not LOADOUT, which has no grid of its own.
    /// </summary>
    public static bool HeadingAlone(DeskRegion region) =>
        region.Kind is DeskRegionKind.Pockets or DeskRegionKind.SpecialSlots or DeskRegionKind.Stash && region.Cells.Count == 0 &&
        Grid(region) is not (> 0, > 0);

    /// <summary>
    /// The page laid out at a cell size: its blocks (<see cref="Blocks"/>) and the column each stands in, each column's
    /// width and height in DIP (the heads, lines and grids under each other, <see cref="BlockGap"/> apart), whether all
    /// stand in one column, and whether the windows stand in a column of their own right of the stash.
    /// </summary>
    public sealed record Arrangement(double Cell, IReadOnlyList<DeskBlock> Blocks, IReadOnlyList<int> Columns, IReadOnlyList<double> ColumnWidths,
        IReadOnlyList<double> ColumnHeights, bool OneColumn, bool WindowsBeside)
    {
        /// <summary>The columns' width with the gaps between them.</summary>
        public double Width => ColumnWidths.Sum() + (ColumnWidths.Count - 1) * ColumnGap;

        /// <summary>The page's height: the lane over it, its tallest column, the room under it.</summary>
        public double Height => Lane + ColumnHeights.Max() + PageBottom;

        /// <summary>What has to fit the desk's height for the size to stand: the stash's column (the windows with it where
        /// they stand under it) and the windows' own column; LOADOUT and the middle column may scroll, as the game scrolls
        /// the middle column. In one column, the page.</summary>
        public double Held => OneColumn ? Height : Lane + ColumnHeights.Skip(2).DefaultIfEmpty(0).Max() + PageBottom;

        /// <summary>Whether it fits a desk this wide (with the page's margins) and the stash this high.</summary>
        public bool Fits(double deskWidth, double deskHeight) => Width + 2 * Margin <= deskWidth && Held <= deskHeight;

        /// <summary>What the page's shape is: the size, each block's column and the columns' widths (a layout anew only when
        /// it changes).</summary>
        public string Shape => Cell.ToString(CultureInfo.InvariantCulture) + ":" + string.Join(",", Columns) + ":" +
                               string.Join(",", ColumnWidths.Select(w => w.ToString(CultureInfo.InvariantCulture)));

        /// <summary>The column a block stands in, by its key; −1 for none.</summary>
        public int ColumnOf(string key)
        {
            for (var i = 0; i < Blocks.Count; i++)
            {
                if (Blocks[i].Key == key)
                    return Columns[i];
            }
            return -1;
        }
    }

    /// <summary>
    /// The page's cell size and columns for a desk this wide and this high (owner, 2026-10-25, decision 1 of the layout
    /// round: the cell size follows the desk's size alone, never what was read): the largest size of <see cref="Sizes"/>
    /// at which the three columns (LOADOUT's eight cells, the middle column's widest row, the stash's ten) fit the desk's
    /// width and the stash's column its height, the windows in a fourth column right of the stash where the width allows,
    /// else under the stash (counted in its column's height). Where the middle column or LOADOUT is taller than the desk at
    /// that size, the page scrolls. Where no size fits the stash's height, 40, scrolling; where even 40 doesn't fit the
    /// three columns' width (the smallest window's desk, 516 DIP), the same blocks in one column at 40, scrolling. A
    /// placeholder and a region the last screenshot didn't show count as one read. Until 2026-10-25 the page flowed into
    /// columns, what was folded counting as its fold line (owner, 2026-10-19: "the cells fill the desk").
    /// </summary>
    /// <param name="regions">The page in hand's regions in reading order.</param>
    /// <param name="deskHeight">The page's room under the head line; infinity for the width alone.</param>
    /// <param name="textScale">The size text is drawn at against the size written (the heads' and lines' heights).</param>
    /// <param name="head">Each region's head without its date ("ZHUK · IN THE BACKPACK"), its name without: each head
    /// takes the room of its dated form at its widest (<see cref="Reserved"/>) whether it is dated or not, so a date never
    /// moves what is under it (UI QA pass of step 2, F1).</param>
    public static Arrangement Layout(IReadOnlyList<DeskRegion> regions, double deskWidth, double deskHeight = double.PositiveInfinity, double textScale = 1,
        Func<DeskRegion, string>? head = null)
    {
        Arrangement? scrolling = null;
        foreach (var size in Sizes.Reverse())
        {
            scrolling = Arrange(regions, size, deskWidth, textScale, head: head);
            if (scrolling.Fits(deskWidth, deskHeight))
                return scrolling;
        }
        // None fits: the last arranged, the smallest size's (Sizes[0]), scrolling.
        return scrolling!.Width + 2 * Margin <= deskWidth ? scrolling : Arrange(regions, Sizes[0], deskWidth, textScale, oneColumn: true, head: head);
    }

    /// <summary>The cell's size for these regions on a desk this wide and high (<see cref="Layout"/>).</summary>
    public static double Cell(IReadOnlyList<DeskRegion> regions, double deskWidth, double deskHeight = double.PositiveInfinity) =>
        Layout(regions, deskWidth, deskHeight).Cell;

    /// <summary>
    /// The page at a cell size: LOADOUT in the first column; TACTICAL RIG, POCKETS beside SPECIAL SLOTS, BACKPACK and POUCH
    /// in the second; STASH in the third; the windows in a fourth where the page with it fits <paramref name="deskWidth"/>,
    /// else under the stash. <paramref name="oneColumn"/>: the same blocks in the same order in one column. Each block stands
    /// under the one before by <see cref="BlockGap"/>: its head, the "Not attached" line of a window attached to none, what
    /// it holds (<see cref="ContentHeight"/>), <see cref="HeadGap"/> apart. The heads' and lines' heights are estimates of
    /// the drawn text (a line about 1.4 of its size, the words about half an em a letter), a little over rather than under,
    /// so a page said to fit doesn't scroll.
    /// </summary>
    public static Arrangement Arrange(IReadOnlyList<DeskRegion> regions, double size, double deskWidth = double.PositiveInfinity, double textScale = 1,
        bool oneColumn = false, Func<DeskRegion, string>? head = null)
    {
        var blocks = Blocks(regions).ToList();
        var widths = blocks.Select(b => BlockWidth(b, size)).ToList();
        int Find(DeskRegionKind kind) => blocks.FindIndex(b => b.Kind == kind);
        var pockets = Find(DeskRegionKind.Pockets);
        var special = Find(DeskRegionKind.SpecialSlots);
        var windows = Enumerable.Range(0, blocks.Count).Where(i => blocks[i].Kind == DeskRegionKind.ContainerWindow).ToList();
        var middle = Enumerable.Range(0, blocks.Count).Where(i => HasBox(blocks[i].Kind)).Select(i => widths[i])
            .Append(widths[pockets] + RowGap + widths[special]).Max();
        var loadout = widths[Find(DeskRegionKind.Loadout)];
        var stash = widths[Find(DeskRegionKind.Stash)];
        var windowsWide = windows.Select(i => widths[i]).DefaultIfEmpty(0).Max();

        List<double> columnWidths;
        int[] columns;
        var beside = false;
        if (oneColumn)
        {
            columnWidths = [Math.Max(Math.Max(loadout, middle), Math.Max(stash, windowsWide))];
            columns = new int[blocks.Count];
        }
        else
        {
            var three = loadout + middle + stash + 2 * ColumnGap;
            beside = windows.Count > 0 && three + ColumnGap + windowsWide + 2 * Margin <= deskWidth;
            columnWidths = beside ? [loadout, middle, stash, windowsWide] : [loadout, middle, Math.Max(stash, windowsWide)];
            columns = blocks.Select(b => b.Kind switch
            {
                DeskRegionKind.Loadout => 0,
                DeskRegionKind.Stash => 2,
                DeskRegionKind.ContainerWindow => beside ? 3 : 2,
                _ => 1,
            }).ToArray();
        }

        // Each column's height: its blocks under each other, POCKETS and SPECIAL SLOTS one row as tall as the taller.
        var heights = new double[columnWidths.Count];
        var count = new int[columnWidths.Count];
        for (var i = 0; i < blocks.Count; i++)
        {
            if (i == special)
                continue;
            var c = columns[i];
            var height = i == pockets
                ? Math.Max(BlockHeight(blocks[i], size, widths[i], textScale, head), BlockHeight(blocks[special], size, widths[special], textScale, head))
                : BlockHeight(blocks[i], size, columnWidths[c], textScale, head);
            heights[c] += (count[c]++ > 0 ? BlockGap : 0) + height;
        }
        return new Arrangement(size, blocks, columns, columnWidths, heights, oneColumn, beside);
    }

    /// <summary>A block's width in DIP: LOADOUT's eight cells; a container's slot box, the gap and its grid; the stash's ten
    /// cells at the least; a grid; a placeholder's five cells.</summary>
    public static double BlockWidth(DeskBlock block, double size)
    {
        var cols = block.Region is { } region ? Grid(region).Cols : Placeholder(block.Kind).Cols;
        return block.Kind switch
        {
            DeskRegionKind.Loadout => LoadoutCells * size + 1,
            DeskRegionKind.Stash => Math.Max(StashCells, cols) * size + 1,
            _ when HasBox(block.Kind) => Box(size) + BoxGap(size) + cols * size + 1,
            _ => cols * size + 1,
        };
    }

    /// <summary>A block's height in DIP: its head's room (its dated form's at its widest, wrapping in
    /// <paramref name="width"/>: <see cref="HeadRoomLines"/>), a window's "Not attached" line or a LOADOUT never read's line,
    /// what it holds.</summary>
    public static double BlockHeight(DeskBlock block, double size, double width, double textScale = 1, Func<DeskRegion, string>? head = null)
    {
        var words = block.Region is { } region ? head?.Invoke(region) ?? Name(region) : Name(block.Kind);
        var height = HeadRoomLines(words, size, width, textScale) * HeadHeight(size, textScale);
        if (block.Region is { Kind: DeskRegionKind.ContainerWindow, AttachedTo: null } window)
            height += HeadGap + NoteHeight((window.Title ?? "").Length + 70, textScale, width);
        if (block is { Placeholder: true, Kind: DeskRegionKind.Loadout })
            height += HeadGap + NoteHeight(RuleTexts.DeskNotRead.Length, textScale, width);
        var content = ContentHeight(block, size, textScale);
        return content > 0 ? height + HeadGap + content : height;
    }

    /// <summary>What a block holds, in DIP: LOADOUT's slot grid (<see cref="LoadoutHeight"/>); a container's slot box beside
    /// its grid, as tall as the taller; a grid; a placeholder's frame of two cells.</summary>
    public static double ContentHeight(DeskBlock block, double size, double textScale = 1)
    {
        if (block.Kind == DeskRegionKind.Loadout)
            return LoadoutHeight(size, textScale);
        var (cols, rows) = block.Region is { } region ? Grid(region) : Placeholder(block.Kind);
        var grid = cols > 0 && rows > 0 ? rows * size + 1 : 0;
        return HasBox(block.Kind) ? Math.Max(Box(size), grid) : grid;
    }

    // A head's line: an eyebrow about 1.4 of its size.
    private static double HeadHeight(double size, double textScale) => Math.Ceiling(HeadSize(size) * textScale * 1.4);

    /// <summary>How many lines a head takes in a column this wide, by an estimate a little over: its capitals about 0.53 em
    /// a letter, its spacing 0.14.</summary>
    public static int HeadLines(string words, double size, double width, double textScale = 1) =>
        Math.Max(1, (int)Math.Ceiling(words.Length * HeadSize(size) * textScale * 0.67 / Math.Max(1, width)));

    /// <summary>
    /// The lines a region's head takes on the page whether it is dated or not (UI QA pass of step 2, F1: after reading 2 at
    /// 40 DIP SPECIAL SLOTS' dated head wrapped to a second line and moved everything under POCKETS and SPECIAL SLOTS 13 px
    /// down; at 125 % German TASCHEN's too): those of its dated form at its widest (<see cref="Reserved"/>), from the first
    /// reading, so the date comes into room kept for it and nothing under a head moves.
    /// </summary>
    public static int HeadRoomLines(string head, double size, double width, double textScale = 1) =>
        Math.Max(HeadLines(head, size, width, textScale), HeadLines(Reserved(head), size, width, textScale));

    /// <summary>A head in its dated form at its widest, the room a head keeps for its date (<see cref="HeadRoomLines"/>, the
    /// page's unseen copy under each head): "SPECIAL SLOTS · 28 SEP 23:58".</summary>
    public static string Reserved(string head) => RuleTexts.DeskHeadSince(head: head, time: WidestSince());

    /// <summary>The longest date and time a head can say in the language in use (<see cref="Since"/> of another day): a day of
    /// two figures and the month written longest ("28 SEP 23:58", German "28. SEPT., 23:58"), of the months of one length
    /// the one with the most of the wide M and W.</summary>
    public static string WidestSince() =>
        Enumerable.Range(1, 12)
            .Select(month => UiLanguage.Upper(UiLanguage.DayMonthTime(new DateTime(2026, month, 28, 23, 58, 0, DateTimeKind.Unspecified))).Replace(' ', ' '))
            .OrderByDescending(s => s.Length).ThenByDescending(s => s.Count(c => c is 'M' or 'W')).First();

    // A note's height in a column this wide: 12.5 px, about half an em a letter, 1.4 of its size a line.
    private static double NoteHeight(int letters, double textScale, double width) =>
        Math.Max(1, Math.Ceiling(letters * 12.5 * textScale * 0.5 / Math.Max(1, width))) * Math.Ceiling(12.5 * textScale * 1.4);

    /// <summary>The regions' heads at a cell size: the eyebrows' 11 px at 40 and 48, 12 at 56 and 64, 13 at 72 and 80, a
    /// pixel more every two steps up to 16 at 120 and 128, so a head stays in scale with its grid.</summary>
    public static double HeadSize(double cell) => 11 + Math.Floor((cell - Sizes[0]) / 16);

    /// <summary>A region's grid as the page draws it, in cells: none for LOADOUT, whose slots stand in their boxes whatever a
    /// reading says (QA pass of 2026-10-19, F1 and F2: a reading's loadout of eleven by one drew a row of empty cells and
    /// widened the gear's column until the stash ran past the window).</summary>
    public static (int Cols, int Rows) Grid(DeskRegion region) =>
        region.Kind == DeskRegionKind.Loadout ? (0, 0) : (Math.Max(0, region.Cols), Math.Max(0, region.Rows));

    // ---- LOADOUT, the game's slot grid ----

    /// <summary>A loadout slot's box in the slot grid, in DIP from the grid's top left; its name stands over it, from
    /// <see cref="Y"/> less <see cref="SlotHeadRoom"/>.</summary>
    public sealed record SlotBox(string Slot, double X, double Y, double Width, double Height);

    /// <summary>The slots' names over their boxes: two pixels under the regions' heads, 9 px at 40 and 48, 10 at 56 and 64,
    /// on up to 14 at 120 and 128.</summary>
    public static double SlotHeadSize(double cell) => HeadSize(cell) - 2;

    /// <summary>The room a slot's name takes over its box: its line at the size the text is drawn, and 2 DIP to the box.</summary>
    public static double SlotHeadRoom(double cell, double textScale = 1) => Math.Ceiling(SlotHeadSize(cell) * textScale * 1.4) + 2;

    /// <summary>
    /// LOADOUT as the game's GEAR tab draws it (owner, 2026-10-25, decision 1 of the layout round): the slots as boxes two
    /// cells square in the game's rows, three columns a cell apart: EARPIECE · HEADWEAR · FACE COVER; ARMBAND over DOGTAG ·
    /// BODY ARMOR · EYEWEAR (the armband's and the dog tag's boxes as the game has them, two short ones in one box's room,
    /// each under its name); ON SLING · HOLSTER; ON BACK · SHEATH, the weapons' boxes five cells wide (the first two
    /// columns and the cell between). Each box under its slot's name, the rows <see cref="BoxGap"/> apart. Until
    /// 2026-10-25 a strip of slot cells without names, one per filled slot.
    /// </summary>
    public static IReadOnlyList<SlotBox> LoadoutBoxes(double size, double textScale = 1)
    {
        var room = SlotHeadRoom(size, textScale);
        var box = Box(size);
        var row = room + box + BoxGap(size);
        double a = 0, b = 3 * size, c = 6 * size;
        var weapon = 5 * size;
        var low = Math.Max(size / 2, (box - room - ShortGap) / 2);
        return
        [
            new("earpiece", a, room, box, box), new("headwear", b, room, box, box), new("facecover", c, room, box, box),
            new("armband", a, row + room, box, low), new("dogtag", a, row + room + low + ShortGap + room, box, low),
            new("bodyarmor", b, row + room, box, box), new("eyewear", c, row + room, box, box),
            new("onsling", a, 2 * row + room, weapon, box), new("holster", c, 2 * row + room, box, box),
            new("onback", a, 3 * row + room, weapon, box), new("sheath", c, 3 * row + room, box, box),
        ];
    }

    /// <summary>LOADOUT's slot grid's height in DIP: four rows of a name and a box, <see cref="BoxGap"/> apart.</summary>
    public static double LoadoutHeight(double size, double textScale = 1) =>
        4 * (SlotHeadRoom(size, textScale) + Box(size)) + 3 * BoxGap(size);

    /// <summary>
    /// A slot's name over its box, fitted to the box's width: at <see cref="SlotHeadSize"/>, letter-spaced 0.1 em; where
    /// that is wider than the box, without the spacing; then as large as the box takes it, never drawn under 8 px. The fit
    /// is made at the size the text is drawn, so a larger text size grows a long name as far as its box allows (UI QA pass
    /// of step 2, F5: in steps of half a pixel as written it stopped short of the box). By the estimate below, the German
    /// FACE COVER, "GESICHTSBEDECKUNG", over a box of 80 DIP at 40 is set at 8.875 px; the page measures the name as drawn
    /// (DeskBoard), which takes it at 9 px without its spacing, the size of its row.
    /// </summary>
    /// <param name="drawn">A size as written to the size the window's text size draws it.</param>
    /// <param name="wide">The name's width in DIP at a size as drawn and a spacing in ems; by default an estimate:
    /// Bahnschrift's semi-condensed semibold capitals run about 0.51 em (measured), 0.53 is taken, a little over.</param>
    /// <returns>The size as written (in 64ths of a pixel) and the spacing in thousandths of an em (a TextBlock's
    /// CharacterSpacing).</returns>
    public static (double Size, int Spacing) SlotHead(string name, double width, double cell, Func<double, double>? drawn = null,
        Func<double, double, double>? wide = null)
    {
        drawn ??= s => s;
        wide ??= (at, spacing) => name.Length * at * (0.53 + spacing);
        bool Fits(double at, double spacing) => wide(drawn(at), spacing) <= width;
        var size = SlotHeadSize(cell);
        if (Fits(size, 0.1))
            return (size, 100);
        if (Fits(size, 0))
            return (size, 0);
        // The least size as written that is drawn at 8 px (the drawn size grows with the written); a name too long even at
        // that stands at it.
        double low = 4, high = size;
        if (drawn(low) < DeskCells.LeastFigure)
        {
            for (var i = 0; i < 30; i++)
            {
                var mid = (low + high) / 2;
                if (drawn(mid) >= DeskCells.LeastFigure)
                    high = mid;
                else
                    low = mid;
            }
            low = Math.Ceiling(high * 64) / 64;
        }
        if (!Fits(low, 0))
            return (low, 0);
        high = size;
        for (var i = 0; i < 30; i++)
        {
            var mid = (low + high) / 2;
            if (Fits(mid, 0))
                low = mid;
            else
                high = mid;
        }
        // In 64ths, rounded down: never over the box; never under the least, itself a 64th.
        return (Math.Floor(low * 64) / 64, 0);
    }

    // ---- when a region was read: one the last screenshot didn't show ----

    /// <summary>Whether the last screenshot didn't show a region (owner, 2026-10-25): it is older than the page in hand. A
    /// region of a file written before its time was kept (<see cref="DateTimeOffset.MinValue"/>) is the page's.</summary>
    public static bool Kept(DeskRegion region, DeskPage page) => region.ReadAt != DateTimeOffset.MinValue && region.ReadAt < page.ReadAt;

    /// <summary>A region the last screenshot didn't show is drawn whole, a step dimmer: its block at 70 %.</summary>
    public const double KeptOpacity = 0.7;

    /// <summary>When the screenshot that last showed a region was taken, as its head says it: "23:08" on the day it is now,
    /// else the date and time as the language writes them, in capitals ("19 OCT 23:08", German "19. OKT., 23:08"), as the
    /// operational rail's "As of" does; its spaces no-break, so a head that wraps never parts the day from its month.</summary>
    public static string Since(DateTimeOffset readAt, DateTimeOffset now)
    {
        var local = readAt.ToLocalTime();
        return local.Date == now.ToLocalTime().Date
            ? local.ToString("HH:mm", CultureInfo.InvariantCulture)
            : UiLanguage.Upper(UiLanguage.DayMonthTime(local.DateTime)).Replace(' ', ' ');
    }

    /// <summary>A region's head as the page draws it: its head (<see cref="Head"/>), and where the last screenshot didn't
    /// show it, since when: "BACKPACK · 23:08", "KEYS · IN THE STASH · 19 OCT 23:08".</summary>
    public static string HeadOf(DeskRegion region, DeskPage page, DateTimeOffset now) =>
        Kept(region, page) ? RuleTexts.DeskHeadSince(head: Head(region, page), time: Since(region.ReadAt, now)) : Head(region, page);

    /// <summary>The regions whose cells come in, 50 ms apart, as a reading's page does (docs/DESIGN.md §4, *Fun*): of a new
    /// reading, what it changed (every region on the mode's first); nothing otherwise, so a reading that changed nothing
    /// moves nothing.</summary>
    public static IReadOnlySet<string> Entering(IReadOnlyList<string> keys, IReadOnlySet<string> changed, bool first, bool newReading) =>
        !newReading ? new HashSet<string>(StringComparer.Ordinal)
        : first ? keys.ToHashSet(StringComparer.Ordinal)
        : keys.Where(changed.Contains).ToHashSet(StringComparer.Ordinal);

    // ---- the page's scroll ----

    /// <summary>
    /// Whether the page runs on past the desk's foot, so the scroll mark stands there, "MORE BELOW ▾" (UI QA pass of step 2,
    /// F3: at 900×560 the first screen showed LOADOUT alone, its last boxes flush with the window's foot, and at 1600×900 the
    /// windows ran past it, with no bar or mark to say the page scrolls; the coordinator's rule, after the in-raid glance's
    /// "MORE BELOW +"): more than a DIP of what the page holds lies under the desk's foot, the room under the page
    /// (<see cref="PageBottom"/>) not counted, so it is gone once the page is scrolled to its end.
    /// </summary>
    /// <param name="extent">The page's height, its padding included (the scroll's extent).</param>
    /// <param name="viewport">The desk's room for the page (the scroll's viewport).</param>
    /// <param name="offset">How far the page is scrolled down.</param>
    public static bool MoreBelow(double extent, double viewport, double offset) => extent - PageBottom - (offset + viewport) > 1;

    /// <summary>
    /// The page in screens, for the layout check (UI QA pass of step 2, F4: at 900×560 the desk's check passed with 95 texts
    /// below the first screen, none of them in a picture): the offsets to scroll the page to, from its top, each the last
    /// plus the desk's room less the scroll mark's band (what the mark covers at one screen's foot stands in the next),
    /// until the page's end shows; the last at the page's end. One where the page fits.
    /// </summary>
    /// <param name="band">The scroll mark's height, its fade included.</param>
    public static IReadOnlyList<double> ScreenOffsets(double extent, double viewport, double band)
    {
        var offsets = new List<double> { 0 };
        if (viewport <= 0)
            return offsets;
        var step = Math.Max(viewport / 2, viewport - Math.Max(0, band));
        var end = Math.Max(0, extent - viewport);
        while (MoreBelow(extent, viewport, offsets[^1]) && offsets[^1] < end && offsets.Count < 64)
            offsets.Add(Math.Min(offsets[^1] + step, end));
        return offsets;
    }

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

    /// <summary>
    /// A window's stem through its cell's grid (docs/DESIGN.md §4, *A window's stem and light*): the corners of its way, in
    /// grid units, from the cell's edge to the grid's edge on the window's side (the left, or the right where the window
    /// stands in the column right of the cell's), where the page's margin takes it on. It never crosses a cell an item
    /// covers (UI QA re-check, 2026-10-19, F2: on the owner's Keys reading it ran along a row line straight across a 3×3
    /// case's picture): it runs along the lattice's lines, of which an item larger than one cell covers its inner ones;
    /// an item's edges and an empty cell's lines are free, and so is the region's outside edge. A cell at the grid's edge
    /// on that side leaves from the middle of its side, as before. Else the way with the fewest turns, then the shortest:
    /// straight out along a free row line of its side, its lower corner first (as before); else along the column line
    /// beside it to the nearest free row line, below before above; else, where every such line is blocked, along the free
    /// lines to the region's outside edge and round it.
    /// </summary>
    /// <param name="toLeft">The window's margin is left of the cell's grid.</param>
    /// <returns>The corners, a lattice line's i and j (the middle of a side a half): the first on the cell's edge, the last
    /// on the grid's edge on the window's side; one alone for a cell at that edge. Null where the grid gives no way (a
    /// reading whose items overlap or run past the grid).</returns>
    public static IReadOnlyList<(double I, double J)>? StemRoute(DeskRegion region, DeskRect cell, bool toLeft)
    {
        var (cols, rows) = Grid(region);
        if (cols <= 0 || rows <= 0 || cell.W <= 0 || cell.H <= 0 || cell.I < 0 || cell.J < 0 || cell.I + cell.W > cols || cell.J + cell.H > rows)
            return null;
        var side = toLeft ? cell.I : cell.I + cell.W;
        var edge = toLeft ? 0 : cols;
        if (side == edge)
            return [(side, cell.J + cell.H / 2.0)];

        // The lines the items cover: across, the step from (i, j) to (i + 1, j) on a row line inside an item taller than one
        // cell; down, the step from (i, j) to (i, j + 1) on a column line inside one wider.
        var across = new bool[cols, rows + 1];
        var down = new bool[cols + 1, rows];
        foreach (var item in region.Cells)
        {
            if (item.IsSlot)
                continue;
            for (var i = Math.Max(0, item.I); i < Math.Min(cols, item.I + item.W); i++)
            {
                for (var j = Math.Max(0, item.J + 1); j < Math.Min(rows + 1, item.J + item.H); j++)
                    across[i, j] = true;
            }
            for (var i = Math.Max(0, item.I + 1); i < Math.Min(cols + 1, item.I + item.W); i++)
            {
                for (var j = Math.Max(0, item.J); j < Math.Min(rows, item.J + item.H); j++)
                    down[i, j] = true;
            }
        }

        // The search over the lattice's points, each with the way it came in (left, right, up, down, or none at the start),
        // by the fewest turns, then the shortest way, then where it leaves the cell: its side from the lower corner up, its
        // bottom edge, its top edge, its far side, each from the side's end on.
        const int None = 4;
        int[] di = [-1, 1, 0, 0], dj = [0, 0, -1, 1];
        var outward = toLeft ? 0 : 1;
        int Node(int i, int j, int way) => (i * (rows + 1) + j) * 5 + way;
        static long Cost(long turns, long length, long rank) => (turns << 42) | (length << 21) | rank;
        var best = new long[(cols + 1) * (rows + 1) * 5];
        Array.Fill(best, long.MaxValue);
        var cameFrom = new int[best.Length];
        Array.Fill(cameFrom, -1);
        var queue = new PriorityQueue<int, long>();
        void Start(int i, int j, int rank)
        {
            var node = Node(i, j, None);
            if (Cost(0, 0, rank) < best[node])
            {
                best[node] = Cost(0, 0, rank);
                queue.Enqueue(node, best[node]);
            }
        }
        var toFar = toLeft ? 1 : -1;
        var far = toLeft ? cell.I + cell.W : cell.I;
        for (var k = 0; k <= cell.H; k++)
            Start(side, cell.J + cell.H - k, k);
        for (var k = 1; k <= cell.W; k++)
        {
            Start(side + toFar * k, cell.J + cell.H, cell.H + k);
            Start(side + toFar * k, cell.J, cell.H + cell.W + k);
        }
        for (var k = 1; k < cell.H; k++)
            Start(far, cell.J + cell.H - k, cell.H + 2 * cell.W + k);

        var end = -1;
        var endCost = long.MaxValue;
        while (queue.TryDequeue(out var node, out var cost))
        {
            if (cost != best[node])
                continue;
            if (cost >= endCost)
                break;
            var way = node % 5;
            var i = node / 5 / (rows + 1);
            var j = node / 5 % (rows + 1);
            if (i == edge)
            {
                // Out of the grid toward the margin: a turn more where it came along the grid's edge.
                var leaving = cost + (way == None || way == outward ? 0 : Cost(1, 0, 0));
                if (leaving < endCost)
                {
                    endCost = leaving;
                    end = node;
                }
                continue;
            }
            for (var m = 0; m < 4; m++)
            {
                int ni = i + di[m], nj = j + dj[m];
                if (ni < 0 || ni > cols || nj < 0 || nj > rows)
                    continue;
                if (m < 2 ? across[Math.Min(i, ni), j] : down[i, Math.Min(j, nj)])
                    continue;
                var next = cost + Cost(way == None || way == m ? 0 : 1, 1, 0);
                var to = Node(ni, nj, m);
                if (next < best[to])
                {
                    best[to] = next;
                    cameFrom[to] = node;
                    queue.Enqueue(to, next);
                }
            }
        }
        if (end < 0)
            return null;

        // The way back to the cell, its corners only.
        var points = new List<(int I, int J, int Way)>();
        for (var at = end; at >= 0; at = cameFrom[at])
            points.Add((at / 5 / (rows + 1), at / 5 % (rows + 1), at % 5));
        points.Reverse();
        var corners = new List<(double I, double J)> { (points[0].I, points[0].J) };
        for (var k = 1; k < points.Count - 1; k++)
        {
            if (points[k].Way != points[k + 1].Way)
                corners.Add((points[k].I, points[k].J));
        }
        if (points.Count > 1)
            corners.Add((points[^1].I, points[^1].J));
        return corners;
    }

    /// <summary>POCKETS and SPECIAL SLOTS stand on one row, as the game has them, each under its own head.</summary>
    public static bool SameRow(DeskRegionKind first, DeskRegionKind second) =>
        first == DeskRegionKind.Pockets && second == DeskRegionKind.SpecialSlots;

    /// <summary>A region's name as its head says it: LOADOUT, TACTICAL RIG, POCKETS, SPECIAL SLOTS, BACKPACK, POUCH, STASH;
    /// a window by its title ("ZHUK").</summary>
    public static string Name(DeskRegion region) => region.Kind == DeskRegionKind.ContainerWindow ? UiLanguage.Upper(region.Title ?? "") : Name(region.Kind);

    /// <summary>A fixed region's name, read or not.</summary>
    public static string Name(DeskRegionKind kind) => kind switch
    {
        DeskRegionKind.Loadout => RuleTexts.DeskLoadout,
        DeskRegionKind.Rig => RuleTexts.DeskRig,
        DeskRegionKind.Pockets => RuleTexts.DeskPockets,
        DeskRegionKind.SpecialSlots => RuleTexts.DeskSpecialSlots,
        DeskRegionKind.Backpack => RuleTexts.DeskBackpack,
        DeskRegionKind.Pouch => RuleTexts.DeskPouch,
        DeskRegionKind.Stash => RuleTexts.DeskStash,
        _ => "",
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

    /// <summary>
    /// The head line over the page, in muted capitals: "ON YOU 1.01 M ₽ · STASH 6.84 M ₽ · KEEP 3 · 2 · 4", a total with an
    /// unseen container in it ending in "+" ("1.01 M+ ₽"). A part never read is left out (owner, 2026-10-26, decision 5:
    /// "STASH 0 ₽" under a stash never read said a stash worth nothing): STASH until the page holds a stash read, ON YOU
    /// until it holds a gear page read, by the rule that draws a region as never read (<see cref="PartsRead"/>), "ON YOU
    /// 3.20 M+ ₽ · KEEP 0 · 2 · 1"; KEEP stays. The totals are given apart from the rest, so they can count up.
    /// </summary>
    /// <param name="regions">The page in hand's regions.</param>
    public static string HeadLine(DeskTotals totals, double onYou, double stash, IReadOnlyList<DeskRegion> regions)
    {
        string OnYou() => DeskNumbers.Total(onYou, totals.OnYouPlus);
        string Stash() => DeskNumbers.Total(stash, totals.StashPlus);
        return PartsRead(regions) switch
        {
            (true, true) => RuleTexts.DeskHeadLine(onYou: OnYou(), stash: Stash(), now: totals.KeepNow, next: totals.KeepNext, later: totals.KeepLater),
            (true, false) => RuleTexts.DeskHeadLineOnYou(onYou: OnYou(), now: totals.KeepNow, next: totals.KeepNext, later: totals.KeepLater),
            (false, true) => RuleTexts.DeskHeadLineStash(stash: Stash(), now: totals.KeepNow, next: totals.KeepNext, later: totals.KeepLater),
            _ => RuleTexts.DeskHeadLineKeep(now: totals.KeepNow, next: totals.KeepNext, later: totals.KeepLater),
        };
    }
}
