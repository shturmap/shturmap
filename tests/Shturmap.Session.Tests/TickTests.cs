using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Core.Planning;
using Shturmap.Core.Quests;
using Shturmap.Core.Raid;
using Shturmap.Data.TarkovDev;
using Shturmap.Map;

namespace Shturmap.Session.Tests;

// Ticking an objective as done (owner, 2026-10-04): the game's logs never say that a single objective is done, so the
// player may say it. A tick is kept per mode, leaves with its quest, and takes the objective out of the plans, the
// rail and the map's guide; the quest's own state still comes from the logs alone.
public class TickTests
{
    private readonly Dictionary<string, string> _settings = [];

    private ObjectiveTicks NewTicks() => new(key => _settings.GetValueOrDefault(key), (key, value) => _settings[key] = value);

    private static readonly DateOnly Day = new(2026, 10, 4);

    private static QuestStatus Status(string id, QuestState state) => new(id, state, ObservationSource.Log, DateTime.Now);

    // ---- the store ----

    [Fact]
    public void A_tick_is_kept_across_restarts_with_its_day_and_taken_back_by_a_second_click()
    {
        var ticks = NewTicks();
        Assert.True(ticks.Toggle(GameMode.Pve, "o1", Day));
        Assert.True(ticks.Toggle(GameMode.Pve, "o2", Day.AddDays(1)));
        Assert.Equal("o1@2026-10-04,o2@2026-10-05", _settings[ObjectiveTicks.Key(GameMode.Pve)]);

        var again = NewTicks();
        Assert.Equal(Day, again.Of(GameMode.Pve)["o1"]);
        Assert.False(again.Toggle(GameMode.Pve, "o1", Day));
        Assert.Equal(["o2"], NewTicks().Of(GameMode.Pve).Keys);
    }

    [Fact]
    public void Each_mode_has_its_own_ticks() // PvE and PvP progress are separate
    {
        var ticks = NewTicks();
        ticks.Toggle(GameMode.Pve, "o1", Day);
        Assert.Empty(ticks.Of(GameMode.Pvp));
        Assert.Single(ticks.Of(GameMode.Pve));
    }

    [Fact]
    public void A_scripts_tick_shows_but_is_never_saved()
    {
        var ticks = NewTicks();
        Assert.True(ticks.Toggle(GameMode.Pve, "o1", Day, save: false));
        Assert.Contains("o1", ticks.Of(GameMode.Pve).Keys);
        Assert.Empty(NewTicks().Of(GameMode.Pve));
        Assert.False(ticks.Toggle(GameMode.Pve, "o1", Day));
        Assert.Empty(ticks.Of(GameMode.Pve));
    }

    [Fact]
    public void A_tick_without_a_readable_day_is_still_a_tick()
    {
        _settings[ObjectiveTicks.Key(GameMode.Pve)] = "o1,o2@not-a-day,o3@2026-10-04";
        var ticks = NewTicks().Of(GameMode.Pve);
        Assert.Equal(["o1", "o2", "o3"], ticks.Keys.Order());
        Assert.Equal(default, ticks["o1"]);
        Assert.Equal(Day, ticks["o3"]);
        Assert.Equal("Done · ticked by you", QuestCards.TickedText(ticks["o2"]));
    }

    [Fact]
    public void A_tick_leaves_by_itself_when_the_log_reports_its_quest_completed_or_failed()
    {
        var ticks = NewTicks();
        foreach (var id in new[] { "of-done", "of-failed", "of-active", "of-unknown-state", "of-no-quest" })
            ticks.Toggle(GameMode.Pve, id, Day);
        var quests = new Dictionary<string, QuestStatus>
        {
            ["done"] = Status("done", QuestState.Completed),
            ["failed"] = Status("failed", QuestState.Failed),
            ["active"] = Status("active", QuestState.Active),
        };
        // The data knows which quest an objective belongs to, except for one it doesn't list.
        string? QuestOf(string objective) => objective == "of-no-quest" ? null : objective["of-".Length..].Replace("unknown-state", "elsewhere");

        Assert.Equal(["of-done", "of-failed"], ticks.Prune(GameMode.Pve, quests, QuestOf).Order());
        // Only what the log says counts: an objective the data doesn't know stays, and so does one whose quest's state
        // isn't known.
        Assert.Equal(["of-active", "of-no-quest", "of-unknown-state"], NewTicks().Of(GameMode.Pve).Keys.Order());
        Assert.Empty(ticks.Prune(GameMode.Pve, quests, QuestOf));
    }

    // ---- the plan follows from the objectives that are left ----

    private const string TwoPlaces = "aaaaaaaaaaaaaaaaaaaaaa01", Marking = "aaaaaaaaaaaaaaaaaaaaaa02", Handover = "aaaaaaaaaaaaaaaaaaaaaa03";
    private const string Marker = "ms2000", Key = "key114";

