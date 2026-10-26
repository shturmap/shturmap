using Shturmap.Core.Desk;
using Shturmap.Core.Quests;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Screenshots;

namespace Shturmap.Session;

/// <summary>
/// The stash desk's view of tarkov.dev's data (docs/DESIGN.md §2, "For the stash desk"; §3): the catalogue its reading
/// names cells from, each item's price facts, the flea's rules, and the keep levels from the quest tree and the hideout.
/// What each assumes of the data is listed in docs/UPDATES.md.
/// </summary>
public static class DeskData
{
    private const string MagazineCategory = "5448bc234bdc2d3c308b4569";

    // The game's categories its slots take (tarkov.dev's item categories, each item listing its whole chain; read
    // 2026-10-19): headphones, headwear, face cover, armband, armour, eyewear (glasses and visors), the dog tags' "Other",
    // weapons, knives, chest rigs, backpacks and secure containers.
    private static readonly (string Category, DeskSlotKind Kind)[] SlotCategories =
    [
        ("5645bcb74bdc2ded0b8b4578", DeskSlotKind.Earpiece),
        ("5a341c4086f77401f2541505", DeskSlotKind.Headwear),
        ("5a341c4686f77469e155819e", DeskSlotKind.FaceCover),
        ("5b3f15d486f77432d0509248", DeskSlotKind.Armband),
        ("5448e54d4bdc2dcc718b4568", DeskSlotKind.BodyArmor),
        ("5448e5724bdc2ddf718b4568", DeskSlotKind.Eyewear),
        ("590c745b86f7743cc433c5f2", DeskSlotKind.Dogtag),
        ("5422acb9af1c889c16000029", DeskSlotKind.Weapon),
        ("5447e1d04bdc2dff2f8b4567", DeskSlotKind.Knife),
        ("5448e5284bdc2dcb718b4567", DeskSlotKind.Rig),
        ("5448e53e4bdc2d60728b4567", DeskSlotKind.Backpack),
        ("5448bf274bdc2dfc2f8b456a", DeskSlotKind.SecureContainer),
    ];

    /// <summary>
    /// Every item a cell can be: its short name (and full name, for a window's title), its footprint and what it holds.
    /// Presets are left out: a preset is its gun with mods, and a cell labelled with a preset's name ("USP .45 Expert") is
    /// named the gun (owner, 2026-10-26; <c>DeskMatch.Preset</c>). Without the item file, footprints are unknown (one by
    /// one) and nothing holds anything.
    /// </summary>
    public static DeskCatalogue Catalogue(GameData data, ItemSources? sources) =>
        new(Candidates(data, sources));

    public static IEnumerable<DeskCandidate> Candidates(GameData data, ItemSources? sources)
    {
        foreach (var (id, shortName) in data.ItemShortNames)
        {
            if (shortName.Length == 0)
                continue;
            var item = sources?.Items.GetValueOrDefault(id);
            if (item?.Types?.Contains("preset") == true)
                continue;
            yield return new DeskCandidate(id, shortName, data.ItemNames.GetValueOrDefault(id) ?? shortName,
                Math.Max(1, item?.Width ?? 1), Math.Max(1, item?.Height ?? 1), item is null ? DeskHolds.Nothing : HoldsOf(item),
                item is null ? DeskSlotKind.Any : SlotsOf(item));
        }
    }

    /// <summary>The slots an item goes in, by its categories (a gun: on the sling, in the holster, on the back), and a
    /// special slot by its type <c>specialSlot</c>; none for an item no slot takes.</summary>
    public static DeskSlotKind SlotsOf(ApiItem item)
    {
        var kind = DeskSlotKind.None;
        foreach (var (category, slot) in SlotCategories)
        {
            if (item.Categories?.Contains(category) == true)
                kind |= slot;
        }
        if (item.Types?.Contains("gun") == true)
            kind |= DeskSlotKind.Weapon;
        if (item.Types?.Contains("specialSlot") == true)
            kind |= DeskSlotKind.Special;
        return kind;
    }

    /// <summary>What an item holds out of sight: a grid of its own (tarkov.dev's <c>hasGrid</c>, or a backpack, rig or
    /// container type) or, for a weapon, its mods.</summary>
    public static DeskHolds HoldsOf(ApiItem item) =>
        item.Types?.Contains("gun") == true ? DeskHolds.Mods
        : item.HasGrid == true || item.Types?.Any(t => t is "backpack" or "container") == true ? DeskHolds.Contents
        : DeskHolds.Nothing;

    /// <summary>The kind of an item, as its figure goes.</summary>
    public static DeskItemKind KindOf(ApiItem item)
    {
        var types = item.Types ?? [];
        var kind = DeskItemKind.None;
        if (HoldsOf(item) == DeskHolds.Contents)
            kind |= DeskItemKind.Container;
        if (types.Contains("gun"))
            kind |= DeskItemKind.Gun;
        if (item.Categories?.Contains(MagazineCategory) == true)
            kind |= DeskItemKind.Magazine;
        if (types.Contains("meds") || types.Contains("injectors"))
            kind |= DeskItemKind.Meds;
        if (types.Contains("provisions"))
            kind |= DeskItemKind.FoodDrink;
        if (types.Any(t => t is "armor" or "helmet" or "armorPlate"))
            kind |= DeskItemKind.Armour;
        return kind;
    }

