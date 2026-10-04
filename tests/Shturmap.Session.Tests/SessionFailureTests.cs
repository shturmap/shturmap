using Shturmap.Core.Quests;
using Shturmap.Core.Raid;

namespace Shturmap.Session.Tests;

// One failure doesn't end the session's background work: log following and the quest history carry on, and the
// failure is said once (review of 2026-10-04, A31).
public class SessionFailureTests
{
    private const string Login = "PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0";

    [Fact]
    public async Task Log_following_survives_a_failure_while_showing_what_a_line_said()
    {
        await using var rig = new SessionRig();
        var failures = new List<string>();
        rig.Session.Failure += (what, _) =>
        {
            lock (failures)
                failures.Add(what);
        };
        // Something that takes the snapshots fails on every raid snapshot (a subscriber's bug, a full disk under it).
        rig.Session.Changed += s =>
        {
            if (s.Raid.Phase == RaidPhase.InRaid)
                throw new InvalidOperationException("the subscriber failed");
        };
        rig.Log("Session mode: Pve");
        rig.Log(Login);
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null && s.Map is not null && s.Raid.Mode == Core.Logs.GameMode.Pve, "the data and the replay");

        var started = DateTime.Now;
        rig.Log("scene preset path:maps/customs_preset.bundle rcid:x.scenespreset.asset", started.AddSeconds(-3));
        rig.Log("GameStarting:55.73(0) real:62.6(0) diff:6.87", started.AddSeconds(-1));
        rig.Log("GameStarted:55.73(0) real:62.6(0) diff:6.87", started);
        await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid, "the raid");
        await rig.Until(() =>
        {
            lock (failures)
                return failures.Count > 0;
        }, "the failure being said");

        // Another line in the raid fails the same way: said once, not again.
        rig.Log("[Transit] Flag:Common, RaidId:test, Count:0, Locations:bigmap -> ");
        await Task.Delay(1200, TestContext.Current.CancellationToken);

        // The following is still alive: the raid's end line arrives and is shown.
        rig.Log(Login);
        await rig.Until(s => s.Raid.Phase == RaidPhase.Menu && s.LastRaid is not null, "the raid's end");
        lock (failures)
        {
            // Said once, however often the same thing fails.
            Assert.Equal(["Showing what the game's log said"], failures);
        }
    }

    [Fact]
    public async Task One_unreadable_log_session_does_not_cost_the_quest_history_of_the_others()
    {
        // The oldest session: its notification log holds a message in a shape the parser doesn't expect.
        var oldest = DateTime.Now.AddDays(-3);
        await using var rig = new SessionRig(logSessionStarted: oldest);
        rig.Log("Session mode: Pve", oldest);
        rig.Notification("ChatMessageReceived", """
            {
              "type": "new_message",
              "message": "not an object"
            }
            """, oldest.AddMinutes(5));
        // The session after it: a quest started. Only the quest history reads it; the newest session is the one followed.
        var middle = DateTime.Now.AddDays(-2);
        rig.NewLogSession(middle);
        rig.Log("Session mode: Pve", middle);
        rig.Notification("ChatMessageReceived", $$"""
            {
              "type": "new_message",
              "eventId": "test-event-1",
              "dialogId": "54cb50c76803fa8b248b4571",
              "message": {
                "_id": "test-message-1",
                "type": 10,
                "dt": {{new DateTimeOffset(middle.AddMinutes(10)).ToUnixTimeSeconds()}},
                "text": "quest started",
                "templateId": "{{SessionRig.QuestId}} description"
              }
            }
            """, middle.AddMinutes(10));
        rig.NewLogSession(DateTime.Now.AddMinutes(-30));
        rig.Log("Session mode: Pve", DateTime.Now.AddMinutes(-29));
        rig.Log(Login, DateTime.Now.AddMinutes(-29));
        await rig.StartAsync();

        var s = await rig.Until(x => x.Quests.ContainsKey(SessionRig.QuestId), "the quest from the readable session");
        Assert.Equal(QuestState.Active, s.Quests[SessionRig.QuestId].State);
    }
}
