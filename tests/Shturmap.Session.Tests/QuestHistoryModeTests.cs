using Shturmap.Core.Quests;

namespace Shturmap.Session.Tests;

// The quest history read back from older log sessions: a session whose log names no mode counts for the mode
// Shturmap is in, as a quest message followed live does; its quests used to be left out (review of 2026-10-09).
public class QuestHistoryModeTests
{
    private const string Login = "PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0";

    private static string QuestStarted(DateTime at) => $$"""
        {
          "type": "new_message",
          "eventId": "test-event-history",
          "dialogId": "54cb50c76803fa8b248b4571",
          "message": {
            "_id": "test-message-history",
            "type": 10,
            "dt": {{new DateTimeOffset(at).ToUnixTimeSeconds()}},
            "text": "quest started",
            "templateId": "{{SessionRig.QuestId}} description"
          }
        }
        """;

    [Theory]
    [InlineData(null, true)]
    [InlineData("Pve", true)]
    [InlineData("Regular", false)]
    public async Task An_older_session_counts_for_its_own_mode_or_without_one_for_the_mode_shturmap_is_in(string? oldMode, bool shown)
    {
        // An older game session: a quest started, with the mode line or without it.
        var older = DateTime.Now.AddDays(-2);
        await using var rig = new SessionRig(logSessionStarted: older);
        if (oldMode is not null)
            rig.Log("Session mode: " + oldMode, older);
        rig.Notification("ChatMessageReceived", QuestStarted(older.AddMinutes(5)), older.AddMinutes(5));
        // The newest session, followed live, is PvE and says nothing of the quest.
        rig.NewLogSession(DateTime.Now.AddMinutes(-30));
        rig.Log("Session mode: Pve", DateTime.Now.AddMinutes(-29));
        rig.Log(Login, DateTime.Now.AddMinutes(-29));
        await rig.StartAsync();

        await rig.Until(s => s.Data is not null && s.Raid.Mode == Core.Logs.GameMode.Pve, "the data in PvE");
        if (shown)
        {
            var s = await rig.Until(x => x.Quests.ContainsKey(SessionRig.QuestId), "the quest from the older session");
            Assert.Equal(QuestState.Active, s.Quests[SessionRig.QuestId].State);
        }
        else
        {
            // Read back for PvP, its own mode: not PvE's.
            await Task.Delay(1000, TestContext.Current.CancellationToken);
            Assert.False(rig.Snapshot.Quests.ContainsKey(SessionRig.QuestId));
        }
    }
}
