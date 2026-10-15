using Shturmap.Core;
using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Core.Planning;
using Shturmap.Core.Quests;
using Shturmap.Core.Raid;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;
using Shturmap.Map;

namespace Shturmap.Session.Tests;

// A marker per place (owner, 2026-10-15: "Ill wisher and humanitarian supplies does not show that you have to bring
// multiple ms2000 or jammers. Also doesn't update with checked elements"). A quest that marks two places needs two
// markers, one per mark objective, wherever its needs are listed: the quest card's BRING, the plan's BRING row and the
// need cell beside its name, the kit, the item card; the raid card's lines keep "Bring: MS2000 Marker" on each place.
// Only open objectives count: a ticked place takes its marker off, and an untick puts it back.
public class BringCountTests
{
    private const string Places = "bbbbbbbbbbbbbbbbbbbbbb01", OnePlace = "bbbbbbbbbbbbbbbbbbbbbb02";
    private const string Marker = "ms2000";

    private static readonly DateOnly Day = new(2026, 1, 1);

    private static ApiZone Zone(string id, double at) => new(id + "-zone", null, "map-customs", new ApiPosition(at, 0, at), null, null, null);

    private static ApiObjective Visit(string id, double at) =>
        new(id, "visit", $"Locate the place {id}", false, ["map-customs"], [Zone(id, at)], null, 1, null, null, null, null, null, false);

    private static ApiObjective Mark(string id, double at) =>
        new(id, "mark", $"Mark the place {id}", false, ["map-customs"], [Zone(id, at)], null, 1, null, null, Marker, null, null, false);

    private static ApiTask Task(string id, string name, params ApiObjective[] objectives) =>
        new(id, name, null, null, null, null, false, false, null, null, null, false, null, [.. objectives], null);

    // Two places, each located and then marked, as both quests of the report are laid out; and a quest with one place.
    private static GameData Data(GameMode mode) => new()
    {
        Mode = mode,
        Language = "en",
        Maps = new[] { new ApiMap("map-customs", "Customs", "customs", "bigmap", "maps/customs_preset.bundle", null, 40, [], [], [], [], []) }
            .ToDictionary(m => m.Id),
        Tasks = new[]
        {
            Task(Places, "Two places", Visit("o-find-1", 10), Mark("o-mark-1", 10), Visit("o-find-2", 60), Mark("o-mark-2", 60)),
            Task(OnePlace, "One place", Mark("o-mark-3", 100)),
        }.ToDictionary(t => t.Id),
        Traders = new Dictionary<string, ApiTrader>(),
        ItemNames = new Dictionary<string, string> { [Marker] = "MS2000 Marker" },
        MapDefinitions = [new MapDefinition { Key = "customs", Transform = [1, 0, 1, 0], Bounds = new WorldBox(-500, -500, 500, 500) }],
        CheckedAt = DateTimeOffset.Now,
    };

    private static readonly string[] Both = [Places, OnePlace];

    private static IReadOnlySet<string> Done(params string[] objectives) => objectives.ToHashSet();

    private static Dictionary<string, QuestStatus> Active(params string[] ids) =>
        ids.ToDictionary(id => id, id => new QuestStatus(id, QuestState.Active, ObservationSource.Log, DateTime.Now));

    private static Dictionary<string, DateOnly> Ticks(params string[] objectives) => objectives.ToDictionary(o => o, _ => Day);

    private static RequirementView? MarkerRow(MapPlanView plan) => plan.Requirements.SingleOrDefault(r => r.ItemId == Marker);

    private static CardNeed? CardMarker(GameData data, string quest, params string[] ticked) =>
        QuestCards.Build(data, Active(quest), quest, ticks: Ticks(ticked))!.Needs.SingleOrDefault(n => n.ItemId == Marker);

    private static IEnumerable<string> Uses(GameData data, IReadOnlySet<string>? done = null) =>
        ItemCards.Build(data, null, Active(Both), Marker, done).Uses.Select(u => $"{u.QuestName}: {u.How}");