    private static ApiObjective Visit(string id, string map, double at) =>
        new(id, "visit", $"Find the place {id}", false, [map], [new ApiZone(id + "-zone", null, map, new ApiPosition(at, 0, at), null, null, null)], null, 1, null, null, null,
            null, null, false);

    private static ApiObjective Mark(string id, string map, double at) =>
        new(id, "mark", $"Mark the place {id}", false, [map], [new ApiZone(id + "-zone", null, map, new ApiPosition(at, 0, at), null, null, null)], null, 1, null, null, Marker,
            null, null, false);

    private static ApiTask Task(string id, string name, List<ApiNeededKeys>? keys, params ApiObjective[] objectives) =>
        new(id, name, null, null, null, null, false, false, null, null, null, false, null, [.. objectives], keys);

    internal static GameData Data(GameMode mode) => new()
    {
        Mode = mode,
        Language = "en",
        Maps = new[]
        {
            new ApiMap("map-customs", "Customs", "customs", "bigmap", "maps/customs_preset.bundle", null, 40, [], [], [], [], []),
            new ApiMap("map-woods", "Woods", "woods", "Woods", "maps/woods_preset.bundle", null, 40, [], [], [], [], []),
        }.ToDictionary(m => m.Id),
        Tasks = new[]
        {
            Task(TwoPlaces, "Two places", [new ApiNeededKeys("map-woods", [Key])], Visit("o-customs", "map-customs", 10), Visit("o-woods", "map-woods", 20)),
            Task(Marking, "Marking", null, Mark("o-mark", "map-customs", 50)),
            Task(Handover, "Handover", null, new ApiObjective("o-give", "giveItem", "Hand over the thing", false, null, null, null, 1, null, ["thing"], null, null, null, false)),
        }.ToDictionary(t => t.Id),
        Traders = new Dictionary<string, ApiTrader>(),
        ItemNames = new Dictionary<string, string> { [Marker] = "MS2000 Marker", [Key] = "Dorm room 114 key", ["thing"] = "Thing" },
        MapDefinitions = new[] { "customs", "woods" }
            .Select(key => new MapDefinition { Key = key, Transform = [1, 0, 1, 0], Bounds = new WorldBox(-500, -500, 500, 500) })
            .ToList(),
        CheckedAt = DateTimeOffset.Now,
    };

    private static readonly string[] Active = [TwoPlaces, Marking, Handover];

    private static IReadOnlySet<string> Done(params string[] objectives) => objectives.ToHashSet();

    private static MapPlanView On(IReadOnlyList<MapPlanView> plans, string map) => plans.Single(p => p.NormalizedName == map);

    [Fact]
    public void Without_ticks_the_plan_is_as_before()
    {
        var data = Data(GameMode.Pve);
        var plans = Planning.Suggest(data, Active);
        Assert.Equal(plans.Select(p => p.NormalizedName), Planning.Suggest(data, Active, done: Done()).Select(p => p.NormalizedName));
        // A quest with a place on each map only progresses on either.
        Assert.Equal([Marking], On(plans, "customs").Finish.Select(q => q.QuestId));
        Assert.Equal([TwoPlaces], On(plans, "customs").Progress.Select(q => q.QuestId));
        Assert.Equal("1 of 2 objectives here", On(plans, "customs").Progress[0].Note);
        Assert.Equal([TwoPlaces], On(plans, "woods").Progress.Select(q => q.QuestId));
    }

    [Fact]
    public void A_ticked_objective_is_left_out_so_the_quest_can_be_completed_where_the_rest_is()
    {
        var data = Data(GameMode.Pve);
        var plans = Planning.Suggest(data, Active, done: Done("o-woods"));
        // What is left of the quest is all on Customs: it can be completed there, and Woods has nothing of it.
        Assert.Equal([TwoPlaces, Marking], On(plans, "customs").Finish.Select(q => q.QuestId).Order());
        Assert.Empty(On(plans, "customs").Progress);
        Assert.DoesNotContain(plans, p => p.NormalizedName == "woods");
        // Its key was for Woods, where nothing of it is left.
        Assert.DoesNotContain(On(plans, "customs").Requirements, r => r.ItemId == Key);
        Assert.Null(Planning.PlanFor(data, Active, "woods", Done("o-woods"))?.Requirements.FirstOrDefault(r => r.ItemId == Key));
    }

    [Fact]
    public void A_quest_with_no_raid_work_left_appears_on_no_map()
    {
        var plans = Planning.Suggest(Data(GameMode.Pve), Active, done: Done("o-customs", "o-woods"));
        Assert.DoesNotContain(plans.SelectMany(p => p.Finish.Concat(p.Progress)), q => q.QuestId == TwoPlaces);
        Assert.Equal([Marking], On(plans, "customs").Finish.Select(q => q.QuestId));
    }

