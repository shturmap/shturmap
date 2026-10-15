using System.Globalization;
using Shturmap.Core.Logs;
using Shturmap.Core.Planning;
using Shturmap.Core.Quests;
using Shturmap.Core.Raid;
using Shturmap.Core.Stash;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Screenshots;

namespace Shturmap.Session.Tests;

// What the player has of what the quests need (owner, 2026-10-11): marks from the game's notification log (a quest's
// start items) and from menu screenshots read against the items BRING lists, kept in stash.json, shown as an
// observation with its date, retracted with one click, gone with the quest. The reader is the test's own here: it
// says what a picture "shows", so no picture is needed (tests\Shturmap.Game.Tests reads real ones).
public class StashSessionTests
{
    private const string Key = "000000000000000000000011", Camera = "000000000000000000000012", Other = "000000000000000000000013";

    // One quest on Customs: a key to a door there, and three cameras to plant.
    private static GameData Data(GameMode mode)
    {
        var data = SessionRig.Data(mode);
        var objectives = new List<ApiObjective>
        {
            new("obj-visit", "visit", "Enter the dorm", false, ["map-customs"], null, null, null, null, null, null, null, [[Key]], false),
            new("obj-plant", "plantItem", "Plant the cameras", false, ["map-customs"], null, null, 3, null, [Camera], null, null, null, false),
        };
        return new GameData
        {
            Mode = data.Mode,
            Language = data.Language,
            Maps = data.Maps,
            Tasks = new Dictionary<string, ApiTask>
            {
                [SessionRig.QuestId] = new(SessionRig.QuestId, "A quest", null, null, "map-customs", null, false, false, null, null, null, false, null, objectives, null),
            },
            Traders = data.Traders,
            ItemNames = new Dictionary<string, string> { [Key] = "Dorm room key", [Camera] = "WI-FI Camera", [Other] = "Bolts" },
            ItemShortNames = new Dictionary<string, string> { [Key] = "Dorm 1", [Camera] = "Camera", [Other] = "Bolts" },
            MapDefinitions = data.MapDefinitions,
            CheckedAt = data.CheckedAt,
        };
    }

    // A quest message with items the game put into the stash, as the parser reads it (GrantedItemsTests).
    private static string QuestMessage(string quest, int type, DateTime at, params (string ItemId, int Count)[] items)
    {
        var data = string.Join(",", items.Select((i, n) => $$"""
            { "_id": "test-item-{{n}}", "_tpl": "{{i.ItemId}}", "parentId": "test-stash", "slotId": "hideout"{{(i.Count > 1 ? $$""", "upd": { "StackObjectsCount": {{i.Count}} }""" : "")}} }
            """));
        return $$"""
            {
              "type": "new_message",
              "eventId": "test-event-{{quest}}-{{type}}-{{at.Ticks}}",
              "dialogId": "54cb50c76803fa8b248b4571",
              "message": {
                "_id": "test-message-{{quest}}-{{type}}",
                "type": {{type}},
                "dt": {{new DateTimeOffset(at).ToUnixTimeSeconds()}},
                "text": "quest",
                "templateId": "{{quest}} description",
                "items": { "stash": "test-stash", "data": [{{data}}] }
              }
            }
            """;
    }

    private static StashReading Shows(params (string ItemId, int Count, bool? Fir)[] items) =>
        new(false, [new StashGridReading(StashGridKind.Stash, items.Select(i => new StashItemSeen(i.ItemId, i.Count, i.Fir)).ToList())]);

    private static SessionRig Rig(Func<IReadOnlyList<StashCandidate>, StashReading?> shows, List<IReadOnlyList<StashCandidate>>? asked = null) =>
        new(configure: (paths, locations) => new GameSession(paths, locations)
        {
            GivenData = Data,
            StashReader = (_, candidates, _) =>
            {
                asked?.Add(candidates);
                return Task.FromResult(shows(candidates));
            },
        });

