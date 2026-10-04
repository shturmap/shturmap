using Shturmap.Core.Raid;

namespace Shturmap.Session.Tests;

// "Delete position screenshots" (owner, 2026-10-04: "a mode that auto-deletes screenshots after a couple of seconds
// grace period"): off unless the player ticked it, and then only what arrives from that moment on.
public class DeleteScreenshotsTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("off", false)]
    [InlineData("true", false)]
    [InlineData("on", true)]
    public void Only_the_saved_word_on_turns_it_on(string? setting, bool on) =>
        Assert.Equal(on, GameSession.DeleteScreenshotsOn(setting));

    [Fact]
    public async Task A_position_screenshot_stays_unless_the_setting_is_ticked_and_then_goes_after_its_position_was_read()
    {
        await using var rig = new SessionRig(configure: (paths, locations) => new GameSession(paths, locations)
        {
            GivenData = SessionRig.Data,
            ScreenshotGrace = TimeSpan.FromMilliseconds(50),
        });
        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", DateTime.Now.AddMinutes(-5));
        await rig.StartAsync();
        await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid && s.RaidMap?.NormalizedName == "customs", "the raid on Customs");
        Assert.False(rig.Snapshot.DeleteScreenshots);

        // Not ticked: the position is read and the file stays.
        rig.Screenshot(12, 1, 34);
        await rig.Until(s => s.RaidFix?.Position.X == 12, "the position");
        await Task.Delay(400, TestContext.Current.CancellationToken);
        Assert.Single(rig.Screenshots);

        // Ticked: the next one gives its position and goes. The earlier one stays.
        await rig.Session.SetDeleteScreenshotsAsync(true);
        Assert.True(rig.Snapshot.DeleteScreenshots);
        Assert.Equal("on", rig.Session.GetSetting(GameSession.DeleteScreenshotsSetting));
        rig.Screenshot(56, 1, 78);
        await rig.Until(s => s.RaidFix?.Position.X == 56, "the second position");
        await rig.Until(() => rig.Screenshots.Count == 1, "the second screenshot deleted");
        Assert.Contains("_12.00, ", rig.Screenshots.Single(), StringComparison.Ordinal);

        // Unticked again: nothing more goes.
        await rig.Session.SetDeleteScreenshotsAsync(false);
        rig.Screenshot(90, 1, 12);
        await rig.Until(s => s.RaidFix?.Position.X == 90, "the third position");
        await Task.Delay(400, TestContext.Current.CancellationToken);
        Assert.Equal(2, rig.Screenshots.Count);
    }
}
