namespace Shturmap.Core.Desk;

/// <summary>Which total of the head line a region counts in: ON YOU, STASH, or neither (a window whose containers of its
/// name are split between the two); in a raid's reading, LOOT (the LOOT pane, a window attached in it).</summary>
public enum DeskPart
{
    OnYou,
    Stash,
    Neither,
    Loot,
}

/// <summary>One cell with what the desk draws on it (docs/DESIGN.md §4, *The cell*).</summary>
/// <param name="Cell">The cell as read: place, footprint (none for a slot), item or candidates, count, printed condition,
/// found-in-raid mark.</param>
/// <param name="Figure">Its value (*The cell*, top right: the better of the flea's net and the best trader's, its source,
/// "~", "+", the "one of N" range, the tint, the arrow, the liquidity); null when no item of it has a price (the secure
/// container: no figure).</param>
/// <param name="Keep">The keep notch (*The cell*, top left): the strongest need's level; "later" only for a found-in-raid
/// need or an item that can't be bought.</param>
/// <param name="Unseen">Contents unseen (*The cell*, the addendum's C1a): a container no window of the page is attached
/// to, or a weapon's mods; its figure ends in "+".</param>
public sealed record DeskCellSheet(DeskCell Cell, DeskFigure? Figure, DeskKeepLevel Keep, bool Unseen)
{
    /// <summary>The card's KEEP lines (*The card*): every need of the cell's item in progress order (now, next, a hideout
    /// module's, later), each with its level, its quest or station, count and found-in-raid flag; of a "?" cell's candidates,
    /// those all of them share are not told apart: empty.</summary>
    public IReadOnlyList<DeskNeed> Needs { get; init; } = [];
}

/// <summary>One region in reading order with its cells' figures, the part it counts in and its total.</summary>
public sealed record DeskRegionSheet(DeskRegion Region, IReadOnlyList<DeskCellSheet> Cells, DeskPart Part, double Total, bool Plus);

/// <summary>The head line (docs/DESIGN.md §4, *The head line and the fold*, B1): "ON YOU 1.01 M ₽ · STASH 6.84 M ₽ · KEEP 3 ·
/// 2 · 4", a "+" on a total with an unseen container (or a weapon's mods) in it, and the page's cells with a now, next
/// and later notch.</summary>
public sealed record DeskTotals(double OnYou, bool OnYouPlus, double Stash, bool StashPlus, int KeepNow, int KeepNext, int KeepLater)
{
    /// <summary>In a raid's reading, what the LOOT pane shows (the in-raid glance), and whether contents of it went unseen
    /// (a part not searched, a container without its window, a weapon's mods): "+".</summary>
    public double Loot { get; init; }

    public bool LootPlus { get; init; }
}

/// <summary>
/// The page with its values: each cell's figure and keep notch, each region's part and total, the head line. Totals count
/// what the cells read would bring (each one's better sale: the flea's net after the fee or the best trader's; a "one of
/// N" at the least of its candidates); a window counts in the part of the region its container is in; a window attached
/// to none counts where all the containers of its name are, or in neither when they are split between ON YOU and the
/// stash (docs/DESIGN.md §4, *What it mirrors*).
/// </summary>
public sealed class DeskSheet
{
    /// <summary>A sheet of no page: nothing read yet.</summary>
    public static DeskSheet Empty { get; } = new()
    {
        Page = new DeskPage(DateTimeOffset.MinValue, []),
        Regions = [],
        Totals = new DeskTotals(0, false, 0, false, 0, 0, 0),
    };

    public required DeskPage Page { get; init; }

    /// <summary>The regions in reading order (<see cref="DeskPage.InReadingOrder"/>).</summary>
    public required IReadOnlyList<DeskRegionSheet> Regions { get; init; }

    public required DeskTotals Totals { get; init; }

