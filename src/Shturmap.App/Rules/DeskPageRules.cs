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

    /// <summary>The cell's size for a desk this wide.</summary>
    public static double Cell(double deskWidth) => deskWidth >= LargeFrom ? DeskCells.Large : DeskCells.Small;

    /// <summary>Where the second column starts in reading order: the stash, from a desk 980 DIP across (what follows it,
    /// its windows and the windows attached to none, stands under it); −1 for one column.</summary>
    public static int ColumnBreak(IReadOnlyList<DeskRegion> regions, double deskWidth)
    {
        if (deskWidth < TwoColumnsFrom)
            return -1;
        for (var i = 0; i < regions.Count; i++)
        {
            if (regions[i].Kind == DeskRegionKind.Stash)
                return i > 0 ? i : -1;
        }
        return -1;
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
