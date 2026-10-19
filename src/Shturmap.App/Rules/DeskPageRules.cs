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
    /// <summary>The cell's sizes, in DIP, smallest first: the item cell's 40 up to 80, in steps of 8, each a whole number of
    /// pixels at every Windows scale from 100 to 200 % in quarters, so the grid's hairlines and the art stay crisp.</summary>
    public static readonly IReadOnlyList<double> Sizes = [40, 48, 56, 64, 72, 80];

    /// <summary>The least cell the game's two columns take: under it one column keeps the cells larger.</summary>
    public const double TwoColumnsLeast = 56;

    /// <summary>The space between the two columns, between regions side by side in a row, and the page's margin on
    /// either side, in DIP.</summary>
    public const double ColumnGap = 24;

    public const double RowGap = 24;

    public const double Margin = 18;

    /// <summary>
    /// The page's cell size and columns for a desk this wide (owner, 2026-10-20: the cells scale with the desk's width,
    /// the rail stays for the cards): the game's two columns, the gear left and the stash right, at the largest size of
    /// <see cref="Sizes"/> from 56 where both fit the desk; else one column at the largest size where its widest row fits;
    /// 40 at the least. A 900×560 window's desk (516 DIP) stands at 40, one column; 1600×900's (1216) at 64 in two; a
    /// 2560 monitor's at 80. The break is the stash's place in reading order, −1 for one column.
    /// </summary>
    public static (double Cell, int Break) Layout(IReadOnlyList<DeskRegion> regions, double deskWidth)
    {
        var stash = -1;
        for (var i = 0; i < regions.Count && stash < 0; i++)
        {
            if (regions[i].Kind == DeskRegionKind.Stash)
                stash = i;
        }
        if (stash > 0)
        {
            foreach (var size in Sizes.Where(s => s >= TwoColumnsLeast).Reverse())
            {
                if (TwoColumnsWidth(regions, stash, size) + 2 * Margin <= deskWidth)
                    return (size, stash);
            }
        }
        foreach (var size in Sizes.Reverse())
        {
            if (ColumnWidth(regions, size, 10) + 2 * Margin <= deskWidth)
                return (size, -1);
        }
        return (Sizes[0], -1);
    }

    /// <summary>The cell's size for these regions on a desk this wide (<see cref="Layout"/>).</summary>
    public static double Cell(IReadOnlyList<DeskRegion> regions, double deskWidth) => Layout(regions, deskWidth).Cell;

    // Both columns side by side: the gear's (six cells at the least) and the stash's (ten), the gap between.
    private static double TwoColumnsWidth(IReadOnlyList<DeskRegion> regions, int at, double size) =>
        ColumnWidth(regions.Take(at).ToList(), size, 6) + ColumnGap + ColumnWidth(regions.Skip(at).ToList(), size, 10);

    /// <summary>The regions' heads and the fold lines at a cell size: the eyebrows' 11 px at 40 and 48, 12 at 56 and 64,
    /// 13 at 72 and 80, so a head stays in scale with its grid.</summary>
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

    /// <summary>Where the second column starts in reading order: the stash, where both columns fit the desk at 56 DIP or
    /// more (what follows the stash, its windows and the windows attached to none, stands under it); −1 for one column
    /// (<see cref="Layout"/>).</summary>
    public static int ColumnBreak(IReadOnlyList<DeskRegion> regions, double deskWidth) => Layout(regions, deskWidth).Break;

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
