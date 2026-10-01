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
        _session = new GameSession(AppPaths.Default);
        _session.Notice += message => AppLog.Info("Notice: " + message);
        _window = new MainWindow(_session);
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
}
