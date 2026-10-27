namespace Shturmap.Data.TarkovDev;

/// <summary>Where items can be had: trader offers, barters, hideout crafts, the flea market (json.tarkov.dev).</summary>
public sealed class ItemSources
{
    public required IReadOnlyDictionary<string, ApiItem> Items { get; init; }

    /// <summary>Barters by the item they give.</summary>
    public required ILookup<string, ApiBarter> Barters { get; init; }

    /// <summary>Crafts by the item they make.</summary>
    public required ILookup<string, ApiCraft> Crafts { get; init; }

    /// <summary>Hideout station names by id, in the game's language.</summary>
    public required IReadOnlyDictionary<string, string> Stations { get; init; }

    /// <summary>What every hideout level takes, by station id: the stash desk's hideout need, whatever the player's
    /// hideout has built (its progress isn't in the logs; docs/DESIGN.md §2, "For the stash desk").</summary>
    public IReadOnlyList<HideoutNeed> HideoutNeeds { get; init; } = [];

    /// <summary>The flea market's rules from the item file (the fee's rates, its level, the found-in-raid rule); null
    /// when the file carried none.</summary>
    public ApiFleaMarket? FleaMarket { get; init; }

    /// <summary>When the item file, and with it every price, was last confirmed current with tarkov.dev.</summary>
    public DateTimeOffset PricesAt { get; init; }

    /// <summary>The item file's Last-Modified as tarkov.dev sent it: when the file in hand was made (about 3 minutes after
    /// a scan, measured 2026-10-27); null where none was sent. The stash desk asks for the file again when the next is due
    /// by it (docs/DESIGN.md §3, "The stash desk's prices come per reading").</summary>
    public DateTimeOffset? ModifiedAt { get; init; }

    /// <summary>tarkov.dev wasn't reached for the item file: this is the saved copy (the stash desk's head line says so,
    /// owner, 2026-10-27).</summary>
    public bool Stale { get; init; }

    private DateTimeOffset? _scannedAt;
    private bool _scannedKnown;

    /// <summary>The newest of the items' scans (their <see cref="ApiItem.Updated"/>): how old the prices in the file in hand
    /// are at best, the stash desk's head line's "PRICES 3 MIN OLD" (owner, 2026-10-27); null where no item says.</summary>
    public DateTimeOffset? ScannedAt
    {
        get
        {
            if (!_scannedKnown)
            {
                DateTimeOffset? newest = null;
                foreach (var item in Items.Values)
                {
                    if (item.Updated is { Length: > 0 } text && DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AssumeUniversal, out var at) && (newest is null || at > newest))
                        newest = at;
                }
                (_scannedAt, _scannedKnown) = (newest, true);
            }
            return _scannedAt;
        }
    }

    /// <summary>The same file had again at <paramref name="at"/>: confirmed current (tarkov.dev answered "not modified"), or
    /// the saved copy given back where tarkov.dev wasn't reached (<paramref name="stale"/>); the same items, barters, crafts
    /// and stations.</summary>
    public ItemSources Confirmed(DateTimeOffset at, bool stale = false) => new()
    {
        Items = Items,
        Barters = Barters,
        Crafts = Crafts,
        Stations = Stations,
        HideoutNeeds = HideoutNeeds,
        FleaMarket = FleaMarket,
        PricesAt = at,
        ModifiedAt = ModifiedAt,
        Stale = stale,
    };

    private ILookup<string, string>? _members;

    /// <summary>
    /// Item ids by category id; an item is listed under every category in its chain (<see cref="ApiItem.Categories"/>).
    /// Presets (a weapon in a ready build, tarkov.dev type "preset") are left out: they repeat the weapon they build.
    /// </summary>
    public ILookup<string, string> Members =>
        _members ??= Items.Values.Where(i => i.Types?.Contains("preset") != true)
            .SelectMany(i => (i.Categories ?? []).Select(c => (Category: c, i.Id))).ToLookup(x => x.Category, x => x.Id);

    /// <summary>
    /// The item whose picture shows <paramref name="itemId"/>: a gun's default preset, the gun assembled as the traders
    /// sell it (its own item, <see cref="ApiItemProperties.DefaultPreset"/>), else the item itself. The bare gun's icon
    /// is its receiver alone, without stock, grip or sight (owner, 2026-10-19: from a panel of five, A; docs/DESIGN.md
    /// §3, "Trader portraits and item icons"). Only the picture: links, cards and names stay the item's.
    /// </summary>
    public string PictureId(string itemId) =>
        Items.TryGetValue(itemId, out var item) && item.Properties?.DefaultPreset is { Length: > 0 } preset ? preset : itemId;

    /// <summary><see cref="PictureId"/>, or the item itself while the sources haven't loaded.</summary>
    public static string PictureOf(ItemSources? sources, string itemId) => sources?.PictureId(itemId) ?? itemId;
}

/// <summary>An item a hideout level takes: the station (id and name), the level, how many, and whether found in raid.</summary>
public sealed record HideoutNeed(string StationId, string Station, int Level, string ItemId, int Count, bool FoundInRaid);

