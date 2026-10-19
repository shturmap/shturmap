using Shturmap.Core.Logs;
using Shturmap.Core.Operational;

namespace Shturmap.Core.Tests.Operational;

// The daily and weekly operational tasks read from a screenshot of the Tasks screen (owner, 2026-10-19: from a panel
// of four, B; docs/DESIGN.md §2 and §8): the checks the countdowns read by vote must pass, the set a newer table
// replaces, the objectives an expanded row brings, the tasks that run out, the file. Made-up names and ids.
public class OperationalTasksTests
{
    private static readonly DateTimeOffset Day = new(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(1));

    private const string Customs = "map-customs", Woods = "map-woods";

    private static OperationalRead Row(string name, string? map, TimeSpan? left, int votes = 3, int? progress = null, params OperationalObjective[] objectives) =>
        new(name, map, left, votes, progress, objectives);

    // ---- the checks on the countdowns ----

    [Fact]
    public void A_countdown_outside_a_week_or_at_nothing_is_not_taken()
    {
        var kept = OperationalChecks.Countdowns([(new TimeSpan(8, 0, 0), 3), (TimeSpan.FromDays(7.5), 5), (TimeSpan.Zero, 4), (null, 0), (TimeSpan.FromDays(7), 2)]);
        Assert.Equal([new TimeSpan(8, 0, 0), null, null, null, TimeSpan.FromDays(7)], kept);
    }

    [Fact]
    public void Rows_that_share_a_countdown_agree_or_the_one_with_more_votes_keeps_its_own()
    {
        // 10:14:11 and 10:14:12 agree (a second apart: one shot, one moment); 10:14:41 is half a minute off them, so one of
        // them was misread: the one more ways read keeps its own.
        var at = new TimeSpan(10, 14, 11);
        var kept = OperationalChecks.Countdowns([(at, 4), (at + TimeSpan.FromSeconds(1), 3), (at + TimeSpan.FromSeconds(30), 2)]);
        Assert.Equal([at, at + TimeSpan.FromSeconds(1), null], kept);
        // A tie leaves neither.
        Assert.Equal([null, null], OperationalChecks.Countdowns([(at, 3), (at + TimeSpan.FromSeconds(20), 3)]));
        // Countdowns minutes apart are two countdowns, not one misread.
        Assert.Equal([at, at + TimeSpan.FromMinutes(5)], OperationalChecks.Countdowns([(at, 2), (at + TimeSpan.FromMinutes(5), 5)]));
    }

    [Theory]
    [InlineData(57, 4, 7, true, 57)]   // read, and the count agrees
    [InlineData(58, 4, 7, true, 58)]   // a point off: rounding
    [InlineData(75, 4, 7, true, 57)]   // the count read on the same shot wins over a percentage it contradicts
    [InlineData(75, 4, 7, false, 75)]  // an older count can't overrule a newer percentage
    [InlineData(null, 4, 7, true, 57)] // no percentage: from the count
    public void Progress_is_the_percentage_read_when_it_passes_else_the_count(int? read, int done, int of, bool readNow, int expected) =>
        Assert.Equal(expected, OperationalChecks.Progress(read, [new OperationalObjective("Survive and extract from the location", done, of)], readNow));

    [Fact]
    public void Progress_without_a_percentage_or_counts_is_none() =>
        Assert.Null(OperationalChecks.Progress(null, [new OperationalObjective("Hand over the found in raid item: \"Zorb drive\"")], true));

    // ---- the set ----

    [Fact]
    public void A_table_is_kept_per_mode_with_each_tasks_expiry_and_a_newer_table_replaces_the_set()
    {
        var ledger = new OperationalLedger();
        ledger.ApplyTable(GameMode.Pve, Day, [Row("Elimination", Customs, new TimeSpan(10, 14, 11)), Row("Find and transfer", null, TimeSpan.FromDays(2))]);
        var tasks = ledger.Tasks(GameMode.Pve);
        Assert.Equal(["Elimination", "Find and transfer"], tasks.Select(t => t.Name));
        Assert.Equal(Day + new TimeSpan(10, 14, 11), tasks[0].ExpiresAt);
        Assert.True(tasks[1].AnyMap);
        Assert.Empty(ledger.Tasks(GameMode.Pvp));
        Assert.Equal(Day, ledger.ReadAt(GameMode.Pve));

        // A task handed in or replaced is gone from the next table: gone from the set.
        ledger.ApplyTable(GameMode.Pve, Day.AddHours(1), [Row("Find and transfer", null, TimeSpan.FromDays(2) - TimeSpan.FromHours(1))]);
        Assert.Equal(["Find and transfer"], ledger.Tasks(GameMode.Pve).Select(t => t.Name));
        // An older screenshot changes nothing.
        Assert.Null(ledger.ApplyTable(GameMode.Pve, Day, [Row("Elimination", Customs, null)]));
        Assert.Single(ledger.Tasks(GameMode.Pve));
    }

