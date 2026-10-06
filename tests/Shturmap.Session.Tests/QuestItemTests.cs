using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Core.Quests;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

// A quest item to stash is nothing to bring (owner, 2026-10-06: One Less Loose End's lab journal stood in BRING, "but it
// is one you actually have to collect first as part of the mission"): got in the quest, it goes to the quest items on
// pickup and into every raid from there by itself.
public class QuestItemTests
{
    private const string Quest = "aaaaaaaaaaaaaaaaaaaaaa41", Earlier = "aaaaaaaaaaaaaaaaaaaaaa42", Journal = "lab-journal", Handguard = "handguard";

    // One Less Loose End: find the journal on Factory, stash it on Woods.
    private static readonly ApiObjective Find = new("o-find", "findQuestItem", "Locate and obtain the lab journal on Factory", false, ["map-factory"],
        null, [new ApiPossibleLocation("map-factory", [new(10, 0, 10), new(40, 0, 10)])], 1, Journal, null, null, null, null, false);

    private static readonly ApiObjective Stash = new("o-stash", "plantQuestItem", "Stash the journal at the old sawmill on Woods", false, ["map-woods"],
        [new ApiZone("o-stash-zone", null, "map-woods", new ApiPosition(5, 0, 5), null, null, null)], null, 1, Journal, null, null, null, null, false);

    // Hobby Club's handguard, got in Fair Price - Part 2.
    private static readonly ApiObjective StashHandguard = new("o-handguard", "plantQuestItem", "Stash the AK-50 handguard", false, ["map-woods"],
        [new ApiZone("o-handguard-zone", null, "map-woods", new ApiPosition(5, 0, 5), null, null, null)], null, 1, Handguard, null, null, null, null, false);

    private static readonly ApiObjective FindHandguard = new("o-find-handguard", "findQuestItem", "Obtain the handguard", false, ["map-factory"],
        null, [new ApiPossibleLocation("map-factory", [new(1, 0, 1)])], 1, Handguard, null, null, null, null, false);

    private static ApiTask Task(string id, string name, params ApiObjective[] objectives) =>
        new(id, name, null, null, null, null, false, false, null, null, null, false, null, [.. objectives], null);

    private static readonly ApiTask LooseEnd = Task(Quest, "One Less Loose End", Find, Stash);

    private static GameData Data(params ApiTask[] tasks) => new()
    {
        Mode = GameMode.Pve,
        Language = "en",
        Maps = new[]
        {
            new ApiMap("map-factory", "Factory", "factory", "factory4_day", null, null, 20, [], [], [], [], []),
            new ApiMap("map-woods", "Woods", "woods", "Woods", null, null, 40, [], [], [], [], []),
        }.ToDictionary(m => m.Id),
        Tasks = tasks.ToDictionary(t => t.Id),
        Traders = new Dictionary<string, ApiTrader>(),
        ItemNames = new Dictionary<string, string> { [Journal] = "Lab journal", [Handguard] = "AK-50 handguard" },
        MapDefinitions = [new MapDefinition { Key = "woods", Transform = [1, 0, 1, 0], Bounds = new WorldBox(-500, -500, 500, 500) }],
        CheckedAt = DateTimeOffset.Now,
    };

    private static Dictionary<string, QuestStatus> Active(params string[] ids) =>
        ids.ToDictionary(id => id, id => new QuestStatus(id, QuestState.Active, ObservationSource.Log, DateTime.Now));

    [Fact]
    public void A_quest_item_to_stash_is_not_brought_but_said_where_it_comes_from()
    {
        var data = Data(LooseEnd);
        Assert.Empty(Planning.ToPlan(LooseEnd, data).Objectives.Single(o => o.Id == "o-stash").Bring);
        Assert.Empty(QuestCards.Build(data, Active(Quest), Quest)!.Needs);
        Assert.Equal("With: Lab journal, found on Factory", Planning.Needs(data, LooseEnd, Stash, new HashSet<string> { "map-woods" }, true));
    }

    [Fact]
    public void One_got_in_a_quest_before_names_that_quest()
    {
        var hobby = Task(Quest, "Hobby Club", StashHandguard);
        var fairPrice = Task(Earlier, "Fair Price - Part 2", FindHandguard);
        Assert.Equal("With: AK-50 handguard, from Fair Price - Part 2",
            Planning.Needs(Data(hobby, fairPrice), hobby, StashHandguard, new HashSet<string> { "map-woods" }, true));
    }

    [Fact]
    public void The_item_card_says_it_is_picked_up_and_stashed_not_brought()
    {
        var card = ItemCards.Build(Data(LooseEnd), null, Active(Quest), Journal);
        Assert.DoesNotContain(card.Uses, u => u.How.StartsWith("Bring", StringComparison.Ordinal));
        Assert.Contains(card.Uses, u => u.How.StartsWith("Stash", StringComparison.Ordinal) && u.How.Contains("Woods", StringComparison.Ordinal));
    }
}
