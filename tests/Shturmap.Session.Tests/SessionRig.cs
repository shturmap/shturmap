using System.Globalization;
using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Install;

namespace Shturmap.Session.Tests;

/// <summary>
/// A whole <see cref="GameSession"/> against a game folder made under %TEMP%: what the test writes into its log and
/// its Screenshots folder goes through the real tailer, tracker and watcher, as the game's files would. The game data
/// is given (<see cref="GameSession.GivenData"/>), so nothing asks the network, and the app's own files are in the
/// same temporary folder. Two hand-made maps, Customs and Woods: no tarkov.dev payload is stored in the repository.
/// </summary>
internal sealed class SessionRig : IAsyncDisposable
{
    private const string Build = "1.1.5.1.47510";

    private readonly string _root = Directory.CreateTempSubdirectory("shturmap-session-").FullName;
    private readonly List<string> _notices = [];
    private readonly List<ViewCue> _cues = [];
    private string _log = "";

    /// <param name="logSessionStarted">When the game "started": the log session's folder is named by it.</param>
    /// <param name="configure">Makes the session, for a test that sets more than the data (a clock, a shorter check).</param>
    public SessionRig(DateTime? logSessionStarted = null, Func<AppPaths, GameLocations, GameSession>? configure = null)
    {
        Directory.CreateDirectory(Path.Combine(_root, "Screenshots"));
        NewLogSession(logSessionStarted ?? DateTime.Now.AddMinutes(-30));
        var install = new InstallCandidate(InstallKind.Manual, _root, Path.Combine(_root, "Logs"), DateTime.Now, "test", null);
        var paths = new AppPaths(Path.Combine(_root, "app"));
        var locations = new GameLocations(install, [install], Path.Combine(_root, "Screenshots"), Path.Combine(_root, "Settings"));
        Session = configure?.Invoke(paths, locations) ?? new GameSession(paths, locations) { GivenData = Data };
        Session.Notice += notice =>
        {
            lock (_notices)
                _notices.Add(notice.Text);
        };
        Session.Cue += cue =>
        {
            lock (_cues)
                _cues.Add(cue);
        };
    }

    public GameSession Session { get; }

    public SessionSnapshot Snapshot => Session.Snapshot;

    public IReadOnlyList<string> Notices
    {
        get
        {
            lock (_notices)
                return _notices.ToList();
        }
    }

    public IReadOnlyList<ViewCue> Cues
    {
        get
        {
            lock (_cues)
                return _cues.ToList();
        }
    }

    /// <summary>The raid length the data gives Customs, in minutes.</summary>
    public const int CustomsMinutes = 40;

    /// <summary>The one quest the data has, so that the snapshot shows its state (quests the catalog doesn't list aren't shown).</summary>
    public const string QuestId = "5967733e86f774602332fc84";

    public static GameData Data(GameMode mode) => new()
    {
        Mode = mode,
        Language = "en",
        Maps = new[]
        {
            new ApiMap("map-customs", "Customs", "customs", "bigmap", "maps/customs_preset.bundle", null, CustomsMinutes, [], [], [], [], []),
            new ApiMap("map-woods", "Woods", "woods", "Woods", "maps/woods_preset.bundle", null, 40, [], [], [], [], []),
            // A map tarkov.dev gives no scene for: only the match setup's or the transit line's location names it.
            new ApiMap("map-gz21", "Ground Zero 21+", "ground-zero-21", "Sandbox_high", null, null, 35, [], [], [], [], []),
        }.ToDictionary(m => m.Id),
        Tasks = new Dictionary<string, ApiTask>
        {
            [QuestId] = new(QuestId, "A quest", null, null, null, null, false, false, null, null, null, false, null, [], null),
        },
        Traders = new Dictionary<string, ApiTrader>(),
        MapDefinitions = new[] { "customs", "woods", "ground-zero" }
            .Select(key => new MapDefinition { Key = key, Transform = [1, 0, 1, 0], Bounds = new WorldBox(-500, -500, 500, 500) })
            .ToList(),
        CheckedAt = DateTimeOffset.Now,
    };

