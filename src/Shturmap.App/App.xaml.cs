using System.Globalization;
using Microsoft.UI.Xaml;
using Shturmap.Session;
using Windows.Graphics;

namespace Shturmap.App;

public partial class App : Application
{
    /// <summary>The app icon for title bars, the taskbar and Alt+Tab (the exe carries the same icon).</summary>
    public static string IconPath { get; } = Path.Combine(AppContext.BaseDirectory, "Assets", "Shturmap.ico");

    private MainWindow? _window;
    private GameSession? _session;

    public App()
    {
        InitializeComponent();
        AppLog.Initialize(AppPaths.Default.Logs);
        UnhandledException += (_, e) => AppLog.Error("Unhandled exception", e.Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => AppLog.Error("Unobserved task exception", e.Exception);
    }

    /// <summary>"single exe" when run from the release's unpacked copy, else "folder build".</summary>
    public static string BuildKind { get; } =
        Shturmap.Core.UnpackedCopies.RunsFromCopy(AppContext.BaseDirectory, Path.GetTempPath()) ? "single exe" : "folder build";

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var cli = Environment.GetCommandLineArgs();
        // The website demo times its clip by its DEBUG lines (tools\fake-raid.ps1 -Demo).
        AppLog.Verbose = cli.Contains("--verbose") || cli.Contains("--demo");
        AppLog.Info($"Starting Shturmap {GameSession.Version} ({BuildKind}) on {Diagnostics.WindowsVersion()}");
        // Run as the single exe, earlier versions left their unpacked copies in %TEMP%: remove them, off the start.
        _ = Task.Run(() =>
        {
            try
            {
                foreach (var line in Shturmap.Core.UnpackedCopies.RemoveOthers())
                    AppLog.Info("Unpacked copies: " + line);
            }
            catch (Exception e)
            {
                AppLog.Warn("Removing old unpacked copies failed", e);
            }
        });
        // Developer aids for website media: "--culture en-US" formats dates and numbers in that culture, and
        // "--window 1600x900" renders at that size instead of maximised, so the UI reads larger in a screenshot.
        if (Arg(cli, "--culture") is { } culture)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        }
        SizeInt32? windowSize = Arg(cli, "--window")?.Split('x') is [var w, var h] && int.TryParse(w, out var width)
            && int.TryParse(h, out var height) ? new SizeInt32(width, height) : null;
        _session = CreateSession(cli);
        _session.Notice += notice => AppLog.Debug("Notice: " + notice.Text);
        // The study log follows the player's switch in help; "--study" keeps it for one session; developer runs stay
        // out of it.
        _session.StudyOverride = GameSession.StudyOverrideFor(cli);
        _window = new MainWindow(_session, windowSize)
        {
            SnapshotMode = cli.Contains("--snapshot"),
            SnapshotScale = int.TryParse(Arg(cli, "--snapshot-scale"), out var snapshotScale) ? Math.Clamp(snapshotScale, 1, 4) : 1,
            ShowQuest = Arg(cli, "--show-quest"),
            // Developer aid for the website's hero clip: plays a scripted interaction (Demo.cs); fake games only.
            DemoQuest = cli.Contains("--fake-game") ? Arg(cli, "--demo") : null,
        };
        _window.Closed += async (_, _) => await _session.DisposeAsync();
        _window.Activate();
        try
        {
            await _session.StartAsync();
            AppLog.Info("Session started");
        }
        catch (Exception e)
        {
            AppLog.Error("Session failed to start", e);
        }

        // Developer aid: "--snapshot <folder> [seconds]" renders the window and the map to PNGs, then exits.
        var at = Array.IndexOf(cli, "--snapshot");
        if (at >= 0 && at + 1 < cli.Length)
        {
            var delay = at + 2 < cli.Length && int.TryParse(cli[at + 2], out var s) ? s : 8;
            await Task.Delay(TimeSpan.FromSeconds(delay));
            await _window.SaveSnapshotAsync(cli[at + 1]);
            Exit();
        }
    }

    // The value after a command-line flag, or null.
    private static string? Arg(string[] cli, string flag)
    {
        var at = Array.IndexOf(cli, flag);
        return at >= 0 && at + 1 < cli.Length ? cli[at + 1] : null;
    }

    // Developer aid: "--fake-game <folder>" reads <folder>\Logs and <folder>\Screenshots instead of the real game,
    // with its own database in <folder>\app (downloads are shared with the real cache).
    private static GameSession CreateSession(string[] cli)
    {
        var at = Array.IndexOf(cli, "--fake-game");
        if (at < 0 || at + 1 >= cli.Length)
            return new GameSession(AppPaths.Default);
        var root = cli[at + 1];
        var install = new Shturmap.Game.Install.InstallCandidate(Shturmap.Game.Install.InstallKind.Manual, root, Path.Combine(root, "Logs"),
            DateTime.Now, "fake game", null);
        var settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Battlestate Games", "Escape from Tarkov", "Settings");
        AppLog.Debug("Using a fake game folder: " + root);
        return new GameSession(new AppPaths(Path.Combine(root, "app"), AppPaths.Default.CacheRoot),
            new Shturmap.Game.Install.GameLocations(install, [install], Path.Combine(root, "Screenshots"), settings));
    }
}