    /// <param name="traderName">A trader's name by id, for a figure whose source is a trader.</param>
    public static DeskSheet Of(DeskPage page, Func<string, DeskItemFacts?> facts, DeskFleaRules flea, DeskKeep? keep = null, DeskFeeLevels levels = default,
        Func<string, string>? traderName = null)
    {
        keep ??= DeskKeep.Empty;
        var regions = new List<DeskRegionSheet>();
        foreach (var region in page.InReadingOrder())
        {
            var cells = region.Cells.Select(cell => CellOf(page, region, cell, facts, flea, keep, levels))
                .Select(c => c.Figure is { Trader: { } trader } f && traderName is not null ? c with { Figure = f with { TraderName = traderName(trader) } } : c)
                .ToList();
            var part = PartOf(page, region, []);
            // What the page is worth: each cell's better sale (the flea's net after the fee or the best trader's), not the
            // listing price the cell shows (owner, 2026-10-19: the cell shows the flea's price, the net is the card's).
            regions.Add(new DeskRegionSheet(region, cells, part, cells.Sum(c => c.Figure?.Worth ?? 0), cells.Any(c => c.Unseen) || region.ContentsUnseen));
        }
        double onYou = regions.Where(r => r.Part == DeskPart.OnYou).Sum(r => r.Total);
        double stash = regions.Where(r => r.Part == DeskPart.Stash).Sum(r => r.Total);
        var all = regions.SelectMany(r => r.Cells).ToList();
        var totals = new DeskTotals(onYou, regions.Any(r => r.Part == DeskPart.OnYou && r.Plus), stash, regions.Any(r => r.Part == DeskPart.Stash && r.Plus),
            all.Count(c => c.Keep == DeskKeepLevel.Now), all.Count(c => c.Keep == DeskKeepLevel.Next), all.Count(c => c.Keep == DeskKeepLevel.Later))
        {
            Loot = regions.Where(r => r.Part == DeskPart.Loot).Sum(r => r.Total),
            LootPlus = regions.Any(r => r.Part == DeskPart.Loot && r.Plus),
        };
        return new DeskSheet { Page = page, Regions = regions, Totals = totals };
    }

    /// <summary>One cell's figure and notch: of its item, or of "one of N" its candidates (a pick narrows them to one). A
    /// "?" cell takes a keep notch only when every candidate has one, the weakest of them.</summary>
    public static DeskCellSheet CellOf(DeskPage page, DeskRegion region, DeskCell cell, Func<string, DeskItemFacts?> facts, DeskFleaRules flea,
        DeskKeep keep, DeskFeeLevels levels = default)
    {
        var unseen = page.Unseen(region, cell);
        var ids = cell.ItemId is { } one ? [one] : cell.Ids;
        var items = ids.Select(facts).OfType<DeskItemFacts>().ToList();
        if (items.Count == 0)
            return new DeskCellSheet(cell, null, DeskKeepLevel.None, unseen);
        // A count past what the item stacks to is a misread (the owner's test of 2026-10-19: a drill read "×310", 53 M of a
        // Junk window's 60 M): one, at least, as an unread count is.
        if (cell.Count > 1 && items.Count == ids.Count && items.All(f => f.StackMax is not null) && cell.Count > items.Max(f => f.StackMax!.Value))
            cell = cell with { Count = 1 };
        var values = items.Select(f => (f, DeskValues.Of(f, cell.Count, cell.Print, cell.FoundInRaid, flea, levels))).ToList();
        // A slot shows no footprint: its item's own counts for the value per slot.
        var cells = cell.IsSlot ? (items.Count == 1 ? items[0].W * items[0].H : 1) : cell.W * cell.H;
        var figure = DeskValues.Figure(values, cells, unseen);
        var marks = items.Select(f => keep.MarkOf(f.Id, f.Buyable)).ToList();
        var mark = items.Count < ids.Count || marks.Any(m => m == DeskKeepLevel.None) ? DeskKeepLevel.None : marks.Min();
        return new DeskCellSheet(cell, figure, mark, unseen) { Needs = items.Count == 1 ? keep.NeedsOf(items[0].Id) : [] };
    }

    // The part a region counts in: the loadout and the middle column ON YOU, the stash STASH, the LOOT pane LOOT; an attached window its
    // container's region's; one attached to none where all containers of its name are, else neither.
    private static DeskPart PartOf(DeskPage page, DeskRegion region, HashSet<string> seen)
    {
        if (region.Kind != DeskRegionKind.ContainerWindow)
            return region.IsLoot ? DeskPart.Loot : region.OnYou ? DeskPart.OnYou : DeskPart.Stash;
        if (!seen.Add(region.Key))
            return DeskPart.Neither;
        if (region.AttachedTo is { } anchor)
            return page.Region(anchor.Region) is { } holder ? PartOf(page, holder, seen) : DeskPart.Neither;
        var parts = region.SameNameIn.Select(page.Region).OfType<DeskRegion>().Select(r => PartOf(page, r, seen)).Distinct().ToList();
        return parts.Count == 1 ? parts[0] : DeskPart.Neither;
    }
}
