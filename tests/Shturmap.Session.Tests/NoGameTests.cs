using Shturmap.Game.Install;

namespace Shturmap.Session.Tests;

// No game on this PC (owner, 2026-10-03: the no-game fallback): a line where the Plan card would be, "Choose game
// folder…", the mode chosen by hand, and a switch over once the game is found.
public class NoGameTests
{
    private static InstallCandidate Game(string root, bool logs = true) =>
        new(InstallKind.Steam, root, logs ? root + @"\Logs" : null, logs ? new DateTime(2026, 10, 3) : null, "test", null);

    private static GameLocations Found(InstallCandidate? install, params InstallCandidate[] others) =>
        new(install, install is null ? others : [install, .. others], @"C:\Users\Player\Documents\Escape from Tarkov\Screenshots",
            @"C:\Users\Player\AppData\Roaming\Battlestate Games\Escape from Tarkov\Settings");

    [Fact]
    public void No_game_found_says_so_and_offers_the_folder_choice_where_it_can_work()
    {
        var line = GameStateLine.For(Found(null), canChoose: true)!;
        Assert.Equal("No game found on this PC", line.Title);
        Assert.Contains("browse the maps", line.Note);
        Assert.True(line.OffersChoice);
        Assert.False(GameStateLine.For(Found(null), canChoose: false)!.OffersChoice);
    }

    [Fact]
    public void A_game_that_hasnt_run_says_where_it_was_found()
    {
        var line = GameStateLine.For(Found(Game(@"D:\EFT", logs: false)), canChoose: true)!;
        Assert.Equal("The game hasn't run on this PC yet", line.Title);
        Assert.Contains(@"D:\EFT", line.Note);
    }

    [Fact]
    public void A_found_game_with_logs_or_one_still_looked_for_has_no_line()
    {
        Assert.Null(GameStateLine.For(Found(Game(@"D:\EFT")), canChoose: true));
        Assert.Null(GameStateLine.For(null, canChoose: true));
    }

    [Fact]
    public void The_snapshot_tells_no_game_from_a_game_not_run_yet()
    {
        Assert.True(new SessionSnapshot { Locations = Found(null) }.NoGameFound);
        Assert.False(new SessionSnapshot { Locations = Found(null) }.GameNotRunYet);
        Assert.True(new SessionSnapshot { Locations = Found(Game(@"D:\EFT", logs: false)) }.GameNotRunYet);
        Assert.False(new SessionSnapshot { Locations = Found(Game(@"D:\EFT")) }.NoGameFound);
        // Still looking: neither, so the mode chooser and the line don't flash at start.
        Assert.False(new SessionSnapshot().NoGameFound);
    }

    [Fact]
    public void A_switch_is_said_once_when_logs_are_followed_where_none_or_others_were()
    {
        var none = Found(null);
        var game = Found(Game(@"D:\EFT"));
        var notRun = Found(Game(@"D:\EFT", logs: false));
        Assert.True(GameSession.SaysFound(none, game));
        Assert.True(GameSession.SaysFound(notRun, game));
        Assert.True(GameSession.SaysFound(Found(Game(@"C:\Steam\EFT")), game));
        Assert.False(GameSession.SaysFound(game, game));
        Assert.False(GameSession.SaysFound(none, notRun));
        Assert.True(GameSession.SameGame(game, Found(Game(@"d:\eft"))));
        Assert.False(GameSession.SameGame(game, notRun));
    }

    [Fact]
    public void The_chosen_folder_is_the_manual_candidate()
    {
        var manual = new InstallCandidate(InstallKind.Manual, @"D:\Downloads", null, null, "chosen", "no EscapeFromTarkov.exe and no log sessions");
        Assert.Same(manual, GameSession.ChosenCandidate(Found(null, Game(@"C:\Steam\EFT") with { Rejected = "x" }, manual)));
        Assert.Null(GameSession.ChosenCandidate(Found(Game(@"C:\Steam\EFT"))));
    }
}
