using System.Text.Json;
using System.Text.Json.Serialization;
using Shturmap.Core.Logs;

namespace Shturmap.Core.Desk;

/// <summary>What a reading did to the page in hand: the page now, the regions it changed (the fold opens these and folds
/// the rest; docs/DESIGN.md §4, *The head line and the fold*), and whether it was the mode's first.</summary>
public sealed record DeskChange(DeskPage Page, IReadOnlySet<string> Changed, bool First)
{
    /// <summary>The regions the reading showed but read too little of to replace the page's (<see cref="DeskLedger.Blank"/>):
    /// kept as they were, unchanged; the session logs them.</summary>
    public IReadOnlySet<string> Refused { get; init; } = new HashSet<string>();

    /// <summary>The regions the reading showed in part (<see cref="DeskRegion.Partial"/>: the column's foot cut their grid,
    /// or none of it was in view) whose page's cells and compartments outside the part read were kept; the session logs
    /// them.</summary>
    public IReadOnlySet<string> Kept { get; init; } = new HashSet<string>();

    /// <summary>The windows that left the page: attached to nothing and not shown by three readings in a row (owner,
    /// 2026-10-25); the session logs them.</summary>
    public IReadOnlySet<string> Dropped { get; init; } = new HashSet<string>();
}

/// <summary>
/// The stash desk's page in hand, one per game mode, kept in <c>desk.json</c> in the data folder (docs/DESIGN.md §8, "The
/// desk file"): a newer reading replaces the regions it shows and keeps the others; the cells a window covered keep their
/// last reading. "Unchanged" compares a reading with this page only, never with a register of the stash (§1).
/// </summary>
public sealed class DeskLedger
{
    public const int Version = 1;

    private readonly Dictionary<GameMode, DeskPage> _pages = [];

    /// <summary>Whether something changed since the file was last written.</summary>
    public bool Dirty { get; private set; }

    /// <summary>The mode's page, or null before its first reading.</summary>
    public DeskPage? Page(GameMode mode) => _pages.GetValueOrDefault(mode);

    /// <summary>Takes a reading into the mode's page.</summary>
    public DeskChange Apply(GameMode mode, DeskPage reading)
    {
        var old = Page(mode);
        var merge = MergeAll(old, reading);
        _pages[mode] = merge.Page;
        Dirty = true;
        return new DeskChange(merge.Page, merge.Changed, old is null) { Refused = merge.Refused, Kept = merge.Kept, Dropped = merge.Dropped };
    }

    /// <summary>A "?" cell's pick (step C: a candidate clicked on its card): kept with the cell until a reading changes it;
    /// null takes it back. A slot by its key, a grid's cell by its top left. False when no such cell, or the item isn't one
    /// of its candidates.</summary>
    public bool Pick(GameMode mode, string region, string? slot, int i, int j, string? itemId)
    {
        if (Page(mode) is not { } page || page.Region(region) is not { } r)
            return false;
        var cell = r.Cells.FirstOrDefault(c => slot is not null ? c.Slot == slot : !c.IsSlot && c.I == i && c.J == j);
        if (cell is null || (itemId is not null && !cell.Ids.Contains(itemId)))
            return false;
        var cells = r.Cells.Select(c => ReferenceEquals(c, cell) ? c with { Pick = itemId } : c).ToList();
        _pages[mode] = page with { Regions = page.Regions.Select(x => ReferenceEquals(x, r) ? r with { Cells = cells } : x).ToList() };
        Dirty = true;
        return true;
    }

    /// <summary>
    /// The page after a reading, and the regions that changed: each region the reading shows replaces the old one, the
    /// cells a window covered (and the covered slots) taken from the old one where they don't meet a cell read; the
    /// others stay. A region changed when it is new, or its cells, title or attachment differ from the old one's.
    /// </summary>
    public static (DeskPage Page, IReadOnlySet<string> Changed) Merge(DeskPage? old, DeskPage reading)
    {
        var merge = MergeAll(old, reading);
        return (merge.Page, merge.Changed);
    }

    /// <summary>How many readings in a row that didn't show a window attached to nothing take it off the page (owner,
    /// 2026-10-25).</summary>
    public const int MissedToDrop = 3;

