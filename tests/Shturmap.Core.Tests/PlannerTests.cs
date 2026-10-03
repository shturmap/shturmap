using Shturmap.Core.Planning;
using Shturmap.Core.Quests;

namespace Shturmap.Core.Tests;

public class TaxonomyTests
{
    [Theory]
    [InlineData("shoot", ObjectiveKind.Elimination)]
    [InlineData("visit", ObjectiveKind.Exploration)]
    [InlineData("findQuestItem", ObjectiveKind.Pickup)]
    [InlineData("plantItem", ObjectiveKind.Place)]
    [InlineData("mark", ObjectiveKind.Place)]
    [InlineData("useItem", ObjectiveKind.Place)]
    [InlineData("findItem", ObjectiveKind.FindInRaid)]
    [InlineData("extract", ObjectiveKind.Survive)]
    [InlineData("experience", ObjectiveKind.Survive)]
    [InlineData("giveQuestItem", ObjectiveKind.Trader)]
    [InlineData("buildWeapon", ObjectiveKind.Trader)]
    [InlineData("somethingNew", ObjectiveKind.Trader)]
    public void Classifies_tarkov_dev_objective_types(string type, ObjectiveKind expected) =>
        Assert.Equal(expected, QuestTaxonomy.Classify(type));

    [Fact]
    public void Quest_kind_is_its_most_common_in_raid_objective()
    {
        Assert.Equal(ObjectiveKind.Pickup, QuestTaxonomy.QuestKind([ObjectiveKind.Pickup, ObjectiveKind.Trader, ObjectiveKind.Trader]));
        Assert.Equal(ObjectiveKind.Elimination, QuestTaxonomy.QuestKind([ObjectiveKind.Exploration, ObjectiveKind.Elimination])); // tie: Kill first
        Assert.Equal(ObjectiveKind.Trader, QuestTaxonomy.QuestKind([ObjectiveKind.Trader]));
    }
}

public class PlannerTests
{
    private static readonly PlanMap Streets = new("streets", "Streets of Tarkov", new HashSet<string> { "streets" }, 40);
    private static readonly PlanMap Customs = new("customs", "Customs", new HashSet<string> { "customs" }, 35);
    private static readonly PlanMap GroundZero = new("gz", "Ground Zero", new HashSet<string> { "gz", "gz21" }, 30);

    private static readonly Dictionary<string, IReadOnlyList<string>> NoKeys = new();

    private static PlanObjective Obj(string id, ObjectiveKind kind, string[] maps, (string Map, WorldPoint At)[]? places = null,
        int count = 1, string[][]? keys = null, (string, int)[]? bring = null) =>
        new(id, kind, maps,
            (places ?? []).GroupBy(p => p.Map).ToDictionary(g => g.Key, g => (IReadOnlyList<WorldPoint>)g.Select(p => p.At).ToList()),
            count, false, (keys ?? []).Select(k => (IReadOnlyList<string>)k).ToList(), bring ?? []);

    private static readonly PlanQuest Audit = new("audit", "Audit",
    [
        Obj("a1", ObjectiveKind.Pickup, ["streets"], [("streets", new WorldPoint(-177, 6, 227))], keys: [["hotel-key"]]),
        Obj("a2", ObjectiveKind.Trader, []),
    ], NoKeys);

    private static readonly PlanQuest Revision = new("revision", "Revision - Streets of Tarkov",
    [
        Obj("r1", ObjectiveKind.Place, ["streets"], [("streets", new WorldPoint(0, 0, 0))], bring: [("ms2000", 1)]),
        Obj("r2", ObjectiveKind.Place, ["streets"], [("streets", new WorldPoint(0, 0, 300))], bring: [("ms2000", 1)]),
    ], NoKeys);

    private static readonly PlanQuest Swift = new("swift", "Swift",
    [
        Obj("s1", ObjectiveKind.Elimination, ["woods"], count: 5),
    ], NoKeys);

    private static readonly PlanQuest AnyKills = new("kills", "Kill 2 PMCs anywhere",
    [
        Obj("k1", ObjectiveKind.Elimination, [], count: 2),
    ], NoKeys);

