using Shturmap.Core.Planning;
using Shturmap.Core.Quests;

namespace Shturmap.Core.Tests;

// The plan's order (owner, 2026-10-03): going somewhere, then finding or surviving, then fighting; simpler first, then
// the game's trader order, then the name. Only tarkov.dev's structured fields count.
public class EffortTests
{
    private static readonly PlanMap Customs = new("customs", "Customs", new HashSet<string> { "customs" }, 35);
    private static readonly Dictionary<string, IReadOnlyList<string>> NoKeys = new();

    private static PlanObjective Obj(string type, string[]? targets = null, string[]? conditions = null, string[]? exit = null,
        bool fir = false, int count = 1, string id = "o") =>
        new(id, QuestTaxonomy.Classify(type), ["customs"], new Dictionary<string, IReadOnlyList<WorldPoint>>(), count, false, [], [],
            Type: type, Targets: targets ?? [], ExitStatus: exit ?? [], Conditions: conditions ?? [], FoundInRaid: fir);

    [Theory]
    [InlineData("visit")]
    [InlineData("mark")]
    [InlineData("plantItem")]
    [InlineData("plantQuestItem")]
    [InlineData("useItem")]
    [InlineData("findQuestItem")]
    public void Reaching_a_place_and_acting_there_is_going_there(string type) =>
        Assert.Equal(EffortGroup.GoThere, QuestEffort.GroupOf(Obj(type)));

    [Fact]
    public void Extracting_through_an_exit_is_a_trip_but_surviving_is_not()
    {
        Assert.Equal(EffortGroup.GoThere, QuestEffort.GroupOf(Obj("extract", exit: ["marathon Name"])));
        Assert.Equal(EffortGroup.FindOrSurvive, QuestEffort.GroupOf(Obj("extract", exit: ["ExpBonusSurvived", "marathon Name"])));
    }

    [Fact]
    public void Items_found_in_raid_and_health_conditions_are_finding_or_surviving()
    {
        Assert.Equal(EffortGroup.FindOrSurvive, QuestEffort.GroupOf(Obj("findItem", fir: true)));
        Assert.Equal(EffortGroup.GoThere, QuestEffort.GroupOf(Obj("findItem")));
        Assert.Equal(EffortGroup.FindOrSurvive, QuestEffort.GroupOf(Obj("experience")));
    }

    [Theory]
    [InlineData("Savage")]
    [InlineData("Marksman")]
    [InlineData("assaultGroup")]
    [InlineData("Any")]
    public void Scav_kills_without_conditions_are_finding_or_surviving(string target) =>
        Assert.Equal(EffortGroup.FindOrSurvive, QuestEffort.GroupOf(Obj("shoot", [target])));

    [Theory]
    [InlineData("AnyPmc")]
    [InlineData("Bear")]
    [InlineData("ExUsec")]
    [InlineData("PmcBot")]
    [InlineData("pmcBotBlackDiv")]
    [InlineData("bossKnight")]
    [InlineData("followerBigPipe")]
    [InlineData("sectantPriest")]
    [InlineData("infectedTagilla")]
    public void Kills_of_pmcs_rogues_raiders_cultists_and_bosses_are_fights(string target)
    {
        Assert.Equal(EffortGroup.Fight, QuestEffort.GroupOf(Obj("shoot", [target])));
        Assert.True(QuestEffort.IsKnownTarget(target));
    }

    [Fact]
    public void A_kill_with_a_condition_or_an_unknown_target_is_a_fight()
    {
        Assert.Equal(EffortGroup.Fight, QuestEffort.GroupOf(Obj("shoot", ["Savage"], ["weapon"])));
        Assert.Equal(EffortGroup.Fight, QuestEffort.GroupOf(Obj("shoot", ["Savage", "AnyPmc"])));
        Assert.Equal(EffortGroup.Fight, QuestEffort.GroupOf(Obj("shoot", ["somethingNew"])));
        Assert.False(QuestEffort.IsKnownTarget("somethingNew"));
        Assert.Equal(EffortGroup.Fight, QuestEffort.GroupOf(Obj("shoot")));
    }

    [Fact]
    public void Known_objective_types_are_known()
    {
        Assert.True(QuestEffort.IsKnownType("visit"));
        Assert.True(QuestEffort.IsKnownType("giveItem"));
        Assert.False(QuestEffort.IsKnownType("somethingNew"));
        Assert.False(QuestEffort.IsKnownType(null));
    }

    [Fact]
    public void A_quests_effort_is_its_most_demanding_objective_then_steps_conditions_and_kill_count()
    {
        var quest = new PlanQuest("q", "Q", [], NoKeys);
        var effort = QuestEffort.Of(new QuestOnMap(quest,
        [
            Obj("visit", id: "a"),
            Obj("shoot", ["Savage"], ["weapon", "zone"], count: 6, id: "b"),
        ]));
        Assert.Equal(new Effort(EffortGroup.Fight, 2, 2, 2), effort);

        Assert.Equal(1, QuestEffort.Of(new QuestOnMap(quest, [Obj("shoot", ["Savage"], count: 5)])).KillBucket);
        Assert.Equal(2, QuestEffort.Of(new QuestOnMap(quest, [Obj("shoot", ["Savage"], count: 15)])).KillBucket);
        Assert.Equal(3, QuestEffort.Of(new QuestOnMap(quest, [Obj("shoot", ["Savage"], count: 16)])).KillBucket);
        Assert.Equal(0, QuestEffort.Of(new QuestOnMap(quest, [Obj("visit")])).KillBucket);
    }

    [Fact]
    public void A_plan_section_is_in_effort_order_then_trader_then_name()
    {
        PlanQuest Quest(string name, int trader, params PlanObjective[] objectives) => new(name, name, objectives, NoKeys, trader);
        var quests = new List<PlanQuest>
        {
            Quest("Kill Knight", 0, Obj("shoot", ["bossKnight"])),
            Quest("Scav hunt", 0, Obj("shoot", ["Savage"], count: 3)),
            Quest("Two visits", 0, Obj("visit", id: "v1"), Obj("visit", id: "v2")),
            Quest("Visit for Skier", 3, Obj("visit")),
            Quest("Visit for Prapor B", 0, Obj("visit")),
            Quest("Visit for Prapor A", 0, Obj("visit")),
            Quest("Survive", 0, Obj("extract", exit: ["ExpBonusSurvived"])),
        };
        var plan = RaidPlanner.Plan(quests, Customs);
        Assert.Equal(
            // Surviving (no kills) is simpler than three Scav kills: the kill count comes last in the tuple.
            ["Visit for Prapor A", "Visit for Prapor B", "Visit for Skier", "Two visits", "Survive", "Scav hunt", "Kill Knight"],
            plan.Finish.Select(q => q.Quest.Name));
    }
}
