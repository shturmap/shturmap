using Shturmap.Core.Logs;
using Shturmap.Core.Quests;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

// The item card: how each active quest needs the item, said as the data has it.
public class ItemCardTests
{
    private const string Salewa = "salewa";

    private static readonly Dictionary<string, string> Names = new() { [Salewa] = "Salewa first aid kit" };

    private static GameData Data(params ApiTask[] tasks) => new()
    {
        Mode = GameMode.Pve,
        Language = "en",
        Maps = new Dictionary<string, ApiMap>(),
        Tasks = tasks.ToDictionary(t => t.Id),
        Traders = new Dictionary<string, ApiTrader>(),
        ItemNames = Names,
        MapDefinitions = [],
        CheckedAt = DateTimeOffset.Now,
    };

    private static ApiTask Task(string id, params ApiObjective[] objectives) =>
        new(id, id, null, null, null, null, false, false, null, null, null, false, null, [.. objectives], null);

    private static ApiObjective Objective(string id, string type, int count = 1, List<string>? items = null, bool foundInRaid = false) =>
        new(id, type, id, false, null, null, null, count, null, items, null, null, null, foundInRaid);

    private static Dictionary<string, QuestStatus> Active(params string[] ids) =>
        ids.ToDictionary(id => id, id => new QuestStatus(id, QuestState.Active, ObservationSource.Log, DateTime.Now));

    private static IEnumerable<string> Uses(GameData data, string item, params string[] active) =>
        ItemCards.Build(data, null, Active(active), item).Uses.Select(u => $"{u.QuestName}: {u.How}");

    [Fact]
    public void An_item_is_found_in_raid_only_where_the_data_says_so()
    {
        var data = Data(
            Task("Shortage", Objective("s1", "findItem", 3, [Salewa], foundInRaid: true)),
            Task("Supply", Objective("p1", "findItem", 2, [Salewa])));
        Assert.Equal(["Shortage: Find in raid ×3", "Supply: Find ×2"], Uses(data, Salewa, "Shortage", "Supply"));
    }
}
