using Shturmap.Core.Logs;
using Shturmap.Core.Raid;

namespace Shturmap.Core.Tests;

// The game's logs give local wall-clock times without an offset, and two of them apart are an hour off across a clock
// change (review of 2026-10-04, A41). Tested in a named time zone, not the PC's own: Central Europe, where in 2026
// the clocks go forward on 29 March (02:00 becomes 03:00) and back on 25 October (03:00 becomes 02:00 again).
public class WallClockTests
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    private static DateTime Ordinary(int hour, int minute, int second = 0) => new(2026, 10, 3, hour, minute, second);

    private static DateTime Spring(int hour, int minute) => new(2026, 3, 29, hour, minute, 0);

    private static DateTime Autumn(int hour, int minute, int second = 0) => new(2026, 10, 25, hour, minute, second);

    private static TimeSpan Minutes(double minutes) => TimeSpan.FromMinutes(minutes);

    // ---- time passed ----

    [Fact]
    public void On_an_ordinary_night_the_time_passed_is_the_clock_times_apart()
    {
        Assert.Equal(Minutes(12), WallClock.Elapsed(Ordinary(21, 2), Ordinary(21, 14), Zone));
        Assert.Equal(Minutes(12), WallClock.Apart(Ordinary(21, 2), Ordinary(21, 14), Zone));
        Assert.Single(WallClock.Instants(Ordinary(21, 2), Zone));
    }

    [Fact]
    public void On_the_night_the_clocks_go_forward_an_hour_of_the_clock_did_not_pass()
    {
        // 01:50 to 03:25 is 95 minutes on the clock and 35 minutes of time.
        Assert.Equal(Minutes(35), WallClock.Elapsed(Spring(1, 50), Spring(3, 25), Zone));
        Assert.Equal(Minutes(35), WallClock.Apart(Spring(1, 50), Spring(3, 25), Zone));
    }

    [Fact]
    public void On_the_night_the_clocks_go_back_an_hour_passed_that_the_clock_does_not_show()
    {
        // 01:50 to 03:10 is 80 minutes on the clock and 140 minutes of time.
        Assert.Equal(Minutes(140), WallClock.Elapsed(Autumn(1, 50), Autumn(3, 10), Zone));
    }

    [Fact]
    public void In_the_repeated_hour_time_does_not_run_backwards_and_the_shorter_reading_is_taken()
    {
        // The clock stepped back between the two: 02:50 in the first pass, 02:10 in the second.
        Assert.Equal(Minutes(20), WallClock.Elapsed(Autumn(2, 50), Autumn(2, 10), Zone));
        // Both in one pass, or 100 minutes across both: nothing says which, and the shorter is taken.
        Assert.Equal(Minutes(40), WallClock.Elapsed(Autumn(2, 10), Autumn(2, 50), Zone));
        // A start in the repeated hour counts from its later reading, an end in it to its earlier one.
        Assert.Equal(Minutes(60), WallClock.Elapsed(Autumn(2, 30), Autumn(3, 30), Zone));
        Assert.Equal(Minutes(60), WallClock.Elapsed(Autumn(1, 30), Autumn(2, 30), Zone));
    }

    [Fact]
    public void A_clock_set_back_by_hand_gives_a_negative_time_not_a_made_up_one() =>
        Assert.Equal(Minutes(-12), WallClock.Elapsed(Ordinary(21, 14), Ordinary(21, 2), Zone));

    [Fact]
    public void Two_times_close_together_in_either_order_are_read_as_close()
    {
        // A note 18 s before a raid's end line, both in the repeated hour.
        Assert.Equal(TimeSpan.FromSeconds(-18), WallClock.Apart(Autumn(2, 20, 18), Autumn(2, 20, 0), Zone));
        Assert.Equal(TimeSpan.FromSeconds(18), WallClock.Apart(Autumn(2, 20, 0), Autumn(2, 20, 18), Zone));
        Assert.Equal(TimeSpan.FromSeconds(-18), WallClock.Apart(Ordinary(21, 20, 18), Ordinary(21, 20, 0), Zone));
    }

    // ---- instants ----

    [Fact]
    public void A_time_in_the_repeated_hour_stands_for_two_instants_an_hour_apart()
    {
        var readings = WallClock.Instants(Autumn(2, 30), Zone);
        Assert.Equal(new[] { new DateTime(2026, 10, 25, 0, 30, 0), new DateTime(2026, 10, 25, 1, 30, 0) }, readings);
        Assert.All(readings, r => Assert.Equal(DateTimeKind.Utc, r.Kind));
    }

    [Fact]
    public void A_time_in_the_skipped_hour_is_read_as_if_the_clocks_had_not_moved_yet()
    {
        // No clock shows 02:30 on that night; a line that says so must not stop the reading.
        Assert.Equal(new[] { new DateTime(2026, 3, 29, 1, 30, 0) }, WallClock.Instants(Spring(2, 30), Zone));
        Assert.Equal(Minutes(40), WallClock.Elapsed(Spring(1, 50), Spring(2, 30), Zone));
    }

    [Fact]
    public void A_time_that_is_an_instant_already_stays_what_it_is()
    {
        var utc = new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc);
        Assert.Equal(new[] { utc }, WallClock.Instants(utc, Zone));
        // 00:30 UTC is the first pass through 02:30; the log's 02:10 after it can only be the second.
        Assert.Equal(Minutes(40), WallClock.Elapsed(utc, Autumn(2, 10), Zone));
    }

    // ---- the lines of one log ----

    [Fact]
    public void A_logs_lines_keep_their_order_through_the_repeated_hour()
    {
        var log = new WallClock.Sequence(Zone);
        var written = new[] { Autumn(1, 55), Autumn(2, 40), Autumn(2, 59), Autumn(2, 5), Autumn(2, 30), Autumn(3, 10) };
        var instants = written.Select(log.Next).ToList();
        Assert.Equal(instants.Order(), instants);
        // The first pass until the clock steps back, the second after it.
        Assert.Equal(new DateTime(2026, 10, 25, 0, 40, 0), instants[1]);
        Assert.Equal(new DateTime(2026, 10, 25, 1, 5, 0), instants[3]);
        Assert.Equal(new DateTime(2026, 10, 25, 2, 10, 0), instants[5]);
    }

    [Fact]
    public void A_log_that_begins_in_the_repeated_hour_is_read_from_its_first_pass()
    {
        // Only so can a later step back to 02:00 be told from the hour going on.
        var log = new WallClock.Sequence(Zone);
        Assert.Equal(new DateTime(2026, 10, 25, 0, 50, 0), log.Next(Autumn(2, 50)));
        Assert.Equal(new DateTime(2026, 10, 25, 1, 10, 0), log.Next(Autumn(2, 10)));
    }

    // ---- where it is used ----

    [Fact]
    public void A_raids_length_is_the_time_it_ran()
    {
        static RaidEnded Raid(DateTime started, DateTime ended) =>
            new(new RaidState { Phase = RaidPhase.InRaid, RaidStartedAt = started }, new RaidState(), ended);

        Assert.Equal(Minutes(18), Raid(Ordinary(21, 2), Ordinary(21, 20)).LengthIn(Zone));
        Assert.Equal(Minutes(35), Raid(Spring(1, 50), Spring(3, 25)).LengthIn(Zone));
        // Begun at 02:50, ended at 02:10: minus 40 minutes by the clock times.
        Assert.Equal(Minutes(20), Raid(Autumn(2, 50), Autumn(2, 10)).LengthIn(Zone));
        Assert.Null((Raid(Autumn(2, 50), Autumn(2, 10)) with { EndInLog = false }).LengthIn(Zone));
    }

    [Fact]
    public void A_positions_age_is_the_time_since_its_screenshot()
    {
        Assert.Equal(Minutes(4), Shturmap.App.Rules.FixAge.Of(Ordinary(21, 2), Ordinary(21, 6), Zone));
        // Taken at 01:58, looked at at 03:02: four minutes old, not sixty-four.
        Assert.Equal(Minutes(4), Shturmap.App.Rules.FixAge.Of(Spring(1, 58), Spring(3, 2), Zone));
    }

    [Fact]
    public void An_open_raid_is_not_closed_for_an_hour_the_clock_skipped()
    {
        var raid = new RaidState { Phase = RaidPhase.InRaid, RaidStartedAt = Spring(1, 50) };
        // 35 minutes into a 40-minute raid; by the clock times 95, which is past the raid's length and its margin.
        Assert.False(UnfinishedRaid.CannotStillRun(raid, Spring(3, 25), raidMinutes: 40, newerSession: false, Zone));
        Assert.True(UnfinishedRaid.CannotStillRun(raid, Spring(4, 5), raidMinutes: 40, newerSession: false, Zone));
    }

    [Fact]
    public void An_open_raid_begun_in_the_repeated_hour_counts_from_its_later_reading()
    {
        // Closing a raid that still runs is the worse mistake: 02:40 may have been 55 minutes ago, or 115.
        var raid = new RaidState { Phase = RaidPhase.InRaid, RaidStartedAt = Autumn(2, 40) };
        Assert.False(UnfinishedRaid.CannotStillRun(raid, Autumn(3, 35), raidMinutes: 40, newerSession: false, Zone));
        Assert.True(UnfinishedRaid.CannotStillRun(raid, Autumn(4, 0), raidMinutes: 40, newerSession: false, Zone));
        // A raid from before the change is as old as the time that passed: 01:00 to 03:30 is three and a half hours,
        // not the two and a half the clock times are apart.
        var early = new RaidState { Phase = RaidPhase.InRaid, RaidStartedAt = Autumn(1, 0) };
        Assert.True(UnfinishedRaid.CannotStillRun(early, Autumn(3, 30), raidMinutes: 150, newerSession: false, Zone));
    }
}
