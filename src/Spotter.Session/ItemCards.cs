using Spotter.Core.Quests;
using Spotter.Data.TarkovDev;

namespace Spotter.Session;

/// <summary>One way an active quest needs an item: "Key · Customs", "Bring ×3 · Streets of Tarkov", "Find in raid ×2".</summary>
public sealed record ItemUse(string QuestId, string QuestName, ObjectiveKind QuestKind, string? TraderId, string How);

/// <summary>The item card: what it is and which of your active quests need it, and how.</summary>
public sealed record ItemCardView(string ItemId, string Name, bool IsKey, IReadOnlyList<ItemUse> Uses);

public static class ItemCards
{
    public static ItemCardView Build(GameData data, IReadOnlyDictionary<string, QuestStatus> quests, string itemId)
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
            uses.OrderBy(u => u.QuestName, StringComparer.CurrentCulture).ToList());
    }
}
