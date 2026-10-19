using Shturmap.Core;
using Shturmap.Core.Desk;

namespace Shturmap.App.Rules;

/// <summary>
/// The desk page's layout and words (owner, 2026-10-19, the addendum's A(2) and B1; docs/DESIGN.md §4, *Its place*,
/// *What it mirrors*, *The head line and the fold*): the cell's size and the columns by the desk's width, the regions'
/// heads, the fold line's names, the "Not attached" line and the head line.
/// </summary>
public static class DeskPageRules
{
    /// <summary>From a desk this wide the cells are 56 DIP, under it the item cell's 40.</summary>
    public const double LargeFrom = 600;

    /// <summary>From a desk this wide the column breaks before STASH into the game's two columns.</summary>
    public const double TwoColumnsFrom = 980;

    /// <summary>The space between the two columns, between regions side by side in a row, and the page's margin on
    /// either side, in DIP.</summary>
    public const double ColumnGap = 24;

    public const double RowGap = 24;

    public const double Margin = 18;

    /// <summary>The cell's size for a desk this wide.</summary>
    public static double Cell(double deskWidth) => deskWidth >= LargeFrom ? DeskCells.Large : DeskCells.Small;

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

    /// <summary>Where the second column starts in reading order: the stash, from a desk 980 DIP across where both columns
    /// fit it (what follows the stash, its windows and the windows attached to none, stands under it); −1 for one
    /// column.</summary>
    public static int ColumnBreak(IReadOnlyList<DeskRegion> regions, double deskWidth)
    {
        if (deskWidth < TwoColumnsFrom)
            return -1;
        var at = -1;
        for (var i = 0; i < regions.Count && at < 0; i++)
        {
            if (regions[i].Kind == DeskRegionKind.Stash)
                at = i;
        }
        if (at <= 0)
            return -1;
        var size = Cell(deskWidth);
        var both = ColumnWidth(regions.Take(at).ToList(), size, 6) + ColumnGap + ColumnWidth(regions.Skip(at).ToList(), size, 10);
        return both + 2 * Margin <= deskWidth ? at : -1;
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
