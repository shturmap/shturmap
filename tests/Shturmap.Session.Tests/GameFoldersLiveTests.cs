using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Win32;
using Shturmap.Core.Raid;
using Shturmap.Game.Install;

namespace Shturmap.Session.Tests;

// The game's folders while Shturmap runs, with two installs on a made-up PC under %TEMP% (the launcher's and Steam's),
// found by the real discovery through a made-up registry (docs/NEXT.md, review of 2026-10-04: A20, another copy of
// the game started later was never followed; A35, choosing a folder read every log session on the window's thread).
public sealed class GameFoldersLiveTests : IAsyncDisposable
{
    private const string Build = "1.1.5.1.47510";

    private readonly string _root = Directory.CreateTempSubdirectory("shturmap-folders-").FullName;
    private readonly string _launcher;
    private readonly string _steam;
    private readonly ConcurrentQueue<string> _notices = new();
    private readonly ConcurrentQueue<SynchronizationContext?> _lookedFrom = new();
    private GameSession? _session;

    public GameFoldersLiveTests()
    {
        _launcher = Path.Combine(_root, "Battlestate Games", "EFT");
        _steam = Path.Combine(_root, "Steam", "steamapps", "common", "Escape from Tarkov", "build");
    }

    public async ValueTask DisposeAsync()
    {
        if (_session is not null)
            await _session.DisposeAsync();
        try
        {
            Directory.Delete(_root, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The database's file can stay open a moment longer; the folder is under %TEMP% either way.
        }
    }

    // The PC as discovery sees it: both uninstall entries, and nothing outside the test's folder.
    private sealed class MadeUpPc(string root, string launcher, string steam) : IGameEnvironment
    {
        public string? ReadRegistryString(RegistryHive hive, RegistryView view, string subKey, string valueName) =>
            hive != RegistryHive.LocalMachine || view != RegistryView.Registry64 || valueName != "InstallLocation" ? null
            : subKey.EndsWith(@"\EscapeFromTarkov", StringComparison.Ordinal) ? launcher
            : subKey.EndsWith(@"\Steam App " + InstallLocator.SteamAppId, StringComparison.Ordinal) ? steam
            : null;

        public string DocumentsFolder => Path.Combine(root, "Documents");

        public string RoamingAppData => Path.Combine(root, "Roaming");

        private bool Mine(string path) => path.StartsWith(root, StringComparison.OrdinalIgnoreCase);

        public bool FileExists(string path) => Mine(path) && File.Exists(path);

        public bool DirectoryExists(string path) => Mine(path) && Directory.Exists(path);

        public IEnumerable<string> EnumerateDirectories(string path) => DirectoryExists(path) ? Directory.EnumerateDirectories(path).ToList() : [];

        public string? ReadAllText(string path) => FileExists(path) ? File.ReadAllText(path) : null;
    }

    // The game ran from this install: a log session that began then, with its first lines. Returns its application log.
    private static string GameRan(string install, DateTime started)
    {
        var stamp = started.ToString("yyyy.MM.dd_HH-mm-ss", CultureInfo.InvariantCulture);
        var folder = Path.Combine(install, "Logs", $"log_{stamp}_{Build}");
        Directory.CreateDirectory(folder);
        var log = Path.Combine(folder, $"{stamp}_{Build} application_000.log");
        Write(log, "Session mode: Pve", started.AddSeconds(10));
        return log;
    }

    // One application-log line and an empty-worded one after it, which makes the first complete at once.
    private static void Write(string log, string message, DateTime? at = null)
    {
        var stamp = (at ?? DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        File.AppendAllText(log, $"{stamp}|{Build}|Info|application|{message}\r\n{stamp}|{Build}|Info|application|.\r\n");
    }

    private async Task<GameSession> StartAsync()
    {
        var pc = new MadeUpPc(_root, _launcher, _steam);
        _session = new GameSession(new AppPaths(Path.Combine(_root, "app")))
        {
            GivenData = SessionRig.Data,
            LookAgain = TimeSpan.FromMilliseconds(50),
            Discovery = chosen =>
            {
                _lookedFrom.Enqueue(SynchronizationContext.Current);
                return new InstallLocator(pc).Locate(chosen);
            },
        };
        _session.Notice += notice => _notices.Enqueue(notice.Text);
        await _session.StartAsync();
        return _session;
    }

    private async Task Until(Func<bool> expected, string what)
    {
        var until = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < until)
        {
            if (expected())
                return;
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
        Assert.Fail($"Never saw: {what}. Game folder: {_session?.Snapshot.Locations?.Install?.Root ?? "none"}; notices: {string.Join(" | ", _notices)}");
    }

    [Fact]
    public async Task Another_copy_of_the_game_started_later_is_followed_without_a_restart()
    {
        GameRan(_launcher, DateTime.Now.AddDays(-1));
        GameRan(_steam, DateTime.Now.AddDays(-3));
        var session = await StartAsync();
        Assert.Equal(_launcher, session.Snapshot.Locations?.Install?.Root);
        // Looking again changes nothing while the followed install has the newest session.
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Equal(_launcher, session.Snapshot.Locations?.Install?.Root);
        Assert.Empty(_notices);

        // The player starts the Steam copy.
        var log = GameRan(_steam, DateTime.Now);
        await Until(() => string.Equals(session.Snapshot.Locations?.Install?.Root, _steam, StringComparison.OrdinalIgnoreCase), "the Steam copy followed");
        Assert.Equal($"Newer game logs in {_steam}: quests and raids follow that game now.", Assert.Single(_notices));

        // Its log is followed live from now on.
        await Until(() => session.Snapshot.Data is not null, "the data");
        Write(log, "scene preset path:maps/customs_preset.bundle rcid:x.scenespreset.asset");
        await Until(() => session.Snapshot.Raid.Phase == RaidPhase.Loading && session.Snapshot.RaidMap?.NormalizedName == "customs", "the raid loading on Customs");
    }

    [Fact]
    public async Task A_folder_the_player_chose_is_kept_whatever_else_runs()
    {
        GameRan(_launcher, DateTime.Now.AddDays(-1));
        var session = await StartAsync();
        Assert.True(await session.ChooseGameFolderAsync(_launcher));
        await Until(() => session.Snapshot.Locations?.Install?.Kind == InstallKind.Manual, "the chosen folder");
        _notices.Clear();

        GameRan(_steam, DateTime.Now);
        // The hint says it; nothing switches by itself.
        await Until(() => GameFolder.NewerElsewhere(session.Snapshot.Locations) is not null, "the hint at newer logs");
        Assert.Equal(_launcher, session.Snapshot.Locations?.Install?.Root);
        Assert.Empty(_notices);
    }

    private sealed class WindowThread : SynchronizationContext
    {
        public int Posts;

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref Posts);
            base.Post(d, state);
        }
    }

    // Calls from a thread that has a context of its own, as the window's thread has.
    private static TTask FromTheWindow<TTask>(WindowThread window, Func<TTask> call)
        where TTask : Task
    {
        TTask? task = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(window);
            task = call();
        });
        thread.Start();
        thread.Join();
        return task!;
    }

    [Fact]
    public async Task Choosing_and_finding_the_game_run_off_the_callers_thread()
    {
        GameRan(_launcher, DateTime.Now.AddDays(-1));
        GameRan(_steam, DateTime.Now.AddDays(-3));
        var session = await StartAsync();
        var window = new WindowThread();

        _lookedFrom.Clear();
        Assert.True(await FromTheWindow(window, () => session.ChooseGameFolderAsync(_steam)));
        Assert.Equal(_steam, session.Snapshot.Locations?.Install?.Root);
        await FromTheWindow(window, session.FindGameAutomaticallyAsync);
        Assert.Equal(_launcher, session.Snapshot.Locations?.Install?.Root);

        // Neither the look at the folders nor anything after it (the quest history of every log session) ran on the
        // caller's thread or came back to it.
        Assert.NotEmpty(_lookedFrom);
        Assert.DoesNotContain(window, _lookedFrom);
        Assert.Equal(0, window.Posts);
    }
}
