namespace Shturmap.Core.Desk;

// The stash desk's page in hand (owner, 2026-10-19, from a panel: A1 · B1 · C1 · D2, and the addendum: A(2) · B1 · C1a ·
// Da; docs/DESIGN.md §2, "For the stash desk", and §4, "Screen anatomy", *The stash desk*): every grid a screenshot of the
// player's own stash or GEAR tab showed, each under its own head, mirrored cell for cell. No pixel and no word beyond the
// items' ids is kept: what a cell is (one item, or "one of N" when its label fits several alike), where it stands, its
// count, its printed condition and its found-in-raid mark.

/// <summary>Which of the screen's grids a region is (docs/DESIGN.md §4, *What it mirrors*): the GEAR tab's loadout slots
/// (LOADOUT), the middle column's parts in the game's order (TACTICAL RIG, POCKETS, SPECIAL SLOTS, BACKPACK, POUCH), the
/// stash (STASH), or a container's floating window ("ZHUK · IN THE BACKPACK"). The order is the reading order.</summary>
public enum DeskRegionKind
{
    Loadout,
    Rig,
    Pockets,
    SpecialSlots,
    Backpack,
    Pouch,
    Stash,
    ContainerWindow,

    /// <summary>In a raid's inventory screen, the LOOT pane of a body: the dead player's slots, a strip as the loadout
    /// (the in-raid glance, docs/DESIGN.md §2 and §4, *The in-raid glance*; never on the desk).</summary>
    LootSlots,

    /// <summary>A body's TACTICAL RIG in the LOOT pane: its own slot and, once searched, its grid.</summary>
    LootRig,

    /// <summary>A body's POCKETS in the LOOT pane.</summary>
    LootPockets,

    /// <summary>A body's BACKPACK in the LOOT pane: its own slot and, once searched, its grid.</summary>
    LootBackpack,

    /// <summary>The LOOT pane's grid of a container (a drawer, a safe, a jacket).</summary>
    Loot,
}

/// <summary>How much of a looted part's contents the screenshot showed: all of it, part of it while the game searches
/// ("SEARCHING ✕ 0:00"), or none ("UNSEARCHED", with its SEARCH link). Contents not shown are unseen ("+"), never zero.</summary>
public enum DeskSearch
{
    Searched,
    Searching,
    Unsearched,
}

/// <summary>What a cell's item holds out of sight: a container's contents (until its window is read), a weapon's mods.</summary>
public enum DeskHolds
{
    Nothing,
    Contents,
    Mods,
}

/// <summary>A "current/max" the game prints where a stack's count stands: a condition ("46/50", "1469/1800"), or for a
/// magazine or a weapon its rounds (which the values don't take for a condition).</summary>
public readonly record struct DeskPrint(int Current, int Max)
{
    public bool Full => Current >= Max;

    /// <summary>The printed share, 0 to 1.</summary>
    public double Share => Max <= 0 ? 1 : Math.Clamp((double)Current / Max, 0, 1);

    public override string ToString() => $"{Current}/{Max}";

    /// <summary>"46/50" as the game prints it, or null when the text isn't one (digits, a slash, digits; the current not
    /// above the maximum).</summary>
    public static DeskPrint? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var slash = text.IndexOf('/');
        if (slash <= 0 || slash == text.Length - 1)
            return null;
        // The recognition reads the game's slashed 0 as an Ø or an O now and then, a 5 as an S or an s, a 1 as an l: folded
        // before the digits are taken.
        static string Digits(string part) => new(part.Replace('O', '0').Replace('o', '0').Replace('Ø', '0').Replace('ø', '0')
            .Replace('S', '5').Replace('s', '5').Replace('l', '1').Replace('I', '1').Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
        var current = Digits(text[..slash]);
        var max = Digits(text[(slash + 1)..]);
        if (current.Length is 0 or > 6 || max.Length is 0 or > 6 || !current.All(char.IsAsciiDigit) || !max.All(char.IsAsciiDigit))
            return null;
        int c = int.Parse(current), m = int.Parse(max);
        return m > 0 && c <= m ? new DeskPrint(c, m) : null;
    }
}

/// <summary>A rectangle of cells in a region, in grid units: its top left cell and its size.</summary>
public readonly record struct DeskRect(int I, int J, int W, int H)
{
    public bool Contains(DeskRect other) => other.I >= I && other.J >= J && other.I + other.W <= I + W && other.J + other.H <= J + H;

    public bool Overlaps(DeskRect other) => other.I < I + W && I < other.I + other.W && other.J < J + H && J < other.J + other.H;
}