    /// <summary>A window no reading should have made, never kept (owner, 2026-10-25): no cell read (no item, no "one of N")
    /// and no title that names a container of the catalogue (owner's second test, 2026-10-25: a pistol's rounds "0/17" in
    /// the stash, read "Ø/17", made a window of one cell of nothing; until then only a window of no cell and no
    /// title).</summary>
    public static bool Junk(DeskRegion region) =>
        region.Kind == DeskRegionKind.ContainerWindow && region.ReadCells == 0 &&
        (string.IsNullOrWhiteSpace(region.Title) || region.TitleIds.Count == 0);

    /// <summary>A middle region a reading made from its heading alone (code review, 2026-10-25): read in part, no grid and
    /// no cell in it, its frames out of view, under a window or under no heading. It says nothing of the region: a page
    /// that has no version of it doesn't take it, and draws the region as never read (a container's stood as its empty
    /// slot box, "empty" for "not read"; docs/DESIGN.md §4, *A region never read*); one that has keeps its own.</summary>
    public static bool HeadingAlone(DeskRegion region) => region.Partial && region.Cells.Count == 0 && region.Parts.Count == 0;

    // A window attached to nothing (no container of its name on the page) that enough readings in a row didn't show.
    private static bool Gone(DeskRegion region) =>
        region.Kind == DeskRegionKind.ContainerWindow && region.AttachedTo is null && region.SameName == 0 && region.Missed >= MissedToDrop;

    /// <summary>
    /// A region read in part (<see cref="DeskRegion.Partial"/>) filled from the page's version (owner, 2026-10-25): the
    /// cells read stand; the page's cells outside the compartments read (below the column's foot, or all of them when none
    /// was in view) and under a window stay, as does its slot where none was read; a compartment the foot cut stands as
    /// tall as the page had it. Not when the slot reads another container than the page's: its grid isn't that one. Null
    /// when the page's version adds nothing to the reading (the region whole after all).
    /// </summary>
    private static DeskRegion? Filled(DeskRegion before, DeskRegion region)
    {
        // A slot read as nothing (its label unread) says nothing of the container.
        var slotRead = region.Cells.FirstOrDefault(c => c.IsSlot && c.Ids.Count > 0);
        var slotKept = before.Cells.FirstOrDefault(c => c.IsSlot && c.Ids.Count > 0);
        if (slotRead is not null && slotKept is not null && !slotRead.Ids.Intersect(slotKept.Ids).Any())
            return null;
        var view = region.Parts;
        // The page's slot stays where the reading has none, or one whose label it couldn't read.
        var pageSlot = before.Cells.FirstOrDefault(c => c.IsSlot);
        var readSlot = region.Cells.FirstOrDefault(c => c.IsSlot);
        var keepSlot = pageSlot is not null && (readSlot is null || (readSlot.Ids.Count == 0 && pageSlot.Ids.Count > 0));
        if (keepSlot && readSlot is not null)
            region = region with { Cells = region.Cells.Where(c => !c.IsSlot).ToList() };
        // A page's cell in part in the compartments read is what the reading saw there, unless a window covered it there
        // (owner's second test, 2026-10-25: the Beta 2 backpack, five by five, stood on in the backpack after the four
        // rows in view read empty; until then only a cell wholly in them gave way).
        var kept = before.Cells.Where(c => c.IsSlot
            ? keepSlot
            : !region.Cells.Any(n => !n.IsSlot && n.Rect.Overlaps(c.Rect)) && (!view.Any(p => p.Overlaps(c.Rect)) || region.Covered.Any(r => r.Overlaps(c.Rect)))).ToList();
        var parts = view.Select(p => before.Parts.FirstOrDefault(q => q.I == p.I && q.J == p.J && q.W == p.W && q.H > p.H) is { W: > 0 } whole ? whole : p).ToList();
        parts.AddRange(before.Parts.Where(q => !parts.Any(p => p.Overlaps(q))));
        var cols = parts.Count > 0 ? Math.Max(region.Cols, parts.Max(p => p.I + p.W)) : Math.Max(region.Cols, before.Cols);
        var rows = parts.Count > 0 ? Math.Max(region.Rows, parts.Max(p => p.J + p.H)) : Math.Max(region.Rows, before.Rows);
        if (kept.Count == 0 && parts.SequenceEqual(view) && cols == region.Cols && rows == region.Rows)
            return null;
        var cells = region.Cells.Concat(kept).OrderBy(c => c.IsSlot ? 0 : 1).ThenBy(c => c.J).ThenBy(c => c.I).ToList();
        return region with { Cells = cells, Parts = parts, Cols = cols, Rows = rows };
    }

