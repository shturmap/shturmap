using System.Globalization;
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
    // One way a quest needs the item: "Bring", how many in all (" ×3"), and what for (", to mark"), on these maps.
    private sealed class Way(string verb, string what)
    {
        public string Verb => verb;

        public string What => what;

        public int Count { get; set; }

        public List<string> Maps { get; } = [];

        /// <summary>What it is brought to be used up for ("to plant", "to mark"), in place of <see cref="What"/>.</summary>
        public List<string> Purposes { get; } = [];
    }

    /// <param name="done">The objectives the player ticked as done (<see cref="ObjectiveTicks"/>): what they needed is
    /// no longer a use of the item.</param>
    public static ItemCardView Build(GameData data, ItemSources? sources, IReadOnlyDictionary<string, QuestStatus> quests, string itemId,
        IReadOnlySet<string>? done = null)
    {
        var uses = new List<ItemUse>();
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
            Way Add(string verb, IEnumerable<string> mapIds, int count = 0, string what = "", bool usedUp = true)
            {
                var way = ways.FirstOrDefault(w => w.Verb == verb && w.What == what);
                if (way is null)
                    ways.Add(way = new Way(verb, what));
                // What is used up each time adds up; an exit asks the same whichever objective names it.
                way.Count = usedUp ? way.Count + count : Math.Max(way.Count, count);
                way.Maps.AddRange(mapIds.Where(m => !way.Maps.Contains(m)).Distinct().ToList());
                return way;
            }
            // What is brought to be used up is one line however it is used ("Bring ×3, to plant and to mark"), as it
            // is one row in BRING.
            void Bring(IEnumerable<string> mapIds, int count, string purpose)
            {
                var way = Add("Bring", mapIds, count);
                if (!way.Purposes.Contains(purpose))
                    way.Purposes.Add(purpose);
            }

            foreach (var (o, plan) in (task.Objectives ?? []).Zip(Planning.ToPlan(task, data).Objectives))
            {
                if (Done(o))
                    continue;
                var maps = QuestCards.MapIds(o).ToList();
                var count = Math.Max(1, o.Count ?? 1);
                if ((o.RequiredKeys ?? []).Any(set => set.Contains(itemId)))
                    Add("Key", maps);
                if ((o.Wearing ?? []).Any(set => set.Any(i => i.Id == itemId)))
                    Add("Wear, for kills", maps);
                if (o.UsingWeapon?.Contains(itemId) == true)
                    Add("Use, for kills", maps);
                if ((o.UsingWeaponMods ?? []).Any(set => set.Contains(itemId)))
                    Add("Fit, for kills", maps);
                if ((plan.ExitItems ?? []).FirstOrDefault(i => i.ItemId == itemId) is { ItemId: not null } exitItem)
                    Add("Bring", maps, exitItem.Count, $", to leave through {plan.Exit}", usedUp: false);
                var listed = o.Items?.Contains(itemId) == true;
                switch (o.Type)
                {
                    case "plantItem" when listed:
                        Bring(maps, count, "to plant");
                        break;
                    case "mark" when o.MarkerItem == itemId:
                        Bring(maps, 1, "to mark");
                        break;
                    case "useItem" when o.UseAny?.Contains(itemId) == true:
                        Bring(maps, count, "to use");
                        break;
                    case "plantQuestItem" when o.QuestItem == itemId:
                        Bring(maps, count, "to plant");
                        break;
                    case "findQuestItem" when o.QuestItem == itemId:
                        Add("Pick up", maps);
                        break;
                    case "giveQuestItem" when o.QuestItem == itemId:
                        Add("Hand over", []);
                        break;
                    // "In raid" only where the data says the item must be found in raid; one that may be bought is just found.
                    case "findItem" when listed:
                        Add(o.FoundInRaid ? "Find in raid" : "Find", [], count);
                        break;
                    case "giveItem" when listed:
                        Add("Hand over", [], count, o.FoundInRaid ? ", found in raid" : "");
                        break;
                    case "sellItem" when listed:
                        Add("Sell", [], count);
                        break;
                }
            }
            foreach (var needed in task.NeededKeys ?? [])
            {
                // A quest-level key counts only for a map where an open objective is left (as on the quest card).
                if (anyDone && needed.Map is not null && !openMaps.Contains(needed.Map))
                    continue;
                if (needed.Keys?.Contains(itemId) == true)
                    Add("Key", needed.Map is null ? [] : [needed.Map]);
            }
            foreach (var way in ways)
            {
                var what = way.Purposes.Count > 0 ? ", " + string.Join(" and ", way.Purposes) : way.What;
                var how = $"{way.Verb}{(way.Count > 1 ? $" ×{way.Count}" : "")}{what}";
                var where = QuestCards.MapNames(data, way.Maps);
                uses.Add(new ItemUse(task.Id, task.Name, QuestCards.KindOf(task), task.Trader, where.Length > 0 ? $"{how} · {where}" : how));
            }
        }
        return new ItemCardView(itemId, data.ItemName(itemId), uses.Any(u => u.How.StartsWith("Key", StringComparison.Ordinal)),
            Sources(data, sources, itemId, quests),
            uses.OrderBy(u => u.QuestName, StringComparer.CurrentCulture).ToList());
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
                return $"e.g. {data.ItemName(id)} · {way.Text}";
            behindQuest ??= (id, way);
        }
        return behindQuest is { } only ? $"e.g. {data.ItemName(only.Id)} · {only.Way.Text}" : "";
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
        string After(string? quest) => Open(quest) ? "" : $" · after {data.Tasks.GetValueOrDefault(quest!)?.Name ?? "a quest"}";
        if (sources is not null)
        {
            var item = sources.Items.GetValueOrDefault(itemId);
            foreach (var offer in (item?.BuyFromTrader ?? []).OrderBy(o => Open(o.TaskUnlock) ? 0 : 1).ThenBy(o => o.PriceRUB ?? o.Price).Take(3))
            {
                var open = Open(offer.TaskUnlock);
                (open ? found : locked).Add(new ItemSource(SourceKind.Trader, offer.Trader,
                    $"{data.TraderName(offer.Trader)} LL{offer.MinTraderLevel} · {Price(offer.Price, offer.Currency)}{After(offer.TaskUnlock)}", !open));
            }
            foreach (var barter in sources.Barters[itemId].OrderBy(b => Open(b.TaskUnlock) ? 0 : 1).ThenBy(b => b.MinTraderLevel).Take(2))
            {
                var parts = (barter.RequiredItems ?? []).Select(r => $"{r.Count:0}× {data.ItemName(r.Item)}").ToList();
                var cost = parts.Count <= 2 ? string.Join(", ", parts) : $"{parts[0]}, {parts[1]} and {parts.Count - 2} more";
                var open = Open(barter.TaskUnlock);
                (open ? found : locked).Add(new ItemSource(SourceKind.Barter, barter.Trader,
                    $"{data.TraderName(barter.Trader)} LL{barter.MinTraderLevel} barter · {cost}{After(barter.TaskUnlock)}", !open));
            }
            foreach (var craft in sources.Crafts[itemId].OrderBy(c => c.Level).Take(2))
            {
                var station = sources.Stations.GetValueOrDefault(craft.Station) ?? "Hideout";
                found.Add(new ItemSource(SourceKind.Craft, null, $"Craft · {station} level {craft.Level}"));
            }
            if (item is not null && item.Types?.Contains("noFlea") != true)
            {
                var price = item.LastLowPrice is { } low and > 0 ? $" · ~{Price(low, "RUB")}" : "";
                var level = item.MinLevelForFlea is > 1 and var min ? $" from level {min}" : "";
                found.Add(new ItemSource(SourceKind.Flea, null, $"Flea market{level}{price}"));
            }
        }
        var loose = data.SpawnsOf(itemId)
            .GroupBy(s => QuestCards.MapNames(data, [s.MapId]))
            .Where(g => g.Key.Length > 0)
            .OrderByDescending(g => g.Count())
            .Take(3);
        foreach (var map in loose)
            found.Add(new ItemSource(SourceKind.Loose, null, $"Loose on {map.Key} · {map.Count()} {(map.Count() == 1 ? "spot" : "spots")}"));
        found.AddRange(locked);
        return found;
    }

    private static string Price(double amount, string? currency) => currency switch
    {
        "USD" => "$" + amount.ToString("N0", CultureInfo.CurrentCulture),
        "EUR" => "€" + amount.ToString("N0", CultureInfo.CurrentCulture),
        _ => amount.ToString("N0", CultureInfo.CurrentCulture) + " ₽",
    };
}
