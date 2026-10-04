using Shturmap.Core.Raid;

namespace Shturmap.Session.Tests;

// The LOGS light (docs/NEXT.md, review of 2026-10-04, A43): "Logs live" means the game writes now. Reading the last
// session back at a start said it for ten minutes, with the game closed.
public class LogsLightTests
{
    [Fact]
    public async Task The_light_is_live_only_once_the_game_writes_a_line()
    {
        await using var rig = new SessionRig(configure: (paths, locations) => new GameSession(paths, locations)
        {
            GivenData = SessionRig.Data,
            OpenRaidCheck = TimeSpan.FromMilliseconds(100),
        });
        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", DateTime.Now.AddMinutes(-5));
        await rig.StartAsync();

        // The old session is read back: the raid shows, and the light says no more than that the logs are there.
        var read = await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid && s.RaidMap?.NormalizedName == "customs", "the raid read back");
        Assert.Equal("Logs", read.Logs.Text);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.Equal("Logs", rig.Snapshot.Logs.Text);

        // The game writes a line, one that is no event at all: live, without anything else changing.
        rig.Log("Application awaken");
        await rig.Until(s => s.Logs.Text == "Logs live", "the light going live");
        Assert.True(rig.Snapshot.Logs.Ok);
    }
}