    [Fact]
    public void An_expanded_row_brings_the_tasks_objectives_and_a_folded_row_keeps_those_read_before()
    {
        var ledger = new OperationalLedger();
        var kill = new OperationalObjective("Eliminate Zorbs with a headshot from a distance of more than 50m", 0, 3);
        ledger.ApplyTable(GameMode.Pve, Day, [Row("Elimination", Customs, null, objectives: kill), Row("Exit the location", Woods, null)]);
        var first = ledger.Tasks(GameMode.Pve);
        Assert.Equal(OperationalKind.Kill, first[0].Kind);
        // Not expanded: its kind from its name's words, no objectives.
        Assert.Equal(OperationalKind.Survive, first[1].Kind);
        Assert.Empty(first[1].Objectives);

        // The next shot opens the second row: it brings that task's objectives, and the first keeps its own.
        var survive = new OperationalObjective("Survive and extract from the location", 4, 7);
        ledger.ApplyTable(GameMode.Pve, Day.AddMinutes(5), [Row("Elimination", Customs, null), Row("Exit the location", Woods, null, objectives: survive)]);
        var second = ledger.Tasks(GameMode.Pve);
        Assert.Equal([kill], second[0].Objectives);
        Assert.Equal(Day, second[0].ObjectivesAt);
        Assert.Equal([survive], second[1].Objectives);
        Assert.Equal(Day.AddMinutes(5), second[1].ObjectivesAt);

        // A newer expanded row replaces the objectives (the count moved on).
        var later = survive with { Done = 5 };
        ledger.ApplyTable(GameMode.Pve, Day.AddMinutes(30), [Row("Elimination", Customs, null), Row("Exit the location", Woods, null, objectives: later)]);
        Assert.Equal([later], ledger.Tasks(GameMode.Pve)[1].Objectives);
        Assert.Equal((5, 7), ledger.Tasks(GameMode.Pve)[1].Count);
    }

    [Fact]
    public void A_count_read_before_a_newer_percentage_that_contradicts_it_is_not_shown_as_the_count()
    {
        var ledger = new OperationalLedger();
        ledger.ApplyTable(GameMode.Pve, Day, [Row("Exit the location", Woods, null, objectives: new OperationalObjective("Survive and extract from the location", 4, 7))]);
        ledger.ApplyTable(GameMode.Pve, Day.AddHours(2), [Row("Exit the location", Woods, null, progress: 71)]);
        var task = Assert.Single(ledger.Tasks(GameMode.Pve));
        Assert.Equal(71, task.Progress);
        Assert.Null(task.Count);
    }

    [Fact]
    public void The_same_tasks_expiry_from_two_screenshots_must_agree_or_it_has_none()
    {
        var ledger = new OperationalLedger();
        ledger.ApplyTable(GameMode.Pve, Day, [Row("Elimination", Customs, new TimeSpan(10, 14, 11)), Row("Exit the location", Woods, new TimeSpan(8, 33, 19))]);
        // Ten minutes later: the first reads ten minutes less (agrees, within a few seconds), the second a digit off.
        var dropped = ledger.ApplyTable(GameMode.Pve, Day.AddMinutes(10),
            [Row("Elimination", Customs, new TimeSpan(10, 4, 13)), Row("Exit the location", Woods, new TimeSpan(8, 23, 49))]);
        Assert.Equal(1, dropped);
        var tasks = ledger.Tasks(GameMode.Pve);
        Assert.Equal(Day.AddMinutes(10) + new TimeSpan(10, 4, 13), tasks[0].ExpiresAt);
        Assert.Null(tasks[1].ExpiresAt);
    }