    /// <summary>An item's price facts from the item file, or null when the file doesn't have it.</summary>
    public static DeskItemFacts? Facts(ItemSources? sources, string itemId)
    {
        if (sources?.Items.GetValueOrDefault(itemId) is not { } item)
            return null;
        return new DeskItemFacts(itemId)
        {
            W = Math.Max(1, item.Width ?? 1),
            H = Math.Max(1, item.Height ?? 1),
            Kind = KindOf(item),
            StackMax = item.StackMaxSize > 0 ? item.StackMaxSize : null,
            NoFlea = item.Types?.Contains("noFlea") == true,
            FleaLevel = item.MinLevelForFlea,
            LastLow = item.LastLowPrice,
            Avg24h = item.Avg24hPrice,
            Low24h = item.Low24hPrice,
            High24h = item.High24hPrice,
            Change48hPercent = item.ChangeLast48hPercent,
            Offers = item.LastOfferCount,
            BasePrice = item.BasePrice ?? 0,
            Traders = (item.SellToTrader ?? []).Where(t => (t.PriceRUB ?? 0) > 0).Select(t => (t.Trader, t.PriceRUB!.Value)).ToList(),
            TraderSells = item.BuyFromTrader is { Count: > 0 },
            Updated = DateTimeOffset.TryParse(item.Updated, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var at) ? at : null,
        };
    }

    /// <summary>The flea's rules from the item file, else the rates seen on 2026-10-19.</summary>
    public static DeskFleaRules Flea(ItemSources? sources) =>
        sources?.FleaMarket is { } f
            ? new DeskFleaRules(f.SellOfferFeeRate ?? DeskFleaRules.Default.OfferFeeRate, f.SellRequirementFeeRate ?? DeskFleaRules.Default.RequirementFeeRate,
                f.FoundInRaidRequired == true, f.MinPlayerLevel ?? DeskFleaRules.Default.MinPlayerLevel, f.Enabled != false)
            : DeskFleaRules.Default;

    /// <summary>
    /// The keep levels (docs/DESIGN.md §2, "For the stash desk"): each quest's item needs from its objectives (what it
    /// takes at a hand-over or a find, a plant, a marker, items to use, keys, gear to wear and mods to fit, as BRING lists
    /// them; a find whose hand-over folds into it once, Handovers), the quest tree from <c>taskRequirements</c>, the
    /// quests' states from the logs, and every hideout level's needs.
    /// </summary>
    public static DeskKeep Keep(GameData data, IReadOnlyDictionary<string, QuestStatus> quests, ItemSources? sources)
    {
        var active = quests.Values.Where(q => q.State == QuestState.Active).Select(q => q.QuestId).ToHashSet(StringComparer.Ordinal);
        var done = quests.Values.Where(q => q.State is QuestState.Completed or QuestState.Failed).Select(q => q.QuestId).ToHashSet(StringComparer.Ordinal);
        var needs = data.Tasks.Values.Select(QuestNeeds).ToList();
        var hideout = (sources?.HideoutNeeds ?? []).Select(n => new DeskHideoutNeed(n.StationId, n.Level, n.ItemId, n.Count, n.FoundInRaid));
        return DeskKeep.Of(needs, active, done, hideout);
    }

    /// <summary>What one quest needs of items, and the quests before it.</summary>
    public static DeskQuestNeeds QuestNeeds(ApiTask task)
    {
        var items = new List<(string ItemId, int Count, bool FoundInRaid)>();
        void Add(IEnumerable<string>? ids, int count, bool fir)
        {
            foreach (var id in ids ?? [])
            {
                if (!string.IsNullOrEmpty(id))
                    items.Add((id, Math.Max(1, count), fir));
            }
        }
        foreach (var o in task.Objectives ?? [])
        {
            var count = Math.Max(1, o.Count ?? 1);
            switch (o.Type)
            {
                case "giveItem":
                    Add(o.Items, count, o.FoundInRaid);
                    break;
                // A find that a hand-over of the same items finishes is that hand-over's need, counted once.
                case "findItem" when Handovers.HandoverOf(task, o.Id) is null:
                    Add(o.Items, count, o.FoundInRaid);
                    break;
                case "plantItem":
                    Add(o.Items, count, o.FoundInRaid);
                    break;
                case "mark":
                    Add(o.MarkerItem is { } marker ? [marker] : [], 1, false);
                    break;
                case "useItem":
                    Add(o.UseAny, count, false);
                    break;
            }
            foreach (var keys in o.RequiredKeys ?? [])
                Add(keys, 1, false);
            foreach (var set in o.Wearing ?? [])
                Add(set.Select(i => i.Id), 1, false);
            foreach (var set in o.UsingWeaponMods ?? [])
                Add(set, 1, false);
        }
        foreach (var keys in task.NeededKeys ?? [])
            Add(keys.Keys, 1, false);
        // One line per item: the most a quest's objectives want of it, found in raid when any wants it so.
        var merged = items.GroupBy(i => i.ItemId, StringComparer.Ordinal)
            .Select(g => (g.Key, g.Max(i => i.Count), g.Any(i => i.FoundInRaid))).ToList();
        return new DeskQuestNeeds(task.Id, (task.TaskRequirements ?? []).Select(r => r.Task).Where(t => t.Length > 0).ToList(), merged);
    }
}
