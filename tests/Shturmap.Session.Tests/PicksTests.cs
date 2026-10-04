using Shturmap.Core.Logs;
using Shturmap.Core.Planning;
using Shturmap.Core.Quests;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

// Picks: several quests chosen for the coming raid, first everywhere, no upkeep (owner, 2026-10-03).
public class PicksTests
{
    private readonly Dictionary<string, string> _settings = [];

    private QuestPicks NewPicks() => new(key => _settings.GetValueOrDefault(key), (key, value) => _settings[key] = value);

    private static QuestStatus Status(string id, QuestState state) => new(id, state, ObservationSource.Log, DateTime.Now);

    [Fact]
    public void A_pick_is_kept_across_restarts_and_unpicked_by_a_second_click()
    {
        var picks = NewPicks();
        Assert.True(picks.Toggle(GameMode.Pve, "q1"));
        Assert.True(picks.Toggle(GameMode.Pve, "q2"));
        Assert.Equal(["q1", "q2"], NewPicks().Of(GameMode.Pve).Order());

        var again = NewPicks();
        Assert.False(again.Toggle(GameMode.Pve, "q1"));
        Assert.Equal(["q2"], NewPicks().Of(GameMode.Pve));
    }

    [Fact]
    public void Each_mode_has_its_own_picks() // PvE and PvP progress are separate
    {
        var picks = NewPicks();
        picks.Toggle(GameMode.Pve, "q1");
        Assert.Empty(picks.Of(GameMode.Pvp));
        Assert.Equal("q1", _settings[QuestPicks.Key(GameMode.Pve)]);
    }

    [Fact]
    public void A_snapshots_pick_shows_but_is_never_saved()
    {
        var picks = NewPicks();
        Assert.True(picks.Toggle(GameMode.Pve, "q1", save: false));
        Assert.Contains("q1", picks.Of(GameMode.Pve));
        Assert.Empty(NewPicks().Of(GameMode.Pve));
        Assert.False(picks.Toggle(GameMode.Pve, "q1"));
        Assert.Empty(picks.Of(GameMode.Pve));
    }

    [Fact]
    public void A_quest_the_log_reports_done_or_failed_leaves_the_picks_by_itself()
    {
        var picks = NewPicks();
        foreach (var id in new[] { "done", "failed", "active", "unknown" })
            picks.Toggle(GameMode.Pve, id);
        var quests = new Dictionary<string, QuestStatus>
        {
            ["done"] = Status("done", QuestState.Completed),
            ["failed"] = Status("failed", QuestState.Failed),
            ["active"] = Status("active", QuestState.Active),
        };
        Assert.Equal(["done", "failed"], picks.Prune(GameMode.Pve, quests).Order());
        // Only what the log says counts: a quest whose state isn't known stays picked.
        Assert.Equal(["active", "unknown"], NewPicks().Of(GameMode.Pve).Order());
        Assert.Empty(picks.Prune(GameMode.Pve, quests));
    }

    [Fact]
    public void Clear_unpicks_everything_for_the_mode_only()
    {
        var picks = NewPicks();
        picks.Toggle(GameMode.Pve, "q1");
        picks.Toggle(GameMode.Pvp, "q2");
        picks.Clear(GameMode.Pve);
        Assert.Empty(NewPicks().Of(GameMode.Pve));
        Assert.Equal(["q2"], NewPicks().Of(GameMode.Pvp));
    }

    // ---- the rail ----

    private static PlanQuestView Row(string id, EffortGroup group, bool startsGroup = false, string note = "") =>
        new(id, id, ObjectiveKind.Exploration, Group: group, StartsGroup: startsGroup, Note: note);

    private static MapPlanView Plan() => new("customs", "Customs",
        [Row("A", EffortGroup.GoThere), Row("B", EffortGroup.FindOrSurvive, true), Row("C", EffortGroup.Fight, true)],
        [Row("D", EffortGroup.GoThere, note: "2 of 5 objectives here"), Row("E", EffortGroup.Fight, true, "25 kills in all")],
        [], 35, []);

    [Fact]
    public void Picks_come_first_in_their_own_group_and_the_sections_keep_the_rest()
    {
        var sections = Planning.Sections(Plan(), new HashSet<string> { "B", "D" });
        // Those that can be completed first, then those that only progress, which keep their note.
        Assert.Equal(["B", "D"], sections.Picks.Select(q => q.QuestId));
        Assert.Equal("2 of 5 objectives here", sections.Picks[1].Note);
        Assert.All(sections.Picks, q => Assert.False(q.StartsGroup));
        Assert.Equal(["A", "C"], sections.Finish.Select(q => q.QuestId));
        Assert.Equal(["E"], sections.Progress.Select(q => q.QuestId));
        // The hairlines are drawn anew for what is left: C now follows A, a different group; E is alone.
        Assert.Equal([false, true], sections.Finish.Select(q => q.StartsGroup));
        Assert.False(sections.Progress[0].StartsGroup);
    }

