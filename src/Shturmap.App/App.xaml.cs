using System.Globalization;
using Microsoft.UI.Xaml;
using Shturmap.Session;
using Shturmap.Session.Reporting;
using Windows.Graphics;

namespace Shturmap.App;

public partial class App : Application
{
#if DEVTOOLS
    /// <summary>A developer build (Debug, or eng\dev.ps1): "Shturmap DEV", its own icon, data folder and update feed,
    /// never GitHub's releases (docs/DESIGN.md §8, "Developer aids").</summary>
    public const bool IsDevBuild = true;
#else
    public const bool IsDevBuild = false;
#endif

    /// <summary>The app icon for title bars, the taskbar and Alt+Tab (the exe carries the same icon); the dev build's
    /// has a cyan plate, so it is never taken for the release.</summary>
    public static string IconPath { get; } = Path.Combine(AppContext.BaseDirectory, "Assets", IsDevBuild ? "Shturmap-dev.ico" : "Shturmap.ico");

    /// <summary>The windows' title.</summary>
    public const string Title = IsDevBuild ? "Shturmap DEV" : "Shturmap";

    private MainWindow? _window;
    private GameSession? _session;
    private readonly RunningMarker? _marker;

    /// <summary>Reports and crash reports: the one way anything about the player leaves the PC (docs/DESIGN.md §8,
    /// "Reports").</summary>
    public static Reporter Reporter { get; private set; } = null!;

    // The command line, read once. A developer build turns "--dev-view" into a fake game of its own (Dev/App.Dev.cs).
    private static string[]? _cli;

    private static string[] CommandLine()
    {
        if (_cli is null)
        {
            _cli = Environment.GetCommandLineArgs();
#if DEVTOOLS
            _cli = DevCommandLine(_cli);
#endif
        }
        return _cli;
    }

