using Shturmap.Core;
using Shturmap.Core.Desk;

namespace Shturmap.App.Rules;

/// <summary>
/// The in-raid glance's plate (owner, 2026-10-19: from its panel, Variant 1, "with the looted container's or body's item
/// layout mirrored"; docs/DESIGN.md §4, "Screen anatomy", *The in-raid glance*): what a raid screenshot of an inventory
/// screen showed, over the map at the top of its column, for about 6 s. Variant 1's head (FROM YOUR SCREENSHOT, the LOOT
/// total large, ON YOU and the "?" cells' count under it), then the LOOT pane's regions as the game shows them, each under
/// its own head with the desk's cells (*The stash desk*, *The cell*): a container's grid, or a body's slots, its rig,
/// pockets and backpack, a part not searched yet a closed region with its word and "+". The player's own regions are
/// never drawn: their gear is known, the glance is about the loot; with no LOOT pane the plate is its head alone, ON YOU's
/// total and a line saying nothing was looted on the screen. The cells take the desk's sizes (<see cref="DeskPageRules.Sizes"/>)
/// up to <see cref="LargestCell"/>, the largest at which the regions fit the plate's room, flowed into columns where one
/// is too short; the plate's height is about half the map's, and where even the desk's least size doesn't fit the room,
/// the last regions give way to one line in the fold line's words, "ALSO · BACKPACK 412k+ ₽": nothing scrolls in a raid.
/// Nothing on it is operated (no click, pointer or tip in a raid; principle 1).
/// </summary>
public static class GlancePlateRules
{
    /// <summary>How long the plate stands (owner, 2026-10-19: "about 6 seconds"), from its own arrival; a newer reading
    /// replaces it at once and the clock starts again.</summary>
    public static readonly TimeSpan Stands = TimeSpan.FromSeconds(6);

    /// <summary>How long it fades out, as the panel drew it (0.4 s); at once with the animation effects off.</summary>
    public static readonly TimeSpan FadesOut = TimeSpan.FromSeconds(0.4);

    /// <summary>The plate's cells at the most, in DIP: the desk's sizes from 40 up to 80, the desk's largest until the cells
    /// filled the screen (2026-10-19). A glance stands beside the map, it doesn't fill it (the coordinator's rule).</summary>
    public const double LargestCell = 80;

    /// <summary>The plate's share of the map's height at the most (the brief: "about half"); the map stays under it.</summary>
    public const double HeightShare = 0.5;

    /// <summary>The plate's room from the map column's sides and top, in DIP (the notes' margin); under the notes 8 DIP.</summary>
    public const double Margin = 16;

    public const double UnderNotes = 8;

    /// <summary>The plate's padding: its sides, its top over the eyebrow, its foot under the last region.</summary>
    public const double PadSide = 16;

    public const double PadTop = 12;

    public const double PadBottom = 16;

    /// <summary>The space between the head and the first region, between regions in a column, and between columns.</summary>
    public const double HeadGap = 12;

    public const double BlockGap = 16;

    public const double ColumnGap = DeskPageRules.ColumnGap;

    /// <summary>A column's least width in cells: four, so a body's slots stand two to a row and its weapons whole (a weapon's
    /// slot, four cells and a gap, widens it a little); the desk's gear takes six, a plate over the map no more than its loot
    /// needs.</summary>
    public const int LeastCells = 4;

    /// <summary>The type sizes of the head, as the panel's Variant 1 drew them: the eyebrow, the title, the line under it.</summary>
    public const double EyebrowSize = 13;

    public const double TitleSize = 48;

    public const double LineSize = 15;

    /// <summary>The regions the plate draws: the LOOT pane's in reading order (a body's slots, rig, pockets, backpack, or a
    /// container's grid; a window attached in it), never the player's own; none without a LOOT pane.</summary>
    public static IReadOnlyList<DeskRegionSheet> Drawn(RaidGlance glance) =>
        glance.Loot is null ? [] : glance.Sheet.Regions.Where(r => r.Part == DeskPart.Loot).ToList();

