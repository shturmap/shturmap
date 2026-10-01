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
public sealed class GameSession(AppPaths paths, GameLocations? locations = null) : IAsyncDisposable
{

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly RaidTracker _tracker = new();
    private readonly List<WorldPoint> _trail = [];

    private ProgressStore? _store;
    private GameDataLoader? _loader;
    private LogTailer? _tailer;
    private ScreenshotWatcher? _watcher;
    private GameSettings _settings = new([], null, false);
    private GameLocations? _locations;

    private GameMode _mode = GameMode.Pve;
    private GameData? _data;
    private ItemSources? _sources;
    private string? _dataError;
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

    /// <summary>The study log: game events here, the player's use of Shturmap from the UI.</summary>
    public StudyLog Study { get; } = new(paths.Study);

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

    /// <summary>Short messages for the user ("Raid started on Customs", "Loading Streets of Tarkov · bring: …").</summary>
    public event Action<SessionNotice>? Notice;

    public async Task StartAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var env = new WindowsGameEnvironment();
            Study.Context = StudyContext;
            Study.Game("app.start", ("version", typeof(GameSession).Assembly.GetName().Version?.ToString()));
            _store = new ProgressStore(paths.Database);
            _locations = locations ?? new InstallLocator(env).Locate(_store.GetSetting("installFolder"));
            AppLog.Info($"Game: {_locations.Install?.Kind} {_locations.Install?.Root}; logs {_locations.LogsFolder ?? "not found"}; screenshots {_locations.ScreenshotsFolder}");
            _settings = new GameSettingsReader(env).Read(_locations.SettingsFolder);
            var http = CachedHttp.CreateClient();
            _loader = new GameDataLoader(new CachedHttp(http, paths.DataCache));
            Artwork = new ArtworkProvider(new ArtworkCache(new CachedHttp(http, paths.ArtworkCache)), paths.PictureCache);
            Art = new GameArt(http, paths.GameArtCache);
            if (Enum.TryParse<GameMode>(_store.GetSetting("mode"), out var savedMode) && savedMode != GameMode.Unknown)
                _mode = savedMode;
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
                RecomputeQuests();
                ResolveMap();
                Publish();
                Study.Game("data.loaded", ("mode", mode), ("activeQuests", _quests.Values.Count(q => q.State == QuestState.Active)),
                    ("planTop", _plan.FirstOrDefault()?.NormalizedName));
                _ = Task.Run(() => LoadSourcesAsync(mode, data.Language));
            }
            finally
            {
                _gate.Release();
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
        catch (Exception e) when (e is HttpRequestException or IOException or TaskCanceledException or System.Text.Json.JsonException)
        {
            AppLog.Error("Item sources could not be loaded", e);
        }
    }

    private void SwitchMode(GameMode mode)
    {
        if (mode == GameMode.Unknown || mode == _mode)
            return;
        _mode = mode;
        _store?.SetSetting("mode", mode.ToString());
        _data = null;
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

        var transition = _tracker.Apply(item.Event);
        switch (transition)
        {
            case ModeChanged changed:
                SwitchMode(changed.State.Mode);
                break;
            case RaidLoading:
                _lastClock = null;
                _trail.Clear();
                _fix = null;
                _fixMapId = null;
                ResolveMap();
                if (!item.IsReplay && _map is not null)
                {
                    // Last call while matching can still be cancelled: what this map's quests need.
                    var active = _quests.Values.Where(q => q.State == QuestState.Active).Select(q => q.QuestId);
                    var needs = _data is null ? null : Planning.PlanFor(_data, active, _map.NormalizedName)?.Requirements;
                    Say(needs is { Count: > 0 }
                        ? $"Loading {_map.Name} · bring: {string.Join(", ", needs.Select(r => r.Text))}"
                        : $"Loading {_map.Name}", needs is { Count: > 0 } ? 45 : 6);
                }
                break;
            case RaidStarted:
                ResolveMap();
                break;
            case RaidEnded ended:
                _lastRaidMap = _map;
                _lastRaidEnded = ended.At;
                if (ended.Previous.RaidStartedAt is not null)
                    _lastRaidState = (ended.Previous, ended.At);
                break;
        }
        if (!item.IsReplay)
            StudyRaid(transition);
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
                var bring = _data is null || _map is null ? null
                    : Planning.PlanFor(_data, _quests.Values.Where(q => q.State == QuestState.Active).Select(q => q.QuestId), _map.NormalizedName)?.Requirements;
                Study.Game("raid.loading", ("raidMap", _map?.NormalizedName), ("planRank", rank < 0 ? null : rank + 1), ("planTop", _plan.FirstOrDefault()?.NormalizedName),
                    ("bring", bring?.Select(r => r.Text).ToList() ?? []));
                break;
            case RaidStarted started:
                Study.Game("raid.start", ("raidMap", _map?.NormalizedName), ("side", started.State.Side));
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
            return;
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
            Study.Game("fix", ("x", p.X), ("y", p.Y), ("z", p.Z), ("floor", Snapshot.Floor?.Name), ("fixMap", map.NormalizedName));
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
        _plan = _data is null ? [] : Planning.Suggest(_data, active);
        _anyMap = _data is null ? [] : Planning.AnyMap(_data, active);
    }

    public string? GetSetting(string key) => _store?.GetSetting(key);

    public void SetSetting(string key, string value) => _store?.SetSetting(key, value);

    // ---- snapshot ----

    private void Say(string message, double seconds = 6)
    {
        Study.Game("notice", ("text", message));
        Notice?.Invoke(new SessionNotice(message, TimeSpan.FromSeconds(seconds)));
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
            var active = _quests.Values.Where(q => q.State == QuestState.Active).Select(q => q.QuestId);
            content = MapContentBuilder.Build(_data, _map.Id, active, new HashSet<string>());
            var sameArtwork = _data.MapIdsSharing(_map.NormalizedName);
            var projection = definition is not null ? new MapProjection(definition) : null;
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
                    string.IsNullOrWhiteSpace(o.Objective.Description) ? "(no description)" : o.Objective.Description!,
                    o.Done, o.Places.Count > 0, distance, direction, height,
                    QuestTaxonomy.Classify(o.Objective.Type), Planning.Needs(_data, o.Quest, o.Objective, sameArtwork, o.Places.Count > 0),
                    bearing, o.Quest.Trader));
            }
            var side = _tracker.State.Side;
            foreach (var m in content.Markers.Where(m => m.Kind is MarkerKind.ExtractPmc or MarkerKind.ExtractScav or MarkerKind.ExtractShared or MarkerKind.Transit))
            {
                if ((side == RaidSide.Scav && m.Kind == MarkerKind.ExtractPmc) || (side != RaidSide.Scav && m.Kind == MarkerKind.ExtractScav))
                    continue;
                double? distance = fix is not null ? fix.Position.HorizontalDistanceTo(m.Position) : null;
                RelativeDirection? direction = fix?.YawDegrees is { } yaw ? Bearing.Relative(yaw, Bearing.YawTo(fix.Position, m.Position)) : null;
                extracts.Add(new ExtractView(m.Id, m.Label, m.Kind, distance, direction, MapBearing(m.Position)));
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
            Logs = LogsHealth(),
            Screenshots = _locations is null ? new(false, "Looking for screenshots…")
                : Directory.Exists(_locations.ScreenshotsFolder) ? new(true, "Screenshots") : new(false, "No screenshots yet"),
            DataHealth = _data is not null
                ? new(!_data.Offline, _data.Offline ? "Data (offline copy)" : "Data")
                : new(false, _dataError is null ? "Loading game data…" : "No game data: " + _dataError),
            ScreenshotKeys = _settings.ScreenshotKeys,
            Plan = _plan,
            AnyMap = _anyMap,
            LastRaid = _lastRaidState is ({ } state, var endedAt) && _data?.CreateResolver().Resolve(state.ScenePath, state.LocationId) is { } lastMap
                ? new LastRaidView(lastMap.Name, endedAt - state.RaidStartedAt!.Value, state.Side, endedAt)
                : null,
            RaidInfo = _map is not null && _data?.Maps.GetValueOrDefault(_map.Id) is { } raidMap
                ? new RaidInfo(raidMap.RaidDuration ?? 0, _data.BossesOn(raidMap.Id).Take(3).Select(Planning.BossText).ToList(),
                    _tracker.State.Phase == RaidPhase.InRaid ? _lastClock : null)
                : null,
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
        Study.Game("app.exit");
        Study.Dispose();
        _stop.Dispose();
    }
}