    [Fact]
    public void Each_place_a_quest_marks_takes_a_marker_of_its_own()
    {
        var data = Data(GameMode.Pve);
        var plan = Planning.PlanFor(data, Both, "customs")!;
        var row = MarkerRow(plan)!;
        // The plan's row adds up both quests; the cell beside each quest's name says its own number.
        Assert.Equal(("MS2000 Marker ×3", 3), (row.Text, row.Count));
        Assert.Equal((2, 1), (row.CountFor(Places), row.CountFor(OnePlace)));
        // The quest card's BRING: one per place.
        Assert.Equal(("MS2000 Marker ×2", 2), (CardMarker(data, Places)!.Text, CardMarker(data, Places)!.Count));
        Assert.Equal(("MS2000 Marker", 1), (CardMarker(data, OnePlace)!.Text, CardMarker(data, OnePlace)!.Count));
        // The kit while the raid loads, its rows and the cue's pictures.
        var kit = Planning.Kit(plan, new HashSet<string>());
        Assert.Equal(3, Assert.Single(kit.All).Count);
        Assert.Equal(3, Assert.Single(Planning.CueKit(kit).Shown).Count);
        // The item card.
        Assert.Equal(["One place: Bring, to mark · Customs", "Two places: Bring ×2, to mark · Customs"], Uses(data));
        // The raid card keeps the need on every place's line: each needs one of its own (owner, 2026-10-04).
        var task = data.Tasks[Places];
        var customs = new HashSet<string> { "map-customs" };
        Assert.Equal(["Bring: MS2000 Marker", "Bring: MS2000 Marker"],
            task.Objectives!.Where(o => o.Type == "mark").Select(o => Planning.Needs(data, task, o, customs, true)));
        Assert.All(task.Objectives!.Where(o => o.Type == "visit"), o => Assert.Null(Planning.Needs(data, task, o, customs, true)));
    }

    [Fact]
    public void A_ticked_place_takes_its_marker_off_everywhere()
    {
        var data = Data(GameMode.Pve);
        var plan = Planning.PlanFor(data, Both, "customs", Done("o-mark-1"))!;
        var row = MarkerRow(plan)!;
        Assert.Equal(("MS2000 Marker ×2", 2), (row.Text, row.Count));
        Assert.Equal((1, 1), (row.CountFor(Places), row.CountFor(OnePlace)));
        Assert.Equal(("MS2000 Marker", 1), (CardMarker(data, Places, "o-mark-1")!.Text, CardMarker(data, Places, "o-mark-1")!.Count));
        Assert.Equal(2, Assert.Single(Planning.CueKit(Planning.Kit(plan, new HashSet<string>())).Shown).Count);
        Assert.Equal(["One place: Bring, to mark · Customs", "Two places: Bring, to mark · Customs"], Uses(data, Done("o-mark-1")));
        // Either place: the one left needs its marker.
        Assert.Equal(1, MarkerRow(Planning.PlanFor(data, Both, "customs", Done("o-mark-2"))!)!.CountFor(Places));
    }

    // A place located but not yet marked still needs its marker: only the mark objective's tick takes it off.
    [Fact]
    public void A_place_only_located_still_needs_its_marker()
    {
        var data = Data(GameMode.Pve);
        Assert.Equal(2, MarkerRow(Planning.PlanFor(data, Both, "customs", Done("o-find-1", "o-find-2"))!)!.CountFor(Places));
        Assert.Equal(2, CardMarker(data, Places, "o-find-1", "o-find-2")!.Count);
    }