    /// <summary>The plate's head, Variant 1's words: the eyebrow, the title's word and its total, the line under it.</summary>
    /// <param name="Unsure">The line's muted end, "2 unsure" (the "?" cells), or null.</param>
    public sealed record Head(string Eyebrow, string Word, string Total, string Line, string? Unsure);

    /// <summary>
    /// The head: FROM YOUR SCREENSHOT over "LOOT 1.87 M+ ₽", and under it "ON YOU 1.01 M+ ₽ · 2 unsure", the "?" cells
    /// drawn counted; a total with contents unseen in it ends in "+" (a part not searched, a container without its window,
    /// a weapon's mods). Without a LOOT pane, "ON YOU 1.01 M+ ₽" over "Your gear · nothing looted on this screen".
    /// </summary>
    public static Head HeadOf(RaidGlance glance)
    {
        var totals = glance.Sheet.Totals;
        var onYou = DeskNumbers.Total(totals.OnYou, totals.OnYouPlus);
        if (glance.Loot is null)
            return new Head(RuleTexts.GlanceEyebrow, RuleTexts.GlanceOnYouWord, onYou, RuleTexts.GlanceNothingLooted, null);
        var unsure = Unsure(Drawn(glance));
        return new Head(RuleTexts.GlanceEyebrow, RuleTexts.GlanceLootWord, DeskNumbers.Total(totals.Loot, totals.LootPlus), RuleTexts.GlanceOnYou(total: onYou),
            unsure > 0 ? RuleTexts.GlanceUnsure(count: unsure) : null);
    }

    /// <summary>How many cells of these regions fit several items alike and weren't picked (the "?" cells).</summary>
    public static int Unsure(IEnumerable<DeskRegionSheet> regions) => regions.SelectMany(r => r.Cells).Count(c => c.Cell.Ids.Count > 1 && c.Cell.ItemId is null);

    /// <summary>The container's kind in the game's words, from the LOOT regions' title ("LOOT · BODY": "BODY"), or null.</summary>
    public static string? Kind(string? lootTitle)
    {
        if (lootTitle is null)
            return null;
        var dot = lootTitle.IndexOf('·', StringComparison.Ordinal);
        var kind = dot < 0 ? "" : lootTitle[(dot + 1)..].Trim();
        return kind.Length > 0 ? UiLanguage.Upper(kind) : null;
    }

    /// <summary>A region's head on the plate: a body's slots under its kind ("BODY"), its parts under the desk's heads
    /// (TACTICAL RIG, POCKETS, BACKPACK), a container's grid under its kind ("DRAWER"), or LOOT where the game's kind wasn't
    /// read; a window under its title and the part it stands in ("ZHUK · IN THE BACKPACK").</summary>
    public static string Name(DeskRegion region, DeskPage page) => region.Kind switch
    {
        DeskRegionKind.LootSlots or DeskRegionKind.Loot => Kind(region.Title) ?? RuleTexts.GlanceLootWord,
        DeskRegionKind.LootRig => RuleTexts.DeskRig,
        DeskRegionKind.LootPockets => RuleTexts.DeskPockets,
        DeskRegionKind.LootBackpack => RuleTexts.DeskBackpack,
        DeskRegionKind.ContainerWindow => WindowHead(region, page),
        _ => DeskPageRules.Name(region),
    };

    // A window attached in the LOOT pane: "ZHUK · IN THE BACKPACK" by the part its container is in, "ZHUK · IN BODY" in the
    // slots or a container's grid; attached to none, its title.
    private static string WindowHead(DeskRegion window, DeskPage page)
    {
        if (window.AttachedTo is not { } anchor || page.Region(anchor.Region) is not { } holder)
            return DeskPageRules.Name(window);
        var title = UiLanguage.Upper(window.Title ?? "");
        return holder.Kind switch
        {
            DeskRegionKind.LootRig => RuleTexts.DeskWindowInRig(title: title),
            DeskRegionKind.LootPockets => RuleTexts.DeskWindowInPockets(title: title),
            DeskRegionKind.LootBackpack => RuleTexts.DeskWindowInBackpack(title: title),
            _ => RuleTexts.DeskWindowInWindow(title: title, container: Name(holder, page)),
        };
    }