    [Fact]
    public void Finishable_quests_rank_a_map_first()
    {
        var plans = RaidPlanner.Rank([Audit, Revision, Swift, AnyKills], [Customs, Streets]);

        Assert.Equal("streets", plans[0].Map.Id);
        Assert.Equal(new[] { "audit", "revision" }, plans[0].Finish.Select(f => f.Quest.Id).ToArray());
        Assert.DoesNotContain(plans, p => p.Map.Id == "customs"); // only work that fits any map
        Assert.DoesNotContain(plans, p => p.Finish.Any(f => f.Quest.Id == "swift"));
        Assert.Equal("kills", Assert.Single(RaidPlanner.AnyMap([Audit, Revision, Swift, AnyKills])).Id);
    }

    [Fact]
    public void Found_in_raid_items_never_make_a_quest_finishable()
    {
        var quest = new PlanQuest("ice", "Ice Cream Cones",
        [
            Obj("i1", ObjectiveKind.Exploration, ["streets"], [("streets", new WorldPoint(5, 0, 5))]),
            Obj("i2", ObjectiveKind.FindInRaid, [], count: 1),
        ], NoKeys);
        var plan = RaidPlanner.Plan([quest], Streets);
        Assert.Empty(plan.Finish);
        Assert.Equal("ice", Assert.Single(plan.Progress).Quest.Id);
    }

    [Fact]
    public void Large_kill_counts_are_progress_not_completion()
    {
        var woods = new PlanMap("woods", "Woods", new HashSet<string> { "woods" }, 35);
        var plan = RaidPlanner.Plan([Swift], woods);
        Assert.Empty(plan.Finish);
        Assert.Equal("swift", Assert.Single(plan.Progress).Quest.Id);
    }

    [Fact]
    public void Requirements_are_summed_and_name_their_quests()
    {
        var plan = RaidPlanner.Plan([Audit, Revision], Streets);

        var key = Assert.Single(plan.Requirements, r => r.Kind == RequirementKind.Key);
        Assert.Equal(new[] { "hotel-key" }, key.Alternatives);
        Assert.Equal(new[] { "audit" }, key.ForQuests);
        var marker = Assert.Single(plan.Requirements, r => r.Kind == RequirementKind.Bring);
        Assert.Equal("ms2000", marker.Alternatives[0]);
        Assert.Equal(2, marker.Count);
    }

    [Fact]
    public void Gear_to_wear_is_a_requirement_once_per_set_of_choices()
    {
        string[][] beanieAndGlasses = [["beanie"], ["glasses"]];
        var kill = new PlanObjective("d1", ObjectiveKind.Elimination, ["streets"], new Dictionary<string, IReadOnlyList<WorldPoint>>(), 1, false, [], [],
            beanieAndGlasses.Select(s => (IReadOnlyList<string>)s).ToList());
        var dandies = new PlanQuest("dandies", "Dandies", [kill], NoKeys);
        var other = new PlanQuest("other", "Other", [kill with { Id = "o1" }], NoKeys);

        var plan = RaidPlanner.Plan([dandies, other], Streets);

        var wear = Assert.Single(plan.Requirements, r => r.Kind == RequirementKind.Wear);
        Assert.Equal(new[] { "beanie", "glasses" }, wear.Alternatives);
        Assert.Equal(new[] { "dandies", "other" }, wear.ForQuests.Order().ToArray());
        Assert.Equal(1, wear.Count); // worn, not used up
    }

    [Fact]
    public void Map_variants_count_as_one_map()
    {
        var quest = new PlanQuest("gzq", "Ground Zero quest", [Obj("g1", ObjectiveKind.Exploration, ["gz21"], [("gz21", new WorldPoint(1, 0, 1))])], NoKeys);
        Assert.Equal("gz", Assert.Single(RaidPlanner.Rank([quest], [GroundZero, Streets])).Map.Id);
    }

    [Fact]
    public void Route_length_for_ranking_ties_visits_each_place_once()
    {
        // Only breaks ties between maps; never shown as a walking time (owner, 2026-10-03).
        var plan = RaidPlanner.Plan([Revision], Streets);
        Assert.Equal(300, plan.RouteMeters, 3);
    }
}
