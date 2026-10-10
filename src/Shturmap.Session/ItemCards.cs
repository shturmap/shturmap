using Shturmap.Core;
using Shturmap.Core.Quests;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session;

/// <summary>One way an active quest needs an item: "Key · Customs", "Bring ×3 · Streets of Tarkov", "Find in raid ×2".</summary>
public sealed record ItemUse(string QuestId, string QuestName, ObjectiveKind QuestKind, string? TraderId, string How);

public enum SourceKind
{
    Trader,
    Barter,
    Craft,
    Flea,
    Loose,
}

/// <summary>One way to get an item: "Prapor LL1 · 18,936 ₽", "Mechanic LL2 barter · 2× Bolts", "Loose on Customs · 3 spots".</summary>
/// <param name="Locked">A trader's offer or barter that needs a quest the game's log hasn't seen completed
/// ("Ragman LL2 · 41,283 ₽ · after Dandies"): not a way yet, so it comes after every other.</param>
public sealed record ItemSource(SourceKind Kind, string? TraderId, string Text, bool Locked = false);

/// <summary>The item card: what it is, where to get it, and which of your active quests need it and how.</summary>
/// <param name="Get">Where to get it, the easiest first; empty until the item sources have loaded.</param>
public sealed record ItemCardView(string ItemId, string Name, bool IsKey, IReadOnlyList<ItemSource> Get, IReadOnlyList<ItemUse> Uses)
{
    /// <summary>The active quests that need the item, each once: what the card keeps lit beside the item while it is read.</summary>
    public IReadOnlyList<string> QuestIds => Uses.Select(u => u.QuestId).Distinct(StringComparer.Ordinal).ToList();
}

public static class ItemCards
{
    // How a quest needs the item: one line of the item card each (How words it).
    private enum Use
    {
        Key,
        Wear,
        Weapon,
        Mods,
        Bring,
        Leave,
        Stash,
        PickUp,
        PickUpThenHandOver,
        HandOver,
        HandOverFoundInRaid,
        FindInRaid,
        FindInRaidThenHandOver,
        Find,
        FindThenHandOver,
        Sell,
    }

    // One way a quest needs the item: how ("Bring"), how many in all ("×3"), and what for ("to mark"), on these maps.
    private sealed class Way(Use use, string exit)
    {
        public Use Use => use;

        /// <summary>The exit it is brought to leave through (<see cref="Use.Leave"/>); empty otherwise.</summary>
        public string Exit => exit;

        public int Count { get; set; }

        public List<string> Maps { get; } = [];

        /// <summary>What it is brought to be used up for ("to plant", "to mark").</summary>
        public List<string> Purposes { get; } = [];
    }

    // A way's line, its count from two up: "Bring ×3, to plant and to mark", "Find in raid, then hand over".
    private static string How(Way way)
    {
        var count = Math.Max(1, way.Count);
        return way.Use switch
        {
            Use.Key => SessionTexts.ItemUseKey,
            Use.Wear => SessionTexts.ItemUseWear,
            Use.Weapon => SessionTexts.ItemUseWeapon,
            Use.Mods => SessionTexts.ItemUseMods,
            Use.Bring => SessionTexts.ItemUseBring(count: count, purposes: Planning.And(way.Purposes)),
            Use.Leave => SessionTexts.ItemUseLeave(count: count, exit: way.Exit),
            Use.Stash => SessionTexts.ItemUseStash,
            Use.PickUp => SessionTexts.ItemUsePickUp,
            Use.PickUpThenHandOver => SessionTexts.ItemUsePickUpThenHandOver,
            Use.HandOver => SessionTexts.ItemUseHandOver(count: count),
            Use.HandOverFoundInRaid => SessionTexts.ItemUseHandOverFoundInRaid(count: count),
            Use.FindInRaid => SessionTexts.ItemUseFindInRaid(count: count),
            Use.FindInRaidThenHandOver => SessionTexts.ItemUseFindInRaidThenHandOver(count: count),
            Use.Find => SessionTexts.ItemUseFind(count: count),
            Use.FindThenHandOver => SessionTexts.ItemUseFindThenHandOver(count: count),
            _ => SessionTexts.ItemUseSell(count: count),
        };
    }

