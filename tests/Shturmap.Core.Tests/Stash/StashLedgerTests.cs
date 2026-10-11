using Shturmap.Core.Stash;

namespace Shturmap.Core.Tests.Stash;

// The ledger of what the player has (docs/DESIGN.md §4, "What you have"): one mark per item, the newest observation
// wins, counts are "at least", passes over the stash, retractions, and the file.
public class StashLedgerTests
{
    private static readonly DateTimeOffset Day = new(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(1));

    private const string Key = "000000000000000000000001", Camera = "000000000000000000000002", Quest = "000000000000000000000100";

    [Fact]
    public void A_quest_start_marks_what_it_handed_over_with_the_quest()
    {
        var ledger = new StashLedger();
        var changed = ledger.ApplyGrant(Day, GrantKind.QuestStart, Quest, [(Key, 1), (Camera, 3)]);
        Assert.Equal([Key, Camera], changed);
        var mark = Assert.IsType<StashMark>(ledger.MarkOf(Camera));
        Assert.Equal(3, mark.Count);
        Assert.Equal(MarkSource.Log, mark.Source);
        Assert.Equal(GrantKind.QuestStart, mark.Grant);
        Assert.Equal(Quest, mark.QuestId);
        Assert.True(ledger.Dirty);
    }

    [Fact]
    public void The_same_message_again_changes_nothing()
    {
        var ledger = new StashLedger();
        ledger.ApplyGrant(Day, GrantKind.QuestStart, Quest, [(Key, 1)]);
        ledger.Save(Path.Combine(Path.GetTempPath(), "shturmap-ledger-" + Guid.NewGuid() + ".json"));
        Assert.Empty(ledger.ApplyGrant(Day, GrantKind.QuestStart, Quest, [(Key, 1)]));
        Assert.False(ledger.Dirty);
    }

    [Fact]
    public void A_screenshot_names_the_items_it_showed_with_the_most_one_screenshot_showed()
    {
        var ledger = new StashLedger();
        ledger.ApplyScreenshot(Day, stashPage: true, [(Camera, 2, true), (Camera, 1, false)]);
        Assert.Equal(3, ledger.MarkOf(Camera)!.Count);
        Assert.Equal(2, ledger.MarkOf(Camera)!.FoundInRaid);
        // The next page of the same pass shows two of them again: not five.
        ledger.ApplyScreenshot(Day.AddMinutes(1), stashPage: true, [(Camera, 2, true)]);
        Assert.Equal(3, ledger.MarkOf(Camera)!.Count);
        Assert.Equal(MarkSource.Screenshot, ledger.MarkOf(Camera)!.Source);
        Assert.Equal(Day.AddMinutes(1), ledger.MarkOf(Camera)!.SeenAt);
    }

    [Fact]
    public void A_newer_observation_wins_an_older_one_does_not()
    {
        var ledger = new StashLedger();
        ledger.ApplyScreenshot(Day, stashPage: true, [(Camera, 2, null)]);
        ledger.ApplyGrant(Day.AddHours(-1), GrantKind.QuestStart, Quest, [(Camera, 5)]);
        Assert.Equal(2, ledger.MarkOf(Camera)!.Count);
        ledger.ApplyGrant(Day.AddHours(1), GrantKind.QuestReward, Quest, [(Camera, 5)]);
        Assert.Equal(5, ledger.MarkOf(Camera)!.Count);
        Assert.Equal(GrantKind.QuestReward, ledger.MarkOf(Camera)!.Grant);
    }

    [Fact]
    public void A_full_pass_that_did_not_show_an_item_says_so_and_takes_nothing_away()
    {
        var ledger = new StashLedger();
        ledger.ApplyScreenshot(Day, stashPage: true, [(Key, 1, null)]);
        // A later pass of three stash pages without the key: the mark says so.
        var later = Day.AddDays(7);
        ledger.ApplyScreenshot(later, true, [(Camera, 1, null)]);
        ledger.ApplyScreenshot(later.AddMinutes(1), true, [(Camera, 1, null)]);
        ledger.ApplyScreenshot(later.AddMinutes(2), true, [(Camera, 1, null)]);
        ledger.RaidStarted();
        var mark = ledger.MarkOf(Key)!;
        Assert.Equal(Day, mark.SeenAt);
        Assert.Equal(later.AddMinutes(2), mark.NotSeenAt);
        Assert.Equal(2, ledger.Passes.Count);
        Assert.True(ledger.Passes[1].Full);
        Assert.Equal(3, ledger.Passes[1].Screenshots);
        // Seen again: the words go.
        ledger.ApplyScreenshot(later.AddDays(1), true, [(Key, 1, null)]);
        Assert.Null(ledger.MarkOf(Key)!.NotSeenAt);
    }