/// <summary>
/// One cell of a region: an item rectangle of a grid (its top left and footprint in the region's grid units) or a slot
/// (<see cref="Slot"/>: the loadout's slot, or a middle region's own container; one cell a slot, no footprint).
/// </summary>
/// <param name="Ids">What it is: one id when named; several when its label fits several items alike ("one of N", the
/// candidates the footprint left); none when nothing was said (the label unread, or contradicted by the footprint).</param>
public sealed record DeskCell(int I, int J, int W, int H, IReadOnlyList<string> Ids)
{
    /// <summary>A slot's key (<see cref="DeskSlots"/>, or the middle region's own "rig", "backpack", "pouch"), or null for
    /// a grid's cell.</summary>
    public string? Slot { get; init; }

    /// <summary>The stack's count as read (one when none is printed or readable: at least that many).</summary>
    public int Count { get; init; } = 1;

    /// <summary>The "current/max" printed on it, or null.</summary>
    public DeskPrint? Print { get; init; }

    /// <summary>Whether the found-in-raid mark was seen; null when it wasn't looked for (slots, unread cells).</summary>
    public bool? FoundInRaid { get; init; }

    /// <summary>What its item holds out of sight, from the catalogue: a container's contents, a weapon's mods.</summary>
    public DeskHolds Holds { get; init; }

    /// <summary>The player's pick among <see cref="Ids"/> for a "?" cell (step C's click), or null.</summary>
    public string? Pick { get; init; }

    public bool IsSlot => Slot is not null;

    public DeskRect Rect => new(I, J, W, H);

    /// <summary>The item when it is one: named, or picked among the candidates.</summary>
    public string? ItemId => Ids.Count == 1 ? Ids[0] : Pick is { } pick && Ids.Contains(pick) ? pick : null;

    /// <summary>The same reading of the same place (the fold's "unchanged"): place, ids or candidates, count and printed
    /// condition (docs/DESIGN.md §4, *The head line and the fold*).</summary>
    public bool SameAs(DeskCell other) =>
        I == other.I && J == other.J && W == other.W && H == other.H && Slot == other.Slot && Count == other.Count && Print == other.Print &&
        Ids.Order(StringComparer.Ordinal).SequenceEqual(other.Ids.Order(StringComparer.Ordinal), StringComparer.Ordinal);
}

/// <summary>Where a container window attaches: the region and the top left of the container's cell, or its slot.</summary>
public sealed record DeskAnchor(string Region, int I, int J, string? Slot = null)
{
    public bool Is(DeskRegion region, DeskCell cell) =>
        Region == region.Key && (Slot is not null ? Slot == cell.Slot : !cell.IsSlot && I == cell.I && J == cell.J);
}

/// <summary>
/// One grid of the screen under its own head. A grid's cells are in its grid units (<see cref="Cols"/> × <see cref="Rows"/>;
/// compartments that stand apart on screen stand side by side here, <see cref="Parts"/>); a region's slots are cells
/// with <see cref="DeskCell.Slot"/>.
/// </summary>
/// <param name="Key">The region's identity on the page: its kind's word ("loadout", "stash"), a window's "window:" and its
/// folded title (and "#2" for a second of one name).</param>
public sealed record DeskRegion(string Key, DeskRegionKind Kind, int Cols, int Rows, IReadOnlyList<DeskCell> Cells)
{
    /// <summary>A window's title as the game prints it (the container's short name).</summary>
    public string? Title { get; init; }

    /// <summary>The items a window's title may name (its short name fits theirs as a label does), for its attachment.</summary>
    public IReadOnlyList<string> TitleIds { get; init; } = [];

    /// <summary>A window's container, when exactly one cell of the page is a container of its name; else null.</summary>
    public DeskAnchor? AttachedTo { get; init; }

    /// <summary>For a window not attached: how many containers of its name the page holds ("Not attached: 3 wallets on
    /// the page"); 0 when there is none, or the window is attached.</summary>
    public int SameName { get; init; }

    /// <summary>For a window not attached: the regions those containers of its name are in (where it counts: all on the
    /// player, all in the stash, or split, then in neither).</summary>
    public IReadOnlyList<string> SameNameIn { get; init; } = [];

    /// <summary>The grid's compartments in its units (a rig's pockets, the pockets, the special slots), for the mirror.</summary>
    public IReadOnlyList<DeskRect> Parts { get; init; } = [];

    /// <summary>What a window covered of this region in the reading (grid units): its cells keep their last reading.
    /// Only on a reading, never kept.</summary>
    public IReadOnlyList<DeskRect> Covered { get; init; } = [];