    /// <param name="done">The objectives the player ticked as done (<see cref="ObjectiveTicks"/>): what they needed is
    /// no longer a use of the item.</param>
    public static ItemCardView Build(GameData data, ItemSources? sources, IReadOnlyDictionary<string, QuestStatus> quests, string itemId,
        IReadOnlySet<string>? done = null)
    {
        var uses = new List<ItemUse>();
        var isKey = false;
        foreach (var status in quests.Values.Where(q => q.State == QuestState.Active))
        {
            if (!data.Tasks.TryGetValue(status.QuestId, out var task))
                continue;
            bool Done(ApiObjective o) => done?.Contains(o.Id) == true;
            var anyDone = (task.Objectives ?? []).Any(Done);
            var openMaps = (task.Objectives ?? []).Where(o => !Done(o)).SelectMany(QuestCards.MapIds).ToHashSet(StringComparer.Ordinal);
            // One line per way the quest needs it, gathered over its objectives: how many in all and on which maps
            // (the review of 2026-10-04: three "mark" objectives read "Bring, to mark" where BRING said "×3").
            var ways = new List<Way>();
            Way Add(Use use, IEnumerable<string> mapIds, int count = 0, string exit = "", bool usedUp = true)
            {
                var way = ways.FirstOrDefault(w => w.Use == use && w.Exit == exit);
                if (way is null)
                    ways.Add(way = new Way(use, exit));
                // What is used up each time adds up; an exit asks the same whichever objective names it.
                way.Count = usedUp ? way.Count + count : Math.Max(way.Count, count);
                way.Maps.AddRange(mapIds.Where(m => !way.Maps.Contains(m)).Distinct().ToList());
                return way;
            }
            // What is brought to be used up is one line however it is used ("Bring ×3, to plant and to mark"), as it
            // is one row in BRING.
            void Bring(IEnumerable<string> mapIds, int count, string purpose)
            {
                var way = Add(Use.Bring, mapIds, count);
                if (!way.Purposes.Contains(purpose))
                    way.Purposes.Add(purpose);
            }
            bool HandedOver(ApiObjective get) => Handovers.HandoverOf(task, get.Id) is not null;
            bool FoldsOpen(ApiObjective handover) => Handovers.GetOf(task, handover.Id) is { } get && !Done(get);

            foreach (var (o, plan) in (task.Objectives ?? []).Zip(Planning.ToPlan(task, data).Objectives))
            {
                if (Done(o))
                    continue;
                var maps = QuestCards.MapIds(o).ToList();
                var count = Math.Max(1, o.Count ?? 1);
                if ((o.RequiredKeys ?? []).Any(set => set.Contains(itemId)))
                    Add(Use.Key, maps);
                if ((o.Wearing ?? []).Any(set => set.Any(i => i.Id == itemId)))
                    Add(Use.Wear, maps);
                if (o.UsingWeapon?.Contains(itemId) == true)
                    Add(Use.Weapon, maps);
                if ((o.UsingWeaponMods ?? []).Any(set => set.Contains(itemId)))
                    Add(Use.Mods, maps);
                if ((plan.ExitItems ?? []).FirstOrDefault(i => i.ItemId == itemId) is { ItemId: not null } exitItem)
                    Add(Use.Leave, maps, exitItem.Count, plan.Exit ?? "", usedUp: false);
                var listed = o.Items?.Contains(itemId) == true;
                switch (o.Type)
                {
                    case "plantItem" when listed:
                        Bring(maps, count, SessionTexts.PurposePlant);
                        break;
                    case "mark" when o.MarkerItem == itemId:
                        Bring(maps, 1, SessionTexts.PurposeMark);
                        break;
                    case "useItem" when o.UseAny?.Contains(itemId) == true:
                        Bring(maps, count, SessionTexts.PurposeUse);
                        break;
                    // A quest item goes into the raid by itself, from the quest items: stashed there, not brought.
                    case "plantQuestItem" when o.QuestItem == itemId:
                        Add(Use.Stash, maps);
                        break;
                    // A hand-over of what another objective gets is said with that objective ("Pick up, then hand
                    // over"), while it is open; once it is ticked, the hand-over is what is left and says so itself.
                    case "findQuestItem" when o.QuestItem == itemId:
                        Add(HandedOver(o) ? Use.PickUpThenHandOver : Use.PickUp, maps);
                        break;
                    case "giveQuestItem" when o.QuestItem == itemId && !FoldsOpen(o):
                        Add(Use.HandOver, []);
                        break;
                    // "In raid" only where the data says the item must be found in raid; one that may be bought is just found.
                    case "findItem" when listed:
                        Add(o.FoundInRaid ? (HandedOver(o) ? Use.FindInRaidThenHandOver : Use.FindInRaid)
                            : (HandedOver(o) ? Use.FindThenHandOver : Use.Find), [], count);
                        break;
                    case "giveItem" when listed && !FoldsOpen(o):
                        Add(o.FoundInRaid ? Use.HandOverFoundInRaid : Use.HandOver, [], count);
                        break;
                    case "sellItem" when listed:
                        Add(Use.Sell, [], count);
                        break;
                }
            }
            foreach (var needed in task.NeededKeys ?? [])
            {
                // A quest-level key counts only for a map where an open objective is left (as on the quest card).
                if (anyDone && needed.Map is not null && !openMaps.Contains(needed.Map))
                    continue;
                if (needed.Keys?.Contains(itemId) == true)
                    Add(Use.Key, needed.Map is null ? [] : [needed.Map]);
            }
            foreach (var way in ways)
            {
                isKey |= way.Use == Use.Key;
                var how = How(way);
                var where = QuestCards.MapNames(data, way.Maps);
                uses.Add(new ItemUse(task.Id, task.Name, QuestCards.KindOf(task), task.Trader, where.Length > 0 ? $"{how} · {where}" : how));
            }
        }
        return new ItemCardView(itemId, data.ItemName(itemId), isKey,
            Sources(data, sources, itemId, quests),
            uses.OrderBy(u => u.QuestName, StringComparer.Create(UiLanguage.Culture, ignoreCase: false)).ToList());
    }