    [Fact]
    public void A_countdown_unread_or_failing_keeps_the_expiry_an_earlier_screenshot_gave()
    {
        var ledger = new OperationalLedger();
        ledger.ApplyTable(GameMode.Pve, Day, [Row("Elimination", Customs, new TimeSpan(10, 14, 11))]);
        ledger.ApplyTable(GameMode.Pve, Day.AddMinutes(10), [Row("Elimination", Customs, null)]);
        Assert.Equal(Day + new TimeSpan(10, 14, 11), Assert.Single(ledger.Tasks(GameMode.Pve)).ExpiresAt);
        // Past a week: fails, and the earlier one stands.
        Assert.Equal(1, ledger.ApplyTable(GameMode.Pve, Day.AddMinutes(20), [Row("Elimination", Customs, TimeSpan.FromDays(9))]));
        Assert.Equal(Day + new TimeSpan(10, 14, 11), Assert.Single(ledger.Tasks(GameMode.Pve)).ExpiresAt);
    }

    [Fact]
    public void Two_tasks_of_one_name_on_one_map_stay_apart()
    {
        var ledger = new OperationalLedger();
        ledger.ApplyTable(GameMode.Pve, Day, [
            Row("Elimination", Customs, null, objectives: new OperationalObjective("Eliminate Zorbs", 0, 3)),
            Row("Elimination", Customs, null, objectives: new OperationalObjective("Eliminate Quenviks", 1, 5))]);
        ledger.ApplyTable(GameMode.Pve, Day.AddMinutes(1), [Row("Elimination", Customs, null), Row("Elimination", Customs, null)]);
        Assert.Equal(["Eliminate Zorbs", "Eliminate Quenviks"], ledger.Tasks(GameMode.Pve).Select(t => t.Objectives.Single().Text));
    }

    [Fact]
    public void A_task_goes_at_its_expiry_and_is_said_to_have_ended_until_the_next_table_or_a_day()
    {
        var ledger = new OperationalLedger();
        ledger.ApplyTable(GameMode.Pve, Day, [Row("Elimination", Customs, TimeSpan.FromHours(2)), Row("Find and transfer", null, TimeSpan.FromDays(2))]);
        Assert.Equal(Day.AddHours(2), ledger.NextExpiry());
        Assert.Equal(0, ledger.Expire(Day.AddHours(1)));
        Assert.Null(ledger.Ended(GameMode.Pve));

        Assert.Equal(1, ledger.Expire(Day.AddHours(2)));
        Assert.Equal(["Find and transfer"], ledger.Tasks(GameMode.Pve).Select(t => t.Name));
        Assert.Equal(new OperationalEnded(1, Day.AddHours(2)), ledger.Ended(GameMode.Pve));

        // A day after it ended, it isn't said any more.
        ledger.Expire(Day.AddHours(2) + OperationalLedger.EndedKept + TimeSpan.FromMinutes(1));
        Assert.Null(ledger.Ended(GameMode.Pve));

        // The next table replaces what was said.
        var again = new OperationalLedger();
        again.ApplyTable(GameMode.Pve, Day, [Row("Elimination", Customs, TimeSpan.FromHours(2))]);
        again.Expire(Day.AddHours(3));
        Assert.NotNull(again.Ended(GameMode.Pve));
        again.ApplyTable(GameMode.Pve, Day.AddHours(3), [Row("Elimination", Customs, TimeSpan.FromHours(20))]);
        Assert.Null(again.Ended(GameMode.Pve));
    }

    // ---- the file ----

