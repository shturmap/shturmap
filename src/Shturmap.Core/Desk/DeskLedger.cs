using System.Text.Json;
using System.Text.Json.Serialization;
using Shturmap.Core.Logs;

namespace Shturmap.Core.Desk;

/// <summary>What a reading did to the page in hand: the page now, the regions it changed (the fold opens these and folds
/// the rest; docs/DESIGN.md §4, *The head line and the fold*), and whether it was the mode's first.</summary>
public sealed record DeskChange(DeskPage Page, IReadOnlySet<string> Changed, bool First);

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
        var (page, changed) = Merge(old, reading);
        _pages[mode] = page;
        Dirty = true;
        return new DeskChange(page, changed, old is null);
    }

    /// <summary>A "?" cell's pick (step C's click): kept with the cell until a reading changes it. False when no such cell.</summary>
    public bool Pick(GameMode mode, string region, DeskRect at, string? itemId)
    {
        if (Page(mode) is not { } page || page.Region(region) is not { } r)
            return false;
        var cell = r.Cells.FirstOrDefault(c => !c.IsSlot && c.Rect == at);
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
        var changed = new HashSet<string>(StringComparer.Ordinal);
        var regions = new List<DeskRegion>();
        foreach (var region in reading.Regions)
        {
            var before = old?.Region(region.Key);
            var merged = region;
            if (before is not null && (region.Covered.Count > 0 || region.CoveredSlots.Count > 0))
            {
                var kept = before.Cells.Where(c =>
                    c.IsSlot
                        ? region.CoveredSlots.Contains(c.Slot!) && !region.Cells.Any(n => n.Slot == c.Slot)
                        : region.Covered.Any(r => r.Overlaps(c.Rect)) && !region.Cells.Any(n => !n.IsSlot && n.Rect.Overlaps(c.Rect)));
                merged = region with { Cells = region.Cells.Concat(kept).OrderBy(c => c.IsSlot ? 0 : 1).ThenBy(c => c.J).ThenBy(c => c.I).ToList() };
            }
            merged = merged with { Covered = [], CoveredSlots = [] };
            if (before is null || !Same(before, merged))
                changed.Add(region.Key);
            regions.Add(merged);
        }
        foreach (var region in old?.Regions ?? [])
        {
            if (regions.All(r => r.Key != region.Key))
                regions.Add(region);
        }
        var page = Attach(new DeskPage(reading.ReadAt, regions));
        // A window kept from before whose container this reading brought (or took away) changed too.
        foreach (var window in page.Regions.Where(r => r.Kind == DeskRegionKind.ContainerWindow))
        {
            if (old?.Region(window.Key) is { } before && (before.AttachedTo != window.AttachedTo || before.SameName != window.SameName))
                changed.Add(window.Key);
            else if (changed.Contains(window.Key) && old?.Region(window.Key) is { } same && Same(same, window))
                changed.Remove(window.Key);
        }
        return (page, changed);
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
                                                    && !(c.IsSlot && r.Kind is DeskRegionKind.Rig or DeskRegionKind.Backpack or DeskRegionKind.Pouch))
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
                ledger._pages[mode] = new DeskPage(page.ReadAt, (page.Regions ?? []).Select(FromFile).OfType<DeskRegion>().ToList());
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
            p => new PageFile(p.Value.ReadAt, p.Value.Regions.Select(ToFile).ToList()), StringComparer.Ordinal));
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

    private static RegionFile ToFile(DeskRegion r) => new(r.Key, KindName(r.Kind), r.Cols, r.Rows,
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
    };

    private static DeskRegion? FromFile(RegionFile? r)
    {
        if (r is null || KindOf(r.Kind) is not { } kind || r.Key.Length == 0)
            return null;
        return new DeskRegion(r.Key, kind, Math.Max(0, r.Cols), Math.Max(0, r.Rows), (r.Cells ?? []).Where(c => c is not null).Select(c => new DeskCell(c.I, c.J,
            Math.Max(1, c.W), Math.Max(1, c.H), (c.Ids ?? []).Where(id => id.Length > 0).ToList())
        {
            Slot = c.Slot,
            Count = Math.Max(1, c.Count ?? 1),
            Print = DeskPrint.Parse(c.Print),
            FoundInRaid = c.Fir,
            Holds = c.Holds switch { "contents" => DeskHolds.Contents, "mods" => DeskHolds.Mods, _ => DeskHolds.Nothing },
            Pick = c.Pick,
        }).ToList())
        {
            Title = r.Title,
            TitleIds = (r.TitleIds ?? []).Where(id => id.Length > 0).ToList(),
            AttachedTo = r.AttachedTo is { } a ? new DeskAnchor(a.Region, a.I, a.J, a.Slot) : null,
            SameName = r.SameName ?? 0,
            SameNameIn = r.SameNameIn ?? [],
            Parts = (r.Parts ?? []).Where(p => p is { Length: 4 }).Select(p => new DeskRect(p[0], p[1], p[2], p[3])).ToList(),
        };
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