    /// <summary>A new log session folder, as the game makes one each time it starts (of this build, or the rig's); lines go
    /// there from now on.</summary>
    public void NewLogSession(DateTime started, string build = Build)
    {
        var stamp = started.ToString("yyyy.MM.dd_HH-mm-ss", CultureInfo.InvariantCulture);
        var folder = Path.Combine(_root, "Logs", $"log_{stamp}_{build}");
        Directory.CreateDirectory(folder);
        _log = Path.Combine(folder, $"{stamp}_{build} application_000.log");
        _pushLog = Path.Combine(folder, $"{stamp}_{build} push-notifications_000.log");
    }

    private string _pushLog = "";

    /// <summary>One notification in the session's push-notifications log: its header line, then its JSON body.</summary>
    public void Notification(string kind, string body, DateTime? at = null)
    {
        var stamp = (at ?? DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        File.AppendAllText(_pushLog, $"{stamp}|{Build}|Info|push-notifications|Got notification | {kind}\r\n{body.ReplaceLineEndings("\r\n")}\r\n");
    }

    /// <summary>
    /// One application-log line, at <paramref name="at"/> or now. A second, empty-worded line follows it: a line is only
    /// known to be complete once the next one starts (or after a quiet second), and the tests shouldn't wait for that.
    /// </summary>
    public void Log(string message, DateTime? at = null)
    {
        var stamp = (at ?? DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        File.AppendAllText(_log, $"{stamp}|{Build}|Info|application|{message}\r\n{stamp}|{Build}|Info|application|.\r\n");
    }

    /// <summary>The lines of a raid from the menus up to its start: on the server (the match setup), as a PMC.</summary>
    public void RaidUpToItsStart(string scene, string location, DateTime started)
    {
        Log("Session mode: Pve", started.AddMinutes(-3));
        Log("PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0", started.AddMinutes(-3));
        Log($"scene preset path:{scene} rcid:x.scenespreset.asset", started.AddMinutes(-2));
        Log($"TRACE-NetworkGameCreate profileStatus: 'Profileid: 000000000000000000000003, Status: Busy, RaidMode: Online, Location: {location}, shortId: TEST01'", started.AddMinutes(-2).AddSeconds(2));
        Log("GameStarting:80.26(1.7) real:95.46(2.73) diff:15.19", started.AddSeconds(-1));
        Log("GameStarted:90.6(10.33) real:107.49(12.02) diff:16.89", started);
    }

    /// <summary>A screenshot file named as the game names it, with a position; empty, since only the name is read.</summary>
    public void Screenshot(double x, double y, double z)
    {
        var now = DateTime.Now;
        var name = string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd}[{0:HH-mm}]_{1:0.00}, {2:0.00}, {3:0.00}_0.00000, 0.00000, 0.00000, 1.00000_14.13 ({4}).png",
            now, x, y, z, Interlocked.Increment(ref _shots));
        File.WriteAllBytes(Path.Combine(_root, "Screenshots", name), []);
    }

    private int _shots;

    /// <summary>The names of the files in the Screenshots folder now.</summary>
    public IReadOnlyList<string> Screenshots =>
        Directory.EnumerateFiles(Path.Combine(_root, "Screenshots")).Select(Path.GetFileName).OfType<string>().Order().ToList();

    public Task StartAsync() => Session.StartAsync();

    /// <summary>Waits until the session's snapshot shows what the test expects, or fails saying what it showed instead.</summary>
    public async Task<SessionSnapshot> Until(Func<SessionSnapshot, bool> expected, string what)
    {
        var until = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < until)
        {
            if (expected(Snapshot))
                return Snapshot;
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
        var s = Snapshot;
        Assert.Fail($"Never saw: {what}. Last: {s.Raid.Phase}, raid map {s.RaidMap?.Name ?? "none"}, shown {s.Map?.Name ?? "none"}, " +
                    $"fix {(s.RaidFix is null ? "none" : "there")}, data {(s.Data is null ? "none" : "there")}; notices: {string.Join(" | ", Notices)}");
        return s;
    }

    /// <summary>Waits until something the session says outside its snapshot (a notice, a cue) has come.</summary>
    public async Task Until(Func<bool> expected, string what)
    {
        var until = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < until)
        {
            if (expected())
                return;
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
        Assert.Fail($"Never saw: {what}. Notices: {string.Join(" | ", Notices)}; cues: {string.Join(" | ", Cues.Select(c => $"{c.Kind} {c.MapName}"))}");
    }

    public async ValueTask DisposeAsync()
    {
        await Session.DisposeAsync();
        try
        {
            Directory.Delete(_root, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The database's file can stay open a moment longer; the folder is under %TEMP% either way.
        }
    }
}
