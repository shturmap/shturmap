using Shturmap.Core.Raid;

namespace Shturmap.Session.Tests;

// The raid replay (owner, 2026-10-07): the session keeps a raid's positions with their minutes, and hands them to the
// RAID OVER cue and to Plan's REPLAY until the next raid loads. In memory only.
public class ReplaySessionTests
{
    [Fact]
    public async Task A_raids_positions_with_their_minutes_come_with_its_end()
    {
        await using var rig = new SessionRig();
        var started = DateTime.Now.AddMinutes(-33);
        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", started);
        await rig.StartAsync();
        await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid && s.RaidMap?.NormalizedName == "customs", "the raid on Customs");

        rig.Screenshot(12, 1, 34, started.AddMinutes(1));
        rig.Screenshot(40, 1, 34, started.AddMinutes(8));
        rig.Screenshot(80, 1, 10, started.AddMinutes(23));
        await rig.Until(s => s.Trail.Count == 2, "three positions");
        Assert.Null(rig.Snapshot.Replay); // nothing to replay while the raid runs

        rig.Log("PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0");
        await rig.Until(() => rig.Cues.Any(c => c.Kind == CueKind.RaidOver), "the RAID OVER cue");
        var replay = rig.Cues.Last(c => c.Kind == CueKind.RaidOver).Replay;
        Assert.NotNull(replay);
        Assert.Equal("customs", replay.MapNormalizedName);
        Assert.Equal([1, 8, 23], replay.Fixes.Select(f => Math.Round(f.Minute)));
        Assert.Equal(80, replay.Fixes[^1].Position.X);
        Assert.InRange(replay.Minutes, 32.5, 34);
        Assert.True(replay.Plays);
        Assert.Same(replay, rig.Snapshot.Replay);

        // Until the next raid loads.
        rig.Log("scene preset path:maps/woods_preset.bundle rcid:x.scenespreset.asset");
        await rig.Until(s => s.Raid.Phase == RaidPhase.Loading, "the next raid loading");
        Assert.Null(rig.Snapshot.Replay);
    }

    [Fact]
    public async Task A_raid_with_one_position_brings_a_replay_that_does_not_play()
    {
        await using var rig = new SessionRig();
        var started = DateTime.Now.AddMinutes(-20);
        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", started);
        await rig.StartAsync();
        await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid && s.RaidMap?.NormalizedName == "customs", "the raid on Customs");
        rig.Screenshot(12, 1, 34, started.AddMinutes(5));
        await rig.Until(s => s.RaidFix is not null, "the position");

        rig.Log("PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0");
        await rig.Until(() => rig.Cues.Any(c => c.Kind == CueKind.RaidOver), "the RAID OVER cue");
        // Today's cue then: one dot is not a raid.
        Assert.False(rig.Cues.Last(c => c.Kind == CueKind.RaidOver).Replay?.Plays ?? false);
    }
}
