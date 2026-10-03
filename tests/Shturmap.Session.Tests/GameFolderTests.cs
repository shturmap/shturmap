using Shturmap.Data.Progress;
using Shturmap.Game.Install;

namespace Shturmap.Session.Tests;

// The way back from a chosen game folder (owner, 2026-10-04): settings say where the folder comes from, FIND
// AUTOMATICALLY forgets the choice and finds the game again, and a quiet hint when another install has newer logs.
public class GameFolderTests
{
    private static InstallCandidate Install(InstallKind kind, string root, DateTime? newest) =>
        new(kind, root, newest is null ? null : root + @"\Logs", newest, "test", null);

    private static GameLocations Found(InstallCandidate? install, params InstallCandidate[] others) =>
        new(install, install is null ? others : [install, .. others], @"C:\Users\Player\Documents\Escape from Tarkov\Screenshots",
            @"C:\Users\Player\AppData\Roaming\Battlestate Games\Escape from Tarkov\Settings");

    private static readonly DateTime Older = new(2026, 9, 1);
    private static readonly DateTime Newer = new(2026, 10, 3);

    [Fact]
    public void The_game_folder_says_where_it_comes_from()
    {
        Assert.Equal(@"GAME FOLDER: D:\EFT (chosen by you)", GameFolder.Label(Found(Install(InstallKind.Manual, @"D:\EFT", Older))));
        Assert.Equal(@"GAME FOLDER: C:\Steam\EFT (found automatically)", GameFolder.Label(Found(Install(InstallKind.Steam, @"C:\Steam\EFT", Older))));
        Assert.Equal("GAME FOLDER: NOT FOUND", GameFolder.Label(Found(null)));
    }

    [Fact]
    public void Finding_automatically_forgets_the_choice_and_finds_the_game_again()
    {
        var folder = Path.Combine(Path.GetTempPath(), "shturmap-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var store = new ProgressStore(Path.Combine(folder, "progress.db"));
            store.SetSetting(GameSession.InstallFolderSetting, @"D:\Old EFT");
            var steam = Install(InstallKind.Steam, @"C:\Steam\EFT", Newer);
            var asked = new List<string?>();
            var found = GameSession.FindAutomatically(store, chosen =>
            {
                asked.Add(chosen);
                return chosen is null ? Found(steam) : Found(Install(InstallKind.Manual, chosen, Older), steam);
            });

            Assert.Null(store.GetSetting(GameSession.InstallFolderSetting));
            Assert.Equal([null], asked);
            Assert.Same(steam, found.Install);
            Assert.Equal(@"GAME FOLDER: C:\Steam\EFT (found automatically)", GameFolder.Label(found));
            Assert.Equal(@"Found Escape from Tarkov in C:\Steam\EFT: quests and raids follow the game now.", GameSession.FoundText(found));
            // The other logs are followed from now on.
            Assert.True(GameSession.SaysFound(Found(Install(InstallKind.Manual, @"D:\Old EFT", Older), steam), found));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Finding_automatically_says_what_it_found()
    {
        Assert.StartsWith("No game found on this PC", GameSession.FoundText(Found(null)));
        Assert.Equal(@"Found Escape from Tarkov in D:\EFT; it hasn't run on this PC yet.",
            GameSession.FoundText(Found(Install(InstallKind.Steam, @"D:\EFT", null))));
    }

    [Fact]
    public void Newer_logs_elsewhere_are_hinted_only_when_another_install_is_newer_than_the_chosen_one()
    {
        var chosenOld = Install(InstallKind.Manual, @"D:\EFT", Older);
        var steamNew = Install(InstallKind.Steam, @"C:\Steam\EFT", Newer);
        Assert.Same(steamNew, GameFolder.NewerElsewhere(Found(chosenOld, steamNew)));
        Assert.Contains(@"Newer game logs in C:\Steam\EFT", GameFolder.LogsTip(Found(chosenOld, steamNew)));

        // The chosen folder has the newest logs: no hint.
        var chosenNew = Install(InstallKind.Manual, @"D:\EFT", Newer);
        Assert.Null(GameFolder.NewerElsewhere(Found(chosenNew, Install(InstallKind.Steam, @"C:\Steam\EFT", Older))));
        // The same install, found by discovery too: no hint.
        Assert.Null(GameFolder.NewerElsewhere(Found(chosenOld, Install(InstallKind.Steam, @"d:\eft", Newer))));
        // Found automatically: discovery took the newest already.
        Assert.Null(GameFolder.NewerElsewhere(Found(steamNew, Install(InstallKind.BsgLauncher, @"D:\EFT", Older))));
        // A candidate discovery rejected doesn't count.
        Assert.Null(GameFolder.NewerElsewhere(Found(chosenOld, steamNew with { Rejected = "no EscapeFromTarkov.exe" })));
        Assert.Equal(@"Game logs: D:\EFT\Logs", GameFolder.LogsTip(Found(chosenNew)));
    }
}
