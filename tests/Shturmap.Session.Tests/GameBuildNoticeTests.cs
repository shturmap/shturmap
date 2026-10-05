#if DEVTOOLS
namespace Shturmap.Session.Tests;

// Developer builds say once when the game's logs come from a newer build than any seen: the game was patched, and the
// checks after a patch are due (docs/UPDATES.md; owner, 2026-10-05). The first build ever seen is only kept.
public class GameBuildNoticeTests
{
    [Fact]
    public async Task A_newer_game_build_says_once_that_the_checks_after_a_patch_are_due()
    {
        await using var rig = new SessionRig();
        rig.Session.NoticeGameBuilds = true;
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(() => rig.Session.GetSetting(GameSession.GameBuildSetting) == "1.1.5.1.47510", "the first build kept");
        Assert.DoesNotContain(rig.Notices, n => n.StartsWith("Game build", StringComparison.Ordinal));

        rig.NewLogSession(DateTime.Now.AddMinutes(-1), "1.1.6.0.48001");
        rig.Log("Session mode: Pve");
        rig.Log("Session mode: Pve");
        await rig.Until(() => rig.Notices.Any(n => n.StartsWith("Game build 1.1.6.0.48001 is new", StringComparison.Ordinal)), "the notice");
        Assert.Equal("1.1.6.0.48001", rig.Session.GetSetting(GameSession.GameBuildSetting));
        Assert.Single(rig.Notices, n => n.StartsWith("Game build", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Snapshot_and_fake_game_runs_leave_the_build_alone()
    {
        await using var rig = new SessionRig();
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.ModeReading.LoggedAt is not null, "the log's mode line read");
        Assert.Null(rig.Session.GetSetting(GameSession.GameBuildSetting));
    }
}
#endif
