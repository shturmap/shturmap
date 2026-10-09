using Shturmap.Core;
using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Core.Quests;
using Shturmap.Core.Raid;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

// The quests, picks and plans are worked out once for a run of quest messages, not once per message, and the plan is
// one ranking of every map (review of 2026-10-09). Nothing that is shown may change with it.
public class QuestRecomputeTests
{
    private static readonly string[] Names = ["alpha", "bravo", "charlie", "delta", "echo", "foxtrot"];

    // Six maps: the first has six quests with a place on it, the next five, and so on, so the planner ranks them in
    // that order.
    private static GameData SixMaps()
    {
        var tasks = new Dictionary<string, ApiTask>();
        for (var m = 0; m < Names.Length; m++)
        {
            for (var q = 0; q < Names.Length - m; q++)
            {
                var id = $"{Names[m]}-{q}";
                var objective = new ApiObjective("o-" + id, "findQuestItem", "Locate and obtain the item", false, ["map-" + Names[m]],
                    [new ApiZone("z-" + id, null, "map-" + Names[m], new ApiPosition(q * 10, 0, 0), null, null, null)], null, 1, "item-" + id, null, null, null, null, false);
                tasks[id] = new ApiTask(id, "Quest " + id, null, null, "map-" + Names[m], null, false, false, null, null, null, false, null, [objective], null);
            }
        }
        return new GameData
        {
            Mode = GameMode.Pve,
            Language = "en",
            Maps = Names.Select(n => new ApiMap("map-" + n, n, n, n, $"maps/{n}_preset.bundle", null, 40, [], [], [], [], [])).ToDictionary(m => m.Id),
            Tasks = tasks,
            Traders = new Dictionary<string, ApiTrader>(),
            MapDefinitions = Names.Select(n => new MapDefinition { Key = n, Transform = [1, 0, 1, 0], Bounds = new WorldBox(-500, -500, 500, 500) }).ToList(),
            CheckedAt = DateTimeOffset.Now,
        };
    }

    public static TheoryData<string> PickedMaps => new() { "", "echo,foxtrot", "alpha,bravo,charlie,delta,echo,foxtrot", "none" };

    // NEXT RAID lists every map with work on it (owner, 2026-10-09; until then the planner's best four): the maps with
    // picks first, then the rest in the planner's order.
    [Theory]
    [MemberData(nameof(PickedMaps))]
    public void Every_map_with_work_is_suggested_those_with_picks_first(string picked)
    {
        var data = SixMaps();
        var active = data.Tasks.Keys.ToList();
        // A pick on each map named: its first quest. "none" is a session without picks at all.
        var named = picked == "none" ? [] : picked.Split(',', StringSplitOptions.RemoveEmptyEntries);
        IReadOnlyDictionary<string, IReadOnlySet<string>>? picks = picked == "none" ? null
            : named.ToDictionary(n => n, n => (IReadOnlySet<string>)new HashSet<string> { n + "-0" });

        var suggested = Planning.Suggest(data, active, picksByMap: picks);

        // One pick each: among the maps with picks, the planner's order holds.
        Assert.Equal([.. Names.Where(named.Contains), .. Names.Where(n => !named.Contains(n))], suggested.Select(p => p.NormalizedName));
        Assert.Equal(data.Tasks.Count, suggested.Sum(p => p.Finish.Count + p.Progress.Count));
    }

    // ---- a run of quest messages in one batch of lines ----

    private const string First = "aaaaaaaaaaaaaaaaaaaaaa31";
    private const string Second = "aaaaaaaaaaaaaaaaaaaaaa32";
    private const string Third = "aaaaaaaaaaaaaaaaaaaaaa33";

    // Three quests on Customs, each needing a key there: the kit of a raid loading shows the active ones.
    private static GameData WithKeyQuests(GameMode mode)
    {
        var data = SessionRig.Data(mode);
        ApiTask Quest(string id) => new(id, "Quest " + id[^2..], null, null, "map-customs", null, false, false, null, null, null, false, null,
            [new ApiObjective("o-" + id, "findQuestItem", "Locate and obtain the item", false, ["map-customs"],
                [new ApiZone("z-" + id, null, "map-customs", new ApiPosition(10, 0, 10), null, null, null)], null, 1, "item-" + id, null, null, null,
                [["key-" + id[^2..]]], false)], null);
        return new GameData
        {
            Mode = data.Mode, Language = data.Language, Maps = data.Maps, Traders = data.Traders, MapDefinitions = data.MapDefinitions,
            CheckedAt = data.CheckedAt,
            Tasks = new Dictionary<string, ApiTask>(data.Tasks) { [First] = Quest(First), [Second] = Quest(Second), [Third] = Quest(Third) },
        };
    }

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

    [Fact]
    public async Task Quest_messages_just_before_a_raid_loads_are_in_its_kit_and_in_the_plan()
    {
        await using var rig = new SessionRig(configure: (paths, locations) => new GameSession(paths, locations) { GivenData = WithKeyQuests });
        rig.Log("Session mode: Pve");
        rig.Log("PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null && s.Map is not null, "the data");

        // Three quests started and one of them completed, then the raid loads: written at once, as the game writes
        // them in a busy moment, so that they come in as few batches as the polls make them.
        var now = DateTime.Now;
        rig.Notification("ChatMessageReceived", QuestMessage(First, 10, now.AddSeconds(-3)), now.AddSeconds(-3));
        rig.Notification("ChatMessageReceived", QuestMessage(Second, 10, now.AddSeconds(-3)), now.AddSeconds(-3));
        rig.Notification("ChatMessageReceived", QuestMessage(Third, 10, now.AddSeconds(-3)), now.AddSeconds(-3));
        rig.Notification("ChatMessageReceived", QuestMessage(Second, 12, now.AddSeconds(-2)), now.AddSeconds(-2));
        rig.Log("scene preset path:maps/customs_preset.bundle rcid:x.scenespreset.asset", now.AddSeconds(-1));

        await rig.Until(() => rig.Cues.Any(c => c.Kind == CueKind.RaidLoading), "the loading cue");
        var cue = rig.Cues.Single(c => c.Kind == CueKind.RaidLoading);
        Assert.Equal(["key-31", "key-33"], cue.Kit!.Select(k => k.ItemId).Order());
        var s = await rig.Until(s => s.Quests.Count == 3, "the three quests");
        Assert.Equal([QuestState.Active, QuestState.Completed, QuestState.Active], new[] { First, Second, Third }.Select(q => s.Quests[q].State));
        Assert.Equal([First, Third], s.Plan.Single().Finish.Concat(s.Plan.Single().Progress).Select(q => q.QuestId).Order());
    }
}
