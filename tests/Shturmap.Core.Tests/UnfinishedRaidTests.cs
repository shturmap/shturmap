using Shturmap.Core.Logs;
using Shturmap.Core.Raid;

namespace Shturmap.Core.Tests;

// A raid whose end never reached the log (the game closed or crashed in it) doesn't stay "in raid", and nobody takes
// a length from where it was cut off (review of 2026-10-04, A2).
public class UnfinishedRaidTests
{
    private static readonly DateTime Start = new(2026, 10, 3, 21, 0, 0);

    private static RaidTracker InRaid(DateTime started)
    {
        var tracker = new RaidTracker();
        tracker.Apply(new ProfileLoadedEvent(started.AddMinutes(-3), "pmc"));
        tracker.Apply(new MapLoadingEvent(started.AddMinutes(-2), "maps/customs_preset.bundle", null));
        tracker.Apply(new GameStartingEvent(started.AddSeconds(-1)));
        Assert.IsType<RaidStarted>(tracker.Apply(new GameStartedEvent(started)));
        return tracker;
    }

    [Fact]
    public void An_open_raid_can_run_for_its_maps_raid_length_and_a_margin()
    {
        var raid = InRaid(Start).State;
        Assert.Equal(TimeSpan.FromMinutes(70), UnfinishedRaid.Bound(40));
        Assert.False(UnfinishedRaid.CannotStillRun(raid, Start.AddMinutes(39), 40, newerSession: false));
        Assert.False(UnfinishedRaid.CannotStillRun(raid, Start.AddMinutes(70), 40, newerSession: false));
        Assert.True(UnfinishedRaid.CannotStillRun(raid, Start.AddMinutes(71), 40, newerSession: false));
        // The raid of the evening before, read back the next day.
        Assert.True(UnfinishedRaid.CannotStillRun(raid, Start.AddHours(22), 40, newerSession: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Without_a_raid_length_the_bound_is_fixed(int? minutes)
    {
        var raid = InRaid(Start).State;
        Assert.Equal(UnfinishedRaid.WithoutLength, UnfinishedRaid.Bound(minutes));
        Assert.False(UnfinishedRaid.CannotStillRun(raid, Start + UnfinishedRaid.WithoutLength, minutes, newerSession: false));
        Assert.True(UnfinishedRaid.CannotStillRun(raid, Start + UnfinishedRaid.WithoutLength + TimeSpan.FromSeconds(1), minutes, newerSession: false));
    }

    [Fact]
    public void A_newer_log_session_means_the_game_started_again()
    {
        var raid = InRaid(Start).State;
        Assert.True(UnfinishedRaid.CannotStillRun(raid, Start.AddMinutes(5), 40, newerSession: true));
    }

    [Fact]
    public void A_load_counts_from_its_scene_line_and_the_menus_are_never_stale()
    {
        var tracker = new RaidTracker();
        tracker.Apply(new MapLoadingEvent(Start, "maps/customs_preset.bundle", null));
        Assert.Equal(RaidPhase.Loading, tracker.State.Phase);
        Assert.False(UnfinishedRaid.CannotStillRun(tracker.State, Start.AddMinutes(20), 40, newerSession: false));
        Assert.True(UnfinishedRaid.CannotStillRun(tracker.State, Start.AddMinutes(71), 40, newerSession: false));

        Assert.False(UnfinishedRaid.CannotStillRun(new RaidState(), Start.AddDays(3), 40, newerSession: true));
    }

    [Fact]
    public void Closing_an_open_raid_ends_it_without_a_length()
    {
        var tracker = InRaid(Start);
        var lastSaid = Start.AddMinutes(4);
        var ended = tracker.CloseUnfinished(lastSaid)!;
        Assert.False(ended.EndInLog);
        Assert.Null(ended.Length);
        Assert.Equal(lastSaid, ended.At);
        Assert.Equal(Start, ended.Previous.RaidStartedAt);
        Assert.Equal(RaidPhase.Menu, tracker.State.Phase);

        // The next game start's login line finds no raid to end: no raid as long as the game was closed.
        Assert.Null(tracker.Apply(new ProfileLoadedEvent(Start.AddHours(22), "pmc")));
        Assert.Null(tracker.CloseUnfinished(Start.AddHours(23)));
    }

    [Fact]
    public void An_end_the_log_tells_has_its_length()
    {
        var tracker = InRaid(Start);
        var ended = Assert.IsType<RaidEnded>(tracker.Apply(new ProfileLoadedEvent(Start.AddMinutes(18), "pmc")));
        Assert.True(ended.EndInLog);
        Assert.Equal(TimeSpan.FromMinutes(18), ended.Length);

        // A load given up has an end in the log, and no raid to measure.
        tracker.Apply(new MapLoadingEvent(Start.AddMinutes(30), "maps/customs_preset.bundle", null));
        var cancelled = Assert.IsType<RaidEnded>(tracker.Apply(new MatchingCancelledEvent(Start.AddMinutes(31))));
        Assert.True(cancelled.EndInLog);
        Assert.Null(cancelled.Length);
    }

    [Fact]
    public void An_unfinished_end_gives_no_outcome_hint()
    {
        var tracker = InRaid(Start);
        var hints = new RaidOutcomeHints();
        // The insurer's note came during the raid; with an end in the log it would be a hint.
        Assert.Null(hints.Notice(new InsuranceNoticeEvent(Start.AddMinutes(10), InsuranceNotice.Lost, "bigmap", 3), tracker.State, "bigmap"));
        Assert.Null(hints.Ended(tracker.CloseUnfinished(Start.AddMinutes(10))!, "bigmap"));
        // And it is forgotten: the next raid's end doesn't pick it up.
        var next = InRaid(Start.AddHours(1));
        Assert.Null(hints.Ended((RaidEnded)next.Apply(new ProfileLoadedEvent(Start.AddHours(1).AddMinutes(20), "pmc"))!, "bigmap"));
    }

    [Fact]
    public void A_real_session_cut_off_inside_a_raid_replays_to_an_open_raid_that_is_closed_not_ended_at_the_next_login()
    {
        // The Streets session's log, read up to its second raid's start: as if the game had been closed in that raid.
        var events = new List<GameEvent>();
        foreach (var file in Directory.GetFiles(Fixtures.PathTo("logs", "log_2026.01.01_15-00-00_1.1.5.1.47510"), "*application*.log"))
        {
            var reader = new LogRecordReader();
            var records = reader.Append(File.ReadAllText(file)).ToList();
            if (reader.Flush(force: true) is { } last)
                records.Add(last);
            events.AddRange(records.Select(GameLogParser.Parse).OfType<GameEvent>());
        }
        events = events.OrderBy(e => e.At).ToList();
        var starts = events.Select((e, i) => (e, i)).Where(x => x.e is GameStartedEvent).Select(x => x.i).ToList();
        Assert.True(starts.Count >= 2, "the fixture has at least two raids");

        var tracker = new RaidTracker();
        foreach (var e in events.Take(starts[1] + 1))
            tracker.Apply(e);
        Assert.Equal(RaidPhase.InRaid, tracker.State.Phase);
        var started = tracker.State.RaidStartedAt!.Value;
        var cutOff = events[starts[1]].At;

        // Read back within the raid's possible length it is still a raid; the next day it isn't.
        Assert.False(UnfinishedRaid.CannotStillRun(tracker.State, started.AddMinutes(20), 40, newerSession: false));
        Assert.True(UnfinishedRaid.CannotStillRun(tracker.State, started.AddHours(22), 40, newerSession: false));

        var ended = tracker.CloseUnfinished(cutOff)!;
        Assert.False(ended.EndInLog);
        Assert.Null(ended.Length);
        // The next session's first lines (its mode, its login) end nothing.
        Assert.Null(tracker.Apply(new ProfileLoadedEvent(started.AddHours(22), "pmc")));
    }
}