    [Fact]
    public void Two_pages_are_not_a_full_pass_and_a_window_alone_is_not_a_page()
    {
        var ledger = new StashLedger();
        ledger.ApplyScreenshot(Day, true, [(Key, 1, null)]);
        var later = Day.AddDays(1);
        ledger.ApplyScreenshot(later, true, [(Camera, 1, null)]);
        ledger.ApplyScreenshot(later.AddMinutes(1), true, [(Camera, 1, null)]);
        ledger.ApplyScreenshot(later.AddMinutes(2), false, [(Camera, 1, null)]);
        ledger.EndPass();
        Assert.Null(ledger.MarkOf(Key)!.NotSeenAt);
        Assert.False(ledger.Passes[^1].Full);
    }

    [Fact]
    public void Screenshots_a_quarter_of_an_hour_apart_are_separate_passes()
    {
        var ledger = new StashLedger();
        ledger.ApplyScreenshot(Day, true, [(Camera, 1, null)]);
        ledger.ApplyScreenshot(Day.AddMinutes(16), true, [(Camera, 1, null)]);
        Assert.Single(ledger.Passes);
        ledger.EndPass();
        Assert.Equal(2, ledger.Passes.Count);
    }

    [Fact]
    public void Not_any_more_hides_the_mark_until_something_new_is_seen()
    {
        var ledger = new StashLedger();
        ledger.ApplyScreenshot(Day, true, [(Key, 1, null)]);
        Assert.True(ledger.Retract(Key, Day.AddHours(1)));
        Assert.Null(ledger.MarkOf(Key));
        Assert.Empty(ledger.Marks);
        Assert.False(ledger.Retract(Key, Day.AddHours(2)));
        ledger.ApplyScreenshot(Day.AddDays(1), true, [(Key, 1, null)]);
        Assert.NotNull(ledger.MarkOf(Key));
    }

    [Fact]
    public void A_quest_over_takes_its_start_items_and_leaves_rewards_and_screenshots()
    {
        var ledger = new StashLedger();
        ledger.ApplyGrant(Day, GrantKind.QuestStart, Quest, [(Key, 1)]);
        ledger.ApplyGrant(Day, GrantKind.QuestReward, Quest, [(Camera, 1)]);
        const string seen = "000000000000000000000003";
        ledger.ApplyScreenshot(Day, true, [(seen, 1, null)]);
        Assert.Equal([Key], ledger.Forget(Quest));
        Assert.Null(ledger.MarkOf(Key));
        Assert.NotNull(ledger.MarkOf(Camera));
        Assert.NotNull(ledger.MarkOf(seen));
    }

