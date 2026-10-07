using Shturmap.Core.Logs;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

// A quest completed (owner, 2026-10-07: "For completed quests, also make a nice animation"): the log's completion comes
// as the QUEST COMPLETE cue, with its trader and what it unlocks, in place of the one-line notice.
public class CompletionTests
{
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
    public async Task A_quest_completed_live_comes_as_its_cue_and_not_as_a_notice()
    {
        await using var rig = new SessionRig();
        rig.Notification("ChatMessageReceived", QuestMessage(SessionRig.QuestId, 10, DateTime.Now.AddMinutes(-10)), DateTime.Now.AddMinutes(-10));
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Quests.GetValueOrDefault(SessionRig.QuestId)?.State == Shturmap.Core.Quests.QuestState.Active, "the quest active");
        // Read back at start: no cue for it.
        Assert.DoesNotContain(rig.Cues, c => c.Kind == CueKind.QuestComplete);

        rig.Notification("ChatMessageReceived", QuestMessage(SessionRig.QuestId, 12, DateTime.Now));
        await rig.Until(() => rig.Cues.Any(c => c.Kind == CueKind.QuestComplete), "the QUEST COMPLETE cue");
        var cue = rig.Cues.Single(c => c.Kind == CueKind.QuestComplete);
        Assert.Equal("A quest", cue.MapName);
        Assert.Equal(SessionRig.QuestId, cue.Completed?.QuestId);
        Assert.DoesNotContain(rig.Notices, n => n.Contains("completed", StringComparison.Ordinal));
    }

    [Fact]
    public void A_completion_names_its_trader_and_the_quests_it_unlocks()
    {
        ApiTask Task(string id, string name, params string[] after) =>
            new(id, name, null, "trader-1", null, null, false, false, null, null, null, false,
                after.Select(a => new ApiTaskRequirement(a, ["complete"])).ToList(), [], null);
        var data = new GameData
        {
            Mode = GameMode.Pve,
            Language = "en",
            Maps = new Dictionary<string, ApiMap>(),
            Tasks = new[] { Task("a", "Gratitude"), Task("b", "Shooter Born in Heaven", "a"), Task("c", "Setup", "a"), Task("d", "Elsewhere", "x") }
                .ToDictionary(t => t.Id),
            Traders = new Dictionary<string, ApiTrader> { ["trader-1"] = new("trader-1", "Prapor", "prapor", null) },
            MapDefinitions = [],
            CheckedAt = DateTimeOffset.Now,
        };
        var done = GameSession.Completion(data, data.Tasks["a"]);
        Assert.Equal("Prapor", done.TraderName);
        Assert.Equal(["Setup", "Shooter Born in Heaven"], done.Unlocks);
    }
}