    public App()
    {
        InitializeComponent();
        // The developer view's arguments come in here (DEVTOOLS); the app log starts below, in the chosen data folder.
        var cli = CommandLine();
        var started = DateTime.Now;
        // Developer runs (snapshots, fake games, the website demo) send nothing; a build without a DSN can't. The one
        // exception is the release's delivery check, "--send-report", which may run in a fake game so the player's
        // own folder and study log stay untouched.
        var developerRun = (cli.Contains("--snapshot") || cli.Contains("--fake-game") || cli.Contains("--demo")) && !cli.Contains("--send-report");
        // New versions: only an installed app asks, and no developer run; "--update-feed <folder>" tests the whole
        // path against a local feed. The dev build asks only its own local feed (docs/DESIGN.md §8, "Distribution").
        Updater = new Updater(Arg(cli, "--update-feed"), developerRun || cli.Contains("--send-report"), IsDevBuild, IsDevBuild ? DeveloperFeed() : null);
        // Only the installed release keeps the player's data folder; every other build its own, "--data" any
        // (docs/DESIGN.md §8, "Data folders"). Chosen before anything writes there, the app log first.
        var paths = AppPaths.Use(Distribution.DataFolderFor(Updater.AppId, Arg(cli, "--data")), Arg(cli, "--data"));
        AppLog.Initialize(paths.Logs);
        if (Updater.StartProblem is { } problem)
            AppLog.Warn("Updates: Velopack couldn't start", problem);
        BuildKind = IsDevBuild ? "dev build" : Updater.Installed ? "installed" : "folder build";
        Reporter = new Reporter(AppRoot(cli), ReportEndpoint.Parse(BuiltDsn()), developerRun,
            new ReportInfo(GameSession.Version, BuildKind, Diagnostics.WindowsVersion()));
        // A session that ended without closing left its marker behind: note it before marking this one. One from
        // before Windows last started was a shutdown, not a crash.
        var boot = started - TimeSpan.FromMilliseconds(Environment.TickCount64);
        foreach (var exit in Reporter.Crashes.CollectUnexpectedExits(started, boot, (from, to) => AppLog.TailBetween(from, to, CrashRecords.LogLines)))
            AppLog.Warn($"The last session ({exit.Version}) ended without closing; noted as crash record {exit.Id}");
        _marker = Reporter.Crashes.MarkRunning(started, Reporter.Info);
        // A clean end lets go of the marker, however it comes; a crash leaves it for the next start.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _marker?.Dispose();
        UnhandledException += (_, e) => Crashed(e.Exception, "ui", fatal: true);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Crashed(ex, "background", e.IsTerminating);
        };
        TaskScheduler.UnobservedTaskException += (_, e) => Crashed(e.Exception, "task", fatal: false);
    }

    // Noted on this PC at once; sent only as the player's "Crash reports" setting allows, at the next start.
    private static void Crashed(Exception e, string source, bool fatal)
    {
        AppLog.Error(fatal ? $"Unhandled exception ({source})" : "Unobserved task exception", e);
        if (Reporter.Crashes.Record(e, source, fatal, Reporter.Info, AppLog.Tail(CrashRecords.LogLines), DateTime.Now) is { } record)
            AppLog.Info($"Crash record {record.Id} written ({record.Summary})");
    }

    // The DSN the release was built with (eng\release.ps1); developer builds and forks have none.
    private static string? BuiltDsn() => BuiltMetadata(Reporter.DsnMetadata);

    /// <summary>
    /// The dev build's update feed: the local folder eng\dev.ps1 last packed into, which it writes to dev-feed.txt in
    /// the dev install's folder (%LOCALAPPDATA%\ShturmapDev, beside current\) on every run. So the installed dev app
    /// follows whichever checkout built last, and no folder of the developer's PC is compiled into the build.
    /// </summary>
    private static string? DeveloperFeed()
    {
        try
        {
            var install = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
            var file = install is null ? null : Path.Combine(install, "dev-feed.txt");
            return file is not null && File.Exists(file) ? File.ReadAllText(file).Trim() : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? BuiltMetadata(string key) =>
        typeof(App).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .OfType<System.Reflection.AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == key)?.Value;

    // Shturmap's folder, or a fake game's own (see CreateSession).
    private static string AppRoot(string[] cli) =>
        Arg(cli, "--fake-game") is { } root ? Path.Combine(root, "app") : AppPaths.Default.Root;

    /// <summary>"installed" for the release installed by its Setup (Velopack), "dev build" for a developer build
    /// (eng\dev.ps1, Debug), else "folder build".</summary>
    public static string BuildKind { get; private set; } = "folder build";

    /// <summary>New versions, from GitHub Releases.</summary>
    public static Updater Updater { get; private set; } = null!;

    /// <summary>Lets go of this session's running marker, so an exit that isn't a close (RESTART NOW for an update)
    /// isn't taken for a crash at the next start.</summary>
    public void EndSession() => _marker?.Dispose();

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var cli = CommandLine();
        // The website demo times its clip by its DEBUG lines (tools\fake-raid.ps1 -Demo).
        AppLog.Verbose = cli.Contains("--verbose") || cli.Contains("--demo");
        AppLog.Info($"Starting Shturmap {GameSession.Version} ({BuildKind}, {AppPaths.Default.KindText} data folder) on {Diagnostics.WindowsVersion()}");
#if DEVTOOLS
        if (DevUninstallTest(cli))
            return;
#endif
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
#if DEVTOOLS
        // The study log (developer builds only, owner 2026-10-03) follows the switch in settings; "--study" keeps it for
        // one session; snapshot and fake-game runs stay out of it.
        _session.StudyOverride = GameSession.StudyOverrideFor(cli);
#endif
        _window = new MainWindow(_session, windowSize)
        {
            SnapshotMode = cli.Contains("--snapshot"),
            SnapshotScale = int.TryParse(Arg(cli, "--snapshot-scale"), out var snapshotScale) ? Math.Clamp(snapshotScale, 1, 4) : 1,
            ShowQuest = Arg(cli, "--show-quest"),
            // Developer aid for the website's hero clip: plays a scripted interaction (Demo.cs); fake games only.
            DemoQuest = cli.Contains("--fake-game") ? Arg(cli, "--demo") : null,
        };
        _window.Closed += async (_, _) =>
        {
            _marker?.Dispose();
            await _session.DisposeAsync();
        };
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
        _window.ReportsReady();
        await HandleReportsAsync();
        _window.StartUpdates(Updater);
        if (Updater.Installed && !cli.Contains("--fake-game"))
            await RemoveSingleExeLeftoversAsync();
        // Developer aids for snapshots of the report dialog and the question after a crash.
        if (cli.Contains("--show-report"))
            _window.OpenReport(Shturmap.Session.Reporting.ReportKind.Problem, "Example: the map stayed on Woods after I loaded into Customs.", "snapshot", showSent: true);
        if (cli.Contains("--show-crash"))
            _window.AskAboutCrashes(Reporter.Crashes.Waiting() is { Count: > 0 } waiting ? waiting : [ExampleCrash()]);
#if DEVTOOLS
        await StartDevToolsAsync(cli);
#endif

        // Developer aid: "--send-report <text> <folder>" sends one real report through the dialog's own Send (a
        // release's delivery check), saves the window to the folder and exits.
        var send = Array.IndexOf(cli, "--send-report");
        if (send >= 0 && send + 2 < cli.Length)
        {
            await Task.Delay(TimeSpan.FromSeconds(4));
            AppLog.Info("Test report: " + await _window.SendReportForTestAsync(cli[send + 1]));
            await _window.SaveSnapshotAsync(cli[send + 2]);
            _marker?.Dispose();
            Exit();
        }

        // Developer aid: "--snapshot <folder> [seconds]" renders the window and the map to PNGs, then exits.
        var at = Array.IndexOf(cli, "--snapshot");
        if (at >= 0 && at + 1 < cli.Length)
        {
            var delay = at + 2 < cli.Length && int.TryParse(cli[at + 2], out var s) ? s : 8;
            await Task.Delay(TimeSpan.FromSeconds(delay));
            await _window.SaveSnapshotAsync(cli[at + 1]);
            _marker?.Dispose();
            Exit();
        }
    }

    /// <summary>
    /// At start: crash records the player approved go out; the ones not answered yet are asked about, sent or kept as
    /// the "Crash reports" setting says; reports an earlier session couldn't send are sent (docs/DESIGN.md §8,
    /// "Reports").
    /// </summary>
    private async Task HandleReportsAsync()
    {
        try
        {
            Reporter.Crashes.Prune(DateTime.Now);
            var waiting = Reporter.Crashes.Waiting();
            var approved = waiting.Where(r => r.State == CrashState.Approved).ToList();
            var pending = waiting.Where(r => r.State == CrashState.Pending).ToList();
            if (approved.Count > 0 && Reporter.Configured && !Reporter.Muted)
                await Reporter.SendCrashesAsync(approved);
            var mode = CrashModes.Parse(_session?.GetSetting(CrashModes.Setting));
            var action = CrashPolicy.Decide(mode, pending.Count, Reporter.Configured, Reporter.Muted);
            if (pending.Count > 0)
                AppLog.Info($"{pending.Count} crash record(s) not answered; crash reports {CrashModes.Format(mode)}: {action.ToString().ToLowerInvariant()}");
            switch (action)
            {
                case CrashAction.Ask:
                    _window?.AskAboutCrashes(pending);
                    break;
                case CrashAction.Send:
                    await Reporter.SendCrashesAsync(pending);
                    break;
                case CrashAction.Keep:
                    Reporter.Keep(pending);
                    break;
            }
            await Reporter.SendOutboxAsync();
        }
        catch (Exception e)
        {
            AppLog.Warn("Handling reports at start failed", e);
        }
    }

    // The 0.1.0 single exe left unpacked copies in %TEMP%\.net (about 200 MB each): the installed app removes them
    // once, off the start; again at a later start only if one was in use.
    private const string LeftoversSetting = "singleExeLeftovers";

    private async Task RemoveSingleExeLeftoversAsync()
    {
        if (_session is null || _session.GetSetting(LeftoversSetting) == "removed")
            return;
        try
        {
            var (done, complete) = await Task.Run(Shturmap.Core.UnpackedCopies.RemoveLeftovers);
            foreach (var line in done)
                AppLog.Info("Old unpacked copies: " + line);
            if (complete)
                _session.SetSetting(LeftoversSetting, "removed");
        }
        catch (Exception e)
        {
            AppLog.Warn("Removing old unpacked copies failed", e);
        }
    }

    // A record to show the question after a crash in a snapshot, with a real stack.
    private static CrashRecord ExampleCrash()
    {
        try
        {
            throw new InvalidOperationException("Example crash for a snapshot");
        }
        catch (InvalidOperationException e)
        {
            return CrashRecords.FromException(e, "example", "ui", true, Reporter.Info, AppLog.Tail(5), DateTime.Now, null);
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
#if DEVTOOLS
        // Developer switch "--no-game": as if Escape from Tarkov weren't on this PC, so the no-game fallback can be seen
        // (docs/DESIGN.md, "No game on this PC"). Discovery then looks only at a folder chosen in the session. It gets
        // an app folder of its own (a fake game's, or a temporary one), so a folder chosen to try it never sticks in
        // the real settings. With "--fake-game" the fake game isn't found either, until it is chosen; in the
        // developer view the "No game" trigger does the same while keeping the fake game's screenshots.
        if (cli.Contains("--no-game"))
        {
            var app = Arg(cli, "--fake-game") is { } fake ? Path.Combine(fake, "app")
                : Path.Combine(Path.GetTempPath(), "shturmap-nogame-" + Guid.NewGuid().ToString("N")[..8], "app");
            AppLog.Debug("No game: only a folder chosen in this session counts");
            return new GameSession(new AppPaths(app, AppPaths.Default.CacheRoot)) { NoGame = true };
        }
#endif
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
