using Shturmap.Core.Quests;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session;

/// <summary>Something your quests need found in raid: "Gas analyzer ×2", for which quests, how often it lies loose here.</summary>
/// <param name="ItemId">The item to picture: of several that would do, the one lying loose here most.</param>
/// <param name="SpotsHere">Places on the shown map where it (or an item that would do) lies loose.</param>
public sealed record LootView(string ItemId, string Text, string ForQuests, IReadOnlyList<string> QuestIds, int SpotsHere);

/// <summary>
/// A Scav raid, for the quest list: quest objectives only count for the PMC, but items found in raid count whoever
/// found them. So what a Scav raid can do for your quests is the loot they need found in raid.
/// </summary>
public static class ScavRaid
{
    /// <summary>Items your active quests need found in raid, the ones lying loose on this map first.</summary>
    /// <param name="mapIds">The shown map's ids (variants drawn with the same artwork count as one).</param>
    /// <param name="done">The objectives the player ticked as done: what they asked for is no longer looked for.</param>
    public static IReadOnlyList<LootView> Loot(GameData data, IReadOnlyDictionary<string, QuestStatus> quests, IReadOnlySet<string> mapIds,
        IReadOnlySet<string>? done = null)
    {
        // One need per set of items that would do; a quest that asks to find them and then hand them over needs
        // them once.
        var needs = new Dictionary<string, (IReadOnlyList<string> Items, Dictionary<string, int> Quests)>(StringComparer.Ordinal);
        foreach (var status in quests.Values.Where(q => q.State == QuestState.Active))
        {
            if (!data.Tasks.TryGetValue(status.QuestId, out var task))
                continue;
            foreach (var o in task.Objectives ?? [])
            {
                if (o.Type is not ("findItem" or "giveItem") || !o.FoundInRaid || o.Items is not { Count: > 0 } items || done?.Contains(o.Id) == true)
                    continue;
                var key = string.Join('|', items.Order(StringComparer.Ordinal));
                if (!needs.TryGetValue(key, out var need))
                    needs[key] = need = (items, new Dictionary<string, int>(StringComparer.Ordinal));
                need.Quests[task.Id] = Math.Max(need.Quests.GetValueOrDefault(task.Id), Math.Max(1, o.Count ?? 1));
            }
        }

        int SpotsHere(string item) => data.SpawnsOf(item).Count(s => mapIds.Contains(s.MapId));
        return needs.Values
            .Select(n =>
            {
                var spots = n.Items.Select(i => (Item: i, Spots: SpotsHere(i))).ToList();
                var names = n.Items.Select(data.ItemName).Distinct().ToList();
                var text = Planning.OneOf(names);
                var count = n.Quests.Values.Sum();
                if (count > 1)
                    text = SessionTexts.ItemTimes(count: count, item: text);
                var questIds = n.Quests.Keys.OrderBy(id => data.Tasks[id].Name, StringComparer.CurrentCulture).ToList();
                return new LootView(
                    spots.MaxBy(s => s.Spots).Item,
                    text,
                    string.Join(", ", questIds.Select(id => data.Tasks[id].Name)),
                    questIds,
                    spots.Sum(s => s.Spots));
            })
            .OrderByDescending(l => l.SpotsHere)
            .ThenByDescending(l => l.QuestIds.Count)
            .ThenBy(l => l.Text, StringComparer.CurrentCulture)
            .ToList();
    }
}