    /// <summary>The slots a window covered in the reading: they keep their last reading. Only on a reading.</summary>
    public IReadOnlyList<string> CoveredSlots { get; init; } = [];

    /// <summary>How many of the grid's cells held something the reading couldn't read: shapes no label fit (the lines
    /// between items unseen) and rectangles whose label fit nothing. Only on a reading, never kept: with few cells read it
    /// tells a reading that failed from a region that emptied (<see cref="DeskLedger.Merge"/>).</summary>
    public int Unread { get; init; }

    /// <summary>The cells read as an item or "one of N" (not a cell with nothing said).</summary>
    public int ReadCells => Cells.Count(c => c.Ids.Count > 0);

    /// <summary>For a LOOT region, how much of its contents the screenshot showed (a body's rig, pockets and backpack until
    /// searched); <see cref="DeskSearch.Searched"/> for every other region.</summary>
    public DeskSearch Search { get; init; }

    /// <summary>For a LOOT region, whether the pane's scrolling cut its grid at the pane's foot: what lies past it is unseen.</summary>
    public bool Cut { get; init; }

    /// <summary>Whether contents of the region itself went unseen (a looted part not searched yet, or cut by the pane's
    /// scrolling): its total is at least what was read, "+".</summary>
    public bool ContentsUnseen => Search != DeskSearch.Searched || Cut;

    /// <summary>Whether the region is the LOOT pane's (a body's or a container's, in a raid).</summary>
    public bool IsLoot => Kind is DeskRegionKind.LootSlots or DeskRegionKind.LootRig or DeskRegionKind.LootPockets or DeskRegionKind.LootBackpack
        or DeskRegionKind.Loot;

    /// <summary>Whether the region is on the player (ON YOU): the loadout and the middle column; the stash, the windows
    /// and the LOOT pane are not by themselves (a window counts where its container is, <see cref="DeskSheet"/>).</summary>
    public bool OnYou => Kind is not (DeskRegionKind.Stash or DeskRegionKind.ContainerWindow) && !IsLoot;

    /// <summary>The key of a fixed region.</summary>
    public static string KeyOf(DeskRegionKind kind) => kind switch
    {
        DeskRegionKind.Loadout => "loadout",
        DeskRegionKind.Rig => "rig",
        DeskRegionKind.Pockets => "pockets",
        DeskRegionKind.SpecialSlots => "special",
        DeskRegionKind.Backpack => "backpack",
        DeskRegionKind.Pouch => "pouch",
        DeskRegionKind.Stash => "stash",
        DeskRegionKind.LootSlots => "loot:slots",
        DeskRegionKind.LootRig => "loot:rig",
        DeskRegionKind.LootPockets => "loot:pockets",
        DeskRegionKind.LootBackpack => "loot:backpack",
        DeskRegionKind.Loot => "loot",
        _ => "window",
    };

    /// <summary>A window's key: "window:" and its title folded to lower-case letters and digits.</summary>
    public static string WindowKey(string title, int nth = 0) =>
        "window:" + new string(title.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray()) + (nth > 0 ? $"#{nth + 1}" : "");
}

/// <summary>The page in hand: the regions of the readings so far, each as its last reading showed it.</summary>
/// <param name="ReadAt">When the last screenshot read for it was taken.</param>
public sealed record DeskPage(DateTimeOffset ReadAt, IReadOnlyList<DeskRegion> Regions)
{
    public DeskRegion? Region(string key) => Regions.FirstOrDefault(r => r.Key == key);

    /// <summary>The cell a window attaches to, with its region, or null.</summary>
    public (DeskRegion Region, DeskCell Cell)? At(DeskAnchor anchor) =>
        Region(anchor.Region) is { } region && region.Cells.FirstOrDefault(c => anchor.Is(region, c)) is { } cell ? (region, cell) : null;

    /// <summary>The window attached to a cell, or null.</summary>
    public DeskRegion? WindowOf(DeskRegion region, DeskCell cell) =>
        Regions.FirstOrDefault(w => w.Kind == DeskRegionKind.ContainerWindow && w.AttachedTo is { } a && a.Is(region, cell));

    /// <summary>Whether a cell's contents are unseen (its figure's "+"): a container no window of the page is attached
    /// to, or a weapon (its mods are never seen). A middle region's own container shows its grid inline: never unseen.</summary>
    public bool Unseen(DeskRegion region, DeskCell cell) => cell.Holds switch
    {
        DeskHolds.Mods => true,
        DeskHolds.Contents when cell.IsSlot && region.Kind is DeskRegionKind.Rig or DeskRegionKind.Backpack or DeskRegionKind.Pouch
            or DeskRegionKind.LootRig or DeskRegionKind.LootBackpack => false,
        DeskHolds.Contents => WindowOf(region, cell) is null,
        _ => false,
    };