    [Fact]
    public void Without_picks_the_sections_are_as_planned()
    {
        var plan = Plan();
        var sections = Planning.Sections(plan, new HashSet<string>());
        Assert.Empty(sections.Picks);
        Assert.Same(plan.Finish, sections.Finish);
        Assert.Same(plan.Progress, sections.Progress);
    }

    private static RequirementView Need(string item, params string[] quests) => new(RequirementKind.Key, item, "", item, quests);

    [Fact]
    public void Bring_puts_what_the_picks_need_first_with_a_hairline_before_the_rest()
    {
        var rows = Planning.BringOrder([Need("key1", "A"), Need("key2", "B"), Need("marker", "A", "B"), Need("flare", "C")], new HashSet<string> { "B" });
        Assert.Equal(["key2", "marker", "key1", "flare"], rows.Select(r => r.Row.ItemId));
        Assert.Equal([true, true, false, false], rows.Select(r => r.ForPicks));
        Assert.Equal([false, false, true, false], rows.Select(r => r.StartsOthers));
    }

    [Fact]
    public void Without_picks_bring_is_as_planned()
    {
        var rows = Planning.BringOrder([Need("key1", "A"), Need("key2", "B")], new HashSet<string>());
        Assert.Equal(["key1", "key2"], rows.Select(r => r.Row.ItemId));
        Assert.All(rows, r => Assert.False(r.StartsOthers || r.ForPicks));
    }

    private sealed class Offline : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("offline: the test reads the cache only");
    }

    // The player's plan before the planner's: a map with picks is suggested first. From the app's own download
    // cache (tarkov.dev's data isn't in the repository); skipped without one.
    [Fact]
    public async Task A_map_with_picks_is_suggested_first()
    {
        var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shturmap", "cache", "tarkov-dev");
        if (!File.Exists(Path.Combine(cache, "pve_tasks.json")))
            Assert.Skip($"No tarkov.dev cache at {cache}: run Shturmap or shturmap-cli once to download it.");
        var data = await new GameDataLoader(new CachedHttp(new HttpClient(new Offline()), cache)).LoadAsync(GameMode.Pve, "en", TestContext.Current.CancellationToken);
        var active = data.Tasks.Keys.ToList();
        var plans = Planning.Suggest(data, active);
        Assert.True(plans.Count > 1);
        var first = plans[0].Finish.Concat(plans[0].Progress).Select(q => q.QuestId).ToHashSet();
        var pick = plans[^1].Finish.Concat(plans[^1].Progress).Select(q => q.QuestId).First(id => !first.Contains(id));

        var picked = Planning.Suggest(data, active, new HashSet<string> { pick });
        Assert.Contains(pick, picked[0].Finish.Concat(picked[0].Progress).Select(q => q.QuestId));
        // Every map the pick is on, then the planner's best up to four.
        Assert.True(picked.Count >= plans.Count);
        Assert.Equal(plans.Select(p => p.NormalizedName), Planning.Suggest(data, active, new HashSet<string>()).Select(p => p.NormalizedName));
    }

    // A pick on a map the planner ranks below its best four still brings that map up (the review of 2026-10-04: the
    // list was cut to four before the picks were looked at, so such a pick never showed). Same cache as above.
    [Fact]
    public async Task A_pick_on_a_map_outside_the_best_four_brings_it_first()
    {
        var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shturmap", "cache", "tarkov-dev");
        if (!File.Exists(Path.Combine(cache, "pve_tasks.json")))
            Assert.Skip($"No tarkov.dev cache at {cache}: run Shturmap or shturmap-cli once to download it.");
        var data = await new GameDataLoader(new CachedHttp(new HttpClient(new Offline()), cache)).LoadAsync(GameMode.Pve, "en", TestContext.Current.CancellationToken);
        var active = data.Tasks.Keys.ToList();
        var all = RaidPlanner.Rank(active.Select(id => Planning.ToPlan(data.Tasks[id], data)), Planning.Maps(data), top: int.MaxValue);
        Assert.True(all.Count > 4, "the data has more maps with quests than the four suggested");
        var low = all[^1];
        var lowName = data.Maps[low.Map.Id].NormalizedName;
        Assert.DoesNotContain(lowName, Planning.Suggest(data, active).Select(p => p.NormalizedName));

        // A quest of the lowest map, one that is on no other map if there is one (then that map alone leads).
        var elsewhere = all.Take(all.Count - 1).SelectMany(p => p.Finish.Concat(p.Progress)).Select(q => q.Quest.Id).ToHashSet();
        var here = low.Finish.Concat(low.Progress).Select(q => q.Quest.Id).ToList();
        var pick = here.FirstOrDefault(id => !elsewhere.Contains(id)) ?? here[0];

        var picked = Planning.Suggest(data, active, new HashSet<string> { pick });
        Assert.Contains(lowName, picked.Select(p => p.NormalizedName));
        Assert.Contains(pick, picked[0].Finish.Concat(picked[0].Progress).Select(q => q.QuestId));
        if (!elsewhere.Contains(pick))
        {
            Assert.Equal(lowName, picked[0].NormalizedName);
            Assert.Equal(4, picked.Count);
        }
    }
}
