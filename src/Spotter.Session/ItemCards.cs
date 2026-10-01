using System.Globalization;
using Spotter.Core.Quests;
using Spotter.Data.TarkovDev;

namespace Spotter.Session;

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
public sealed record ItemSource(SourceKind Kind, string? TraderId, string Text);

/// <summary>The item card: what it is, where to get it, and which of your active quests need it and how.</summary>
/// <param name="Get">Where to get it, the easiest first; empty until the item sources have loaded.</param>
public sealed record ItemCardView(string ItemId, string Name, bool IsKey, IReadOnlyList<ItemSource> Get, IReadOnlyList<ItemUse> Uses);

public static class ItemCards
{
    public static ItemCardView Build(GameData data, ItemSources? sources, IReadOnlyDictionary<string, QuestStatus> quests, string itemId)
    {
        var uses = new List<ItemUse>();
        foreach (var status in quests.Values.Where(q => q.State == QuestState.Active))
        {
            if (!data.Tasks.TryGetValue(status.QuestId, out var task))
                continue;
            void Add(string how, IEnumerable<string> mapIds)
            {
                var where = QuestCards.MapNames(data, mapIds);
                var text = where.Length > 0 ? $"{how} · {where}" : how;
                if (!uses.Any(u => u.QuestId == task.Id && u.How == text))
                    uses.Add(new ItemUse(task.Id, task.Name, QuestCards.KindOf(task), task.Trader, text));
            }

            foreach (var o in task.Objectives ?? [])
            {
                var maps = QuestCards.MapIds(o).ToList();
                var count = Math.Max(1, o.Count ?? 1);
                var times = count > 1 ? $" ×{count}" : "";
                if ((o.RequiredKeys ?? []).Any(set => set.Contains(itemId)))
                    Add("Key", maps);
                var listed = o.Items?.Contains(itemId) == true;
                var how = o.Type switch
                {
                    "plantItem" when listed => $"Bring{times}, to plant",
                    "mark" when o.MarkerItem == itemId => "Bring, to mark",
                    "useItem" when o.UseAny?.Contains(itemId) == true => $"Bring{times}, to use",
                    "plantQuestItem" when o.QuestItem == itemId => "Bring, to plant",
                    "findQuestItem" when o.QuestItem == itemId => "Pick up",
                    "giveQuestItem" when o.QuestItem == itemId => "Hand over",
                    "findItem" when listed => $"Find in raid{times}",
                    "giveItem" when listed => $"Hand over{times}" + (o.FoundInRaid ? ", found in raid" : ""),
                    "sellItem" when listed => $"Sell{times}",
                    _ => null,
                };
                if (how is not null)
                    Add(how, how.StartsWith("Bring", StringComparison.Ordinal) || how == "Pick up" ? maps : []);
            }
            foreach (var needed in task.NeededKeys ?? [])
            {
                if (needed.Keys?.Contains(itemId) == true)
                    Add("Key", needed.Map is null ? [] : [needed.Map]);
            }
        }
        return new ItemCardView(itemId, data.ItemName(itemId), uses.Any(u => u.How.StartsWith("Key", StringComparison.Ordinal)),
            Sources(data, sources, itemId),
            uses.OrderBy(u => u.QuestName, StringComparer.CurrentCulture).ToList());
    }

    /// <summary>The easiest way to get an item, for one line under it in BRING; null if unknown.</summary>
    public static ItemSource? Best(GameData data, ItemSources? sources, string itemId) => Sources(data, sources, itemId).FirstOrDefault();

    // Easiest first: a trader that sells it outright (cheapest first), a barter, a craft, the flea market, then
    // finding it loose in a raid.
    private static List<ItemSource> Sources(GameData data, ItemSources? sources, string itemId)
    {
        var found = new List<ItemSource>();
        if (sources is not null)
        {
            var item = sources.Items.GetValueOrDefault(itemId);
            foreach (var offer in (item?.BuyFromTrader ?? []).OrderBy(o => o.TaskUnlock is null ? 0 : 1).ThenBy(o => o.PriceRUB ?? o.Price).Take(3))
            {
                var after = offer.TaskUnlock is { } quest ? $" · after {data.Tasks.GetValueOrDefault(quest)?.Name ?? "a quest"}" : "";
                found.Add(new ItemSource(SourceKind.Trader, offer.Trader,
                    $"{data.TraderName(offer.Trader)} LL{offer.MinTraderLevel} · {Price(offer.Price, offer.Currency)}{after}"));
            }
            foreach (var barter in sources.Barters[itemId].OrderBy(b => b.MinTraderLevel).Take(2))
            {
                var parts = (barter.RequiredItems ?? []).Select(r => $"{r.Count:0}× {data.ItemName(r.Item)}").ToList();
                var cost = parts.Count <= 2 ? string.Join(", ", parts) : $"{parts[0]}, {parts[1]} and {parts.Count - 2} more";
                found.Add(new ItemSource(SourceKind.Barter, barter.Trader, $"{data.TraderName(barter.Trader)} LL{barter.MinTraderLevel} barter · {cost}"));
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
        return found;
    }

    private static string Price(double amount, string? currency) => currency switch
    {
        "USD" => "$" + amount.ToString("N0", CultureInfo.CurrentCulture),
        "EUR" => "€" + amount.ToString("N0", CultureInfo.CurrentCulture),
        _ => amount.ToString("N0", CultureInfo.CurrentCulture) + " ₽",
    };
}