    /// <summary>
    /// Where a LOOT region's contents went unseen, its word and "+" (the desk's "at least this, more inside"): a part not
    /// searched yet "UNSEARCHED +", one the game is searching "SEARCHING +", a grid the pane's scrolling cut at its foot
    /// "MORE BELOW +"; null where all of it was seen. The "+" is joined to its word by a no-break space.
    /// </summary>
    public static string? State(DeskRegion region)
    {
        if (!region.ContentsUnseen)
            return null;
        var word = region.Search switch
        {
            DeskSearch.Unsearched => RuleTexts.GlanceUnsearched,
            DeskSearch.Searching => RuleTexts.GlanceSearching,
            _ => RuleTexts.GlanceMoreBelow,
        };
        return word + " +";
    }

    /// <summary>Whether a region draws its grid (a part not searched has none to draw: its closed region stands there).</summary>
    public static bool HasGrid(DeskRegion region) => DeskPageRules.Grid(region) is { Cols: > 0, Rows: > 0 };

    /// <summary>A region's head as the plate writes it: its name, and where its grid is drawn but not all of it was seen,
    /// its state after a " · " ("POCKETS · SEARCHING +"); a region without a grid says its state in its closed region.</summary>
    public static string HeadLine(DeskRegion region, DeskPage page) =>
        HasGrid(region) && State(region) is { } state ? Name(region, page) + " · " + state : Name(region, page);

    /// <summary>The closed region's height at a cell size: three quarters of a cell, never under 28 DIP.</summary>
    public static double ClosedHeight(double cell) => Math.Max(28, Math.Round(cell * 0.75));

    /// <summary>The cut line's words: "ALSO" and each region that gave way, its name and total ("BACKPACK 412k+ ₽").</summary>
    public static (string Word, IReadOnlyList<string> Regions) CutLine(IEnumerable<DeskRegionSheet> cut, DeskPage page) =>
        (RuleTexts.GlanceAlso, cut.Select(r => RuleTexts.GlanceCutRegion(name: Name(r.Region, page), total: DeskNumbers.Total(r.Total, r.Plus))).ToList());

    /// <summary>
    /// The plate's regions laid out: the cell's size, the regions shown and the column each stands in, each column's width,
    /// the regions that gave way to the cut line, and the room they take, the cut line included.
    /// </summary>
    public sealed record Arrangement(double Cell, IReadOnlyList<DeskRegionSheet> Shown, IReadOnlyList<int> Columns, IReadOnlyList<double> ColumnWidths,
        IReadOnlyList<DeskRegionSheet> Cut, double Width, double Height)
    {
        public bool Fits(double width, double height) => Width <= width && Height <= height;
    }

    /// <summary>
    /// The regions in a room this wide and this high: the largest of the desk's sizes, from <see cref="LargestCell"/> down to
    /// 40, at which they fit, each column filled in reading order down to the room's foot and the next one started beside
    /// it, as the desk's gear flows (<see cref="DeskPageRules.Arrange"/>). Where even 40 doesn't fit, the regions at the end
    /// give way to the cut line, as many as must: the first ones stand, the rest are named in one line. None to draw, no
    /// room taken.
    /// </summary>
    /// <param name="textScale">The size text is drawn at against the size written (the heads' and the cut line's height).</param>
    public static Arrangement Layout(IReadOnlyList<DeskRegionSheet> regions, DeskPage page, double width, double height, double textScale = 1)
    {
        if (regions.Count == 0)
            return new Arrangement(DeskPageRules.Sizes[0], [], [], [], [], 0, 0);
        foreach (var size in DeskPageRules.Sizes.Where(s => s <= LargestCell).Reverse())
        {
            if (Arrange(regions, size, height, textScale) is var all && all.Fits(width, height))
                return all;
        }
        var least = DeskPageRules.Sizes[0];
        for (var shown = regions.Count - 1; shown >= 0; shown--)
        {
            var cut = regions.Skip(shown).ToList();
            var line = CutHeight(cut, page, least, width, textScale);
            var room = height - line - (shown > 0 ? BlockGap : 0);
            var some = Arrange(regions.Take(shown).ToList(), least, room, textScale);
            if (some.Fits(width, room) || shown == 0)
                return some with { Cut = cut, Height = some.Height + (shown > 0 ? BlockGap : 0) + line, Width = Math.Max(some.Width, Math.Min(width, LeastCells * least + 1)) };
        }
        return Arrange(regions, least, height, textScale);
    }

