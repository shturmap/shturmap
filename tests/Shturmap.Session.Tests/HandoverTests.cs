using Shturmap.Core;
using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Core.Quests;
using Shturmap.Core.Raid;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

// A hand-over of what another objective of the quest gets is no step of its own in a raid (owner, 2026-10-05: "That
// last step is out of raid but still takes up a 'ToDo' line … this should rather be an icon associated with the
// corresponding item or quest item"). Paired by the data's ids (Handovers), it is a mark on the objective that gets
// the thing: on the quest card, the raid card and the item card. Ids and texts below are made up.
public class HandoverTests
{
    private const string Drive = "aaaaaaaaaaaaaaaaaaaaaa11", Analyzers = "aaaaaaaaaaaaaaaaaaaaaa12", Letter = "aaaaaaaaaaaaaaaaaaaaaa13";
    private const string Therapist = "therapist", HardDrive = "hard-drive", Analyzer = "gas-analyzer", Sealed = "sealed-letter";

    private static ApiObjective Pickup(string id, string questItem, bool optional = false) =>
        new(id, "findQuestItem", "Locate and obtain the hard drive", optional, ["map-customs"],
            [new ApiZone(id + "-zone", null, "map-customs", new ApiPosition(10, 0, 10), null, null, null)], null, 1, questItem, null, null, null, null, false);

    private static ApiObjective GiveQuestItem(string id, string questItem, bool optional = false) =>
        new(id, "giveQuestItem", "Hand over the hard drive", optional, [], null, null, 1, questItem, null, null, null, null, false);

    private static ApiObjective Find(string id, int count, bool foundInRaid, bool optional = false, params string[] items) =>
        new(id, "findItem", "Find the items in raid", optional, null, null, null, count, null, [.. items], null, null, null, foundInRaid);

    private static ApiObjective Give(string id, int count, bool foundInRaid, bool optional = false, params string[] items) =>
        new(id, "giveItem", "Hand over the items", optional, null, null, null, count, null, [.. items], null, null, null, foundInRaid);

    private static ApiTask Task(string id, string name, params ApiObjective[] objectives) =>
        new(id, name, null, Therapist, "map-customs", null, false, false, null, null, null, false, null, [.. objectives], null);

    private static readonly ApiTask Scientist = Task(Drive, "Saving the Scientist", Pickup("o-pick", HardDrive), GiveQuestItem("o-give", HardDrive));

    private static readonly ApiTask Sanitary = Task(Analyzers, "Sanitary", Find("o-find", 3, true, items: [Analyzer]), Give("o-hand", 3, true, items: [Analyzer]));

    // A letter got in an earlier quest: nothing here picks it up, so handing it over is the quest's work.
    private static readonly ApiTask Postman = Task(Letter, "Postman", GiveQuestItem("o-letter", Sealed));

    private static GameData Data(params ApiTask[] tasks) => new()
    {
        Mode = GameMode.Pve,
        Language = "en",
        Maps = new[] { new ApiMap("map-customs", "Customs", "customs", "bigmap", "maps/customs_preset.bundle", null, 40, [], [], [], [], []) }
            .ToDictionary(m => m.Id),
        Tasks = tasks.ToDictionary(t => t.Id),
        Traders = new Dictionary<string, ApiTrader> { [Therapist] = new(Therapist, "Therapist", null, null) },
        ItemNames = new Dictionary<string, string> { [HardDrive] = "Hard drive", [Analyzer] = "Gas analyzer", [Sealed] = "Sealed letter" },
        MapDefinitions = [new MapDefinition { Key = "customs", Transform = [1, 0, 1, 0], Bounds = new WorldBox(-500, -500, 500, 500) }],
        CheckedAt = DateTimeOffset.Now,
    };

    private static Dictionary<string, QuestStatus> Active(params string[] ids) =>
        ids.ToDictionary(id => id, id => new QuestStatus(id, QuestState.Active, ObservationSource.Log, DateTime.Now));

    // ---- the rule ----

    [Fact]
    public void A_quest_item_hand_over_pairs_with_its_pickup_and_an_item_hand_over_with_its_find()
    {
        Assert.Equal("o-give", Handovers.HandoverOf(Scientist, "o-pick")?.Id);
        Assert.Equal("o-pick", Handovers.GetOf(Scientist, "o-give")?.Id);
        Assert.True(Handovers.Folds(Sanitary, "o-hand"));
        Assert.False(Handovers.Folds(Sanitary, "o-find"));
        Assert.False(Handovers.Folds(Postman, "o-letter"));
    }

    [Fact]
    public void Items_pair_as_a_set_in_any_order_and_a_hand_over_listed_first()
    {
        // tarkov.dev lists the items in another order for some quests, and the hand-over before the find for two.
        var task = Task(Analyzers, "Mixed", Give("give", 2, true, items: ["b", "a"]), Find("find", 2, true, items: ["a", "b"]));
        Assert.Equal("give", Handovers.HandoverOf(task, "find")?.Id);
    }

