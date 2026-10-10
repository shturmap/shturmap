#if DEVTOOLS
using Shturmap.Core.Logs;
using Shturmap.Session;
using Shturmap.Session.Dev;

namespace Shturmap.App;

// The developer view's start (docs/DESIGN.md §8, "Developer aids"; developer builds only, never a release):
// "--dev-view" gives the session a fake game folder of its own under %TEMP%, read like "--fake-game", and opens the
// view; "--dev-script <file>" plays the view's steps headless.
public partial class App
{
    /// <summary>The dev view's fake game, when this session was started with "--dev-view".</summary>
    public static FakeGame? DevGame { get; private set; }

    // "--dev-view" becomes "--fake-game <a fresh folder>", so the rest of the start (its own app folder, no study log,
    // nothing sent) is exactly that of a fake game.
    private static string[] DevCommandLine(string[] cli)
    {
        if (!cli.Contains("--dev-view") || cli.Contains("--fake-game"))
            return cli;
        FakeGame.PruneTemporary(TimeSpan.FromDays(1));
        DevGame = FakeGame.CreateTemporary();
        // Logged in, in PvE, as the game is when it reaches the menus.
        DevGame.Mode(GameMode.Pve);
        DevGame.ProfileLoaded();
        return [.. cli, "--fake-game", DevGame.Root];
    }

    // "--uninstall-test keep|delete": help's uninstall at once, before any session starts (so nothing touches the shared
    // download cache), with Velopack's dialogs off. The check of the uninstall on the dev build's own install.
    private bool DevUninstallTest(string[] cli)
    {
        if (Arg(cli, "--uninstall-test") is not { } what)
            return false;
        var deleteData = what == "delete";
        AppLog.Info($"Uninstall test: the data folder {(deleteData ? "goes" : "stays")}");
        EndSession();
        if (!Updater.UninstallerReady || !Updater.StartUninstall(deleteData, silent: true))
            AppLog.Warn("Uninstall test: not an installed build, or no uninstaller");
        Exit();
        return true;
    }

    // After the session has started: the view opens by itself in a dev-view session, and a script plays.
    private async Task StartDevToolsAsync(string[] cli)
    {
        if (_window is null || _session is null)
            return;
        // "--switch-language <culture>": the language switches as a choice in settings does, once the data is on screen
        // and before any snapshot (docs/DESIGN.md §8, "Developer aids"; the layout check, docs/LANGUAGES.md).
        if (Arg(cli, "--switch-language") is { } language)
            await _window.SwitchLanguageWhenLoadedAsync(language);
        _window.DevInstall(_session, DevGame);
        if (DevGame is null)
            return;
        AppLog.Info("Developer view: fake game at " + DevGame.Root);
        _window.DevOpenView();
        if (Arg(cli, "--dev-script") is { } script)
        {
            await _window.DevRunScriptAsync(script);
        }
    }
}
#endif
