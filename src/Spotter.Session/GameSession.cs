using System.Globalization;
using Spotter.Core;
using Spotter.Core.Logs;
using Spotter.Core.Maps;
using Spotter.Core.Navigation;
using Spotter.Core.Quests;
using Spotter.Core.Raid;
using Spotter.Data.Http;
using Spotter.Data.Maps;
using Spotter.Data.Progress;
using Spotter.Data.TarkovDev;
using Spotter.Game.Install;
using Spotter.Game.Logs;
using Spotter.Game.Screenshots;
using Spotter.Game.Settings;
using Spotter.Map;
using Spotter.Ocr;

namespace Spotter.Session;

/// <summary>
/// The running companion: finds the game, follows its logs and screenshots, keeps quest progress and game data,
/// and publishes one <see cref="SessionSnapshot"/> per change. All state changes happen under one gate; slow
/// work (downloads, OCR) runs outside it.
/// </summary>
/// <param name="locations">Game folders to use instead of discovering them (simulations and tests).</param>
public sealed class GameSession(AppPaths paths, GameLocations? locations = null) : IAsyncDisposable
{
    private static readonly TimeSpan RecentScreenshots = TimeSpan.FromDays(7);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _ocrGate = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly RaidTracker _tracker = new();
    private readonly List<WorldPoint> _trail = [];

    private ProgressStore? _store;
    private GameDataLoader? _loader;
    private LogTailer? _tailer;
    private ScreenshotWatcher? _watcher;
    private TasksScreenReader? _tasks;
    private GameSettings _settings = new([], null, false);
    private GameLocations? _locations;

    private GameMode _mode = GameMode.Pve;
    private GameData? _data;
    private string? _dataError;
    private bool _recentScanned;
    private MapIdentity? _map;
    private PlayerFix? _fix;
    private string? _fixMapId;
    private MapIdentity? _lastRaidMap;
    private DateTime? _lastRaidEnded;
    private ScanResult? _lastScan;
    private Dictionary<string, QuestStatus> _quests = new();

    public SessionSnapshot Snapshot { get; private set; } = new();

    public ArtworkProvider? Artwork { get; private set; }

    /// <summary>Raised after every change, on a background thread.</summary>
    public event Action<SessionSnapshot>? Changed;

    /// <summary>Short messages for the user ("Raid started on Customs", "Tasks scan: 2 new active").</summary>
    public event Action<string>? Notice;