    // A screenshot the game names without a position: taken in the menus.
    private static void MenuScreenshot(SessionRig rig)
    {
        var now = DateTime.Now;
        var name = string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd}[{0:HH-mm}] ({1}).png", now, Interlocked.Increment(ref _shots));
        File.WriteAllBytes(Path.Combine(rig.ScreenshotsFolder, name), []);
    }

    // The next menu screenshot after a click the ledger dates (NOT ANY MORE, FORGET ALL MARKS): a moment later, as a
    // player's would be. The click is dated by the precise clock, the screenshot by its file's creation time, which
    // Windows takes from the clock's last tick (15.6 ms apart unless a program asks for finer): a file written within a
    // tick of the click can carry a time before it, and the ledger then rightly holds the screenshot to be older than
    // the click (CI, 2026-10-15: the key, retracted, never came back). A tenth of a second is several ticks.
    private static async Task MenuScreenshotAfterAClick(SessionRig rig)
    {
        await Task.Delay(100, TestContext.Current.CancellationToken);
        MenuScreenshot(rig);
    }

    private static int _shots;

    private static RequirementView Row(SessionSnapshot s, string itemId) =>
        s.Plan.SelectMany(p => p.Requirements).First(r => r.ItemId == itemId);

    [Theory]
    [InlineData(null, true)]
    [InlineData("on", true)]
    [InlineData("off", false)]
    public void It_is_on_unless_the_saved_word_is_off(string? setting, bool on) =>
        Assert.Equal(on, GameSession.ReadStashOn(setting));

    // "Delete screenshots after reading" takes menu screenshots too (owner, 2026-10-14: "otherwise the folder is
    // littered with screenshots from the menu"), and only once their reading is done, however long it takes.
    [Fact]
    public async Task A_menu_screenshot_is_deleted_once_its_reading_is_done()
    {
        var reading = new TaskCompletionSource<StashReading?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var rig = new SessionRig(configure: (paths, locations) => new GameSession(paths, locations)
        {
            GivenData = Data,
            ScreenshotGrace = TimeSpan.FromMilliseconds(50),
            StashReader = (_, _, _) => reading.Task,
        });
        rig.Notification("ChatMessageReceived", QuestMessage(SessionRig.QuestId, 10, DateTime.Now.AddMinutes(-10)), DateTime.Now.AddMinutes(-10));
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Plan.Count > 0 && s.Plan[0].Requirements.Count == 2, "the plan with its two rows");
        await rig.Session.SetDeleteScreenshotsAsync(true);

        MenuScreenshot(rig);
        // Past the grace, still being read: it stays.
        await Task.Delay(400, TestContext.Current.CancellationToken);
        Assert.Single(rig.Screenshots);
        reading.SetResult(Shows((Key, 1, null)));
        await rig.Until(s => s.Marks.ContainsKey(Key), "the key marked");
        await rig.Until(() => rig.Screenshots.Count == 0, "the menu screenshot deleted");
    }

    [Fact]
    public async Task What_a_quest_hands_over_at_its_start_marks_its_row_and_leaves_with_the_quest()
    {
        await using var rig = Rig(_ => null);
        var started = DateTime.Now.AddMinutes(-10);
        rig.Notification("ChatMessageReceived", QuestMessage(SessionRig.QuestId, 10, started, (Camera, 3)), started);
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        var s = await rig.Until(s => s.Marks.ContainsKey(Camera) && s.Plan.Count > 0, "the cameras marked");
        var mark = s.Marks[Camera];
        Assert.Equal(MarkSource.Log, mark.Source);
        Assert.Equal(GrantKind.QuestStart, mark.Grant);
        Assert.Equal(3, mark.Count);
        Assert.Equal(SessionRig.QuestId, mark.QuestId);
        // On the row: the quest's own, so "the quest"; the count covers the row, so nothing more to get.
        var row = StashMarks.ForRow(s.Data!, Row(s, Camera), s.Marks);
        Assert.NotNull(row);
        Assert.StartsWith("Given with the quest · ", row.Text, StringComparison.Ordinal);
        Assert.Equal(0, row.More);
        // The key wasn't handed over: its row has no mark.
        Assert.Null(StashMarks.ForRow(s.Data!, Row(s, Key), s.Marks));
        // The file holds the id and the count, nothing else.
        var file = File.ReadAllText(Path.Combine(rig.AppFolder, "stash.json"));
        Assert.Contains(Camera, file, StringComparison.Ordinal);
        Assert.DoesNotContain("Camera", file, StringComparison.Ordinal);

        // The quest completes, with a reward: the start items go with it, the reward stays.
        rig.Notification("ChatMessageReceived", QuestMessage(SessionRig.QuestId, 12, DateTime.Now, (Other, 1)));
        s = await rig.Until(s => !s.Marks.ContainsKey(Camera), "the cameras' mark gone with the quest");
        Assert.Equal(GrantKind.QuestReward, s.Marks[Other].Grant);
    }

    [Fact]
    public async Task A_menu_screenshot_is_read_for_what_the_quests_need_and_says_what_it_found()
    {
        var asked = new List<IReadOnlyList<StashCandidate>>();
        await using var rig = Rig(c => Shows((Key, 1, null), (Camera, 2, true)), asked);
        rig.Notification("ChatMessageReceived", QuestMessage(SessionRig.QuestId, 10, DateTime.Now.AddMinutes(-10)), DateTime.Now.AddMinutes(-10));
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        var s = await rig.Until(s => s.Plan.Count > 0 && s.Plan[0].Requirements.Count == 2, "the plan with its two rows");
        Assert.True(s.StashNoteShown);

        MenuScreenshot(rig);
        s = await rig.Until(s => s.Marks.Count == 2, "both marks");
        // Read against the rows' items, with their short names, and nothing else.
        Assert.Equal(["Camera", "Dorm 1"], asked.Single().Select(c => c.ShortName).Order());
        Assert.Equal(1, s.Marks[Key].Count);
        Assert.Equal(2, s.Marks[Camera].Count);
        Assert.Equal(2, s.Marks[Camera].FoundInRaid);
        Assert.Equal(MarkSource.Screenshot, s.Marks[Key].Source);
        Assert.False(s.StashNoteShown);
        // The cue: the two items it showed, with the counts seen, the key as a key.
        var cue = Assert.Single(rig.Cues, c => c.Kind == CueKind.StashSeen);
        Assert.Equal([(Key, 1), (Camera, 2)], cue.Kit!.Select(i => (i.ItemId, i.Count)).OrderBy(i => i.ItemId, StringComparer.Ordinal));
        Assert.Equal(RequirementKind.Key, cue.Kit.Single(i => i.ItemId == Key).Kind);
        Assert.Equal(RequirementKind.Bring, cue.Kit.Single(i => i.ItemId == Camera).Kind);
        Assert.Equal(0, cue.KitMore);
        // The rows: the key is covered; two of three cameras were seen, so one more to get.
        Assert.StartsWith("Seen in your stash · ", StashMarks.ForRow(s.Data!, Row(s, Key), s.Marks)!.Text, StringComparison.Ordinal);
        var cameras = StashMarks.ForRow(s.Data!, Row(s, Camera), s.Marks)!;
        Assert.StartsWith("2 of 3 seen in your stash · ", cameras.Text, StringComparison.Ordinal);
        Assert.Equal(1, cameras.More);
        // The card: with the count and the time.
        Assert.StartsWith("2 seen in your stash, found in raid · ", StashMarks.ForCard(s.Data!, s.Marks[Camera]), StringComparison.Ordinal);

        // NOT ANY MORE: the mark goes; the next screenshot brings it back.
        await rig.Session.NotAnyMoreAsync(Key);
        Assert.False(rig.Snapshot.Marks.ContainsKey(Key));
        await MenuScreenshotAfterAClick(rig);
        await rig.Until(s => s.Marks.ContainsKey(Key), "the key seen again");

        // Closed and opened again: the file brings the marks back.
        await rig.RestartAsync();
        await rig.StartAsync();
        s = await rig.Until(s => s.Marks.Count == 2, "the marks from the file");
        Assert.Equal(2, s.Marks[Camera].Count);
    }

    [Fact]
    public async Task Unticked_no_menu_screenshot_is_read_and_the_trading_screen_gives_nothing()
    {
        var asked = new List<IReadOnlyList<StashCandidate>>();
        await using var rig = Rig(_ => new StashReading(true, []), asked);
        rig.Notification("ChatMessageReceived", QuestMessage(SessionRig.QuestId, 10, DateTime.Now.AddMinutes(-10)), DateTime.Now.AddMinutes(-10));
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Plan.Count > 0 && s.Plan[0].Requirements.Count == 2, "the plan");
        MenuScreenshot(rig);
        await rig.Until(() => asked.Count == 1, "the trading screenshot read");
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Empty(rig.Snapshot.Marks);
        Assert.DoesNotContain(rig.Cues, c => c.Kind == CueKind.StashSeen);

        await rig.Session.SetReadStashAsync(false);
        Assert.False(rig.Snapshot.ReadStash);
        Assert.False(rig.Snapshot.StashNoteShown);
        MenuScreenshot(rig);
        await Task.Delay(600, TestContext.Current.CancellationToken);
        Assert.Single(asked);
    }

    // Owner, 2026-10-11: "how do we deal with the inventory depleting over time". A quest completed used up what it
    // was brought (the cameras planted): the marks from before the completion lose that count, once.
    [Fact]
    public async Task A_quest_completed_takes_what_it_used_up_from_the_marks_seen_before()
    {
        await using var rig = Rig(_ => Shows((Camera, 5, true), (Key, 1, null)));
        rig.Notification("ChatMessageReceived", QuestMessage(SessionRig.QuestId, 10, DateTime.Now.AddMinutes(-10)), DateTime.Now.AddMinutes(-10));
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Plan.Count > 0 && s.Plan[0].Requirements.Count == 2, "the plan");
        MenuScreenshot(rig);
        await rig.Until(s => s.Marks.ContainsKey(Camera), "the cameras marked");
        // The quest completes a moment later: three cameras were planted, two are left; the key wasn't used up.
        rig.Notification("ChatMessageReceived", QuestMessage(SessionRig.QuestId, 12, DateTime.Now.AddSeconds(2)));
        var s = await rig.Until(s => s.Marks.TryGetValue(Camera, out var m) && m.Count == 2, "two cameras left");
        Assert.Equal(2, s.Marks[Camera].FoundInRaid);
        Assert.Equal(MarkSource.Screenshot, s.Marks[Camera].Source);
        Assert.Equal(1, s.Marks[Key].Count);
        // Closed and opened again: the log is read back, and the completion doesn't take its count twice.
        await rig.RestartAsync();
        await rig.StartAsync();
        s = await rig.Until(s => s.Marks.Count == 2 && s.Quests.Count > 0, "the marks from the file");
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Equal(2, rig.Snapshot.Marks[Camera].Count);
    }

    [Fact]
    public async Task Forget_all_marks_takes_everything_and_the_log_read_back_brings_nothing_old()
    {
        await using var rig = Rig(_ => Shows((Key, 1, null)));
        rig.Notification("ChatMessageReceived", QuestMessage(SessionRig.QuestId, 10, DateTime.Now.AddMinutes(-10), (Camera, 3)), DateTime.Now.AddMinutes(-10));
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Marks.ContainsKey(Camera) && s.Plan.Count > 0, "the cameras marked");
        MenuScreenshot(rig);
        await rig.Until(s => s.Marks.Count == 2, "both marks");
        await rig.Session.ForgetAllMarksAsync();
        Assert.Empty(rig.Snapshot.Marks);
        Assert.Contains(rig.Notices, n => n.StartsWith("Forgot 2 marks", StringComparison.Ordinal));
        // The log read back at the next start: the quest's start message from before stays forgotten.
        await rig.RestartAsync();
        await rig.StartAsync();
        await rig.Until(s => s.Plan.Count > 0 && s.Plan[0].Requirements.Count == 2, "the plan again");
        Assert.Empty(rig.Snapshot.Marks);
        await MenuScreenshotAfterAClick(rig);
        await rig.Until(s => s.Marks.ContainsKey(Key), "the key seen again");
    }

    // Owner, 2026-10-11: "when making a screenshot in-raid it should never go into the register since the player
    // still can die". A screenshot named without a position while a raid runs opens no picture and marks nothing.
    [Fact]
    public async Task In_a_raid_no_screenshot_is_read_for_the_stash()
    {
        var asked = new List<IReadOnlyList<StashCandidate>>();
        await using var rig = Rig(_ => Shows((Key, 1, null)), asked);
        rig.Notification("ChatMessageReceived", QuestMessage(SessionRig.QuestId, 10, DateTime.Now.AddMinutes(-10)), DateTime.Now.AddMinutes(-10));
        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", DateTime.Now.AddMinutes(-5));
        await rig.StartAsync();
        await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid, "the raid running");
        MenuScreenshot(rig);
        await Task.Delay(600, TestContext.Current.CancellationToken);
        Assert.Empty(asked);
        Assert.Empty(rig.Snapshot.Marks);
        Assert.DoesNotContain(rig.Cues, c => c.Kind == CueKind.StashSeen);
    }
}