    /// <summary>The regions at a cell size, each column down to the room's foot: a region that would pass it starts the next
    /// column, unless it is the column's first.</summary>
    public static Arrangement Arrange(IReadOnlyList<DeskRegionSheet> regions, double size, double room, double textScale = 1)
    {
        var widths = new List<double>();
        var heights = new List<double>();
        var counts = new List<int>();
        var placed = new List<int>();
        foreach (var sheet in regions)
        {
            var width = RegionWidth(sheet.Region, size);
            var height = RegionHeight(sheet.Region, size, width, textScale);
            if (widths.Count == 0 || counts[^1] > 0 && heights[^1] + BlockGap + height > room)
            {
                widths.Add(0);
                heights.Add(0);
                counts.Add(0);
            }
            var c = widths.Count - 1;
            widths[c] = Math.Max(widths[c], width);
            heights[c] += (counts[c] > 0 ? BlockGap : 0) + height;
            counts[c]++;
            placed.Add(c);
        }
        return new Arrangement(size, regions, placed, widths, [], widths.Sum() + Math.Max(0, widths.Count - 1) * ColumnGap,
            heights.Count == 0 ? 0 : heights.Max());
    }

    /// <summary>A region's width at a cell size: its grid's, never under <see cref="LeastCells"/> cells (a weapon's slot fits).</summary>
    public static double RegionWidth(DeskRegion region, double size)
    {
        var widest = LeastCells * size;
        if (HasGrid(region))
            widest = Math.Max(widest, DeskPageRules.Grid(region).Cols * size);
        foreach (var slot in region.Cells.Where(c => c.IsSlot))
            widest = Math.Max(widest, DeskCells.Width(slot, size));
        return widest + 1;
    }

    /// <summary>A region's height at a cell size in a column this wide: its head, its strip of slots (wrapping at the
    /// column's width, 4 DIP apart), its grid, or where it has none and contents went unseen, its closed region; 6 DIP
    /// between them. The head's height is an estimate of the drawn text (an eyebrow about 1.4 of its size), a little over.</summary>
    public static double RegionHeight(DeskRegion region, double size, double column, double textScale = 1)
    {
        var height = Math.Ceiling(DeskPageRules.HeadSize(size) * textScale * 1.4);
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
        if (HasGrid(region))
            height += 6 + DeskPageRules.Grid(region).Rows * size + 1;
        else if (State(region) is not null)
            height += 6 + ClosedHeight(size);
        return height;
    }

    // The cut line's height in a room this wide: its words in the heads' size at the cell size, wrapping (each "·" with the
    // name after it), an estimate a little over (about 0.62 of the size a character, as the desk's fold names).
    private static double CutHeight(IReadOnlyList<DeskRegionSheet> cut, DeskPage page, double size, double width, double textScale)
    {
        if (cut.Count == 0)
            return 0;
        var px = DeskPageRules.HeadSize(size) * textScale;
        var (word, names) = CutLine(cut, page);
        var (lines, x) = (1, word.Length * px * 0.62);
        foreach (var name in names)
        {
            var w = (name.Length + 2) * px * 0.62 + 6;
            if (x + 6 + w > width)
                (lines, x) = (lines + 1, w);
            else
                x += 6 + w;
        }
        var line = Math.Ceiling(px * 1.4);
        return lines * line + (lines - 1) * 2;
    }
}
