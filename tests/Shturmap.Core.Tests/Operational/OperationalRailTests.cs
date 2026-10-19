using Shturmap.App.Rules;
using Shturmap.Core.Operational;

namespace Shturmap.Core.Tests.Operational;

// Where the operational tasks show (owner, 2026-10-19: from a panel of four, B; Rules.OperationalRail): a NEXT RAID row
// has its map's tasks only, a card's section has the map's then the any-map ones, and each row's muted line says
// progress and time as Plan and the raid need them. Made-up names; English texts.
public class OperationalRailTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 14, 2, 0, TimeSpan.FromHours(1));

    private static OperationalShown Shown(string name, string? mapKey, OperationalKind kind, TimeSpan? left, int? progress = null,
        DateTimeOffset? readAt = null, params OperationalObjective[] objectives)
    {
        var at = readAt ?? Now;
        return new OperationalShown(new OperationalTask(name, mapKey is null ? null : "id-" + mapKey, objectives, kind, progress,
            left is { } l ? Now + l : null, at, objectives.Length > 0 ? at : null), mapKey, []);
    }

    private static readonly OperationalShown Survive = Shown("Exit the location", "shoreline", OperationalKind.Survive, new TimeSpan(8, 33, 19), 57, null,
        new OperationalObjective("Survive and extract from the location", 4, 7));

    private static readonly OperationalShown Kill = Shown("Elimination", "lighthouse", OperationalKind.Kill, new TimeSpan(10, 14, 11), 0, null,
        new OperationalObjective("Eliminate Scavs with a headshot from a distance of more than 50m", 0, 3));

    private static readonly OperationalShown Transfer = Shown("Find and transfer", null, OperationalKind.HandOver, TimeSpan.FromDays(2) + TimeSpan.FromHours(3), null, null,
        new OperationalObjective("Hand over the found in raid item: \"Zorb drive\""));

    private static readonly IReadOnlyList<OperationalShown> All = [Survive, Kill, Transfer];

    [Fact]
    public void A_list_row_shows_its_maps_tasks_and_never_the_any_map_ones()
    {
        Assert.Equal([Survive], OperationalRail.OnRow(All, "shoreline"));
        Assert.Empty(OperationalRail.OnRow(All, "customs"));
        Assert.Empty(OperationalRail.OnRow(All, null));
    }

    // Time-sensitive (owner, 2026-10-19: "sorted on top of the list always as they are time-sensitive"): in each group of
    // the section, and on a list row, the task that ends soonest first; one without a countdown last; the dailies the game
    // gives together, which end in the same minute, in the table's order.
    [Fact]
    public void Within_the_section_the_task_that_ends_soonest_comes_first()
    {
        var weekly = Shown("Elimination", "customs", OperationalKind.Kill, TimeSpan.FromDays(4));
        var unsure = Shown("Elimination", "customs", OperationalKind.Kill, null, 20);
        var daily = Shown("Exit the location", "customs", OperationalKind.Survive, new TimeSpan(8, 14, 12));
        var together = Shown("Find and transfer", "customs", OperationalKind.HandOver, new TimeSpan(8, 14, 10));
        var soon = Shown("Exit the location", "customs", OperationalKind.Survive, TimeSpan.FromMinutes(25));
        var anyWeekly = Shown("Find and transfer", null, OperationalKind.HandOver, TimeSpan.FromDays(5));
        var anyDaily = Shown("Find and transfer", null, OperationalKind.HandOver, TimeSpan.FromHours(3));
        IReadOnlyList<OperationalShown> all = [weekly, unsure, anyWeekly, daily, together, soon, anyDaily];
        var (here, anywhere) = OperationalRail.Section(all, "customs");
        Assert.Equal([soon, daily, together, weekly, unsure], here);
        Assert.Equal([anyDaily, anyWeekly], anywhere);
        Assert.Equal(here, OperationalRail.OnRow(all, "customs"));
    }

    [Fact]
    public void A_cards_section_has_the_maps_tasks_then_the_any_map_ones_in_every_card()
    {
        var (here, anywhere) = OperationalRail.Section(All, "lighthouse");
        Assert.Equal([Kill], here);
        Assert.Equal([Transfer], anywhere);
        var (none, still) = OperationalRail.Section(All, "customs");
        Assert.Empty(none);
        Assert.Equal([Transfer], still);
        Assert.True(OperationalRail.Shows(All, "customs", null));
        Assert.False(OperationalRail.Shows([Survive], "customs", null));
        Assert.True(OperationalRail.Shows([], "customs", new OperationalEnded(1, Now)));
    }

    [Fact]
    public void Plan_says_progress_and_time_left_in_a_few_words()
    {
        Assert.Equal("4 of 7 · 8\u00A0h left", OperationalRail.PlanLine(Survive, Now));
        Assert.Equal("0 of 3 · 10\u00A0h left", OperationalRail.PlanLine(Kill, Now));
        Assert.Equal("any map · hand over after · 2\u00A0d left", OperationalRail.PlanLine(Transfer, Now));
        // A countdown not taken: the reading's age instead.
        var unsure = Shown("Elimination", "woods", OperationalKind.Kill, null, 20, Now.AddHours(-3));
        Assert.Equal("20% · read 3\u00A0h ago", OperationalRail.PlanLine(unsure, Now));
    }

    [Theory]
    [InlineData(25, "25\u00A0min left")]
    [InlineData(0.5, "1\u00A0min left")]
    [InlineData(60, "1\u00A0h left")]
    [InlineData(47 * 60 + 59, "47\u00A0h left")]
    [InlineData(48 * 60, "2\u00A0d left")]
    [InlineData(6 * 24 * 60 + 600, "6\u00A0d left")]
    public void A_time_left_goes_from_minutes_to_hours_to_days(double minutes, string text) =>
        Assert.Equal(text, OperationalRail.Left(TimeSpan.FromMinutes(minutes)));

    [Fact]
    public void In_a_raid_the_time_shows_only_when_the_task_ends_before_the_raid_could()
    {
        var soon = Shown("Exit the location", "customs", OperationalKind.Survive, TimeSpan.FromMinutes(24.5), 57, null,
            new OperationalObjective("Survive and extract from the location", 4, 7));
        Assert.Equal("ends in 24\u00A0min", OperationalRail.EndsIn(soon.Task, Now, minutesLeftInRaid: 38));
        Assert.Null(OperationalRail.EndsIn(soon.Task, Now, minutesLeftInRaid: 20));
        Assert.Null(OperationalRail.EndsIn(soon.Task, Now, minutesLeftInRaid: null));
        Assert.Null(OperationalRail.EndsIn(Survive.Task, Now, minutesLeftInRaid: 38));
        Assert.Equal("4 of 7", OperationalRail.RaidLine(soon));
        // ANYWHERE in the row's distance column says it: the line doesn't say "any map" again (QA, 2026-10-19).
        Assert.Equal("hand over after the raid", OperationalRail.RaidLine(Transfer));
    }

    // One rounding for both cards, down: a time never says more than there is (QA, 2026-10-19: Plan's "24 MIN LEFT" and
    // the raid's "ENDS IN 25 MIN" at the same moment).
    [Theory]
    [InlineData(24 * 60 + 59, "24\u00A0min left", "ends in 24\u00A0min")]
    [InlineData(24 * 60 + 40, "24\u00A0min left", "ends in 24\u00A0min")]
    [InlineData(40, "1\u00A0min left", "ends in 1\u00A0min")]
    public void Plan_and_the_raid_round_the_same_time_left_down_alike(int seconds, string plan, string raid)
    {
        var task = Shown("Exit the location", "customs", OperationalKind.Survive, TimeSpan.FromSeconds(seconds));
        Assert.Equal(plan, OperationalRail.PlanLine(task, Now));
        Assert.Equal(raid, OperationalRail.EndsIn(task.Task, Now, minutesLeftInRaid: 38));
    }

    [Fact]
    public void A_rows_line_breaks_only_between_its_parts_and_a_two_word_item_stays_whole()
    {
        Assert.Equal(["4 of 7", "hand over after the raid"],
            OperationalRail.Parts("4 of 7 · hand over after the raid"));
        Assert.Equal(["hand over after the raid"], OperationalRail.Parts("hand over after the raid"));
        Assert.Empty(OperationalRail.Parts(""));
        Assert.Equal("Find in raid: Zorb\u00A0drive, Big zorb chip set",
            OperationalRail.Synopsis("Find in raid: Zorb drive, Big zorb chip set",
                ["Zorb drive", "Big zorb chip set"]));
    }

    [Fact]
    public void The_eyebrow_says_when_they_were_read_and_the_ended_line_how_many_ran_out()
    {
        // In the PC's own time, as the clock beside it.
        var local = Now.ToLocalTime();
        Assert.Equal($"As of {local:HH:mm}", OperationalRail.AsOf(Now, Now.AddMinutes(5)));
        Assert.StartsWith("As of ", OperationalRail.AsOf(Now.AddDays(-1), Now), StringComparison.Ordinal);
        Assert.EndsWith($", {local:HH:mm}", OperationalRail.AsOf(Now.AddDays(-1), Now), StringComparison.Ordinal);
        var at = new DateTimeOffset(2026, 1, 1, 22, 35, 0, TimeSpan.FromHours(1)).ToLocalTime();
        Assert.Equal($"1 ended at {at:HH:mm}. Screenshot your tasks for the new one.", OperationalRail.Ended(new OperationalEnded(1, at)));
        Assert.Equal($"2 ended, the last at {at:HH:mm}. Screenshot your tasks for the new ones.", OperationalRail.Ended(new OperationalEnded(2, at)));
        Assert.Equal("", OperationalRail.Ended(null));
    }
}

// The eyebrow's date is written as the language in use writes a day and month (the senior translator's review of
// 2026-10-19: German read "18 Okt." from English's "d MMM"; UiLanguage.DayMonth).
[Collection("UiLanguage")]
public class OperationalAsOfDayTests
{
    [Fact]
    public void An_earlier_days_reading_says_its_date_in_the_language_in_use()
    {
        var readAt = new DateTimeOffset(new DateTime(2026, 10, 18, 14, 2, 0, DateTimeKind.Local));
        var now = readAt.AddDays(1);
        Assert.Equal("As of 18 Oct, 14:02", OperationalRail.AsOf(readAt, now));
        try
        {
            UiLanguage.Set(UiLanguage.German);
            Assert.Equal("Stand 18. Okt., 14:02", OperationalRail.AsOf(readAt, now));
        }
        finally
        {
            UiLanguage.Set(UiLanguage.English);
        }
    }
}