    // Whether two cells are the one place a pick is kept for: a slot by its key, a grid's cell by its top left.
    private static bool SamePlace(DeskCell a, DeskCell b) => a.IsSlot || b.IsSlot ? a.Slot == b.Slot : a.I == b.I && a.J == b.J;

    /// <summary>
    /// Whether a region as read is too blank to replace the page's version of it (owner's test of 2026-10-19: the Keys
    /// window after the game's SORT read as one "?" and took the place of its 73 keys; docs/DESIGN.md §4, *What it
    /// mirrors*): a window in which no cell was read (no item, no "one of N"), or a region that comes out with fewer than a
    /// third of the cells read that the page has for it while more of its grid held something unread than was read (a
    /// failed reading: the lines between the items not found). A region the player emptied reads its empty cells as
    /// empty, so it still replaces the page's: an unloaded backpack, a stash page paged to an emptier one.
    /// </summary>
    /// <param name="before">The page's version, kept cells and all.</param>
    /// <param name="reading">The region as this reading showed it.</param>
    /// <param name="merged">The reading with the cells a window covered kept from the page's.</param>
    public static bool Blank(DeskRegion before, DeskRegion reading, DeskRegion merged)
    {
        var had = before.ReadCells;
        if (had == 0)
            return false;
        var read = reading.ReadCells;
        if (reading.Kind == DeskRegionKind.ContainerWindow && read == 0)
            return true;
        var readArea = reading.Cells.Where(c => c.Ids.Count > 0).Sum(c => c.IsSlot ? 1 : c.W * c.H);
        return merged.ReadCells * 3 < had && reading.Unread > readArea;
    }

    private sealed record Merged(DeskPage Page, IReadOnlySet<string> Changed, IReadOnlySet<string> Refused, IReadOnlySet<string> Kept, IReadOnlySet<string> Dropped);

    private static Merged MergeAll(DeskPage? old, DeskPage reading)
    {
        var changed = new HashSet<string>(StringComparer.Ordinal);
        var refused = new HashSet<string>(StringComparer.Ordinal);
        var keptFrom = new HashSet<string>(StringComparer.Ordinal);
        var regions = new List<DeskRegion>();
        foreach (var region in reading.Regions)
        {
            // A window of no cell and no title is no reading of anything (owner, 2026-10-25).
            if (Junk(region))
                continue;
            var before = old?.Region(region.Key);
            if (before is null && HeadingAlone(region))
                continue;
            var merged = region;
            if (before is not null && region.Partial && Filled(before, region) is { } filled)
            {
                merged = filled;
                keptFrom.Add(region.Key);
            }
            else if (before is not null && (region.Covered.Count > 0 || region.CoveredSlots.Count > 0))
            {
                var kept = before.Cells.Where(c =>
                    c.IsSlot
                        ? region.CoveredSlots.Contains(c.Slot!) && !region.Cells.Any(n => n.Slot == c.Slot)
                        : region.Covered.Any(r => r.Overlaps(c.Rect)) && !region.Cells.Any(n => !n.IsSlot && n.Rect.Overlaps(c.Rect)));
                merged = region with { Cells = region.Cells.Concat(kept).OrderBy(c => c.IsSlot ? 0 : 1).ThenBy(c => c.J).ThenBy(c => c.I).ToList() };
            }
            merged = merged with { Covered = [], CoveredSlots = [], Unread = 0, Partial = false, ReadAt = reading.ReadAt, Missed = 0 };
            // A reading too blank to stand for the region: the page's version stays, unchanged, shown all the same.
            if (before is not null && Blank(before, region, merged))
            {
                refused.Add(region.Key);
                keptFrom.Remove(region.Key);
                regions.Add(before with { Missed = 0 });
                continue;
            }
            // A "?" cell keeps its pick while the item picked is still among its candidates (owner, 2026-10-25: "until its
            // label changes"; until then only while it read exactly as it was): a slot by its key, a grid's cell by its place.
            if (before is not null && before.Cells.Any(c => c.Pick is not null))
            {
                merged = merged with
                {
                    // A cell its picture settled has one id and no pick (owner, 2026-10-25: DeskIcons).
                    Cells = merged.Cells.Select(c => c.Pick is null && c.Ids.Count > 1 && before.Cells.FirstOrDefault(o => o.Pick is { } pick && SamePlace(o, c) && c.Ids.Contains(pick)) is { } picked
                        ? c with { Pick = picked.Pick } : c).ToList(),
                };
            }
            if (before is null || !Same(before, merged))
                changed.Add(region.Key);
            regions.Add(merged);
        }
        // The regions the reading didn't show stay as they were, one more reading unseen.
        foreach (var region in old?.Regions ?? [])
        {
            if (regions.All(r => r.Key != region.Key))
                regions.Add(region with { Missed = region.Missed + 1 });
        }
        var page = Attach(new DeskPage(reading.ReadAt, regions));
        // A window attached to nothing that three readings in a row didn't show leaves the page (owner, 2026-10-25): what the
        // reading made of a misread heading or a region's grid, which nothing would ever take away.
        var dropped = page.Regions.Where(Gone).Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        if (dropped.Count > 0)
            page = page with { Regions = page.Regions.Where(r => !dropped.Contains(r.Key)).ToList() };
        // A window kept from before whose container this reading brought (or took away) changed too.
        foreach (var window in page.Regions.Where(r => r.Kind == DeskRegionKind.ContainerWindow && !refused.Contains(r.Key)))
        {
            if (old?.Region(window.Key) is { } before && (before.AttachedTo != window.AttachedTo || before.SameName != window.SameName))
                changed.Add(window.Key);
            else if (changed.Contains(window.Key) && old?.Region(window.Key) is { } same && Same(same, window))
                changed.Remove(window.Key);
        }
        return new Merged(page, changed, refused, keptFrom, dropped);
    }