    [Fact]
    public void What_a_ticked_objective_needed_is_no_longer_brought()
    {
        var data = Data(GameMode.Pve);
        Assert.Contains(On(Planning.Suggest(data, Active), "customs").Requirements, r => r.ItemId == Marker);
        var customs = Planning.PlanFor(data, Active, "customs", Done("o-mark"));
        Assert.NotNull(customs);
        Assert.DoesNotContain(customs.Requirements, r => r.ItemId == Marker);
        Assert.Empty(Planning.Kit(customs, new HashSet<string>()).All);
    }

    [Fact]
    public void The_quests_glyph_stays_the_whole_quests_when_objectives_are_ticked()
    {
        var data = Data(GameMode.Pve);
        var before = On(Planning.Suggest(data, Active), "customs").Progress.Single(q => q.QuestId == TwoPlaces).Kind;
        var after = On(Planning.Suggest(data, Active, done: Done("o-woods")), "customs").Finish.Single(q => q.QuestId == TwoPlaces).Kind;
        Assert.Equal(before, after);
    }

    // ---- the quest card says it, and why ----

    private static Dictionary<string, QuestStatus> States(QuestState state, params string[] ids) => ids.ToDictionary(id => id, id => Status(id, state));

    [Fact]
    public void A_ticked_objective_reads_as_done_on_the_card_and_says_where_that_comes_from()
    {
        var data = Data(GameMode.Pve);
        var ticks = new Dictionary<string, DateOnly> { ["o-woods"] = Day };
        var card = QuestCards.Build(data, States(QuestState.Active, TwoPlaces), TwoPlaces, live: _ => "120 m · NE", ticks: ticks)!;
        var open = card.Objectives.Single(o => o.ObjectiveId == "o-customs");
        var done = card.Objectives.Single(o => o.ObjectiveId == "o-woods");

        Assert.False(open.Done);
        Assert.Equal("120 m · NE", open.Live);
        Assert.True(open.Tickable);

        Assert.True(done.Done);
        // Not measured any more: in the place of its distance, that it is done and by whose word.
        Assert.Equal("", done.Live);
        Assert.Equal(QuestCards.TickedText(Day), done.Ticked);
        Assert.StartsWith("Done · ticked by you, ", done.Ticked);
        Assert.True(done.Tickable);
        // The quest itself is as the log says: still active.
        Assert.Equal(QuestState.Active, card.State);
    }

    [Fact]
    public void The_cards_bring_lists_only_what_open_objectives_need()
    {
        var data = Data(GameMode.Pve);
        var states = States(QuestState.Active, TwoPlaces, Marking);
        Assert.Contains(QuestCards.Build(data, states, Marking)!.Needs, n => n.ItemId == Marker);
        Assert.Empty(QuestCards.Build(data, states, Marking, ticks: new Dictionary<string, DateOnly> { ["o-mark"] = Day })!.Needs);
        // A quest-level key is for a map: it goes when nothing of the quest is left open there.
        Assert.Contains(QuestCards.Build(data, states, TwoPlaces)!.Needs, n => n.ItemId == Key);
        Assert.DoesNotContain(QuestCards.Build(data, states, TwoPlaces, ticks: new Dictionary<string, DateOnly> { ["o-woods"] = Day })!.Needs, n => n.ItemId == Key);
        Assert.Contains(QuestCards.Build(data, states, TwoPlaces, ticks: new Dictionary<string, DateOnly> { ["o-customs"] = Day })!.Needs, n => n.ItemId == Key);
    }

    [Fact]
    public void Only_an_active_quests_objectives_offer_a_tick()
    {
        var data = Data(GameMode.Pve);
        Assert.All(QuestCards.Build(data, States(QuestState.Completed, Marking), Marking)!.Objectives, o => Assert.False(o.Tickable));
        Assert.All(QuestCards.Build(data, new Dictionary<string, QuestStatus>(), Marking)!.Objectives, o => Assert.False(o.Tickable));
    }

    [Fact]
    public void The_item_card_drops_the_use_of_a_ticked_objective()
    {
        var data = Data(GameMode.Pve);
        var states = States(QuestState.Active, Marking);
        Assert.Single(ItemCards.Build(data, null, states, Marker).Uses);
        Assert.Empty(ItemCards.Build(data, null, states, Marker, Done("o-mark")).Uses);
    }

    // ---- the session: the rail and the map leave a ticked objective out ----

    private static string QuestMessage(string quest, int type, DateTime at) => $$"""
        {
          "type": "new_message",
          "eventId": "test-event-{{quest}}-{{type}}",
          "dialogId": "54cb50c76803fa8b248b4571",
          "message": {
            "_id": "test-message-{{quest}}-{{type}}",
            "type": {{type}},
            "dt": {{new DateTimeOffset(at).ToUnixTimeSeconds()}},
            "text": "quest",
            "templateId": "{{quest}} description"
          }
        }
        """;

