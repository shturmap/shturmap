using Microsoft.UI.Xaml;
using Spotter.Session;

namespace Spotter.App;

public partial class App : Application
{
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
        AppLog.Info("Starting Spotter " + typeof(App).Assembly.GetName().Version);
        _session = CreateSession(Environment.GetCommandLineArgs());
        _session.Notice += message => AppLog.Info("Notice: " + message);
        _window = new MainWindow(_session) { SnapshotMode = Environment.GetCommandLineArgs().Contains("--snapshot") };
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
        var cli = Environment.GetCommandLineArgs();
        var at = Array.IndexOf(cli, "--snapshot");
        if (at >= 0 && at + 1 < cli.Length)
        {
            var delay = at + 2 < cli.Length && int.TryParse(cli[at + 2], out var s) ? s : 8;
            await Task.Delay(TimeSpan.FromSeconds(delay));
            await _window.SaveSnapshotAsync(cli[at + 1]);
            Exit();
        }
    }

    // Developer aid: "--fake-game <folder>" reads <folder>\Logs and <folder>\Screenshots instead of the real game,
    // with its own database in <folder>\app (downloads are shared with the real cache).
    private static GameSession CreateSession(string[] cli)
    {
        var at = Array.IndexOf(cli, "--fake-game");
        if (at < 0 || at + 1 >= cli.Length)
            return new GameSession(AppPaths.Default);
        var root = cli[at + 1];
        var install = new Spotter.Game.Install.InstallCandidate(Spotter.Game.Install.InstallKind.Manual, root, Path.Combine(root, "Logs"),
            DateTime.Now, "fake game", null);
        var settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Battlestate Games", "Escape from Tarkov", "Settings");
        AppLog.Info("Using a fake game folder: " + root);
        return new GameSession(new AppPaths(Path.Combine(root, "app"), AppPaths.Default.CacheRoot),
            new Spotter.Game.Install.GameLocations(install, [install], Path.Combine(root, "Screenshots"), settings));
    }
}