    [Fact]
    public void The_file_keeps_the_tasks_words_maps_and_times_and_nothing_else()
    {
        var folder = Directory.CreateTempSubdirectory("shturmap-operational-");
        try
        {
            var path = Path.Combine(folder.FullName, OperationalLedger.FileName);
            var ledger = new OperationalLedger();
            ledger.ApplyTable(GameMode.Pve, Day, [
                Row("Elimination", Customs, new TimeSpan(10, 14, 11), progress: 0, objectives: new OperationalObjective("Eliminate Zorbs", 0, 3)),
                Row("Find and transfer", null, TimeSpan.FromDays(2), objectives: new OperationalObjective("Hand over the found in raid item: \"Zorb drive\""))]);
            ledger.ApplyTable(GameMode.Pvp, Day, [Row("Exit the location", Woods, TimeSpan.FromHours(3))]);
            ledger.Expire(Day.AddHours(4));
            ledger.Save(path);
            Assert.False(ledger.Dirty);

            var json = File.ReadAllText(path);
            Assert.Contains("\"pve\"", json, StringComparison.Ordinal);
            Assert.Contains("\"kind\": \"kill\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("votes", json, StringComparison.OrdinalIgnoreCase);

            var back = OperationalLedger.Load(path);
            Assert.Equal(ledger.Tasks(GameMode.Pve).Select(t => (t.Name, t.MapId, t.Kind, t.Progress, t.ExpiresAt, t.ReadAt)),
                back.Tasks(GameMode.Pve).Select(t => (t.Name, t.MapId, t.Kind, t.Progress, t.ExpiresAt, t.ReadAt)));
            Assert.Equal(ledger.Tasks(GameMode.Pve)[0].Objectives, back.Tasks(GameMode.Pve)[0].Objectives);
            Assert.Empty(back.Tasks(GameMode.Pvp));
            Assert.Equal(new OperationalEnded(1, Day.AddHours(3)), back.Ended(GameMode.Pvp));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_file_that_cant_be_read_is_set_aside_and_the_tasks_start_over()
    {
        var folder = Directory.CreateTempSubdirectory("shturmap-operational-");
        try
        {
            var path = Path.Combine(folder.FullName, OperationalLedger.FileName);
            File.WriteAllText(path, "{ not json");
            string? warning = null;
            var ledger = OperationalLedger.Load(path, w => warning = w);
            Assert.Empty(ledger.Tasks(GameMode.Pve));
            Assert.NotNull(warning);
            Assert.True(File.Exists(path + ".unreadable"));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    // ---- kinds and synopses ----

    [Theory]
    [InlineData("Eliminate Scavs", OperationalKind.Kill)]
    [InlineData("Survive and extract from the location", OperationalKind.Survive)]
    [InlineData("Hand over the found in raid item: \"Zorb drive\"", OperationalKind.HandOver)]
    [InlineData("Mark the made-up spots", OperationalKind.Unknown)]
    public void The_objectives_words_say_the_kind(string text, OperationalKind kind) =>
        Assert.Equal(kind, OperationalWords.KindOf([new OperationalObjective(text)], "Zorblat"));

    [Theory]
    [InlineData("Elimination", OperationalKind.Kill)]
    [InlineData("Exit the location", OperationalKind.Survive)]
    [InlineData("Find and transfer", OperationalKind.HandOver)]
    [InlineData("Zorblat", OperationalKind.Unknown)]
    public void Without_objectives_the_names_words_say_it(string name, OperationalKind kind) =>
        Assert.Equal(kind, OperationalWords.KindOf([], name));

    private static OperationalTask Task(params OperationalObjective[] objectives) =>
        new("Zorblat", Customs, objectives, OperationalWords.KindOf(objectives, "Zorblat"), null, null, Day, Day);

    [Theory]
    [InlineData("Eliminate Scavs with a headshot from a distance of more than 50m", 3, "Kill 3 Scavs · headshots over 50 m")]
    [InlineData("Eliminate PMC operatives from a distance of less than 25m", 5, "Kill 5 PMC operatives · under 25 m")]
    [InlineData("Eliminate Zorbs on the location", 2, "Kill 2 Zorbs")]
    [InlineData("Eliminate any target on Lighthouse", 4, "Kill 4 any target")]
    [InlineData("Eliminate Zorbs while wearing a made-up helmet", 2, "Kill 2 Zorbs · while wearing a made-up helmet")]
    public void A_kill_says_how_many_whom_and_its_condition_short(string text, int of, string synopsis) =>
        Assert.Equal(synopsis, OperationalWords.Synopsis(Task(new OperationalObjective(text, 0, of))));

    [Fact]
    public void Survivals_hand_overs_and_unknown_lines_read_as_the_panel_drew_them()
    {
        Assert.Equal("Survive and extract", OperationalWords.Synopsis(Task(new OperationalObjective("Survive and extract from the location", 4, 7))));
        Assert.Equal("Find in raid: Zorb drive, Quill fan", OperationalWords.Synopsis(Task(
            new OperationalObjective("Hand over the found in raid item: \"Zorb drive\""), new OperationalObjective("Hand over the found in raid item: “Quill fan”"))));
        Assert.Equal("Mark the made-up spots", OperationalWords.Synopsis(Task(new OperationalObjective("Mark the made-up spots", 0, 2))));
        // A row never expanded: its name.
        Assert.Equal("Zorblat", OperationalWords.Synopsis(Task()));
        Assert.Equal(["Zorb drive"], OperationalWords.HandedOver("Hand over the found in raid item: \"Zorb drive\""));
    }
}
