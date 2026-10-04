using Shturmap.Core.Raid;

namespace Shturmap.Session.Tests;

// A raid whose end never reached the log (the game was closed or crashed in it) doesn't stay "in raid" for a day, and
// no length is made up for it (review of 2026-10-04, A2).
public class UnfinishedRaidSessionTests
{
    private const string Customs = "maps/customs_preset.bundle";

    // ---- the last-raid line ----

    [Fact]
    public void The_last_raid_line_says_the_length_or_that_the_end_is_not_in_the_log()
    {
        var at = new DateTime(2026, 10, 3, 21, 30, 0);
        Assert.Equal("Last raid · Customs · 18 min · PMC", RaidStatus.LastRaid(new LastRaidView("Customs", TimeSpan.FromMinutes(18.7), RaidSide.Pmc, at)));
        Assert.Equal("Last raid · Customs · 18 min", RaidStatus.LastRaid(new LastRaidView("Customs", TimeSpan.FromMinutes(18), RaidSide.Unknown, at)));
        Assert.Equal("Last raid · Customs · end not in the log · Scav",
            RaidStatus.LastRaid(new LastRaidView("Customs", TimeSpan.Zero, RaidSide.Scav, at) { LengthKnown = false }));
    }

    // ---- a whole session ----

    [Fact]
    public async Task A_raid_left_open_the_day_before_is_not_in_raid_at_the_next_start()
    {
        var yesterday = DateTime.Now.AddHours(-22);
        await using var rig = new SessionRig(logSessionStarted: yesterday.AddMinutes(-10));
        rig.RaidUpToItsStart(Customs, "bigmap", yesterday);
        await rig.StartAsync();

        // The last raid's map can only be named once the data is there; by then the replay has been read.
        var s = await rig.Until(x => x.LastRaid is not null, "the last raid");
        Assert.Equal(RaidPhase.Menu, s.Raid.Phase);
        Assert.Equal("Not in a raid", RaidStatus.Text(s, DateTime.Now));
        Assert.Null(s.RaidMap);
        Assert.False(s.LastRaid!.LengthKnown);
        Assert.Equal("Last raid · Customs · end not in the log · PMC", RaidStatus.LastRaid(s.LastRaid));
        // Read back at start: no cue.
        Assert.Empty(rig.Cues);
    }

    [Fact]
    public async Task A_raid_that_can_still_be_running_stays_a_raid_when_read_back()
    {
        await using var rig = new SessionRig();
        rig.RaidUpToItsStart(Customs, "bigmap", DateTime.Now.AddMinutes(-12));
        await rig.StartAsync();
        var s = await rig.Until(x => x.Raid.Phase == RaidPhase.InRaid && x.RaidMap is not null, "the raid");
        Assert.StartsWith("In raid · Customs · PMC · 12 min", RaidStatus.Text(s, DateTime.Now));
    }

    [Fact]
    public async Task The_game_starting_again_closes_the_open_raid_without_a_length()
    {
        await using var rig = new SessionRig();
        var started = DateTime.Now.AddMinutes(-12);
        rig.RaidUpToItsStart(Customs, "bigmap", started);
        await rig.StartAsync();
        await rig.Until(x => x.Raid.Phase == RaidPhase.InRaid && x.RaidMap is not null, "the raid");

        // The game crashed and starts again: a new log session, its mode, then its login.
        rig.NewLogSession(DateTime.Now);
        rig.Log("Session mode: Pve");
        rig.Log("PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0");

        var s = await rig.Until(x => x.Raid.Phase == RaidPhase.Menu && x.LastRaid is not null, "the raid closed");
        // Not a raid from its start to the new session's login.
        Assert.False(s.LastRaid!.LengthKnown);
        Assert.Equal("Customs", s.LastRaid.MapName);
        Assert.Null(s.RaidFix);
        await rig.Until(() => rig.Cues.Any(c => c.Kind == CueKind.RaidOver), "the RAID OVER cue");
        var cue = rig.Cues.Single(c => c.Kind == CueKind.RaidOver);
        Assert.Equal("Customs", cue.MapName);
        Assert.Null(cue.RaidLength);
    }

    [Fact]
    public async Task A_raid_the_log_falls_silent_in_is_dropped_once_it_cannot_still_run()
    {
        var now = DateTime.Now;
        var clock = now;
        await using var rig = new SessionRig(configure: (paths, locations) => new GameSession(paths, locations)
        {
            GivenData = SessionRig.Data,
            OpenRaidCheck = TimeSpan.FromMilliseconds(50),
            Clock = () => clock,
        });
        rig.RaidUpToItsStart(Customs, "bigmap", now.AddMinutes(-12));
        await rig.StartAsync();
        await rig.Until(x => x.Raid.Phase == RaidPhase.InRaid && x.RaidMap is not null, "the raid");

        // Still within Customs' raid length and the margin: it stays.
        clock = now.AddMinutes(SessionRig.CustomsMinutes - 12 + 25);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Equal(RaidPhase.InRaid, rig.Snapshot.Raid.Phase);

        // Past them, with no end line: not in a raid any more, no length, and the change is announced.
        clock = now.AddMinutes(SessionRig.CustomsMinutes - 12 + 31);
        var s = await rig.Until(x => x.Raid.Phase == RaidPhase.Menu && x.LastRaid is not null, "the raid dropped");
        Assert.False(s.LastRaid!.LengthKnown);
        await rig.Until(() => rig.Cues.Any(c => c.Kind == CueKind.RaidOver), "the RAID OVER cue");
        Assert.Null(rig.Cues.Single(c => c.Kind == CueKind.RaidOver).RaidLength);
    }

    [Fact]
    public async Task An_end_the_log_tells_keeps_its_length()
    {
        await using var rig = new SessionRig();
        var started = DateTime.Now.AddMinutes(-12);
        rig.RaidUpToItsStart(Customs, "bigmap", started);
        rig.Log("PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0", started.AddMinutes(9));
        await rig.StartAsync();
        var s = await rig.Until(x => x.LastRaid is not null, "the last raid");
        Assert.True(s.LastRaid!.LengthKnown);
        Assert.Equal("Last raid · Customs · 9 min · PMC", RaidStatus.LastRaid(s.LastRaid));
    }
}
