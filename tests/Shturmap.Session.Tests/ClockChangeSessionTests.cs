using Shturmap.Core.Raid;

namespace Shturmap.Session.Tests;

// The two nights a year the clocks change (review of 2026-10-04, A41): the log's times are wall-clock times, and what
// is computed from them was an hour off. Central Europe's zone is given to the session, not the PC's: there the
// clocks went forward on 29 March 2026 (02:00 became 03:00) and back on 25 October (03:00 became 02:00 again).
public class ClockChangeSessionTests
{
    private const string Customs = "maps/customs_preset.bundle";
    private const string Menu = "PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0";
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    [Fact]
    public void The_status_bar_counts_the_minutes_that_passed_and_says_the_start_as_the_log_wrote_it()
    {
        var raid = new RaidState { Phase = RaidPhase.InRaid, Side = RaidSide.Pmc, RaidStartedAt = new DateTime(2026, 3, 29, 1, 50, 0) };
        var now = new DateTime(2026, 3, 29, 3, 25, 0);
        // 95 minutes by the two clock times.
        Assert.Equal("In raid · Customs · PMC · 35 min", RaidStatus.Text(raid, "Customs", now, Zone));
        Assert.Equal("The game's log shows a raid, started 01:50", RaidStatus.Tooltip(raid, now));
    }

    [Fact]
    public async Task A_raid_across_the_spring_change_is_not_closed_an_hour_early()
    {
        var clock = new DateTime(2026, 3, 29, 3, 25, 0);
        await using var rig = new SessionRig(new DateTime(2026, 3, 29, 1, 30, 0), (paths, locations) => new GameSession(paths, locations)
        {
            GivenData = SessionRig.Data,
            OpenRaidCheck = TimeSpan.FromMilliseconds(50),
            Clock = () => clock,
            Zone = Zone,
        });
        rig.RaidUpToItsStart(Customs, "bigmap", new DateTime(2026, 3, 29, 1, 50, 0));
        await rig.StartAsync();

        // 35 minutes into Customs' 40: by the clock times 95, past the raid's length and its margin.
        var s = await rig.Until(x => x.Raid.Phase == RaidPhase.InRaid && x.RaidMap is not null, "the raid");
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Equal(RaidPhase.InRaid, rig.Snapshot.Raid.Phase);
        Assert.Equal("In raid · Customs · PMC · 35 min", RaidStatus.Text(s, clock, Zone));

        // 75 minutes in, with no end line: dropped as before.
        clock = new DateTime(2026, 3, 29, 4, 5, 0);
        await rig.Until(x => x.Raid.Phase == RaidPhase.Menu && x.LastRaid is not null, "the raid dropped");
    }

    [Fact]
    public async Task A_raid_through_the_repeated_autumn_hour_ends_after_it_began()
    {
        await using var rig = new SessionRig(new DateTime(2026, 10, 25, 1, 30, 0), (paths, locations) => new GameSession(paths, locations)
        {
            GivenData = SessionRig.Data,
            Clock = () => new DateTime(2026, 10, 25, 3, 30, 0),
            Zone = Zone,
        });
        // Started at 02:50, in the first pass through the hour; back in the menus at 02:10, in the second. Sorted by
        // their clock times the end came first, and the raid then stayed open with no end.
        rig.RaidUpToItsStart(Customs, "bigmap", new DateTime(2026, 10, 25, 2, 50, 0));
        rig.Log(Menu, new DateTime(2026, 10, 25, 2, 10, 0));
        await rig.StartAsync();

        var s = await rig.Until(x => x.LastRaid is not null, "the last raid");
        Assert.Equal(RaidPhase.Menu, s.Raid.Phase);
        Assert.True(s.LastRaid!.LengthKnown);
        Assert.Equal("Last raid · Customs · 20 min · PMC", RaidStatus.LastRaid(s.LastRaid));
    }
}