    /// <summary>
    /// Where to get one of several items that will each do (a weapon class): the easiest way to get the first of them
    /// that has a way open now, named ("e.g. Mosin rifle (Sniper) · Prapor LL1 · 12,000 ₽"), else the first with any
    /// way; for one item, its easiest way. Empty if none is known.
    /// </summary>
    /// <param name="quests">The quests' states from the game's log (<see cref="Best"/>).</param>
    public static string BestOf(GameData data, ItemSources? sources, IReadOnlyList<string> items,
        IReadOnlyDictionary<string, QuestStatus>? quests = null)
    {
        if (items.Count == 1)
            return Best(data, sources, items[0], quests)?.Text ?? "";
        (string Id, ItemSource Way)? behindQuest = null;
        foreach (var id in items.Distinct())
        {
            if (Best(data, sources, id, quests) is not { } way)
                continue;
            if (!way.Locked)
                return SessionTexts.ItemSourceExample(item: data.ItemName(id), source: way.Text);
            behindQuest ??= (id, way);
        }
        return behindQuest is { } only ? SessionTexts.ItemSourceExample(item: data.ItemName(only.Id), source: only.Way.Text) : "";
    }

    /// <summary>The easiest way to get an item, for one line under it in BRING; null if unknown.</summary>
    /// <param name="quests">The quests' states from the game's log, to tell an offer a completed quest has opened from
    /// one that still needs its quest; null when they aren't at hand: then every such offer counts as not open yet.</param>
    public static ItemSource? Best(GameData data, ItemSources? sources, string itemId, IReadOnlyDictionary<string, QuestStatus>? quests = null) =>
        Sources(data, sources, itemId, quests).FirstOrDefault();