    public async Task StartAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var env = new WindowsGameEnvironment();
            _store = new ProgressStore(paths.Database);
            _locations = locations ?? new InstallLocator(env).Locate(_store.GetSetting("installFolder"));
            AppLog.Info($"Game: {_locations.Install?.Kind} {_locations.Install?.Root}; logs {_locations.LogsFolder ?? "not found"}; screenshots {_locations.ScreenshotsFolder}");
            _settings = new GameSettingsReader(env).Read(_locations.SettingsFolder);
            var http = CachedHttp.CreateClient();
            _loader = new GameDataLoader(new CachedHttp(http, paths.DataCache));
            Artwork = new ArtworkProvider(new ArtworkCache(new CachedHttp(http, paths.ArtworkCache)), paths.PictureCache);
            if (TextRecognizer.Create(_settings.Language) is { } recognizer)
                _tasks = new TasksScreenReader(recognizer);
            if (Enum.TryParse<GameMode>(_store.GetSetting("mode"), out var savedMode) && savedMode != GameMode.Unknown)
                _mode = savedMode;
            ImportTarkovEyesOnce();
            RecomputeQuests();
            Publish();
        }
        finally
        {
            _gate.Release();
        }

        _ = Task.Run(() => LoadDataAsync(_mode));
        _ = Task.Run(BackfillLogsAsync);
        if (_locations.LogsFolder is { } logs)
        {
            _tailer = new LogTailer(logs);
            _tailer.Start();
            _ = Task.Run(ConsumeLogsAsync);
        }
        _watcher = new ScreenshotWatcher(_locations.ScreenshotsFolder);
        _watcher.ScreenshotTaken += s => _ = Task.Run(() => OnScreenshotAsync(s));
        _watcher.Start();
    }

    // ---- inputs from the UI ----

    /// <summary>Shows another map (browsing between raids). During a raid the raid's map comes back on the next fix.</summary>
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

    public async Task SetModeAsync(GameMode mode)
    {
        await _gate.WaitAsync();
        try
        {
            SwitchMode(mode);
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task SetQuestStateAsync(string questId, QuestState state) =>
        AddObservationsAsync([new QuestObservation(_mode, questId, state, ObservationSource.Manual, DateTime.Now, "manual:" + Guid.NewGuid())]);

    /// <summary>Accepts a Tasks-scan match that needed confirmation.</summary>
    public async Task ConfirmScanAsync(string questId)
    {
        var scan = _lastScan;
        if (scan is null)
            return;
        await AddObservationsAsync([new QuestObservation(_mode, questId, QuestState.Active, ObservationSource.TasksScan, scan.At, "tasks:" + scan.File)]);
        await _gate.WaitAsync();
        try
        {
            _lastScan = scan with { NeedConfirmation = scan.NeedConfirmation.Where(m => m.Quest?.Id != questId).ToList() };
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DismissScanAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _lastScan = null;
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    // ---- game data ----

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
                _dataError = null;
                ResolveMap();
                RecomputeQuests();
                Publish();
            }
            finally
            {
                _gate.Release();
            }
            if (!_recentScanned)
            {
                _recentScanned = true;
                await ScanRecentTasksScreenshotsAsync();
            }
        }
        catch (Exception e) when (e is HttpRequestException or IOException or TaskCanceledException or System.Text.Json.JsonException)
        {
            await _gate.WaitAsync();
            try
            {
                _dataError = e is TaskCanceledException ? "timed out" : e.Message;
                Publish();
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    private void SwitchMode(GameMode mode)
    {
        if (mode == GameMode.Unknown || mode == _mode)
            return;
        _mode = mode;
        _store?.SetSetting("mode", mode.ToString());
        _data = null;
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
        if (item.Event is QuestEvent quest)
        {
            var mode = _tracker.State.Mode == GameMode.Unknown ? _mode : _tracker.State.Mode;
            if (_store?.Add([FromLog(mode, quest)]) > 0)
            {
                RecomputeQuests();
                if (!item.IsReplay && _data?.Tasks.GetValueOrDefault(quest.QuestId) is { } task)
                    Say($"{task.Name}: {quest.Status switch { QuestLogStatus.Started => "started", QuestLogStatus.Completed => "completed", _ => "failed" }}");
            }
            return;
        }

        switch (_tracker.Apply(item.Event))
        {
            case ModeChanged changed:
                SwitchMode(changed.State.Mode);
                break;
            case RaidLoading:
                _trail.Clear();
                _fix = null;
                _fixMapId = null;
                ResolveMap();
                if (!item.IsReplay && _map is not null)
                    Say($"Loading {_map.Name}");
                break;
            case RaidStarted:
                ResolveMap();
                break;
            case RaidEnded ended:
                _lastRaidMap = _map;
                _lastRaidEnded = ended.At;
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
            var last = _store?.GetSetting("lastMap") ?? "customs";
            if (_data.MapByNormalizedName(last) is { } m)
                _map = new MapIdentity(m.Id, m.NormalizedName, m.NameId, m.ScenePath, m.Name);
        }
    }

    // ---- screenshots ----

    private async Task OnScreenshotAsync(ScreenshotSeen seen)
    {
        if (!seen.Info.HasPosition)
        {
            await ScanTasksAsync(seen);
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
            _fixMapId = map.Id;
            Publish();
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

    private async Task ScanRecentTasksScreenshotsAsync()
    {
        if (_watcher is null || _store is null)
            return;
        foreach (var seen in _watcher.Recent(RecentScreenshots).Where(s => !s.Info.HasPosition).OrderBy(s => s.CreatedAt))
        {
            if (_store.GetSetting("scanned:" + Path.GetFileName(seen.Path)) is null)
                await ScanTasksAsync(seen, quiet: true);
        }
    }

    private async Task ScanTasksAsync(ScreenshotSeen seen, bool quiet = false)
    {
        if (_tasks is null || _data is null || _store is null)
            return;
        await _ocrGate.WaitAsync();
        TasksScreen? screen;
        try
        {
            if (!await ScreenshotWatcher.WaitUntilCompleteAsync(seen.Path, TimeSpan.FromSeconds(15)))
                return;
            screen = await _tasks.ReadAsync(seen.Path);
            _store.SetSetting("scanned:" + Path.GetFileName(seen.Path), "1");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return;
        }
        finally
        {
            _ocrGate.Release();
        }
        if (screen is null || screen.Rows.Count == 0)
            return;

        await _gate.WaitAsync();
        try
        {
            if (_data is not { } data)
                return;
            var candidates = data.Tasks.Values
                .Select(t => new QuestCandidate(t.Id, t.Name, t.Map is not null && data.Maps.TryGetValue(t.Map, out var m) ? [m.Name] : []))
                .ToList();
            var matches = screen.Rows.Select(r => QuestNameMatcher.Match(r.Name, r.Location, candidates)).ToList();
            var accepted = matches.Where(m => m.Verdict == MatchVerdict.Accepted).Select(m => m.Quest!).ToList();
            var newly = accepted.Where(q => _quests.GetValueOrDefault(q.Id)?.State != QuestState.Active).Select(q => q.Name).ToList();
            _store.Add(accepted.Select(q => new QuestObservation(_mode, q.Id, QuestState.Active, ObservationSource.TasksScan, seen.CreatedAt,
                "tasks:" + Path.GetFileName(seen.Path))));
            RecomputeQuests();
            _lastScan = new ScanResult(seen.CreatedAt, Path.GetFileName(seen.Path), screen.Tab, screen.Rows.Count, newly,
                matches.Where(m => m.Verdict == MatchVerdict.NeedsConfirmation).ToList(),
                matches.Count(m => m.Verdict == MatchVerdict.Unread));
            if (!quiet || newly.Count > 0)
                Say($"Tasks scan: {screen.Rows.Count} rows read" + (newly.Count > 0 ? $", new active: {string.Join(", ", newly)}" : ", nothing new"));
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    // ---- quests ----

    private void ImportTarkovEyesOnce()
    {
        if (_store is null || _store.GetSetting("import.tarkoveyes") is not null)
            return;
        var imported = TarkovEyesImport.Read(TarkovEyesImport.DefaultPath);
        if (imported.Count > 0)
            Say($"Imported {imported.Count} quest states from TarkovEyes.");
        _store.Add(imported);
        _store.SetSetting("import.tarkoveyes", DateTime.Now.ToString("O", CultureInfo.InvariantCulture));
    }

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
        _quests = QuestProgress.Resolve(
            _store.Load(_mode),
            id => tasks?.GetValueOrDefault(id)?.TaskRequirements?.Select(r => new QuestRequirement(r.Task, r.Status ?? [])) ?? [],
            id => tasks?.GetValueOrDefault(id)?.Name ?? id);
    }

    // ---- snapshot ----

    private void Say(string message) => Notice?.Invoke(message);

    private void Publish()
    {
        var definition = _map is not null ? _data?.DefinitionFor(_map.NormalizedName) : null;
        var fix = _fix is not null && _fixMapId == _map?.Id ? _fix : null;
        MapContent? content = null;
        var objectives = new List<ObjectiveView>();
        var extracts = new List<ExtractView>();

        if (_data is not null && _map is not null)
        {
            var active = _quests.Values.Where(q => q.State == QuestState.Active).Select(q => q.QuestId);
            content = MapContentBuilder.Build(_data, _map.Id, active, new HashSet<string>());
            foreach (var o in content.Objectives)
            {
                double? distance = null, height = null;
                RelativeDirection? direction = null;
                if (fix is not null && o.Places.Count > 0)
                {
                    var nearest = o.Places.MinBy(p => fix.Position.HorizontalDistanceTo(p));
                    distance = fix.Position.HorizontalDistanceTo(nearest);
                    if (fix.YawDegrees is { } yaw)
                        direction = Bearing.Relative(yaw, Bearing.YawTo(fix.Position, nearest));
                    var dy = nearest.Y - fix.Position.Y;
                    height = Math.Abs(dy) > 3 ? dy : null;
                }
                objectives.Add(new ObjectiveView(o.Quest.Id, o.Quest.Name, _data.TraderName(o.Quest.Trader), o.Objective.Id,
                    string.IsNullOrWhiteSpace(o.Objective.Description) ? "(no description)" : o.Objective.Description!,
                    o.Done, o.Places.Count > 0, distance, direction, height));
            }
            var side = _tracker.State.Side;
            foreach (var m in content.Markers.Where(m => m.Kind is MarkerKind.ExtractPmc or MarkerKind.ExtractScav or MarkerKind.ExtractShared or MarkerKind.Transit))
            {
                if ((side == RaidSide.Scav && m.Kind == MarkerKind.ExtractPmc) || (side != RaidSide.Scav && m.Kind == MarkerKind.ExtractScav))
                    continue;
                double? distance = fix is not null ? fix.Position.HorizontalDistanceTo(m.Position) : null;
                RelativeDirection? direction = fix?.YawDegrees is { } yaw ? Bearing.Relative(yaw, Bearing.YawTo(fix.Position, m.Position)) : null;
                extracts.Add(new ExtractView(m.Id, m.Label, m.Kind, distance, direction));
            }
        }

        Snapshot = new SessionSnapshot
        {
            Raid = _tracker.State,
            Mode = _mode,
            Map = _map,
            Definition = definition,
            Fix = fix,
            Trail = fix is not null ? _trail.ToList() : [],
            Floor = definition is not null && fix is not null ? FloorResolver.LayerFor(definition, fix.Position) : null,
            Data = _data,
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
            Logs = LogsHealth(),
            Screenshots = _locations is null ? new(false, "Looking for screenshots…")
                : Directory.Exists(_locations.ScreenshotsFolder) ? new(true, "Screenshots") : new(false, "No screenshots yet"),
            DataHealth = _data is not null
                ? new(!_data.Offline, _data.Offline ? "Data (offline copy)" : "Data")
                : new(false, _dataError is null ? "Loading game data…" : "No game data: " + _dataError),
            ScreenshotKeys = _settings.ScreenshotKeys,
            LastScan = _lastScan,
        };
        Changed?.Invoke(Snapshot);
    }

    private SourceHealth LogsHealth()
    {
        if (_locations?.LogsFolder is null)
            return new(false, "Game logs not found");
        if (_tailer?.LastActivityUtc is { } at && DateTime.UtcNow - at < TimeSpan.FromMinutes(10))
            return new(true, "Logs live");
        return new(true, "Logs");
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _watcher?.Dispose();
        if (_tailer is not null)
            await _tailer.DisposeAsync();
        Artwork?.Dispose();
        _store?.Dispose();
        _stop.Dispose();
    }
}
