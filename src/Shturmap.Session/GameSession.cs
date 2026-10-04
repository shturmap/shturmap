using Shturmap.Core;
using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Core.Navigation;
using Shturmap.Core.Quests;
using Shturmap.Core.Raid;
using Shturmap.Data.Http;
using Shturmap.Data.Images;
using Shturmap.Data.Maps;
using Shturmap.Data.Progress;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Install;
using Shturmap.Game.Logs;
using Shturmap.Game.Screenshots;
using Shturmap.Game.Settings;
using Shturmap.Map;

namespace Shturmap.Session;

/// <summary>
/// The running companion: finds the game, follows its logs and screenshots, keeps quest progress and game data,
/// and publishes one <see cref="SessionSnapshot"/> per change. All state changes happen under one gate; slow
/// work (downloads) runs outside it.
/// </summary>
/// <param name="locations">Game folders to use instead of discovering them (simulations and tests).</param>
public sealed partial class GameSession(AppPaths paths, GameLocations? locations = null) : IAsyncDisposable
{

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly RaidTracker _tracker = new();
    private readonly RaidOutcomeHints _hints = new();
    private readonly List<WorldPoint> _trail = [];

    private ProgressStore? _store;
    private GameDataLoader? _loader;
    private LogTailer? _tailer;
    private ScreenshotWatcher? _watcher;
    private GameSettings _settings = new([], null, false);
    private GameLocations? _locations;

    private GameMode _mode = GameMode.Pve;
    private ModeReading _modeReading = new();
    private GameData? _data;
    private ItemSources? _sources;
    private LoadProblem? _dataProblem;
    private readonly HashSet<string> _languageSaid = [];
    // The map on screen. In a raid that is the raid's map, unless the player looks at another one (the MAP list);
    // the next position brings the raid's map back.
    private MapIdentity? _map;
    // The raid's own map while one loads or runs, as the game's log names it: where positions go, and what the rail,
    // the status words and the cues are about, whatever map is on screen. Null in the menus, and while the log names
    // a map the data doesn't know: then the raid's map is unknown, and the shown map is never taken for it.
    private MapIdentity? _raidMap;
    // The RAID LOADING or TRANSIT cue still to show: the scene line didn't name a map the data knows, so the cue waits
    // for the line that does (the match setup's or the transit's location).
    private CueKind? _loadingCueOwed;
    private PlayerFix? _fix;
    private string? _fixMapId;
    private MapIdentity? _lastRaidMap;
    private DateTime? _lastRaidEnded;
    private IReadOnlyList<MapPlanView> _plan = [];
    private IReadOnlyList<PlanQuestView> _anyMap = [];
    // Kept raw: a raid replayed at startup ends before the map data has loaded to name it. EndInLog is false for a
    // raid the log never ended: then its length isn't known.
    private (RaidState State, DateTime EndedAt, bool EndInLog)? _lastRaidState;
    private double? _lastClock;
    private Dictionary<string, QuestStatus> _quests = new();

    public SessionSnapshot Snapshot { get; private set; } = new();

    public ArtworkProvider? Artwork { get; private set; }

    /// <summary>Trader portraits and item icons, fetched when first shown.</summary>
    public GameArt? Art { get; private set; }

    /// <summary>The study log: game events here, the player's use of Shturmap from the UI. Developer builds only (owner,
    /// 2026-10-03); a release has the calls, but the log can't be on there (<see cref="StudyLog.Available"/>).</summary>
    public StudyLog Study { get; } = new(paths.Study);

#if DEVTOOLS
    /// <summary>
    /// The study log for this session regardless of the switch: true for <c>--study</c>, false for snapshot and
    /// fake-game runs, null to follow the switch.
    /// </summary>
    public bool? StudyOverride { get; set; }

    /// <summary>The key of the study-log switch in the app's settings ("on" or "off"; absent is on in a dev build).</summary>
    public const string StudySetting = "studyLog";

    /// <summary>The override a command line asks for: snapshot and fake-game runs never keep one, <c>--study</c> does.</summary>
    public static bool? StudyOverrideFor(IReadOnlyCollection<string> cli) =>
        cli.Contains("--snapshot") || cli.Contains("--fake-game") ? false : cli.Contains("--study") ? true : null;

    /// <summary>Whether the study log is kept: the override if there is one, else the switch. Unset is on: a developer
    /// build is for studying how Shturmap is used (owner, 2026-10-03).</summary>
    public static bool StudyOn(bool? studyOverride, string? setting) => studyOverride ?? setting != "off";

    /// <summary>The "Keep a study log" switch, saved with the app's settings; the log starts or stops now.</summary>
    public async Task SetStudyLogAsync(bool on)
    {
        await _gate.WaitAsync();
        try
        {
            _store?.SetSetting(StudySetting, on ? "on" : "off");
            if (StudyOverride is not null || Study.Enabled == on)
                return;
            if (!on)
            {
                Study.Game("study.off");
                Study.Enabled = false;
                ClearRunning();
            }
            else
            {
                Study.Enabled = true;
                Study.Game("study.on", ("version", Version), ("prevClean", MarkRunning()));
            }
            AppLog.Info("Study log " + (on ? "on" : "off"));
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }
#endif

    /// <summary>Shturmap's version with its commit ("0.1.0+d349909").</summary>
    public static string Version { get; } = ShortVersion(
        typeof(GameSession).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion
        ?? typeof(GameSession).Assembly.GetName().Version?.ToString() ?? "?");

    private static string ShortVersion(string version) =>
        version.IndexOf('+') is var plus and >= 0 && version.Length > plus + 8 ? version[..(plus + 8)] : version;

    // Every study line says where in the game the player was.
    private IEnumerable<(string, object?)> StudyContext()
    {
        var s = Snapshot;
        yield return ("phase", s.Raid.Phase);
        yield return ("map", s.Map?.NormalizedName);
        if (s.Raid.RaidStartedAt is { } started)
            yield return ("raidMin", (DateTime.Now - started).TotalMinutes);
        if (s.RaidFix is { } fix)
            yield return ("fixAgeS", (DateTime.Now - fix.At).TotalSeconds);
    }

    /// <summary>Raised after every change, on a background thread.</summary>
    public event Action<SessionSnapshot>? Changed;

    /// <summary>Short messages for the user ("Raid started on Customs", "Scav raid on Customs · …").</summary>
    public event Action<SessionNotice>? Notice;

    public async Task StartAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var env = new WindowsGameEnvironment();
            _store = new ProgressStore(paths.Database);
            _picks = new QuestPicks(_store.GetSetting, _store.SetSetting);
            _ticks = new ObjectiveTicks(_store.GetSetting, _store.SetSetting);
            Study.Context = StudyContext;
#if DEVTOOLS
            // Developer builds only (owner, 2026-10-03); a release leaves the study log off and any old days alone.
            Study.Enabled = StudyOn(StudyOverride, _store.GetSetting(StudySetting));
            Study.Prune(DateTime.Now);
#endif
            Study.Game("app.start", ("version", typeof(GameSession).Assembly.GetName().Version?.ToString()),
                ("build", BuildTime()), ("prevClean", MarkRunning()));
            _env = env;
            // Game folders given by the caller (a fake game, a simulation) stay as they are; discovered ones can be
            // looked for again (the player chose a folder, or the game was installed later).
            _locate = locations is not null ? null : folder => new InstallLocator(env).Locate(folder, discover: !NoGame);
            _chosenFolder = _store.GetSetting(InstallFolderSetting);
            _locations = locations ?? _locate!(_chosenFolder);
            ReportGameFolders(_locations);
            _settings = new GameSettingsReader(env).Read(_locations.SettingsFolder);
            AppLog.Info($"Game settings: language {_settings.Language ?? "unknown"}, screenshot key {(_settings.ScreenshotKeys.Count > 0 ? string.Join(" or ", _settings.ScreenshotKeys) : "unknown")}");
            var http = CachedHttp.CreateClient();
            _loader = new GameDataLoader(new CachedHttp(http, paths.DataCache));
            Artwork = new ArtworkProvider(new ArtworkCache(new CachedHttp(http, paths.ArtworkCache)), paths.PictureCache,
                new CachedHttp(http, paths.MapTileCache));
            Art = new GameArt(http, paths.GameArtCache);
            if (Enum.TryParse<GameMode>(_store.GetSetting("mode"), out var savedMode) && savedMode != GameMode.Unknown)
                _mode = savedMode;
#if DEVTOOLS
            AppLog.Info($"Mode {_mode}; study log {(Study.Enabled ? "on" : "off")}");
#else
            AppLog.Info($"Mode {_mode}");
#endif
            RecomputeQuests();
            Publish();
        }
        finally
        {
            _gate.Release();
        }