    // Owner, 2026-10-11: "how do we deal with the inventory depleting over time". A quest completed used up what it
    // was brought (planted, marked, handed over): the marks from before the completion lose that count, once.
    [Fact]
    public void A_quest_completed_takes_what_it_used_up_from_marks_seen_before_it_and_only_once()
    {
        var path = Path.Combine(Path.GetTempPath(), "shturmap-ledger-" + Guid.NewGuid(), "stash.json");
        var ledger = new StashLedger();
        ledger.ApplyScreenshot(Day, true, [(Camera, 5, true), (Key, 1, null)]);
        // Three cameras planted: two left, the observation as it was; the key wasn't used up.
        Assert.Equal([Camera], ledger.HandOver(Day.AddHours(1), Quest, [(Camera, 3)]));
        var mark = ledger.MarkOf(Camera)!;
        Assert.Equal(2, mark.Count);
        Assert.Equal(2, mark.FoundInRaid);
        Assert.Equal(Day, mark.SeenAt);
        Assert.Equal(MarkSource.Screenshot, mark.Source);
        Assert.NotNull(ledger.MarkOf(Key));
        // The same completion again (the log read back at the next start, after the file): nothing more.
        ledger.Save(path);
        ledger = StashLedger.Load(path);
        Assert.True(ledger.HandedOver(Quest, Day.AddHours(1)));
        Assert.Empty(ledger.HandOver(Day.AddHours(1), Quest, [(Camera, 3)]));
        Assert.Equal(2, ledger.MarkOf(Camera)!.Count);
        // Another quest's hand-over takes the rest: the mark goes.
        const string other = "000000000000000000000101";
        Assert.Equal([Camera], ledger.HandOver(Day.AddHours(2), other, [(Camera, 2)]));
        Assert.Null(ledger.MarkOf(Camera));
        // A mark observed after a completion isn't touched by it: the player had the item again.
        ledger.ApplyScreenshot(Day.AddHours(3), true, [(Camera, 1, null)]);
        const string third = "000000000000000000000102";
        Assert.Empty(ledger.HandOver(Day.AddHours(2).AddMinutes(30), third, [(Camera, 1)]));
        Assert.Equal(1, ledger.MarkOf(Camera)!.Count);
        // The quest done again later (a prestige): that completion counts anew.
        Assert.Equal([Camera], ledger.HandOver(Day.AddDays(30), Quest, [(Camera, 3)]));
        Assert.Null(ledger.MarkOf(Camera));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [Fact]
    public void Forget_all_takes_every_mark_and_keeps_older_messages_forgotten()
    {
        var path = Path.Combine(Path.GetTempPath(), "shturmap-ledger-" + Guid.NewGuid(), "stash.json");
        var ledger = new StashLedger();
        ledger.ApplyGrant(Day, GrantKind.QuestReward, Quest, [(Key, 1)]);
        ledger.ApplyScreenshot(Day.AddHours(1), true, [(Camera, 2, null)]);
        Assert.Equal(2, ledger.ForgetAll(Day.AddHours(2)));
        Assert.Empty(ledger.Marks);
        Assert.Empty(ledger.Passes);
        // The log read back at the next start: the reward from before stays forgotten; a newer observation stands.
        ledger.Save(path);
        var again = StashLedger.Load(path);
        Assert.Empty(again.ApplyGrant(Day, GrantKind.QuestReward, Quest, [(Key, 1)]));
        Assert.Empty(again.Marks);
        Assert.Equal([Camera], again.ApplyScreenshot(Day.AddHours(3), true, [(Camera, 1, null)]));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [Fact]
    public void The_file_round_trips_and_holds_ids_only()
    {
        var path = Path.Combine(Path.GetTempPath(), "shturmap-ledger-" + Guid.NewGuid(), "stash.json");
        var ledger = new StashLedger();
        ledger.ApplyGrant(Day, GrantKind.QuestStart, Quest, [(Key, 1)]);
        ledger.ApplyScreenshot(Day.AddHours(1), true, [(Camera, 2, true)]);
        ledger.ApplyScreenshot(Day.AddHours(1).AddMinutes(1), true, []);
        ledger.ApplyScreenshot(Day.AddHours(1).AddMinutes(2), true, []);
        ledger.EndPass();
        ledger.Retract(Key, Day.AddHours(2));
        ledger.Save(path);
        Assert.False(ledger.Dirty);
        var text = File.ReadAllText(path);
        Assert.Contains("\"version\": 1", text);
        Assert.DoesNotContain("\\\\", text);
        Assert.DoesNotContain("png", text, StringComparison.OrdinalIgnoreCase);

        var again = StashLedger.Load(path);
        Assert.Null(again.MarkOf(Key)); // retracted
        var camera = again.MarkOf(Camera)!;
        Assert.Equal(2, camera.Count);
        Assert.Equal(2, camera.FoundInRaid);
        Assert.Equal(Day.AddHours(1).AddMinutes(0), camera.SeenAt);
        Assert.Equal(MarkSource.Screenshot, camera.Source);
        Assert.Single(again.Passes);
        Assert.True(again.Passes[0].Full);
        // The retraction survives: the key seen before it stays hidden, a key seen after it shows.
        again.ApplyScreenshot(Day.AddHours(3), true, [(Key, 1, null)]);
        Assert.NotNull(again.MarkOf(Key));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [Fact]
    public void A_missing_or_broken_file_gives_an_empty_ledger()
    {
        var folder = Path.Combine(Path.GetTempPath(), "shturmap-ledger-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "stash.json");
        Assert.Empty(StashLedger.Load(path).Marks);
        File.WriteAllText(path, "{ not json");
        string? warned = null;
        Assert.Empty(StashLedger.Load(path, w => warned = w).Marks);
        Assert.NotNull(warned);
        Assert.True(File.Exists(path + ".unreadable"));
        Assert.False(File.Exists(path));
        Directory.Delete(folder, true);
    }
}
