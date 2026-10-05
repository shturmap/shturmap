using Shturmap.Core;
using Shturmap.Core.Planning;
using Shturmap.Core.Quests;
using Shturmap.Core.Raid;

namespace Shturmap.Session.Tests;

// The rail and status bar say only what the logs and the data show (owner, 2026-10-03): the raid state never claims
// the menus, PROGRESS rows say why they only progress instead of being dimmed, and a map card carries no walking time.
public class RailWordsTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 22, 0, 0);

    // ---- the raid state ----

    [Fact]
    public void Outside_a_raid_the_state_is_not_in_a_raid_never_the_menus()
    {
        var menu = new RaidState();
        Assert.Equal("Not in a raid", RaidStatus.Text(menu, "Customs", Now));
        Assert.Contains("can't tell the menus from a closed game", RaidStatus.Tooltip(menu, Now));
        Assert.DoesNotContain("menus", RaidStatus.Text(menu, null, Now), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Loading_and_in_raid_say_the_map_side_and_minutes()
    {
        Assert.Equal("Loading Streets of Tarkov", RaidStatus.Text(new RaidState { Phase = RaidPhase.Loading }, "Streets of Tarkov", Now));
        Assert.Equal("Loading", RaidStatus.Text(new RaidState { Phase = RaidPhase.Loading }, null, Now));
        var raid = new RaidState { Phase = RaidPhase.InRaid, Side = RaidSide.Pmc, RaidStartedAt = Now.AddMinutes(-12) };
        Assert.Equal("In raid · Streets of Tarkov · PMC · 12 min", RaidStatus.Text(raid, "Streets of Tarkov", Now));
        Assert.Equal("The game's log shows a raid, started 21:48", RaidStatus.Tooltip(raid, Now));
        Assert.Equal("In raid · Customs · Scav", RaidStatus.Text(new RaidState { Phase = RaidPhase.InRaid, Side = RaidSide.Scav }, "Customs", Now));
    }

    // ---- why a PROGRESS row only progresses ----

    private static PlanObjective Obj(string id, ObjectiveKind kind, string map, int count = 1, bool optional = false) =>
        new(id, kind, map.Length == 0 ? [] : [map], new Dictionary<string, IReadOnlyList<WorldPoint>>(), count, optional, [], []);

    private static QuestOnMap OnCustoms(PlanObjective[] all, params string[] hereIds) =>
        new(new PlanQuest("q", "Q", all, new Dictionary<string, IReadOnlyList<string>>()), all.Where(o => hereIds.Contains(o.Id)).ToList());

    [Fact]
    public void A_quest_with_objectives_elsewhere_says_how_many_are_here()
    {
        PlanObjective[] all =
        [
            Obj("a", ObjectiveKind.Exploration, "customs"), Obj("b", ObjectiveKind.Exploration, "customs"),
            Obj("c", ObjectiveKind.Exploration, "woods"), Obj("d", ObjectiveKind.Pickup, "woods"),
            Obj("e", ObjectiveKind.Exploration, "shoreline"), Obj("f", ObjectiveKind.Trader, ""),
            Obj("g", ObjectiveKind.Exploration, "woods", optional: true),
        ];
        // The hand-over and the optional objective don't count.
        Assert.Equal("2 of 5 objectives here", Planning.ProgressNote(OnCustoms(all, "a", "b")));
    }

    [Fact]
    public void Found_in_raid_items_and_large_kill_counts_are_said_as_facts()
    {
        PlanObjective[] all = [Obj("a", ObjectiveKind.FindInRaid, "") with { FoundInRaid = true }, Obj("b", ObjectiveKind.Elimination, "customs", count: 25)];
        Assert.Equal("needs items found in raid · 25 kills in all", Planning.ProgressNote(OnCustoms(all, "a", "b")));
        Assert.Equal(new ProgressFacts(2, 2, true, 25), RaidPlanner.WhyProgress(OnCustoms(all, "a", "b")));
        // Only where the data says so: an item that may be bought isn't "found in raid".
        PlanObjective[] bought = [Obj("a", ObjectiveKind.FindInRaid, ""), Obj("b", ObjectiveKind.Elimination, "customs", count: 25)];
        Assert.Equal("25 kills in all", Planning.ProgressNote(OnCustoms(bought, "a", "b")));
    }

    [Fact]
    public void A_quest_that_completes_here_has_no_note()
    {
        PlanObjective[] all = [Obj("a", ObjectiveKind.Exploration, "customs"), Obj("b", ObjectiveKind.Elimination, "customs", count: 3)];
        Assert.Equal("", Planning.ProgressNote(OnCustoms(all, "a", "b")));
    }

    // ---- no walking time ----

    [Fact]
    public void A_map_card_says_the_raid_length_and_bosses_and_no_walking_time()
    {
        var plan = new MapPlanView("streets-of-tarkov", "Streets of Tarkov", [], [], [], 40, ["Kaban 75%", "Kollontay 75%"]);
        Assert.Equal("40 min raid · Kaban 75% · Kollontay 75%", Planning.FactsLine(plan));
        Assert.Equal("Kaban 75%", Planning.FactsLine(plan with { RaidMinutes = 0, Bosses = ["Kaban 75%"] }));
        // The window shows the line part by part (each boss is linked to its markers): the length is the first part.
        Assert.Equal("40 min raid", Planning.LengthText(plan));
        Assert.Equal("", Planning.LengthText(plan with { RaidMinutes = 0 }));
        Assert.Null(typeof(MapPlanView).GetProperty("WalkingMinutes"));
        Assert.Null(typeof(MapPlan).GetProperty("WalkingMinutes"));
    }
}