    /// <summary>
    /// Each window attached to its container: the one cell of the page (outside the window itself) that holds contents and
    /// may be an item the window's title names. Several such cells: attached to none, with how many and where they are
    /// ("Not attached: 3 wallets on the page"); none: attached to none. "Zhuk" names two rigs of one size, so its cell is a
    /// "?" whose candidates are both: the window attaches to it either way (docs/DESIGN.md §4, *The cell*).
    /// </summary>
    public static DeskPage Attach(DeskPage page)
    {
        var regions = page.Regions.ToList();
        for (var w = 0; w < regions.Count; w++)
        {
            var window = regions[w];
            if (window.Kind != DeskRegionKind.ContainerWindow || window.TitleIds.Count == 0)
                continue;
            var holders = regions.Where(r => r.Key != window.Key)
                .SelectMany(r => r.Cells.Where(c => c.Holds == DeskHolds.Contents && c.Ids.Any(window.TitleIds.Contains)
                                                    && !(c.IsSlot && DeskRegion.IsContainer(r.Kind)))
                    .Select(c => (Region: r, Cell: c)))
                .ToList();
            if (holders.Count == 1)
            {
                // The window's grid says its cell is a container of the window's name: a "?" cell narrows to those.
                var (holder, cell) = holders[0];
                var narrowed = cell.Ids.Where(window.TitleIds.Contains).ToList();
                if (narrowed.Count < cell.Ids.Count)
                {
                    var at = regions.IndexOf(holder);
                    regions[at] = holder with { Cells = holder.Cells.Select(c => ReferenceEquals(c, cell) ? c with { Ids = narrowed } : c).ToList() };
                }
                regions[w] = window with { AttachedTo = new DeskAnchor(holder.Key, cell.I, cell.J, cell.Slot), SameName = 0, SameNameIn = [] };
            }
            else
                regions[w] = window with { AttachedTo = null, SameName = holders.Count, SameNameIn = holders.Select(h => h.Region.Key).Distinct().ToList() };
        }
        return page with { Regions = regions };
    }

    private static bool Same(DeskRegion a, DeskRegion b) =>
        a.Kind == b.Kind && a.Title == b.Title && a.AttachedTo == b.AttachedTo && a.SameName == b.SameName && a.Cells.Count == b.Cells.Count &&
        a.Cells.All(c => b.Cells.Any(c.SameAs));

    // ---- the file ----