    // Easiest first: a trader that sells it outright (cheapest first), a barter, a craft, the flea market, then
    // finding it loose in a raid. A trader's offer or barter that needs a quest the game's log hasn't seen completed
    // comes after all of them, saying which ("after Dandies"): it isn't a way yet, and the quest may be the very one
    // the item is for (the review of 2026-10-04: BRING named "Ragman LL2 · after Dandies" for the beanie Dandies asks
    // for). Once the log has seen that quest completed, the offer is one like any other and the note goes.
    private static List<ItemSource> Sources(GameData data, ItemSources? sources, string itemId, IReadOnlyDictionary<string, QuestStatus>? quests)
    {
        var found = new List<ItemSource>();
        var locked = new List<ItemSource>();
        bool Open(string? quest) => quest is null || quests?.GetValueOrDefault(quest)?.State == QuestState.Completed;
        string After(string? quest) => Open(quest) ? ""
            : " · " + (data.Tasks.GetValueOrDefault(quest!)?.Name is { } name ? SessionTexts.ItemSourceAfter(quest: name) : SessionTexts.ItemSourceAfterAQuest);
        if (sources is not null)
        {
            var item = sources.Items.GetValueOrDefault(itemId);
            foreach (var offer in (item?.BuyFromTrader ?? []).OrderBy(o => Open(o.TaskUnlock) ? 0 : 1).ThenBy(o => o.PriceRUB ?? o.Price).Take(3))
            {
                var open = Open(offer.TaskUnlock);
                (open ? found : locked).Add(new ItemSource(SourceKind.Trader, offer.Trader,
                    SessionTexts.ItemSourceTrader(level: offer.MinTraderLevel, trader: data.TraderName(offer.Trader)) + " · " + Price(offer.Price, offer.Currency) + After(offer.TaskUnlock),
                    !open));
            }
            foreach (var barter in sources.Barters[itemId].OrderBy(b => Open(b.TaskUnlock) ? 0 : 1).ThenBy(b => b.MinTraderLevel).Take(2))
            {
                var parts = (barter.RequiredItems ?? []).Select(r => SessionTexts.ItemSourceBarterItem(count: r.Count.ToString("0", UiLanguage.Culture), item: data.ItemName(r.Item))).ToList();
                var cost = parts.Count <= 2 ? string.Join(", ", parts) : SessionTexts.ItemSourceBarterMore(first: parts[0], more: parts.Count - 2, second: parts[1]);
                var open = Open(barter.TaskUnlock);
                (open ? found : locked).Add(new ItemSource(SourceKind.Barter, barter.Trader,
                    SessionTexts.ItemSourceBarter(level: barter.MinTraderLevel, trader: data.TraderName(barter.Trader)) + " · " + cost + After(barter.TaskUnlock),
                    !open));
            }
            foreach (var craft in sources.Crafts[itemId].OrderBy(c => c.Level).Take(2))
            {
                var station = sources.Stations.GetValueOrDefault(craft.Station) ?? SessionTexts.ItemSourceHideout;
                found.Add(new ItemSource(SourceKind.Craft, null, SessionTexts.ItemSourceCraft(level: craft.Level, station: station)));
            }
            if (item is not null && item.Types?.Contains("noFlea") != true)
            {
                var price = item.LastLowPrice is { } low and > 0 ? " · " + SessionTexts.ItemSourceFleaPrice(price: Price(low, "RUB")) : "";
                var flea = item.MinLevelForFlea is > 1 and var min ? SessionTexts.ItemSourceFleaFromLevel(level: min) : SessionTexts.ItemSourceFlea;
                found.Add(new ItemSource(SourceKind.Flea, null, flea + price));
            }
        }
        var loose = data.SpawnsOf(itemId)
            .GroupBy(s => QuestCards.MapNames(data, [s.MapId]))
            .Where(g => g.Key.Length > 0)
            .OrderByDescending(g => g.Count())
            .Take(3);
        foreach (var map in loose)
            found.Add(new ItemSource(SourceKind.Loose, null, SessionTexts.ItemSourceLoose(count: map.Count(), map: map.Key)));
        found.AddRange(locked);
        return found;
    }

    private static string Price(double amount, string? currency) => currency switch
    {
        "USD" => SessionTexts.PriceDollars(amount: amount.ToString("N0", UiLanguage.Culture)),
        "EUR" => SessionTexts.PriceEuros(amount: amount.ToString("N0", UiLanguage.Culture)),
        _ => SessionTexts.PriceRoubles(amount: amount.ToString("N0", UiLanguage.Culture)),
    };
}