    [Theory]
    [InlineData(2, true, "a")] // another count
    [InlineData(3, false, "a")] // found in raid on one side only
    [InlineData(3, true, "a", "b")] // more items
    public void Anything_but_the_same_thing_stays_its_own_line(int count, bool foundInRaid, params string[] items)
    {
        var task = Task(Analyzers, "Other", Find("find", 3, true, items: ["a"]), Give("give", count, foundInRaid, items: items));
        Assert.False(Handovers.Folds(task, "give"));
    }

    [Fact]
    public void Each_objective_pairs_once()
    {
        // Two finds and two hand-overs of the same thing: one each.
        var task = Task(Analyzers, "Twice", Find("f1", 1, true, items: ["a"]), Find("f2", 1, true, items: ["a"]), Give("g1", 1, true, items: ["a"]), Give("g2", 1, true, items: ["a"]));
        Assert.Equal("g1", Handovers.HandoverOf(task, "f1")?.Id);
        Assert.Equal("g2", Handovers.HandoverOf(task, "f2")?.Id);
    }

    [Fact]
    public void An_optional_find_whose_hand_over_is_required_reads_as_required()
    {
        // As in A Bitter Victory: the game counts the finding as optional where the items may be bought.
        var task = Task(Analyzers, "Bitter", Find("find", 2, false, optional: true, items: ["a"]), Give("give", 2, false, items: ["a"]));
        Assert.True(Handovers.Folds(task, "give"));
        Assert.False(Handovers.Optional(task, task.Objectives![0]));
        var both = Task(Analyzers, "Both", Find("find", 2, false, optional: true, items: ["a"]), Give("give", 2, false, optional: true, items: ["a"]));
        Assert.True(Handovers.Optional(both, both.Objectives![0]));
    }

    // ---- the quest card ----

    [Fact]
    public void The_quest_card_shows_the_hand_over_as_a_mark_on_the_pickup_not_as_a_row()
    {
        var card = QuestCards.Build(Data(Scientist), Active(Drive), Drive)!;
        var row = Assert.Single(card.Objectives);
        Assert.Equal(("o-pick", HardDrive, "Hand over to Therapist"), (row.ObjectiveId, row.ItemId, row.Handover));

        var items = QuestCards.Build(Data(Sanitary), Active(Analyzers), Analyzers)!;
        Assert.Equal("Hand over ×3 to Therapist", Assert.Single(items.Objectives).Handover);

        // A hand-over of its own stays a row, without a mark.
        Assert.Equal("", Assert.Single(QuestCards.Build(Data(Postman), Active(Letter), Letter)!.Objectives).Handover);
    }

    [Fact]
    public void A_ticked_pickup_keeps_its_mark_what_was_got_still_goes_to_the_trader()
    {
        var card = QuestCards.Build(Data(Scientist), Active(Drive), Drive, ticks: new Dictionary<string, DateOnly> { ["o-pick"] = new(2026, 10, 5) })!;
        var row = Assert.Single(card.Objectives);
        Assert.True(row.Done);
        Assert.NotEqual("", row.Handover);
    }

    // ---- the item card ----

    [Fact]
    public void The_item_card_says_it_once_and_the_hand_over_alone_once_the_find_is_ticked()
    {
        var data = Data(Scientist, Sanitary);
        string Uses(string item, IReadOnlySet<string>? done = null) =>
            string.Join("; ", ItemCards.Build(data, null, Active(Drive, Analyzers), item, done).Uses.Select(u => u.How));
        Assert.Equal("Pick up, then hand over · Customs", Uses(HardDrive));
        Assert.Equal("Find in raid ×3, then hand over", Uses(Analyzer));
        Assert.Equal("Hand over ×3, found in raid", Uses(Analyzer, new HashSet<string> { "o-find" }));
    }

    // ---- the raid card ----

    [Fact]
    public async Task The_raid_card_has_one_line_with_the_mark_where_it_had_two()
    {
        await using var rig = new SessionRig(configure: (paths, locations) => new GameSession(paths, locations) { GivenData = _ => Data(Scientist, Postman) });
        var started = DateTime.Now.AddMinutes(-5);
        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", started);
        foreach (var quest in new[] { Drive, Letter })
            rig.Notification("ChatMessageReceived", QuestStarted(quest, started.AddMinutes(-2)), started.AddMinutes(-2));
        await rig.StartAsync();
        var s = await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid && s.Objectives.Count > 0 &&
                                     s.Quests.GetValueOrDefault(Letter)?.State == QuestState.Active, "the raid and its quests");
        // The pickup carries the hand-over; the letter, got in an earlier quest, keeps its own line.
        Assert.Equal(["o-letter", "o-pick"], s.Objectives.Select(o => o.ObjectiveId).Order());
        Assert.Equal("Hand over to Therapist", s.Objectives.Single(o => o.ObjectiveId == "o-pick").Handover);
        Assert.Equal("", s.Objectives.Single(o => o.ObjectiveId == "o-letter").Handover);
    }

    private static string QuestStarted(string quest, DateTime at) => $$"""
        {
          "type": "new_message",
          "eventId": "test-event-{{quest}}",
          "dialogId": "54cb50c76803fa8b248b4571",
          "message": {
            "_id": "test-message-{{quest}}",
            "type": 10,
            "dt": {{new DateTimeOffset(at).ToUnixTimeSeconds()}},
            "text": "quest",
            "templateId": "{{quest}} description"
          }
        }
        """;
}