    /// <summary>The regions in reading order (docs/DESIGN.md §4, *What it mirrors*, A(2)): LOADOUT, TACTICAL RIG, POCKETS,
    /// SPECIAL SLOTS, BACKPACK, POUCH, STASH, each attached window right after the region its container is in (a window
    /// in a window after that one); the windows attached to none at the end.</summary>
    public IReadOnlyList<DeskRegion> InReadingOrder()
    {
        var ordered = new List<DeskRegion>();
        void Add(DeskRegion region)
        {
            if (ordered.Contains(region))
                return;
            ordered.Add(region);
            foreach (var window in Regions.Where(w => w.Kind == DeskRegionKind.ContainerWindow && w.AttachedTo?.Region == region.Key))
                Add(window);
        }
        foreach (var region in Regions.Where(r => r.Kind != DeskRegionKind.ContainerWindow).OrderBy(r => (int)r.Kind))
            Add(region);
        foreach (var window in Regions.Where(r => r.Kind == DeskRegionKind.ContainerWindow))
            Add(window);
        return ordered;
    }
}

/// <summary>The kinds of item the game's slots take (its slot filters): a loadout slot's, a middle region's own container's
/// (a rig, a backpack, a secure container), a special slot's. A slot's label is matched against the items of its kind only
/// (QA pass of 2026-10-19, F4: the AK-12 on the sling was "one of 5" with the AK-12's magazine, stock, grip and muzzle
/// device, its range a magazine's to a gun's). An item may fit several (a gun on the sling or on the back).</summary>
[Flags]
public enum DeskSlotKind
{
    None = 0,
    Earpiece = 1 << 0,
    Headwear = 1 << 1,
    FaceCover = 1 << 2,
    Armband = 1 << 3,
    BodyArmor = 1 << 4,
    Eyewear = 1 << 5,
    Dogtag = 1 << 6,
    Weapon = 1 << 7,
    Knife = 1 << 8,
    Rig = 1 << 9,
    Backpack = 1 << 10,
    SecureContainer = 1 << 11,
    Special = 1 << 12,

    /// <summary>Every kind: an item whose kind isn't known (no item file), and a grid's cell, which takes any item.</summary>
    Any = (1 << 13) - 1,
}

/// <summary>The loadout's slots in the game's order (the GEAR tab's left column, row by row), and the words the English
/// game heads them with.</summary>
public static class DeskSlots
{
    public static readonly IReadOnlyList<(string Key, string Heading)> Loadout =
    [
        ("earpiece", "EARPIECE"), ("headwear", "HEADWEAR"), ("facecover", "FACE COVER"), ("armband", "ARMBAND"),
        ("bodyarmor", "BODY ARMOR"), ("eyewear", "EYEWEAR"), ("dogtag", "DOGTAG"), ("onsling", "ON SLING"),
        ("holster", "HOLSTER"), ("onback", "ON BACK"), ("sheath", "SHEATH"),
    ];

    /// <summary>A loadout slot's place in the game's order, or −1.</summary>
    public static int Order(string key)
    {
        for (var i = 0; i < Loadout.Count; i++)
        {
            if (Loadout[i].Key == key)
                return i;
        }
        return -1;
    }

    /// <summary>The slots that hold a weapon: what they print is its rounds, and its mods are unseen.</summary>
    public static bool IsWeapon(string? key) => key is "onsling" or "holster" or "onback";

    /// <summary>The kind of item a slot takes: a loadout slot's by its key, a middle region's own container's ("rig",
    /// "backpack", "pouch"); <see cref="DeskSlotKind.Any"/> for a key of none of them.</summary>
    public static DeskSlotKind KindOf(string? key) => key switch
    {
        "earpiece" => DeskSlotKind.Earpiece,
        "headwear" => DeskSlotKind.Headwear,
        "facecover" => DeskSlotKind.FaceCover,
        "armband" => DeskSlotKind.Armband,
        "bodyarmor" => DeskSlotKind.BodyArmor,
        "eyewear" => DeskSlotKind.Eyewear,
        "dogtag" => DeskSlotKind.Dogtag,
        "onsling" or "holster" or "onback" => DeskSlotKind.Weapon,
        "sheath" => DeskSlotKind.Knife,
        "rig" => DeskSlotKind.Rig,
        "backpack" => DeskSlotKind.Backpack,
        "pouch" => DeskSlotKind.SecureContainer,
        _ => DeskSlotKind.Any,
    };
}