    /// <summary>The pages in a file, or none when there is none; a file that can't be read is set aside as
    /// <c>desk.json.unreadable</c> and the pages start over (the next screenshot brings them back).</summary>
    public static DeskLedger Load(string path, Action<string>? warn = null)
    {
        var ledger = new DeskLedger();
        if (!File.Exists(path))
            return ledger;
        try
        {
            var model = JsonSerializer.Deserialize(File.ReadAllText(path), DeskJson.Default.DeskFile);
            foreach (var (name, page) in model?.Modes ?? [])
            {
                if (ModeOf(name) is not { } mode || page is null)
                    continue;
                // A window of no cell and no title, or one attached to nothing that three readings didn't show, never stays
                // (owner, 2026-10-25). A file written before then says neither when a region was last shown nor how many
                // readings missed it: its windows attached to nothing are taken as long unseen (the owner's file of
                // 2026-10-19 carried four such, made of misread headings and the POUCH's grid).
                var regions = (page.Regions ?? []).Select(r => FromFile(r, page.ReadAt)).OfType<DeskRegion>().Where(r => !Junk(r) && !Gone(r)).ToList();
                ledger._pages[mode] = new DeskPage(page.ReadAt, regions);
            }
            return ledger;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or FormatException)
        {
            warn?.Invoke($"The desk file couldn't be read ({e.GetType().Name}); starting over");
            try
            {
                File.Move(path, path + ".unreadable", overwrite: true);
            }
            catch (Exception moving) when (moving is IOException or UnauthorizedAccessException)
            {
            }
            return new DeskLedger();
        }
    }