    [Fact]
    public void With_every_place_ticked_the_quest_needs_no_marker()
    {
        var data = Data(GameMode.Pve);
        var plan = Planning.PlanFor(data, Both, "customs", Done("o-mark-1", "o-mark-2"))!;
        var row = MarkerRow(plan)!;
        Assert.Equal([OnePlace], row.QuestIds);
        Assert.Equal(1, row.Count);
        Assert.Null(CardMarker(data, Places, "o-mark-1", "o-mark-2"));
        Assert.Equal(["One place: Bring, to mark · Customs"], Uses(data, Done("o-mark-1", "o-mark-2")));
        // Its own places left to locate need nothing brought.
        Assert.Empty(QuestCards.Build(data, Active(Places), Places, ticks: Ticks("o-mark-1", "o-mark-2"))!.Needs);
        // Nothing else needs a marker: no row, and nothing in the kit.
        var alone = Planning.PlanFor(data, [Places], "customs", Done("o-mark-1", "o-mark-2"))!;
        Assert.Null(MarkerRow(alone));
        Assert.Empty(Planning.Kit(alone, new HashSet<string>()).All);
    }

    // ---- the session: a tick on the card changes the count at once, and an untick puts it back ----

    private static string QuestMessage(string quest, DateTime at) => $$"""
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

    [Fact]
    public async Task A_tick_in_a_raid_takes_the_marker_off_the_raid_card_and_an_untick_puts_it_back()
    {
        await using var rig = new SessionRig(configure: (paths, locations) => new GameSession(paths, locations) { GivenData = Data });
        var started = DateTime.Now.AddMinutes(-5);
        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", started);
        rig.Notification("ChatMessageReceived", QuestMessage(Places, started.AddMinutes(-2.5)), started.AddMinutes(-2.5));
        await rig.StartAsync();
        var before = await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid && s.Quests.GetValueOrDefault(Places)?.State == QuestState.Active
                                          && s.MapPlan is { } plan && MarkerRow(plan) is not null, "the raid and the quest's plan");
        Assert.Equal(2, MarkerRow(before.MapPlan!)!.CountFor(Places));
        Assert.Equal(["Bring: MS2000 Marker", "Bring: MS2000 Marker"], before.Objectives.Where(o => o.Needs is not null).Select(o => o.Needs));

        await rig.Session.ToggleTickAsync("o-mark-1", "test");
        var ticked = rig.Snapshot;
        Assert.Equal(("MS2000 Marker", 1), (MarkerRow(ticked.MapPlan!)!.Text, MarkerRow(ticked.MapPlan!)!.CountFor(Places)));
        // The place left keeps its line's need; the ticked one is done.
        Assert.Equal(["o-mark-2"], ticked.Objectives.Where(o => o.Needs is not null && !o.Done).Select(o => o.ObjectiveId));

        await rig.Session.ToggleTickAsync("o-mark-1", "test");
        Assert.Equal(2, MarkerRow(rig.Snapshot.MapPlan!)!.CountFor(Places));
    }

    // ---- tarkov.dev's data: every quest that marks several places needs a marker per place ----

    private sealed class Offline : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("offline: the test reads the cache only");
    }

    private static async Task<GameData> CachedData(GameMode mode)
    {
        var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shturmap", "cache", "tarkov-dev");
        if (!File.Exists(Path.Combine(cache, GameData.Slug(mode) + "_tasks.json")))
            Assert.Skip($"No tarkov.dev cache at {cache}: run Shturmap or shturmap-cli once to download it.");
        try
        {
            return await new GameDataLoader(new CachedHttp(new HttpClient(new Offline()), cache)).LoadAsync(mode, "en", TestContext.Current.CancellationToken);
        }
        catch (HttpRequestException e)
        {
            Assert.Skip("The tarkov.dev cache is incomplete: " + e.Message);
            throw;
        }
    }

    // A quest's mark objectives on a map, by their marker: those that aren't optional, as the planner counts them.
    private static IEnumerable<(PlanMap Map, string Marker, List<ApiObjective> Marks)> MarksByMap(GameData data, ApiTask task) =>
        Planning.Maps(data).SelectMany(map => (task.Objectives ?? [])
            .Where(o => o is { Type: "mark", Optional: false, MarkerItem: not null } && MapIds(o).Any(map.MapIds.Contains))
            .GroupBy(o => o.MarkerItem!)
            .Select(g => (map, g.Key, g.ToList())));

    // The maps an objective names or has a place on.
    private static IEnumerable<string> MapIds(ApiObjective o) =>
        (o.Maps ?? []).Concat((o.Zones ?? []).Select(z => z.Map)).Concat((o.PossibleLocations ?? []).Select(l => l.Map)).OfType<string>();

    private static int CountOn(GameData data, ApiTask task, PlanMap map, string item, IReadOnlySet<string>? done = null) =>
        Planning.PlanFor(data, [task.Id], data.Maps[map.Id].NormalizedName, done) is { } plan
        && plan.Requirements.FirstOrDefault(r => r.Kind == RequirementKind.Bring && r.ItemId == item) is { } row
            ? row.CountFor(task.Id)
            : 0;

    [Theory]
    [InlineData(GameMode.Pve)]
    [InlineData(GameMode.Pvp)]
    public async Task On_the_real_data_a_quest_needs_a_marker_per_open_place(GameMode mode)
    {
        var data = await CachedData(mode);
        var several = new List<string>();
        foreach (var task in data.Tasks.Values)
        {
            foreach (var (map, marker, marks) in MarksByMap(data, task))
            {
                var count = CountOn(data, task, map, marker);
                // At least one per place: a quest may also plant or use the same item there.
                Assert.True(count >= marks.Count, $"{task.Name} on {map.Name}: {marks.Count} places, {count} of {data.ItemName(marker)}");
                // Each place ticked takes one off; all of them, every one of them.
                Assert.Equal(count - 1, CountOn(data, task, map, marker, Done(marks[0].Id)));
                Assert.Equal(count - marks.Count, CountOn(data, task, map, marker, marks.Select(o => o.Id).ToHashSet()));
                if (marks.Count > 1)
                    several.Add($"{task.Name} ×{marks.Count}");
            }
        }
        Assert.True(several.Count > 5, $"only {several.Count} quests mark several places: is the cache complete?");
        TestContext.Current.SendDiagnosticMessage($"{several.Count} quests mark several places: " + string.Join(", ", several));
    }

    // The two quests of the report, while tarkov.dev's data has them so: two places on Shoreline, one marker each.
    [Theory]
    [InlineData(GameMode.Pve, "Humanitarian Supplies", "MS2000 Marker")]
    [InlineData(GameMode.Pve, "Ill-Wisher", "Signal Jammer")]
    [InlineData(GameMode.Pvp, "Humanitarian Supplies", "MS2000 Marker")]
    [InlineData(GameMode.Pvp, "Ill-Wisher", "Signal Jammer")]
    public async Task The_reported_quests_need_two_and_one_once_a_place_is_ticked(GameMode mode, string quest, string item)
    {
        var data = await CachedData(mode);
        if (data.Tasks.Values.FirstOrDefault(t => t.Name == quest) is not { } task
            || MarksByMap(data, task).SingleOrDefault() is not { Marks.Count: 2 } shoreline || data.ItemName(shoreline.Marker) != item)
        {
            Assert.Skip($"{quest} no longer marks two places with the {item} in tarkov.dev's data");
            return;
        }
        var states = new Dictionary<string, QuestStatus> { [task.Id] = new(task.Id, QuestState.Active, ObservationSource.Log, DateTime.Now) };
        Assert.Equal(2, CountOn(data, task, shoreline.Map, shoreline.Marker));
        Assert.Equal($"{item} ×2", QuestCards.Build(data, states, task.Id)!.Needs.Single(n => n.ItemId == shoreline.Marker).Text);
        var first = shoreline.Marks[0].Id;
        Assert.Equal(1, CountOn(data, task, shoreline.Map, shoreline.Marker, Done(first)));
        Assert.Equal(item, QuestCards.Build(data, states, task.Id, ticks: Ticks(first))!.Needs.Single(n => n.ItemId == shoreline.Marker).Text);
        Assert.Equal(0, CountOn(data, task, shoreline.Map, shoreline.Marker, shoreline.Marks.Select(o => o.Id).ToHashSet()));
    }
}
