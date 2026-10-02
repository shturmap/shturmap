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

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppLog.Info("Starting Shturmap " + typeof(App).Assembly.GetName().Version);
        var cli = Environment.GetCommandLineArgs();
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
        _session.Notice += notice => AppLog.Info("Notice: " + notice.Text);
        // Developer runs stay out of the player's study log.
        _session.Study.Enabled = !cli.Contains("--snapshot") && !cli.Contains("--fake-game");
        _window = new MainWindow(_session, windowSize)
        {
            SnapshotMode = cli.Contains("--snapshot"),
            SnapshotScale = int.TryParse(Arg(cli, "--snapshot-scale"), out var snapshotScale) ? Math.Clamp(snapshotScale, 1, 4) : 1,
            ShowQuest = Arg(cli, "--show-quest"),
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
        AppLog.Info("Using a fake game folder: " + root);
        return new GameSession(new AppPaths(Path.Combine(root, "app"), AppPaths.Default.CacheRoot),
            new Shturmap.Game.Install.GameLocations(install, [install], Path.Combine(root, "Screenshots"), settings));
    }
}