        _ = Task.Run(() => LoadDataAsync(_mode));
        _ = Task.Run(BackfillLogsAsync);
        FollowLogs();
        _watcher = new ScreenshotWatcher(_locations.ScreenshotsFolder);
        _watcher.ScreenshotTaken += s => _ = Task.Run(async () =>
        {
            try
            {
                await OnScreenshotAsync(s);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Failed("Placing a position", e);
            }
        });
        // The watcher keeps going and sets itself up again; each kind of trouble is said once (Failed).
        _watcher.WatchProblem += (what, e) => Failed("Watching for screenshots: " + what, e);
        _watcher.Start();
        if (_locate is not null)
            _ = Task.Run(LookAgainAsync);
        _ = Task.Run(WatchOpenRaidAsync);
    }

    // ---- the game's folders: found, chosen, or found later (owner, 2026-10-03: the no-game fallback) ----

    /// <summary>The setting that holds the folder the player chose with "Choose game folder…".</summary>
    public const string InstallFolderSetting = "installFolder";

    /// <summary>While the game or its logs aren't found, discovery runs again this often (registry and file checks
    /// only), so a game installed or first started later is followed without a restart.</summary>
    public static readonly TimeSpan LookAgainEvery = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Developer switch <c>--no-game</c>: discovery looks only at a folder the player chose, so the no-game state can
    /// be seen on a PC that has the game. Set before <see cref="StartAsync"/>.
    /// </summary>
    public bool NoGame { get; init; }

    private IGameEnvironment? _env;
    private Func<string?, GameLocations>? _locate;
    // The folder the player chose ("Choose game folder…"), as saved; null while the game is found automatically.
    private string? _chosenFolder;

    /// <summary>Whether "Choose game folder…" can work here: not with game folders given from outside (fake games).</summary>
    public bool CanChooseGameFolder => _locate is not null;

    /// <summary>
    /// The folder the player chose for the game. It counts if it holds the game (the build folder or the one above it,
    /// as discovery accepts them); then it is saved and followed from now on, without a restart: its logs are read for
    /// quest history and followed live. If it doesn't, nothing changes and the notice says why.
    /// </summary>
    /// <returns>True if the folder holds the game.</returns>
    public async Task<bool> ChooseGameFolderAsync(string folder)
    {
        if (_locate is null)
            return false;
        var found = _locate(folder);
        var chosen = ChosenCandidate(found);
        if (chosen is not { IsValid: true })
        {
            AppLog.Info($"Game folder chosen but not the game: {folder} ({chosen?.Rejected ?? "not considered"})");
            await SayAsync(InstallLocator.Explain(chosen), 12);
            return false;
        }
        await _gate.WaitAsync();
        try
        {
            _store?.SetSetting(InstallFolderSetting, folder);
            _chosenFolder = folder;
            AppLog.Info("Game folder chosen: " + folder);
        }
        finally
        {
            _gate.Release();
        }
        await FollowAsync(found);
        return true;
    }

    /// <summary>
    /// Forgets the folder the player chose and finds the game by itself again, at once (owner, 2026-10-04: the way
    /// back from a folder that holds a game, but not the one played). Says what it found, or that it found none.
    /// </summary>
    public async Task FindGameAutomaticallyAsync()
    {
        if (_locate is null || _store is null)
            return;
        GameLocations found;
        await _gate.WaitAsync();
        try
        {
            found = FindAutomatically(_store, _locate);
            _chosenFolder = null;
            AppLog.Info("Game folder: found automatically again");
        }
        finally
        {
            _gate.Release();
        }
        await FollowAsync(found, announce: true);
    }

    /// <summary>FIND AUTOMATICALLY's finding: the chosen folder forgotten, then the game looked for as at a start
    /// with nothing chosen.</summary>
    public static GameLocations FindAutomatically(ProgressStore store, Func<string?, GameLocations> locate)
    {
        store.RemoveSetting(InstallFolderSetting);
        return locate(store.GetSetting(InstallFolderSetting));
    }

    // ---- background work that outlives one failure ----

    // A step of the session's background work failed: reading a line of the game's log, the quest history of a log
    // session, a look for the game, a position. It is said in the app log, once per kind of failure, and the work
    // carries on. One database or file error used to end log following, the quest history or the look for the game
    // for the rest of the session without a sign, while the LOGS light stayed "live" (review of 2026-10-04, A31).
    // Cancelling is no failure, and neither is what goes wrong while the session closes.
    private readonly HashSet<string> _failuresSaid = [];
    private const int FailuresSaidAtMost = 20;

    private void Failed(string what, Exception e)
    {
        if (e is OperationCanceledException || _stop.IsCancellationRequested)
            return;
        lock (_failuresSaid)
        {
            if (_failuresSaid.Count >= FailuresSaidAtMost || !_failuresSaid.Add($"{what}|{e.GetType().FullName}|{e.Message}"))
                return;
        }
        AppLog.Error($"{what} failed; carrying on", e);
        Failure?.Invoke(what, e);
    }

    /// <summary>A step of the session's background work failed and the work carried on (it is in the app log too); raised
    /// once per kind of failure, on a background thread.</summary>
    public event Action<string, Exception>? Failure;

    // Looks for the game again while it, or its logs, aren't found; stops looking once both are.
    private async Task LookAgainAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                await Task.Delay(LookAgainEvery, _stop.Token);
                try
                {
                    await LookAgainOnceAsync();
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Failed("Looking for the game again", e);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task LookAgainOnceAsync()
    {
        if (_locate is null)
            return;
        if (_locations is { Install: { Kind: InstallKind.Manual }, LogsFolder: not null })
        {
            // A chosen folder is kept; looking again only notices newer game logs elsewhere (the hint).
            var again = _locate(_chosenFolder);
            if (SameGame(again, _locations) &&
                !string.Equals(GameFolder.NewerElsewhere(again)?.Root, GameFolder.NewerElsewhere(_locations)?.Root, StringComparison.OrdinalIgnoreCase))
                await RefreshLocationsAsync(again);
            return;
        }
        if (_locations is { Install: not null, LogsFolder: not null })
            return;
        var found = _locate(_chosenFolder);
        if (SameGame(found, _locations))
            return;
        await FollowAsync(found);
    }

    /// <summary>The same game in the same place: nothing to switch.</summary>
    public static bool SameGame(GameLocations a, GameLocations? b) =>
        string.Equals(a.Install?.Root, b?.Install?.Root, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.LogsFolder, b?.LogsFolder, StringComparison.OrdinalIgnoreCase);

    /// <summary>The chosen folder in what discovery made of it: the game if <see cref="InstallCandidate.IsValid"/>, else why not.</summary>
    public static InstallCandidate? ChosenCandidate(GameLocations found) => found.Candidates.FirstOrDefault(c => c.Kind == InstallKind.Manual);

    /// <summary>A switch worth saying once: the game's logs are followed now, where none (or others) were before.</summary>
    public static bool SaysFound(GameLocations? before, GameLocations after) =>
        after.LogsFolder is not null && !string.Equals(after.LogsFolder, before?.LogsFolder, StringComparison.OrdinalIgnoreCase);

    // The same game, seen again with its install list up to date (a newer session elsewhere): nothing to switch.
    private async Task RefreshLocationsAsync(GameLocations again)
    {
        await _gate.WaitAsync();
        try
        {
            _locations = again;
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    // Follows other game folders from now on: the settings read again, the old logs let go, the new ones read for quest
    // history and followed live. Says once when the game is followed now where it wasn't before; with announce, says
    // what was found either way (FIND AUTOMATICALLY).
    private async Task FollowAsync(GameLocations found, bool announce = false)
    {
        // A choice and the next look-again never switch the logs at the same time.
        await _following.WaitAsync();
        try
        {
            await FollowNowAsync(found, announce);
        }
        finally
        {
            _following.Release();
        }
    }

    private readonly SemaphoreSlim _following = new(1, 1);

    /// <summary>What finding the game automatically says it found.</summary>
    public static string FoundText(GameLocations found) => found.Install switch
    {
        null => "No game found on this PC: browse the maps, or choose the game folder in settings.",
        { } install when found.LogsFolder is null => $"Found Escape from Tarkov in {install.Root}; it hasn't run on this PC yet.",
        { } install => $"Found Escape from Tarkov in {install.Root}: quests and raids follow the game now.",
    };

    private async Task FollowNowAsync(GameLocations found, bool announce = false)
    {
        LogTailer? old;
        await _gate.WaitAsync();
        try
        {
            var say = SaysFound(_locations, found);
            old = string.Equals(found.LogsFolder, _locations?.LogsFolder, StringComparison.OrdinalIgnoreCase) ? null : _tailer;
            if (old is not null)
                _tailer = null;
            _locations = found;
            ReportGameFolders(found, notice: false);
            if (_env is not null)
                _settings = new GameSettingsReader(_env).Read(found.SettingsFolder);
            if (say || announce)
                Say(FoundText(found), 10);
            Publish();
        }
        finally
        {
            _gate.Release();
        }
        if (old is not null)
            await old.DisposeAsync();
        if (_tailer is null)
            FollowLogs();
        await BackfillLogsAsync();
    }

    // Follows the found logs live, if there are any.
    private void FollowLogs()
    {
        if (_locations?.LogsFolder is not { } logs)
            return;
        _tailer = new LogTailer(logs);
        // The tailer leaves out what it can't read and carries on; said once per kind, so the app log shows why an
        // event is missing (the text names the file, never a record's content).
        _tailer.ReadProblem += (what, e) => AppLog.Warn($"Following the game's logs: {what} couldn't be read and was left out", e);
        _tailer.Start();
        _ = Task.Run(ConsumeLogsAsync);
    }

    /// <summary>
    /// The mode, chosen by the player: only while no game is found (owner, 2026-10-03: the mode comes from the game's
    /// log; a chooser belongs to the no-game state, where there is no log to say it). Loads that mode's data.
    /// </summary>
    public async Task ChooseModeAsync(GameMode mode)
    {
        await _gate.WaitAsync();
        try
        {
            if (_locations is not { Install: null })
                return;
            Study.Ui("mode.choose", ("mode", mode));
            SwitchMode(mode);
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SayAsync(string message, double seconds)
    {
        await _gate.WaitAsync();
        try
        {
            Say(message, seconds);
        }
        finally
        {
            _gate.Release();
        }
    }

    // ---- inputs from the UI ----

    /// <summary>
    /// The side for this raid, said by the player because the logs can't tell (PvE raids are hosted locally and look
    /// the same for PMC and Scav). Holds until the raid ends; ignored when the logs know the side.
    /// </summary>
    public async Task SetSideAsync(RaidSide side)
    {
        await _gate.WaitAsync();
        try
        {
            if (_tracker.State.Phase == RaidPhase.Menu || _tracker.State.Side != RaidSide.Unknown)
                return;
            _sideSaid = side;
            Study.Game("side.set", ("side", side));
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    private RaidSide? _sideSaid;

    // The raid as shown: the tracker's, with the side the player said when the logs couldn't tell.
    private RaidState ShownRaid =>
        _sideSaid is { } said && _tracker.State.Phase != RaidPhase.Menu && _tracker.State.Side == RaidSide.Unknown
            ? _tracker.State with { Side = said }
            : _tracker.State;

    /// <summary>
    /// Shows another map (browsing between raids). During a raid it is only a look: the raid, its rail and its status
    /// words stay on the raid's own map, and the raid's map comes back on screen with the next position.
    /// </summary>
    public async Task SelectMapAsync(string normalizedName)
    {
        await _gate.WaitAsync();
        try
        {
            if (_data?.MapByNormalizedName(normalizedName) is { } map)
            {
                _map = new MapIdentity(map.Id, map.NormalizedName, map.NameId, map.ScenePath, map.Name);
                _store?.SetSetting("lastMap", map.NormalizedName);
                Publish();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    // ---- game data ----

    // A load that failed for a reason that may pass (no connection, tarkov.dev busy) is tried again this often.
    private static readonly TimeSpan DataRetry = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Game data given by the caller instead of tarkov.dev's, for a session that never asks the network (tests of the
    /// session itself: a fake game folder's logs and screenshots through the real tailer, tracker and watcher).
    /// </summary>
    public Func<GameMode, GameData>? GivenData { get; init; }

    private async Task LoadDataAsync(GameMode mode)
    {
        try
        {
            var data = GivenData is { } given ? given(mode) : await _loader!.LoadAsync(mode, _settings.Language ?? "en", _stop.Token);
            await _gate.WaitAsync();
            try
            {
                if (mode != _mode)
                    return; // the mode changed while loading; a newer load is under way
                _data = data;
                _dataProblem = null;
                RecomputeQuests();
                ResolveMap();
                AnnounceLoading();
                // The raid length is known now: an open raid may be past it.
                CloseRaidThatCannotRun(announce: !_replaying);
                Publish();
                var how = data.Offline ? "from the saved copy (tarkov.dev unreachable)" : "from tarkov.dev";
                var line = $"Data loaded {how}: {mode}, language {data.Language}, {data.Tasks.Count} quests, {data.Maps.Count} maps, checked {data.CheckedAt.ToLocalTime():yyyy-MM-dd HH:mm}";
                if (data.Offline)
                    AppLog.Warn(line);
                else
                    AppLog.Info(line);
                if (data.MissingLanguage is { } missing)
                {
                    AppLog.Warn($"No '{missing}' texts from tarkov.dev; using English");
                    if (_languageSaid.Add(missing))
                        Say($"No {LanguageName(missing)} texts on tarkov.dev; showing English.", 10);
                }
                Study.Game("data.loaded", ("mode", mode), ("activeQuests", _quests.Values.Count(q => q.State == QuestState.Active)),
                    ("planTop", _plan.FirstOrDefault()?.NormalizedName));
                if (GivenData is null)
                    _ = Task.Run(() => LoadSourcesAsync(mode, data.Language));
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception e) when (!_stop.IsCancellationRequested)
        {
            var problem = LoadProblem.Explain(e);
            await _gate.WaitAsync();
            try
            {
                if (mode != _mode)
                    return;
                var again = _dataProblem?.Kind == problem.Kind;
                _dataProblem = problem;
                if (again)
                    AppLog.Warn($"Data load failed again ({problem.Kind}{(problem.Status is { } s ? " " + s : "")}): {e.GetType().Name}: {e.Message}");
                else
                {
                    AppLog.Error($"Data load failed ({problem.Kind}): {problem.What}", e);
                    Say(DataNotice(problem), 30, offersReport: problem.Transient || problem.Advice == LoadProblem.Report);
                }
                Publish();
            }
            finally
            {
                _gate.Release();
            }
            if (problem.Transient)
                _ = RetryDataAsync(mode);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RetryDataAsync(GameMode mode)
    {
        try
        {
            await Task.Delay(DataRetry, _stop.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (mode == _mode && _data is null)
            await LoadDataAsync(mode);
    }

    /// <summary>What the player is told when the data didn't load: what failed, what to do, and where to report it.</summary>
    public static string DataNotice(LoadProblem problem) => problem.Transient
        ? $"No game data. {problem.Text} Shturmap tries again every {DataRetry.TotalMinutes:0} minutes; if it keeps failing, please report it."
        : $"No game data. {problem.Text}";

    private static string LanguageName(string code)
    {
        try
        {
            var culture = System.Globalization.CultureInfo.GetCultureInfo(code);
            return culture.ThreeLetterISOLanguageName == "ivl" ? $"'{code}'" : culture.EnglishName;
        }
        catch (System.Globalization.CultureNotFoundException)
        {
            return $"'{code}'";
        }
    }

    // Said once at start when the game or its logs aren't found: without them quests and raids can't follow the game.
    // Logs where the game was found. The rail says it as long as the game or its logs are missing (the no-game line),
    // so no notice says it a second time; notice: true only for the developer view's old trigger.
    private void ReportGameFolders(GameLocations found, bool notice = false)
    {
        if (found.Install is { } install)
            AppLog.Info($"Game found: {install.Kind} at {install.Root} ({install.Found})");
        else
            AppLog.Warn($"Game not found ({found.Candidates.Count} candidates: {string.Join("; ", found.Candidates.Select(c => $"{c.Kind} {c.Root}: {c.Rejected}"))})");
        if (found.LogsFolder is { } logs)
            AppLog.Info("Logs: " + logs);
        else
            AppLog.Warn("Logs folder not found");
        var shots = Directory.Exists(found.ScreenshotsFolder);
        var line = $"Screenshots: {found.ScreenshotsFolder}{(shots ? "" : " (not there yet; the game makes it with the first screenshot)")}";
        if (shots)
            AppLog.Info(line);
        else
            AppLog.Warn(line);

        if (!notice)
            return;
        if (found.Install is null)
            Say("Couldn't find Escape from Tarkov on this PC, so quests and raids won't follow the game. If it is installed, please report it.", 30, offersReport: true);
        else if (found.LogsFolder is null)
            Say("Found the game, but not its Logs folder, so quests and raids won't follow the game until it has run once.", 30);
    }

    // Where items come from: only the item cards need it, so it loads after everything else.
    private async Task LoadSourcesAsync(GameMode mode, string language)
    {
        try
        {
            var sources = await _loader!.LoadSourcesAsync(mode, language, _stop.Token);
            await _gate.WaitAsync();
            try
            {
                if (mode != _mode)
                    return;
                _sources = sources;
                Publish();
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception e) when (!_stop.IsCancellationRequested)
        {
            // Only the item cards' "where to get it" is missing; everything else works.
            AppLog.Warn($"Item sources not loaded ({LoadProblem.Explain(e).Kind}): {e.GetType().Name}: {e.Message}");
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void SwitchMode(GameMode mode)
    {
        mode = ModeReading.Follow(_mode, mode);
        if (mode == _mode)
            return;
        _mode = mode;
        _store?.SetSetting("mode", mode.ToString());
        AppLog.Info($"Mode {mode}: loading its data");
        _data = null;
        _dataProblem = null;
        _sources = null;
        RecomputeQuests();
        _ = Task.Run(() => LoadDataAsync(mode));
    }

    // ---- logs ----

    // The quest history from every log session on disk. A session that can't be read (a file error, a line in a shape
    // the parser doesn't expect) costs its own quests only, never the history of all the others.
    private async Task BackfillLogsAsync()
    {
        if (_locations is null || _store is null)
            return;
        try
        {
            var observations = new List<QuestObservation>();
            foreach (var logs in _locations.AllLogsFolders)
            {
                List<string> sessions;
                try
                {
                    sessions = Directory.EnumerateDirectories(logs, "log_*").OrderBy(d => InstallLocator.SessionStart(Path.GetFileName(d))).ToList();
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Failed("Reading the quest history: listing the log sessions", e);
                    continue;
                }
                foreach (var session in sessions)
                {
                    try
                    {
                        var mode = GameMode.Unknown;
                        var read = new List<QuestObservation>();
                        foreach (var e in LogTailer.ReadSession(session, (what, problem) => Failed("Reading the quest history: " + what, problem)))
                        {
                            if (e is SessionModeEvent m)
                                mode = m.Mode;
                            else if (e is QuestEvent q && mode != GameMode.Unknown)
                                read.Add(FromLog(mode, q));
                        }
                        observations.AddRange(read);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        Failed("Reading the quest history of a log session", e);
                    }
                }
            }
            var added = _store.Add(observations);
            if (added == 0)
                return;
            await _gate.WaitAsync();
            try
            {
                RecomputeQuests();
                Publish();
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Failed("Reading the quest history", e);
        }
    }

    private async Task ConsumeLogsAsync()
    {
        var reader = _tailer!.Events;
        try
        {
            while (await reader.WaitToReadAsync(_stop.Token))
            {
                await _gate.WaitAsync(_stop.Token);
                try
                {
                    // Line by line: one that can't be applied doesn't take the rest of the batch, or the following, with it.
                    while (reader.TryRead(out var item))
                    {
                        try
                        {
                            Apply(item);
                        }
                        catch (Exception e) when (e is not OperationCanceledException)
                        {
                            Failed("Applying a line of the game's log", e);
                        }
                    }
                    try
                    {
                        // What the log left open, read back at start or gone silent since, may not still be running.
                        CloseRaidThatCannotRun(announce: !_replaying);
                        Publish();
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        Failed("Showing what the game's log said", e);
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Apply(LogEvent item)
    {
        _replaying = item.IsReplay;
        // The game has started again (a newer log session) while a raid of the session before was still open: its end
        // never reached the log. It is closed at what that session last said, not at this session's login line, which
        // would make it a raid as long as the game was closed.
        if (_raidSession is not null && !string.Equals(item.Session, _raidSession, StringComparison.OrdinalIgnoreCase))
            CloseUnfinishedRaid("the game started again", announce: !item.IsReplay);
        if (item.Event is QuestEvent quest)
        {
            var mode = _tracker.State.Mode == GameMode.Unknown ? _mode : _tracker.State.Mode;
            if (_store?.Add([FromLog(mode, quest)]) > 0)
            {
                RecomputeQuests();
                if (!item.IsReplay)
                {
                    var name = _data?.Tasks.GetValueOrDefault(quest.QuestId)?.Name;
                    Study.Game("quest", ("id", quest.QuestId), ("name", name), ("state", quest.Status));
                    if (name is not null)
                        Say($"{name}: {quest.Status switch { QuestLogStatus.Started => "started", QuestLogStatus.Completed => "completed", _ => "failed" }}");
                }
            }
            return;
        }

        // A quest message's time is the server's; every other line's is the log's own.
        _lastLogAt = item.Event.At;
        switch (item.Event)
        {
            case GroupRaidSettingsEvent pick:
                if (!item.IsReplay)
                    OnGroupPick(pick);
                return;
            case GroupStatusEvent group:
                if (!item.IsReplay)
                    Study.Game("group.status", ("status", group.Status));
                return;
            case InsuranceNoticeEvent notice:
                if (!item.IsReplay)
                {
                    Study.Game("insurance", ("kind", notice.Kind), ("location", notice.LocationId), ("items", notice.ItemCount));
                    if (_hints.Notice(notice, _tracker.State, _raidMap?.NameId) is { } late)
                        StudyHint(late);
                }
                return;
        }

        // The status bar says where the mode comes from; a mode the game names that Shturmap can't read keeps the
        // last known one (SwitchMode ignores Unknown).
        if (item.Event is SessionModeEvent said)
        {
            if (said.Mode == GameMode.Unknown && said.Raw != _modeReading.Unknown)
                AppLog.Warn($"The game's log names a mode Shturmap doesn't know ('{said.Raw}'); keeping {_mode}");
            _modeReading = _modeReading.Read(said);
        }

        var phaseBefore = _tracker.State.Phase;
        var locationBefore = _tracker.State.LocationId;
        var transition = _tracker.Apply(item.Event);
        switch (transition)
        {
            case ModeChanged changed:
                SwitchMode(changed.State.Mode);
                break;
            case RaidLoading:
                _raidSession = item.Session;
                _sideSaid = null;
                _lastClock = null;
                _trail.Clear();
                _fix = null;
                _fixMapId = null;
                // A transit loads another map: the raid's map is worked out anew, never carried over.
                _raidMap = null;
                ResolveMap();
                // The scene line comes 1–2 s after matching starts (owner's logs: 30 loads), while matching can
                // still be cancelled: the cue pictures the kit, the raid card lists it until the raid starts. A
                // transit's gear is what the raid had, so its cue shows none.
                _loadingCueOwed = item.IsReplay ? null : phaseBefore == RaidPhase.InRaid ? CueKind.Transit : CueKind.RaidLoading;
                AnnounceLoading();
                break;
            case RaidStarted started:
                _loadingCueOwed = null;
                ResolveMap();
                // The side is only certain now; the kit shown while loading was a PMC's, unless a setup said Scav.
                if (!item.IsReplay && started.State.Side == RaidSide.Scav && _raidMap is not null)
                {
                    Say($"Scav raid on {_raidMap.Name} · quest objectives don't count, items found in raid do", 8);
                    Announce(new ViewCue(CueKind.ScavRaid, _raidMap.Name));
                }
                break;
            case RaidEnded ended:
                RaidOver(ended, announce: !item.IsReplay);
                break;
            case null when _raidMap is null && _tracker.State.Phase != RaidPhase.Menu && _tracker.State.LocationId != locationBefore:
                // A map tarkov.dev gives no scene for is named by the match setup's or the transit line's location, a
                // moment after the scene line: the raid's map is known from here, and the loading cue comes now.
                ResolveMap();
                AnnounceLoading();
                break;
        }
        if (!item.IsReplay)
            Record(transition);
    }

    // A raid (or a load) is over: back to planning. Its cue says how long it ran only when the log has its end.
    private void RaidOver(RaidEnded ended, bool announce)
    {
        _sideSaid = null;
        _loadingCueOwed = null;
        _raidSession = null;
        // The raid's own map, never one that was only being looked at.
        _lastRaidMap = _raidMap;
        _raidMap = null;
        _lastRaidEnded = ended.At;
        // Out of the raid there is no "you" on the map (owner, 2026-10-01).
        _fix = null;
        _fixMapId = null;
        _trail.Clear();
        if (ended.Previous.RaidStartedAt is not null)
            _lastRaidState = (ended.Previous, ended.At, ended.EndInLog);
        if (announce && _lastRaidMap is not null)
            Announce(new ViewCue(ended.Previous.RaidStartedAt is null ? CueKind.LoadCancelled : CueKind.RaidOver, _lastRaidMap.Name, ended.Length));
    }

    // What the app log and the study log keep of a raid's steps (never during the replay at start).
    private void Record(RaidTransition? transition)
    {
        LogRaid(transition);
        StudyRaid(transition);
        if (transition is RaidEnded over && _hints.Ended(over, _lastRaidMap?.NameId) is { } hint)
            StudyHint(hint);
    }

    // ---- a raid whose end never reached the log (Shturmap.Core.Raid.UnfinishedRaid) ----

    // The log session the open raid's lines are in, and when the log last said anything (not a quest message, whose
    // time is the server's).
    private string? _raidSession;
    private DateTime? _lastLogAt;

    /// <summary>How often an open raid is looked at again: can it still be running?</summary>
    public TimeSpan OpenRaidCheck { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>The time, for telling whether an open raid can still be running; the PC's clock unless a test gives its own.</summary>
    public Func<DateTime> Clock { get; init; } = () => DateTime.Now;

    // Closes the open raid or load if it can't still be running: the game has started again since, or it began longer
    // ago than its map's raid length allows. Looked at after every batch of log lines, when the data (and with it the
    // raid length) arrives, and every half minute while the app runs, since a log that has fallen silent sends nothing.
    private bool CloseRaidThatCannotRun(bool announce)
    {
        var newer = _raidSession is not null && _tailer?.CurrentSession is { } current && !string.Equals(current, _raidSession, StringComparison.OrdinalIgnoreCase);
        var minutes = _raidMap is not null ? _data?.Maps.GetValueOrDefault(_raidMap.Id)?.RaidDuration : null;
        if (!UnfinishedRaid.CannotStillRun(_tracker.State, Clock(), minutes, newer))
            return false;
        return CloseUnfinishedRaid(newer ? "the game started again" : $"it began more than {UnfinishedRaid.Bound(minutes).TotalMinutes:0} min ago", announce);
    }

    // The raid's end isn't in the log: it is over all the same, and nothing says how long it ran. Not announced while
    // the logs are read back at start: no cue then, and nothing for the study log.
    private bool CloseUnfinishedRaid(string why, bool announce)
    {
        var wasInRaid = _tracker.State.RaidStartedAt is not null;
        if (_tracker.CloseUnfinished(_lastLogAt ?? Clock()) is not { } ended)
            return false;
        var map = _raidMap?.Name ?? "unknown map";
        RaidOver(ended, announce);
        AppLog.Info($"{(wasInRaid ? "Raid" : "Raid loading")} on {map} closed: its end isn't in the game's log ({why})");
        // No hint from an end without a time; the hints only forget the raid.
        _hints.Ended(ended, _lastRaidMap?.NameId);
        if (announce)
            StudyRaid(ended);
        return true;
    }

    private async Task WatchOpenRaidAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                await Task.Delay(OpenRaidCheck, _stop.Token);
                await _gate.WaitAsync(_stop.Token);
                try
                {
                    // Long after the start: the view changes now, by itself, so it is announced.
                    if (CloseRaidThatCannotRun(announce: true))
                        Publish();
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Failed("Looking at the open raid", e);
                }
                finally
                {
                    _gate.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    // The RAID LOADING (or TRANSIT) cue that is owed, once the raid's map can be named; until then it waits.
    private void AnnounceLoading()
    {
        if (_loadingCueOwed is not { } kind || _raidMap is null || _tracker.State.Phase != RaidPhase.Loading)
            return;
        _loadingCueOwed = null;
        Announce(kind == CueKind.Transit ? new ViewCue(CueKind.Transit, _raidMap.Name) : KitCue(CueKind.RaidLoading, _raidMap, "loading"));
    }

    // The kit reminder for a map (Planning.Kit): what every active quest needs there, picks first.
    private Planning.KitList KitOn(MapIdentity map)
    {
        var active = _quests.Values.Where(q => q.State == QuestState.Active).Select(q => q.QuestId);
        return _data is null ? Planning.KitList.Empty : Planning.Kit(Planning.PlanFor(_data, active, map.NormalizedName, _done), Picks);
    }

    // A raid loading's or a group pick's cue with the kit pictured (owner, 2026-10-03: the text notice "Loading … ·
    // bring: …" was long and came as a list of words; the pictures read at a glance). The study log notes it.
    private ViewCue KitCue(CueKind kind, MapIdentity map, string when)
    {
        var kit = KitOn(map);
        var (shown, more) = Planning.CueKit(kit);
        var picks = Picks;
        Study.Game("kit.reminder", ("when", when), ("map", map.NormalizedName), ("items", kit.Count),
            ("forPicks", kit.All.Count(r => r.QuestIds.Any(picks.Contains))));
        return new ViewCue(kind, map.Name, Kit: shown.Select(r => new CueItem(r.ItemId, r.Kind)).ToList(), KitMore: more);
    }

    // The group's leader picked a raid, 20–70 s before loading starts (owner's logs): show that map now with its kit,
    // while there is still time to change gear. Only in the menus; a later pick replaces it.
    private void OnGroupPick(GroupRaidSettingsEvent pick)
    {
        var map = _data?.CreateResolver().Resolve(null, pick.LocationId);
        Study.Game("group.pick", ("location", pick.LocationId), ("pickMap", map?.NormalizedName), ("timeVariant", pick.TimeVariant));
        if (_tracker.State.Phase != RaidPhase.Menu || map is null)
            return;
        _map = map;
        _store?.SetSetting("lastMap", map.NormalizedName);
        Announce(KitCue(CueKind.GroupPick, map, "groupPick"));
    }

    // A hint of how the raid ended, for the study log only: never shown, and no hint proves nothing.
    private void StudyHint(OutcomeHint hint) =>
        Study.Game("raid.outcomeHint", ("lostInsured", true), ("location", hint.LocationId), ("noticeFromEndS", hint.NoticeSecondsFromEnd));

    // Raids in the app log: the support trail of what the game did (map names only, never profile ids).
    private void LogRaid(RaidTransition? transition)
    {
        switch (transition)
        {
            case RaidLoading:
                AppLog.Info($"Raid loading: {_raidMap?.Name ?? "unknown map"}");
                break;
            case RaidStarted started:
                AppLog.Info($"Raid started: {_raidMap?.Name ?? "unknown map"}, side {started.State.Side}");
                break;
            case RaidEnded ended:
                AppLog.Info(ended.Length is { } length
                    ? $"Raid ended after {length.TotalMinutes:0} min"
                    : ended.Previous.RaidStartedAt is null ? "Raid loading cancelled" : "Raid ended");
                break;
        }
    }

    // Raids in the study log, with what the planner had suggested, so choices can be compared with suggestions.
    private void StudyRaid(RaidTransition? transition)
    {
        switch (transition)
        {
            case ModeChanged changed:
                Study.Game("mode", ("mode", changed.State.Mode));
                break;
            case RaidLoading:
                var rank = _plan.ToList().FindIndex(p => p.NormalizedName == _raidMap?.NormalizedName);
                var plan = _data is null || _raidMap is null ? null
                    : Planning.PlanFor(_data, _quests.Values.Where(q => q.State == QuestState.Active).Select(q => q.QuestId), _raidMap.NormalizedName, _done);
                // The plan's COMPLETE and PROGRESS quests, to check later which of them the raid actually completed.
                Study.Game("raid.loading", ("raidMap", _raidMap?.NormalizedName), ("planRank", rank < 0 ? null : rank + 1), ("planTop", _plan.FirstOrDefault()?.NormalizedName),
                    ("bring", plan?.Requirements.Select(r => r.Text).ToList() ?? []),
                    ("complete", plan?.Finish.Select(q => q.QuestId).ToList() ?? []),
                    ("progress", plan?.Progress.Select(q => q.QuestId).ToList() ?? []));
                break;
            case RaidStarted started:
                // How long each loading step took ("LocationLoaded:24.9", seconds since the scene line), to tune the
                // loading line's stages.
                Study.Game("raid.start", ("raidMap", _raidMap?.NormalizedName), ("side", started.State.Side), ("sideEvidence", _tracker.SideEvidence),
                    ("loadingSteps", _tracker.LoadingSteps.Select(s => string.Create(System.Globalization.CultureInfo.InvariantCulture,
                        $"{s.Step?.ToString() ?? "GameStarted"}:{s.Seconds:0.0}")).ToList()));
                break;
            case RaidEnded ended:
                Study.Game("raid.end", ("side", ended.Previous.Side),
                    ("minutes", ended.Length?.TotalMinutes),
                    ("endInLog", ended.EndInLog),
                    ("lastMap", _lastRaidMap?.NormalizedName));
                break;
        }
    }

    /// <summary>
    /// Works out the raid's own map while one is loading or running, from what the log names (the scene, then the
    /// location), and shows it; otherwise keeps the map on screen. A raid on a map the data doesn't know has no map:
    /// the one on screen is not taken for it.
    /// </summary>
    private void ResolveMap()
    {
        if (_data is null)
            return;
        var state = _tracker.State;
        _raidMap = state.Phase == RaidPhase.Menu ? null : _data.CreateResolver().Resolve(state.ScenePath, state.LocationId);
        if (_raidMap is not null)
        {
            _map = _raidMap;
            _store?.SetSetting("lastMap", _raidMap.NormalizedName);
            return;
        }
        if (_map is null)
        {
            // Between raids, open on the best suggestion for the next one.
            var last = _plan.FirstOrDefault()?.NormalizedName ?? _store?.GetSetting("lastMap") ?? "customs";
            if (_data.MapByNormalizedName(last) is { } m)
                _map = new MapIdentity(m.Id, m.NormalizedName, m.NameId, m.ScenePath, m.Name);
        }
    }

    // ---- screenshots ----

    // Screenshots only give positions (from their names); quest states come from the logs alone.
    private async Task OnScreenshotAsync(ScreenshotSeen seen)
    {
        if (!seen.Info.HasPosition)
        {
            // A screenshot key press that gave no position (the study log asked how often this happens).
            Study.Game("screenshot.nopos", ("name", Path.GetFileName(seen.Path)), ("phase", _tracker.State.Phase));
            return;
        }
        await _gate.WaitAsync();
        try
        {
            var position = seen.Info.Position!.Value;
            var place = PlaceFix(_tracker.State.Phase, _raidMap, _locations?.LogsFolder is not null, _map,
                onShownMap: _map is not null && _data?.DefinitionFor(_map.NormalizedName) is { } shown && shown.Bounds.Contains(position.X, position.Z),
                // Taken just before the raid's end line reached the log: the raid is over, and so is its "you".
                fromEndedRaid: _lastRaidEnded is { } ended && seen.CreatedAt <= ended.AddSeconds(10));
            if (place.Map is not { } map)
            {
                Study.Game("screenshot.unplaced", ("phase", _tracker.State.Phase), ("said", place.Why is not null));
                if (place.Why is not null)
                    Say(place.Why);
                return;
            }
            // A position is on the raid's map: it brings that map back on screen when another was being looked at.
            if (map.Id != _map?.Id)
                _map = map;
            if (_fix is not null && _fixMapId == map.Id)
                _trail.Add(_fix.Position);
            _fix = new PlayerFix(seen.Info.Position!.Value, seen.Info.YawDegrees, seen.CreatedAt);
            _lastClock = seen.Info.RaidClockHours;
            _fixMapId = map.Id;
            Publish();
            var p = _fix.Position;
            // The raid clock, with group.pick's time variant, settles which of the two raid times "CURR" and "PAST" are.
            Study.Game("fix", ("x", p.X), ("y", p.Y), ("z", p.Z), ("floor", Snapshot.Floor?.Name), ("fixMap", map.NormalizedName),
                ("clock", seen.Info.RaidClockHours));
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Where a position from a screenshot goes.</summary>
    /// <param name="Map">The map it is plotted on, or null: it isn't shown.</param>
    /// <param name="Why">What the player is told when it isn't shown; null to say nothing.</param>
    public sealed record FixPlace(MapIdentity? Map, string? Why = null);

    /// <summary>
    /// Where a position from a screenshot goes (docs/DESIGN.md §4, principle 8). In a raid: on the raid's own map,
    /// whatever map is on screen, and nowhere while the log names a map the data doesn't know (the shown map is never
    /// taken for the raid's). Out of a raid there is no "you": while the game's logs are followed, a position the log
    /// shows no raid for isn't shown. Only with no game logs at all, when nothing can say where the game is, does the
    /// shown map take it, if the position lies on it.
    /// </summary>
    /// <param name="logsFollowed">The game's logs are found, so they say whether a raid runs.</param>
    /// <param name="onShownMap">The position lies within the shown map's bounds.</param>
    /// <param name="fromEndedRaid">The screenshot was taken before the raid's end line reached the log: nothing to say.</param>
    public static FixPlace PlaceFix(RaidPhase phase, MapIdentity? raidMap, bool logsFollowed, MapIdentity? shownMap, bool onShownMap, bool fromEndedRaid)
    {
        if (phase != RaidPhase.Menu)
            return raidMap is not null ? new(raidMap) : new(null, "Got a position, but Shturmap can't tell which map this raid is on, so it isn't shown.");
        if (logsFollowed)
            return new(null, fromEndedRaid ? null : "Got a position, but the game's log shows no raid, so it isn't shown.");
        return shownMap is not null && onShownMap
            ? new(shownMap)
            : new(null, "Got a position, but couldn't tell which map it is on. Pick the map and take another screenshot.");
    }

    // ---- quests ----

    private async Task AddObservationsAsync(IReadOnlyList<QuestObservation> observations)
    {
        await _gate.WaitAsync();
        try
        {
            _store?.Add(observations);
            RecomputeQuests();
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    private static QuestObservation FromLog(GameMode mode, QuestEvent q) =>
        new(mode, q.QuestId, QuestProgress.FromLog(q.Status), ObservationSource.Log, q.At, "log:" + q.EventId);

    private void RecomputeQuests()
    {
        if (_store is null)
            return;
        var tasks = _data?.Tasks;
        // Only the game's own log counts (docs/DESIGN.md §8). Rows from earlier Tasks scans, the TarkovEyes import or
        // quest states once set by hand may still be in the database; they are ignored (owner, 2026-10-04: no editing
        // of quests by hand).
        _quests = QuestProgress.Resolve(
            _store.Load(_mode).Where(o => o.Source is ObservationSource.Log),
            id => tasks?.GetValueOrDefault(id)?.TaskRequirements?.Select(r => new QuestRequirement(r.Task, r.Status ?? [])) ?? [],
            id => tasks?.GetValueOrDefault(id)?.Name ?? id);
        var active = _quests.Values.Where(q => q.State == QuestState.Active).Select(q => q.QuestId).ToList();
        // A picked quest the log reports completed or failed has nothing left to do: it leaves the picks by itself.
        foreach (var done in _picks?.Prune(_mode, _quests) ?? [])
            Study.Ui("unpick", ("quest", done), ("how", "done"));
        // So do the ticks on its objectives: the quest is over, there is nothing left to leave out.
        foreach (var gone in _ticks?.Prune(_mode, _quests, QuestOf) ?? [])
            Study.Ui("untick", ("objective", gone), ("quest", QuestOf(gone)), ("how", "done"));
        _ticked = _ticks?.Of(_mode) ?? NoTicks;
        _done = _ticked.Count == 0 ? NothingDone : _ticked.Keys.ToHashSet(StringComparer.Ordinal);
        _plan = _data is null ? [] : Planning.Suggest(_data, active, Picks, _done);
        _anyMap = _data is null ? [] : Planning.AnyMap(_data, active, _done);
    }

    // ---- ticks: the objectives the player says are done ----

    private ObjectiveTicks? _ticks;

    private static readonly IReadOnlyDictionary<string, DateOnly> NoTicks = new Dictionary<string, DateOnly>();
    private static readonly IReadOnlySet<string> NothingDone = new HashSet<string>();

    // This mode's ticks as of the last RecomputeQuests, and their ids: what the plans, the map and the cards leave out.
    private IReadOnlyDictionary<string, DateOnly> _ticked = NoTicks;
    private IReadOnlySet<string> _done = NothingDone;

    // Which quest an objective belongs to, from the data; built once per data set.
    private (GameData Data, Dictionary<string, string> Quests)? _objectiveQuests;

    private string? QuestOf(string objectiveId)
    {
        if (_data is null)
            return null;
        if (!ReferenceEquals(_objectiveQuests?.Data, _data))
        {
            var quests = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var task in _data.Tasks.Values)
                foreach (var objective in task.Objectives ?? [])
                    quests[objective.Id] = task.Id;
            _objectiveQuests = (_data, quests);
        }
        return _objectiveQuests.Value.Quests.GetValueOrDefault(objectiveId);
    }

    /// <summary>
    /// Ticks an objective as done, or unticks it (the box on its quest card; owner, 2026-10-04). The game's logs never
    /// say that a single objective is done, so this is the one thing the player may tell Shturmap about progress: it is
    /// never asked for, and the quest's own state still comes from the logs alone. Only an active quest's objective
    /// can be ticked; a tick can always be taken back, and leaves by itself when the log reports the quest completed
    /// or failed.
    /// </summary>
    /// <param name="how">For the study log: "card", "dev", ….</param>
    /// <param name="save">False for a tick that must not outlive the session (developer snapshots).</param>
    public async Task ToggleTickAsync(string objectiveId, string how, bool save = true)
    {
        await _gate.WaitAsync();
        try
        {
            if (_ticks is null)
                return;
            var quest = QuestOf(objectiveId);
            var ticked = _ticks.Of(_mode).ContainsKey(objectiveId);
            if (!ticked && (quest is null || _quests.GetValueOrDefault(quest)?.State != QuestState.Active))
                return;
            var now = _ticks.Toggle(_mode, objectiveId, DateOnly.FromDateTime(Clock()), save);
            Study.Ui(now ? "tick" : "untick", ("objective", objectiveId), ("quest", quest), ("how", how));
            RecomputeQuests();
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    // ---- picks: the quests chosen for the coming raid ----

    private QuestPicks? _picks;

    private IReadOnlySet<string> Picks => _picks?.Of(_mode) ?? new HashSet<string>();

    /// <summary>
    /// Picks a quest for the coming raid, or unpicks it (the pen on a quest). Only an active quest can be picked; a
    /// pick holds until the quest is done, the pen is clicked again or the picks are cleared.
    /// </summary>
    /// <param name="how">For the study log: "pen", "snapshot", ….</param>
    /// <param name="save">False for a pick that must not outlive the session (developer snapshots).</param>
    public async Task TogglePickAsync(string questId, string how, bool save = true)
    {
        await _gate.WaitAsync();
        try
        {
            if (_picks is null)
                return;
            var picked = Picks.Contains(questId);
            if (!picked && _quests.GetValueOrDefault(questId)?.State != QuestState.Active)
                return;
            var now = _picks.Toggle(_mode, questId, save);
            Study.Ui(now ? "pick" : "unpick", ("quest", questId), ("how", how), ("picks", Picks.Count));
            RecomputeQuests();
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Unpicks every quest for this mode (CLEAR PICKS, outside raids).</summary>
    public async Task ClearPicksAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_picks is null || Picks.Count == 0)
                return;
            Study.Ui("picks.clear", ("picks", Picks.Count));
            _picks.Clear(_mode);
            RecomputeQuests();
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    public string? GetSetting(string key) => _store?.GetSetting(key);

    public void SetSetting(string key, string value) => _store?.SetSetting(key, value);

    // ---- snapshot ----

    /// <summary>
    /// Changes of view Shturmap makes on its own (a raid loading, a transit, a Scav raid, the raid over), for a big
    /// cue in the middle of the map. Never raised while the logs are replayed at start.
    /// </summary>
    public event Action<ViewCue>? Cue;

    private void Announce(ViewCue cue)
    {
        Study.Game("cue", ("kind", cue.Kind), ("map", cue.MapName));
        Cue?.Invoke(cue);
    }

    private void Say(string message, double seconds = 6, bool offersReport = false)
    {
        Study.Game("notice", ("text", message));
        Notice?.Invoke(new SessionNotice(message, TimeSpan.FromSeconds(seconds), offersReport));
    }

    private void Publish()
    {
        var raid = ShownRaid;
        var inRaid = raid.Phase != RaidPhase.Menu;
        // The rail follows the raid: its objectives, ways out, plan and raid facts are the raid's own map's, also while
        // another map is looked at (the MAP list); between raids they are the shown map's. A raid on a map the data
        // doesn't know has none of them: the shown map is never taken for the raid's.
        var railMap = inRaid ? _raidMap : _map;
        var definition = _map is not null ? _data?.DefinitionFor(_map.NormalizedName) : null;
        var railDefinition = railMap is null ? null : railMap.Id == _map?.Id ? definition : _data?.DefinitionFor(railMap.NormalizedName);
        // The position as the map draws it, only on the map it was taken on; the rail measures from it whatever is shown.
        var shownFix = _fix is not null && _fixMapId == _map?.Id ? _fix : null;
        var fix = _fix is not null && _fixMapId == railMap?.Id ? _fix : null;
        MapContent? content = null;
        var objectives = new List<ObjectiveView>();
        var extracts = new List<ExtractView>();

        if (_data is not null)
        {
            // In a raid the map shows what counts for the side you play: no quest objectives for a Scav (they only
            // count for the PMC), and only your side's extracts.
            var side = inRaid ? raid.Side : RaidSide.Unknown;
            var active = side == RaidSide.Scav ? [] : _quests.Values.Where(q => q.State == QuestState.Active).Select(q => q.QuestId).ToList();
            var data = _data;
            MapContent ContentOf(MapIdentity map)
            {
                var built = MapContentBuilder.Build(data, map.Id, active, _done);
                if (side == RaidSide.Unknown)
                    return built;
                var otherSide = side == RaidSide.Scav ? MarkerKind.ExtractPmc : MarkerKind.ExtractScav;
                return built with { Markers = built.Markers.Where(m => m.Kind != otherSide).ToList() };
            }
            if (_map is not null)
                content = ContentOf(_map);
            var railContent = railMap is null ? null : railMap.Id == _map?.Id ? content : ContentOf(railMap);
            var sameArtwork = railMap is null ? new HashSet<string>() : _data.MapIdsSharing(railMap.NormalizedName);
            var projection = railDefinition is not null ? MapProjection.For(railDefinition) : null;
            // Degrees clockwise from map-up: unlike "ahead-left", still true after the player has turned.
            double? MapBearing(WorldPoint target) =>
                fix is not null && projection is not null ? projection.ScreenHeadingDegrees(fix.Position, Bearing.YawTo(fix.Position, target)) : null;
            foreach (var o in railContent?.Objectives ?? [])
            {
                double? distance = null, height = null, bearing = null;
                RelativeDirection? direction = null;
                // An objective ticked as done isn't measured: it is no place to go to any more.
                if (fix is not null && o.Places.Count > 0 && !o.Done)
                {
                    var nearest = o.Places.MinBy(p => fix.Position.HorizontalDistanceTo(p));
                    distance = fix.Position.HorizontalDistanceTo(nearest);
                    if (fix.YawDegrees is { } yaw)
                        direction = Bearing.Relative(yaw, Bearing.YawTo(fix.Position, nearest));
                    bearing = MapBearing(nearest);
                    var dy = nearest.Y - fix.Position.Y;
                    height = Math.Abs(dy) > 3 ? dy : null;
                }
                objectives.Add(new ObjectiveView(o.Quest.Id, o.Quest.Name, _data.TraderName(o.Quest.Trader), o.Objective.Id,
                    ObjectiveText(o.Objective),
                    o.Done, o.Places.Count > 0, distance, direction, height,
                    QuestTaxonomy.Classify(o.Objective.Type), Planning.Needs(_data, o.Quest, o.Objective, sameArtwork, o.Places.Count > 0, _sources),
                    bearing, o.Quest.Trader, Planning.NeedKey(_data, o.Quest, o.Objective, sameArtwork, o.Places.Count > 0)));
            }
            var shownMap = railMap is null ? null : _data.Maps.GetValueOrDefault(railMap.Id);
            foreach (var m in (railContent?.Markers ?? []).Where(m => m.Kind is MarkerKind.ExtractPmc or MarkerKind.ExtractScav or MarkerKind.ExtractShared or MarkerKind.Transit))
            {
                if ((side == RaidSide.Scav && m.Kind == MarkerKind.ExtractPmc) || (side != RaidSide.Scav && m.Kind == MarkerKind.ExtractScav))
                    continue;
                double? distance = fix is not null ? fix.Position.HorizontalDistanceTo(m.Position) : null;
                RelativeDirection? direction = fix?.YawDegrees is { } yaw ? Bearing.Relative(yaw, Bearing.YawTo(fix.Position, m.Position)) : null;
                // Marker ids are "extract:<id>" and "transit:<id>".
                var id = m.Id[(m.Id.IndexOf(':') + 1)..];
                var (needs, item) = m.Kind == MarkerKind.Transit
                    ? (shownMap?.Transits?.FirstOrDefault(t => t.Id == id) is { } transit ? ExtractRules.Needs(transit) : "", (string?)null)
                    : shownMap?.Extracts?.FirstOrDefault(e => e.Id == id) is { } extract ? ExtractRules.Needs(_data, shownMap, extract) : ("", null);
                extracts.Add(new ExtractView(m.Id, m.Label, m.Kind, distance, direction, MapBearing(m.Position), needs, item));
            }
        }

        Snapshot = new SessionSnapshot
        {
            Raid = raid,
            SideFromLogs = _tracker.State.Side != RaidSide.Unknown,
            Mode = _mode,
            ModeReading = _modeReading,
            Map = _map,
            RaidMap = inRaid ? _raidMap : null,
            Definition = definition,
            Fix = shownFix,
            Trail = shownFix is not null ? _trail.ToList() : [],
            Floor = definition is not null && shownFix is not null ? FloorResolver.LayerFor(definition, shownFix.Position) : null,
            RaidFix = fix,
            RaidFloor = railDefinition is not null && fix is not null ? FloorResolver.LayerFor(railDefinition, fix.Position) : null,
            Data = _data,
            Sources = _sources,
            // Repeatable (daily/weekly) tasks have ids no catalog lists; they are kept in the store but not shown.
            Quests = _data is null ? _quests : _quests.Where(q => _data.Tasks.ContainsKey(q.Key)).ToDictionary(),
            Content = content,
            Objectives = objectives
                .OrderBy(o => o.HasPlace ? 0 : 1)
                .ThenBy(o => o.Distance ?? double.MaxValue)
                .ThenBy(o => o.QuestName, StringComparer.CurrentCulture)
                .ToList(),
            Extracts = extracts.OrderBy(e => e.Distance ?? double.MaxValue).ThenBy(e => e.Name, StringComparer.CurrentCulture).ToList(),
            Locations = _locations,
            CanChooseGameFolder = _locate is not null,
            ChosenGameFolder = _chosenFolder,
            Logs = LogsHealth(),
            Screenshots = _locations is null ? new(false, "Looking for screenshots…")
                : Directory.Exists(_locations.ScreenshotsFolder) ? new(true, "Screenshots") : new(false, "No screenshots yet"),
            DataHealth = _data is not null
                ? new(!_data.Offline, _data.Offline ? "Data (offline copy)" : "Data")
                : new(false, _dataProblem is null ? "Loading game data…" : "No game data"),
            DataProblem = _dataProblem,
            GameLanguage = _settings.Language,
            StudyLogOn = Study.Enabled,
            ScreenshotKeys = _settings.ScreenshotKeys,
            Plan = _plan,
            Picks = Picks,
            Ticks = _ticked,
            Done = _done,
            AnyMap = _anyMap,
            MapPlan = _data is not null && railMap is not null
                ? _plan.FirstOrDefault(p => p.NormalizedName == railMap.NormalizedName)
                  ?? Planning.PlanFor(_data, _quests.Values.Where(q => q.State == QuestState.Active).Select(q => q.QuestId), railMap.NormalizedName, _done)
                : null,
            LastRaid = _lastRaidState is ({ } state, var endedAt, var endInLog) && _data?.CreateResolver().Resolve(state.ScenePath, state.LocationId) is { } lastMap
                ? new LastRaidView(lastMap.Name, endInLog ? endedAt - state.RaidStartedAt!.Value : TimeSpan.Zero, state.Side, endedAt) { LengthKnown = endInLog }
                : null,
            RaidInfo = railMap is not null && _data?.Maps.GetValueOrDefault(railMap.Id) is { } raidMap
                ? new RaidInfo(raidMap.RaidDuration ?? 0, _data.BossesOn(raidMap.Id).Take(3).Select(Planning.BossText).ToList(),
                    _tracker.State.Phase == RaidPhase.InRaid ? _lastClock : null)
                : null,
        };
        // Changes in the number of active quests outside quest events (the study log saw 47 become 56 unexplained):
        // the quest catalog arriving, a mode switch, prerequisites implied by a later quest.
        var activeCount = Snapshot.ActiveQuestCount;
        if (activeCount != _publishedActive && _publishedActive >= 0 && !_replaying)
            Study.Game("quests.count", ("from", _publishedActive), ("to", activeCount), ("data", _data is not null), ("mode", _mode));
        _publishedActive = activeCount;
        Changed?.Invoke(Snapshot);
    }

    private int _publishedActive = -1;
    private bool _replaying;

    private SourceHealth LogsHealth()
    {
        if (_locations?.LogsFolder is null)
            return new(false, "Game logs not found");
        if (_tailer?.LastActivityUtc is { } at && DateTime.UtcNow - at < TimeSpan.FromMinutes(10))
            return new(true, "Logs live");
        return new(true, "Logs");
    }

    /// <summary>
    /// An objective's line in the raid card: its description, and "(optional)" when tarkov.dev marks it so, as the
    /// quest card says it (owner, 2026-10-03: optional places "might still be very relevant for a quest").
    /// </summary>
    public static string ObjectiveText(ApiObjective objective) =>
        (string.IsNullOrWhiteSpace(objective.Description) ? "(no description)" : objective.Description!) + (objective.Optional ? " (optional)" : "");

    // ---- study log: why a session started ----

    // A marker file exists while the app runs; finding it at start means the last session didn't end cleanly (a
    // crash, or a kill to install a new build).
    private string RunningMarker => Path.Combine(paths.Study, "running");

    private bool? MarkRunning()
    {
        if (!Study.Enabled)
            return null;
        try
        {
            Directory.CreateDirectory(paths.Study);
            var clean = !File.Exists(RunningMarker);
            File.WriteAllText(RunningMarker, DateTime.Now.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            return clean;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private void ClearRunning()
    {
        try
        {
            File.Delete(RunningMarker);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    // When this build was made: a new build between sessions is testing, not play. The exe's own time: the 0.1.0 single
    // exe unpacked its assemblies into %TEMP% on its first start, so theirs were the unpacking's.
    private static DateTime? BuildTime()
    {
        var location = Environment.ProcessPath ?? "";
        return location.Length > 0 && File.Exists(location) ? File.GetLastWriteTime(location) : null;
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _watcher?.Dispose();
        if (_tailer is not null)
            await _tailer.DisposeAsync();
        Artwork?.Dispose();
        _store?.Dispose();
        Study.Game("app.exit");
        if (Study.Enabled)
            ClearRunning();
        Study.Dispose();
        _stop.Dispose();
    }
}