    /// <summary>Writes the pages: a temporary file, then one move, so a crash leaves the old file whole.</summary>
    public void Save(string path)
    {
        var model = new DeskFile(Version, _pages.OrderBy(p => p.Key).ToDictionary(p => NameOf(p.Key),
            p => new PageFile(p.Value.ReadAt, p.Value.Regions.Select(r => ToFile(r, p.Value.ReadAt)).ToList()), StringComparer.Ordinal));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(model, DeskJson.Default.DeskFile));
        File.Move(temp, path, overwrite: true);
        Dirty = false;
    }

    // "pve", "pvp": the mode's name in lower case, as the notes and operational files write it.
    private static string NameOf(GameMode mode) => mode.ToString().ToLowerInvariant();

    private static GameMode? ModeOf(string? name) =>
        Enum.GetValues<GameMode>().Where(m => NameOf(m) == name).Select(m => (GameMode?)m).FirstOrDefault();

    private static RegionFile ToFile(DeskRegion r, DateTimeOffset pageAt) => new(r.Key, KindName(r.Kind), r.Cols, r.Rows,
        r.Cells.Select(c => new CellFile(c.I, c.J, c.W, c.H, c.Ids.ToList())
        {
            Slot = c.Slot,
            Count = c.Count > 1 ? c.Count : null,
            Print = c.Print?.ToString(),
            Fir = c.FoundInRaid,
            Holds = c.Holds switch { DeskHolds.Contents => "contents", DeskHolds.Mods => "mods", _ => null },
            Pick = c.Pick,
        }).ToList())
    {
        Title = r.Title,
        TitleIds = r.TitleIds.Count > 0 ? r.TitleIds.ToList() : null,
        AttachedTo = r.AttachedTo is { } a ? new AnchorFile(a.Region, a.I, a.J, a.Slot) : null,
        SameName = r.SameName > 0 ? r.SameName : null,
        SameNameIn = r.SameNameIn.Count > 0 ? r.SameNameIn.ToList() : null,
        Parts = r.Parts.Count > 0 ? r.Parts.Select(p => new[] { p.I, p.J, p.W, p.H }).ToList() : null,
        ReadAt = r.ReadAt == DateTimeOffset.MinValue ? pageAt : r.ReadAt,
        Missed = r.Missed > 0 ? r.Missed : null,
    };

    private static DeskRegion? FromFile(RegionFile? r, DateTimeOffset pageAt)
    {
        if (r is null || KindOf(r.Kind) is not { } kind || r.Key.Length == 0)
            return null;
        return new DeskRegion(r.Key, kind, Math.Max(0, r.Cols), Math.Max(0, r.Rows), Once((r.Cells ?? []).Where(c => c is not null).Select(c => new DeskCell(c.I, c.J,
            Math.Max(1, c.W), Math.Max(1, c.H), (c.Ids ?? []).Where(id => id.Length > 0).ToList())
        {
            Slot = c.Slot,
            Count = Math.Max(1, c.Count ?? 1),
            Print = DeskPrint.Parse(c.Print),
            FoundInRaid = c.Fir,
            Holds = c.Holds switch { "contents" => DeskHolds.Contents, "mods" => DeskHolds.Mods, _ => DeskHolds.Nothing },
            Pick = c.Pick,
        })))
        {
            Title = r.Title,
            TitleIds = (r.TitleIds ?? []).Where(id => id.Length > 0).ToList(),
            AttachedTo = r.AttachedTo is { } a ? new DeskAnchor(a.Region, a.I, a.J, a.Slot) : null,
            SameName = r.SameName ?? 0,
            SameNameIn = r.SameNameIn ?? [],
            Parts = (r.Parts ?? []).Where(p => p is { Length: 4 }).Select(p => new DeskRect(p[0], p[1], p[2], p[3])).ToList(),
            ReadAt = r.ReadAt ?? pageAt,
            // Written before 2026-10-25 (no time of its own): a window attached to nothing is taken as long unseen.
            Missed = r.Missed ?? (r.ReadAt is null && kind == DeskRegionKind.ContainerWindow && r.AttachedTo is null && (r.SameName ?? 0) == 0 ? MissedToDrop : 0),
        };
    }

    // A region's cells with each place taken once: a grid's cell lying over one before it is the same item read twice (a
    // file written before 2026-10-19's fix kept a window's compartment found twice, its cells counted twice), and so is a
    // second cell of one slot.
    private static List<DeskCell> Once(IEnumerable<DeskCell> cells)
    {
        var kept = new List<DeskCell>();
        foreach (var cell in cells)
        {
            if (!kept.Any(k => cell.IsSlot ? k.Slot == cell.Slot : !k.IsSlot && k.Rect.Overlaps(cell.Rect)))
                kept.Add(cell);
        }
        return kept;
    }

    private static string KindName(DeskRegionKind kind) => kind switch
    {
        DeskRegionKind.ContainerWindow => "window",
        _ => DeskRegion.KeyOf(kind),
    };

    private static DeskRegionKind? KindOf(string? name) => name switch
    {
        "loadout" => DeskRegionKind.Loadout,
        "rig" => DeskRegionKind.Rig,
        "pockets" => DeskRegionKind.Pockets,
        "special" => DeskRegionKind.SpecialSlots,
        "backpack" => DeskRegionKind.Backpack,
        "pouch" => DeskRegionKind.Pouch,
        "stash" => DeskRegionKind.Stash,
        "window" => DeskRegionKind.ContainerWindow,
        _ => null,
    };
}

// The file's shape (docs/DESIGN.md §8, "The desk file"): per mode the page's time and regions; per region its kind, size,
// compartments, a window's title and attachment; per cell its place, the ids it may be, count, printed condition,
// found-in-raid mark, what it holds unseen and a pick. Item ids and the containers' names only: no pixel, no other word.
internal sealed record DeskFile(int Version, Dictionary<string, PageFile>? Modes);

internal sealed record PageFile(DateTimeOffset ReadAt, List<RegionFile>? Regions);

internal sealed record RegionFile(string Key, string Kind, int Cols, int Rows, List<CellFile>? Cells)
{
    public string? Title { get; init; }
    public List<string>? TitleIds { get; init; }
    public AnchorFile? AttachedTo { get; init; }
    public int? SameName { get; init; }
    public List<string>? SameNameIn { get; init; }
    public List<int[]>? Parts { get; init; }
    public DateTimeOffset? ReadAt { get; init; }
    public int? Missed { get; init; }
}

internal sealed record AnchorFile(string Region, int I, int J, string? Slot);

internal sealed record CellFile(int I, int J, int W, int H, List<string>? Ids)
{
    public string? Slot { get; init; }
    public int? Count { get; init; }
    public string? Print { get; init; }
    public bool? Fir { get; init; }
    public string? Holds { get; init; }
    public string? Pick { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(DeskFile))]
internal sealed partial class DeskJson : JsonSerializerContext;
