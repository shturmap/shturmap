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
    private MapIdentity? _map;
    private PlayerFix? _fix;
    private string? _fixMapId;
    private MapIdentity? _lastRaidMap;
    private DateTime? _lastRaidEnded;
    private IReadOnlyList<MapPlanView> _plan = [];
    private IReadOnlyList<PlanQuestView> _anyMap = [];
    // Kept raw: a raid replayed at startup ends before the map data has loaded to name it.
    private (RaidState State, DateTime EndedAt)? _lastRaidState;
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
        if (s.Fix is { } fix)
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
        _watcher.ScreenshotTaken += s => _ = Task.Run(() => OnScreenshotAsync(s));
        _watcher.Start();
        if (_locate is not null)
            _ = Task.Run(LookAgainAsync);
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

    // Looks for the game again while it, or its logs, aren't found; stops looking once both are.
    private async Task LookAgainAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                await Task.Delay(LookAgainEvery, _stop.Token);
                if (_locate is null)
                    continue;
                if (_locations is { Install: { Kind: InstallKind.Manual }, LogsFolder: not null })
                {
                    // A chosen folder is kept; looking again only notices newer game logs elsewhere (the hint).
                    var again = _locate(_chosenFolder);
                    if (SameGame(again, _locations) &&
                        !string.Equals(GameFolder.NewerElsewhere(again)?.Root, GameFolder.NewerElsewhere(_locations)?.Root, StringComparison.OrdinalIgnoreCase))
                        await RefreshLocationsAsync(again);
                    continue;
                }
                if (_locations is { Install: not null, LogsFolder: not null })
                    continue;
                var found = _locate(_chosenFolder);
                if (SameGame(found, _locations))
                    continue;
                await FollowAsync(found);
            }
        }
        catch (OperationCanceledException)
        {
        }
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

    /// <summary>Shows another map (browsing between raids). During a raid the raid's map comes back on the next fix.</summary>
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

    public Task SetQuestStateAsync(string questId, QuestState state) =>
        AddObservationsAsync([new QuestObservation(_mode, questId, state, ObservationSource.Manual, DateTime.Now, "manual:" + Guid.NewGuid())]);

    // ---- game data ----

    // A load that failed for a reason that may pass (no connection, tarkov.dev busy) is tried again this often.
    private static readonly TimeSpan DataRetry = TimeSpan.FromMinutes(2);

    private async Task LoadDataAsync(GameMode mode)
    {
        try
        {
            var data = await _loader!.LoadAsync(mode, _settings.Language ?? "en", _stop.Token);
            await _gate.WaitAsync();
            try
            {
                if (mode != _mode)
                    return; // the mode changed while loading; a newer load is under way
                _data = data;
                _dataProblem = null;
                RecomputeQuests();
                ResolveMap();
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

    private async Task BackfillLogsAsync()
    {
        if (_locations is null || _store is null)
            return;
        var observations = new List<QuestObservation>();
        foreach (var logs in _locations.AllLogsFolders)
        {
            foreach (var session in Directory.EnumerateDirectories(logs, "log_*").OrderBy(d => InstallLocator.SessionStart(Path.GetFileName(d))))
            {
                var mode = GameMode.Unknown;
                foreach (var e in LogTailer.ReadSession(session))
                {
                    if (e is SessionModeEvent m)
                        mode = m.Mode;
                    else if (e is QuestEvent q && mode != GameMode.Unknown)
                        observations.Add(FromLog(mode, q));
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
                    while (reader.TryRead(out var item))
                        Apply(item);
                    Publish();
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
                    if (_hints.Notice(notice, _tracker.State, _map?.NameId) is { } late)
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
        var transition = _tracker.Apply(item.Event);
        switch (transition)
        {
            case ModeChanged changed:
                SwitchMode(changed.State.Mode);
                break;
            case RaidLoading:
                _sideSaid = null;
                _lastClock = null;
                _trail.Clear();
                _fix = null;
                _fixMapId = null;
                ResolveMap();
                if (!item.IsReplay && _map is not null)
                {
                    // The scene line comes 1–2 s after matching starts (owner's logs: 30 loads), while matching can
                    // still be cancelled: the cue pictures the kit, the raid card lists it until the raid starts. A
                    // transit's gear is what the raid had, so its cue shows none.
                    if (phaseBefore == RaidPhase.InRaid)
                        Announce(new ViewCue(CueKind.Transit, _map.Name));
                    else
                        Announce(KitCue(CueKind.RaidLoading, _map, "loading"));
                }
                break;
            case RaidStarted started:
                ResolveMap();
                // The side is only certain now; the kit shown while loading was a PMC's, unless a setup said Scav.
                if (!item.IsReplay && started.State.Side == RaidSide.Scav && _map is not null)
                {
                    Say($"Scav raid on {_map.Name} · quest objectives don't count, items found in raid do", 8);
                    Announce(new ViewCue(CueKind.ScavRaid, _map.Name));
                }
                break;
            case RaidEnded ended:
                _sideSaid = null;
                _lastRaidMap = _map;
                _lastRaidEnded = ended.At;
                // Out of the raid there is no "you" on the map (owner, 2026-10-01).
                _fix = null;
                _fixMapId = null;
                _trail.Clear();
                if (ended.Previous.RaidStartedAt is not null)
                    _lastRaidState = (ended.Previous, ended.At);
                if (!item.IsReplay && _map is not null)
                {
                    Announce(new ViewCue(ended.Previous.RaidStartedAt is null ? CueKind.LoadCancelled : CueKind.RaidOver, _map.Name,
                        ended.Previous.RaidStartedAt is { } began ? ended.At - began : null));
                }
                break;
        }
        if (!item.IsReplay)
        {
            LogRaid(transition);
            StudyRaid(transition);
            if (transition is RaidEnded over && _hints.Ended(over, _map?.NameId) is { } hint)
                StudyHint(hint);
        }
    }

    // The kit reminder for a map (Planning.Kit): what every active quest needs there, picks first.
    private Planning.KitList KitOn(MapIdentity map)
    {
        var active = _quests.Values.Where(q => q.State == QuestState.Active).Select(q => q.QuestId);
        return _data is null ? Planning.KitList.Empty : Planning.Kit(Planning.PlanFor(_data, active, map.NormalizedName), Picks);
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
                AppLog.Info($"Raid loading: {_map?.Name ?? "unknown map"}");
                break;
            case RaidStarted started:
                AppLog.Info($"Raid started: {_map?.Name ?? "unknown map"}, side {started.State.Side}");
                break;
            case RaidEnded ended:
                AppLog.Info(ended.Previous.RaidStartedAt is { } at
                    ? $"Raid ended after {(ended.At - at).TotalMinutes:0} min"
                    : "Raid loading cancelled");
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
                var rank = _plan.ToList().FindIndex(p => p.NormalizedName == _map?.NormalizedName);
                var plan = _data is null || _map is null ? null
                    : Planning.PlanFor(_data, _quests.Values.Where(q => q.State == QuestState.Active).Select(q => q.QuestId), _map.NormalizedName);
                // The plan's COMPLETE and PROGRESS quests, to check later which of them the raid actually completed.
                Study.Game("raid.loading", ("raidMap", _map?.NormalizedName), ("planRank", rank < 0 ? null : rank + 1), ("planTop", _plan.FirstOrDefault()?.NormalizedName),
                    ("bring", plan?.Requirements.Select(r => r.Text).ToList() ?? []),
                    ("complete", plan?.Finish.Select(q => q.QuestId).ToList() ?? []),
                    ("progress", plan?.Progress.Select(q => q.QuestId).ToList() ?? []));
                break;
            case RaidStarted started:
                // How long each loading step took ("LocationLoaded:24.9", seconds since the scene line), to tune the
                // loading line's stages.
                Study.Game("raid.start", ("raidMap", _map?.NormalizedName), ("side", started.State.Side), ("sideEvidence", _tracker.SideEvidence),
                    ("loadingSteps", _tracker.LoadingSteps.Select(s => string.Create(System.Globalization.CultureInfo.InvariantCulture,
                        $"{s.Step?.ToString() ?? "GameStarted"}:{s.Seconds:0.0}")).ToList()));
                break;
            case RaidEnded ended:
                Study.Game("raid.end", ("side", ended.Previous.Side),
                    ("minutes", ended.Previous.RaidStartedAt is { } at ? (ended.At - at).TotalMinutes : null),
                    ("lastMap", _lastRaidMap?.NormalizedName));
                break;
        }
    }

    /// <summary>Picks the map to show: the raid's while one is loading or running, otherwise keep the current.</summary>
    private void ResolveMap()
    {
        if (_data is null)
            return;
        var state = _tracker.State;
        if (state.Phase != RaidPhase.Menu && _data.CreateResolver().Resolve(state.ScenePath, state.LocationId) is { } raidMap)
        {
            _map = raidMap;
            _store?.SetSetting("lastMap", raidMap.NormalizedName);
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
            var map = MapForFix(seen);
            if (map is null)
            {
                Say("Got a position, but couldn't tell which map it is on. Pick the map and take another screenshot.");
                return;
            }
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

    private MapIdentity? MapForFix(ScreenshotSeen seen)
    {
        if (_tracker.State.Phase != RaidPhase.Menu && _map is not null)
            return _map;
        // Taken just before the raid-end line reached the log.
        if (_lastRaidMap is not null && _lastRaidEnded is { } ended && seen.CreatedAt <= ended.AddSeconds(10))
            return _lastRaidMap;
        // No logs to go by: accept the shown map if the position lies on it.
        if (_map is not null && _data?.DefinitionFor(_map.NormalizedName) is { } def && def.Bounds.Contains(seen.Info.Position!.Value.X, seen.Info.Position.Value.Z))
            return _map;
        return null;
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
        // Only the game's own log counts (docs/DESIGN.md §8). Rows from earlier Tasks scans or the TarkovEyes import
        // may still be in the database; they are ignored.
        _quests = QuestProgress.Resolve(
            _store.Load(_mode).Where(o => o.Source is ObservationSource.Log or ObservationSource.Manual),
            id => tasks?.GetValueOrDefault(id)?.TaskRequirements?.Select(r => new QuestRequirement(r.Task, r.Status ?? [])) ?? [],
            id => tasks?.GetValueOrDefault(id)?.Name ?? id);
        var active = _quests.Values.Where(q => q.State == QuestState.Active).Select(q => q.QuestId).ToList();
        // A picked quest the log reports completed or failed has nothing left to do: it leaves the picks by itself.
        foreach (var done in _picks?.Prune(_mode, _quests) ?? [])
            Study.Ui("unpick", ("quest", done), ("how", "done"));
        _plan = _data is null ? [] : Planning.Suggest(_data, active, Picks);
        _anyMap = _data is null ? [] : Planning.AnyMap(_data, active);
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
        var definition = _map is not null ? _data?.DefinitionFor(_map.NormalizedName) : null;
        var fix = _fix is not null && _fixMapId == _map?.Id ? _fix : null;
        MapContent? content = null;
        var objectives = new List<ObjectiveView>();
        var extracts = new List<ExtractView>();

        if (_data is not null && _map is not null)
        {
            // In a raid the map shows what counts for the side you play: no quest objectives for a Scav (they only
            // count for the PMC), and only your side's extracts.
            var side = ShownRaid.Phase == RaidPhase.Menu ? RaidSide.Unknown : ShownRaid.Side;
            var active = side == RaidSide.Scav ? [] : _quests.Values.Where(q => q.State == QuestState.Active).Select(q => q.QuestId);
            content = MapContentBuilder.Build(_data, _map.Id, active, new HashSet<string>());
            if (side != RaidSide.Unknown)
            {
                var otherSide = side == RaidSide.Scav ? MarkerKind.ExtractPmc : MarkerKind.ExtractScav;
                content = content with { Markers = content.Markers.Where(m => m.Kind != otherSide).ToList() };
            }
            var sameArtwork = _data.MapIdsSharing(_map.NormalizedName);
            var projection = definition is not null ? MapProjection.For(definition) : null;
            // Degrees clockwise from map-up: unlike "ahead-left", still true after the player has turned.
            double? MapBearing(WorldPoint target) =>
                fix is not null && projection is not null ? projection.ScreenHeadingDegrees(fix.Position, Bearing.YawTo(fix.Position, target)) : null;
            foreach (var o in content.Objectives)
            {
                double? distance = null, height = null, bearing = null;
                RelativeDirection? direction = null;
                if (fix is not null && o.Places.Count > 0)
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
                    bearing, o.Quest.Trader));
            }
            var shownMap = _data.Maps.GetValueOrDefault(_map.Id);
            foreach (var m in content.Markers.Where(m => m.Kind is MarkerKind.ExtractPmc or MarkerKind.ExtractScav or MarkerKind.ExtractShared or MarkerKind.Transit))
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
            Raid = ShownRaid,
            SideFromLogs = _tracker.State.Side != RaidSide.Unknown,
            Mode = _mode,
            ModeReading = _modeReading,
            Map = _map,
            Definition = definition,
            Fix = fix,
            Trail = fix is not null ? _trail.ToList() : [],
            Floor = definition is not null && fix is not null ? FloorResolver.LayerFor(definition, fix.Position) : null,
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
            AnyMap = _anyMap,
            MapPlan = _data is not null && _map is not null
                ? _plan.FirstOrDefault(p => p.NormalizedName == _map.NormalizedName)
                  ?? Planning.PlanFor(_data, _quests.Values.Where(q => q.State == QuestState.Active).Select(q => q.QuestId), _map.NormalizedName)
                : null,
            LastRaid = _lastRaidState is ({ } state, var endedAt) && _data?.CreateResolver().Resolve(state.ScenePath, state.LocationId) is { } lastMap
                ? new LastRaidView(lastMap.Name, endedAt - state.RaidStartedAt!.Value, state.Side, endedAt)
                : null,
            RaidInfo = _map is not null && _data?.Maps.GetValueOrDefault(_map.Id) is { } raidMap
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