    private static SessionRig Rig() => new(configure: (paths, locations) => new GameSession(paths, locations) { GivenData = Data });

    [Fact]
    public async Task A_tick_takes_the_objective_out_of_the_rail_and_marks_it_done_on_the_map()
    {
        await using var rig = Rig();
        var started = DateTime.Now.AddMinutes(-5);
        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", started);
        rig.Notification("ChatMessageReceived", QuestMessage(TwoPlaces, 10, started.AddMinutes(-2.5)), started.AddMinutes(-2.5));
        rig.Notification("ChatMessageReceived", QuestMessage(Marking, 10, started.AddMinutes(-2.4)), started.AddMinutes(-2.4));
        await rig.StartAsync();
        await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid && s.Quests.GetValueOrDefault(Marking)?.State == QuestState.Active
                             && s.Quests.GetValueOrDefault(TwoPlaces)?.State == QuestState.Active && s.RaidMap is not null, "the raid and both quests");
        rig.Screenshot(12, 1, 12);
        var before = await rig.Until(s => s.Objectives.Any(o => o.Distance is not null), "distances from the position");
        // The nearest objective is the place on Customs, at (10, 0, 10).
        Assert.Equal("o-customs", before.Objectives.Where(o => o.HasPlace && o.Distance is not null && !o.Done).MinBy(o => o.Distance)!.ObjectiveId);
        Assert.Contains(before.Content!.Markers, m => m.Id.StartsWith("objective:o-customs", StringComparison.Ordinal) && m.Kind == MarkerKind.Objective);

        await rig.Session.ToggleTickAsync("o-customs", "test");
        var after = rig.Snapshot;
        Assert.Contains("o-customs", after.Done);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), after.Ticks["o-customs"]);
        var ticked = after.Objectives.Single(o => o.ObjectiveId == "o-customs");
        // Done, and no longer measured: no place to go to.
        Assert.True(ticked.Done);
        Assert.Null(ticked.Distance);
        Assert.Equal("o-mark", after.Objectives.Where(o => o.HasPlace && o.Distance is not null && !o.Done).MinBy(o => o.Distance)!.ObjectiveId);
        // The map keeps its place, drawn as done.
        Assert.Contains(after.Content!.Markers, m => m.Id.StartsWith("objective:o-customs", StringComparison.Ordinal) && m.Kind == MarkerKind.ObjectiveDone);
        // Nothing of the quest is left on this map: the raid's plan lists only the other quest.
        Assert.Equal([Marking], after.MapPlan!.Finish.Concat(after.MapPlan.Progress).Select(q => q.QuestId));
        // The quest itself is as the log says.
        Assert.Equal(QuestState.Active, after.Quests[TwoPlaces].State);

        // A second click takes it back.
        await rig.Session.ToggleTickAsync("o-customs", "test");
        Assert.DoesNotContain("o-customs", rig.Snapshot.Done);
        Assert.NotNull(rig.Snapshot.Objectives.Single(o => o.ObjectiveId == "o-customs").Distance);
    }

    [Fact]
    public async Task Only_an_active_quests_objective_can_be_ticked_and_the_tick_leaves_with_the_quest()
    {
        await using var rig = Rig();
        var started = DateTime.Now.AddMinutes(-20);
        rig.Log("Session mode: Pve", started);
        rig.Log("PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0", started);
        rig.Notification("ChatMessageReceived", QuestMessage(Marking, 10, started.AddMinutes(1)), started.AddMinutes(1));
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null && s.Quests.GetValueOrDefault(Marking)?.State == QuestState.Active && s.Raid.Mode == GameMode.Pve, "the quest");

        // A quest the log hasn't seen started: nothing to tick. An objective the data doesn't know: neither.
        await rig.Session.ToggleTickAsync("o-customs", "test");
        await rig.Session.ToggleTickAsync("no-such-objective", "test");
        Assert.Empty(rig.Snapshot.Ticks);

        await rig.Session.ToggleTickAsync("o-mark", "test");
        Assert.Contains("o-mark", rig.Snapshot.Done);
        Assert.DoesNotContain(rig.Snapshot.Plan.SelectMany(p => p.Finish.Concat(p.Progress)), q => q.QuestId == Marking);

        // The log reports the quest completed: its tick has nothing left to leave out, and goes.
        rig.Notification("ChatMessageReceived", QuestMessage(Marking, 12, DateTime.Now));
        var done = await rig.Until(s => s.Quests.GetValueOrDefault(Marking)?.State == QuestState.Completed, "the quest completed");
        Assert.Empty(done.Ticks);
        Assert.Empty(done.Done);
    }
}
