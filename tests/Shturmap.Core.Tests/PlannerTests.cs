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

    // Six maps with one quest each, the earlier ones worth more: "m1" ranks first, "m6" last.
    private static (List<PlanQuest> Quests, List<PlanMap> Maps) SixMaps()
    {
        var maps = Enumerable.Range(1, 6).Select(i => new PlanMap($"m{i}", $"Map {i}", new HashSet<string> { $"m{i}" }, 40)).ToList();
        var quests = Enumerable.Range(1, 6).Select(i => new PlanQuest($"q{i}", $"Quest {i}",
            Enumerable.Range(0, 7 - i).Select(n => Obj($"q{i}-{n}", ObjectiveKind.Exploration, [$"m{i}"], [($"m{i}", new WorldPoint(n, 0, 0))])).ToList(),
            NoKeys)).ToList();
        return (quests, maps);
    }

    [Fact]
    public void A_pick_on_a_map_below_the_best_four_is_still_suggested_first()
    {
        var (quests, maps) = SixMaps();
        Assert.Equal(["m1", "m2", "m3", "m4"], RaidPlanner.Rank(quests, maps).Select(p => p.Map.Id));

        // The player's plan before the planner's: the sixth map leads, and the best three fill up to four.
        var picked = RaidPlanner.Rank(quests, maps, picks: new HashSet<string> { "q6" });
        Assert.Equal(["m6", "m1", "m2", "m3"], picked.Select(p => p.Map.Id));

        // Several maps with picks: the planner's order among them, all shown, then the rest up to four.
        Assert.Equal(["m5", "m6", "m1", "m2"], RaidPlanner.Rank(quests, maps, picks: new HashSet<string> { "q6", "q5" }).Select(p => p.Map.Id));
        // A quest that isn't on any map (or isn't active) changes nothing.
        Assert.Equal(["m1", "m2", "m3", "m4"], RaidPlanner.Rank(quests, maps, picks: new HashSet<string> { "elsewhere" }).Select(p => p.Map.Id));
    }

    [Fact]
    public void Maps_with_picks_are_all_shown_most_picks_first()
    {
        var (quests, maps) = SixMaps();
        // A second quest on the sixth map: two picks there, one each on the others.
        quests.Add(new PlanQuest("q6b", "Quest 6b", [Obj("q6b-0", ObjectiveKind.Exploration, ["m6"], [("m6", new WorldPoint(9, 0, 0))])], NoKeys));
        var picks = new HashSet<string> { "q2", "q3", "q4", "q5", "q6", "q6b" };
        Assert.Equal(["m6", "m2", "m3", "m4", "m5"], RaidPlanner.Rank(quests, maps, picks: picks).Select(p => p.Map.Id));
    }

    [Fact]
    public void Found_in_raid_items_never_make_a_quest_finishable()
    {
        var quest = new PlanQuest("ice", "Ice Cream Cones",
        [
            Obj("i1", ObjectiveKind.Exploration, ["streets"], [("streets", new WorldPoint(5, 0, 5))]),
            Obj("i2", ObjectiveKind.FindInRaid, [], count: 1) with { FoundInRaid = true },
        ], NoKeys);
        var plan = RaidPlanner.Plan([quest], Streets);
        Assert.Empty(plan.Finish);
        var progress = Assert.Single(plan.Progress);
        Assert.Equal("ice", progress.Quest.Id);
        Assert.True(RaidPlanner.WhyProgress(progress).FoundInRaid);
    }

    [Fact]
    public void An_item_that_may_be_bought_doesnt_keep_a_quest_from_being_finishable()
    {
        // tarkov.dev's findItem with foundInRaid false: the item takes no luck, so it isn't "needs items found in raid".
        var quest = new PlanQuest("buy", "Bring a Salewa",
        [
            Obj("b1", ObjectiveKind.Exploration, ["streets"], [("streets", new WorldPoint(5, 0, 5))]),
            Obj("b2", ObjectiveKind.FindInRaid, [], count: 1) with { Type = "findItem", FoundInRaid = false },
        ], NoKeys);
        var plan = RaidPlanner.Plan([quest], Streets);
        Assert.Empty(plan.Progress);
        var finish = Assert.Single(plan.Finish);
        Assert.Equal("buy", finish.Quest.Id);
        Assert.False(RaidPlanner.WhyProgress(finish).FoundInRaid);
        // The plan's order makes the same cut: a trip at most, not luck.
        Assert.Equal(EffortGroup.GoThere, QuestEffort.Of(finish).Group);
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
