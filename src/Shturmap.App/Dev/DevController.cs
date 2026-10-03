#if DEVTOOLS
using System.Globalization;
using System.Net;
using Shturmap.Core;
using Shturmap.Core.Logs;
using Shturmap.Core.Raid;
using Shturmap.Session;
using Shturmap.Session.Dev;
using Shturmap.Session.Reporting;

namespace Shturmap.App.Dev;

/// <summary>
/// What the developer view's buttons and a "--dev-script" do (docs/DESIGN.md §8, "Developer aids"): game log lines and
/// screenshot files written into the session's fake game folder, so the app reacts as it would to the game, plus the
/// few triggers no file can reach (old position, data failure, dialogs). Runs on the UI thread.
/// </summary>
internal sealed class DevController(MainWindow window, GameSession session, FakeGame? game)
{
    private readonly List<string> _history = [];

    public FakeGame? Game => game;

    public MainWindow Window => window;

    public GameSession Session => session;

    /// <summary>Something was done: a line for the view's log.</summary>
    public event Action<string>? Done;

    public IReadOnlyList<string> History => _history;

    public DevMap? Map { get; set; }

    public bool Scav { get; set; }

    public bool Local { get; set; }

    /// <summary>Added to the height worked out for a picked place (the shown floor's band, or nearby places).</summary>
    public double HeightOffset { get; set; }

    public double Yaw { get; private set; }

    /// <summary>Places picked while recording a path, for "walk".</summary>
    public List<WorldPoint> Path { get; } = [];

    public bool RecordingPath { get; set; }

    /// <summary>A click on the map picks a place (a screenshot there, or a point of the path).</summary>
    public bool Picking
    {
        get => window.DevMapView.DevPick is not null;
        set => window.DevMapView.DevPick = value ? OnPicked : null;
    }

    private SessionSnapshot? Snapshot => window.DevSnapshot;

    /// <summary>Every map tarkov.dev's data has, for the raid commands.</summary>
    public IReadOnlyList<DevMap> Maps() =>
        Snapshot?.Data?.Maps.Values.OrderBy(m => m.Name, StringComparer.CurrentCulture)
            .Select(m => DevMap.From(m.NormalizedName, m.Name, m.ScenePath, m.NameId)).ToList() ?? [];

    public DevMap? MapNamed(string normalizedName) =>
        Maps().FirstOrDefault(m => string.Equals(m.NormalizedName, normalizedName, StringComparison.OrdinalIgnoreCase));

    private void Say(string line)
    {
        _history.Add($"{DateTime.Now:HH:mm:ss}  {line}");
        AppLog.Debug("Developer view: " + line);
        Done?.Invoke(line);
    }

    private bool NeedGame()
    {
        if (game is not null)
            return true;
        Say("This session reads the real game: restart in the developer view (F12, then Restart).");
        return false;
    }

    private bool NeedMap()
    {
        if (Map is not null)
            return true;
        Say("Pick a map first.");
        return false;
    }

    // ---- mode and raid ----

    public void SetMode(GameMode mode)
    {
        if (!NeedGame())
            return;
        game!.Mode(mode);
        Say($"mode {mode}");
    }

    public async Task SelectMapAsync(DevMap map)
    {
        Map = map;
        if (Snapshot?.Raid.Phase is null or RaidPhase.Menu)
            await session.SelectMapAsync(map.NormalizedName);
        Say("map " + map.Name);
    }

    public void GroupPick()
    {
        if (!NeedGame() || !NeedMap())
            return;
        game!.GroupPick(Map!);
        Say("group pick " + Map!.Name);
    }

    public void Load()
    {
        if (!NeedGame() || !NeedMap())
            return;
        game!.LoadingStarts(Map!, Scav, Local);
        Say($"loading {Map!.Name} as {(Scav ? "Scav" : "PMC")}, {(Local ? "local" : "server")}");
    }

    public void Steps()
    {
        if (!NeedGame())
            return;
        game!.LoadingSteps();
        Say("loading steps");
    }

    public void Start()
    {
        if (!NeedGame())
            return;
        game!.RaidStarts(Local);
        Say("raid start");
    }

    /// <summary>Loading, its steps and the start in one go.</summary>
    public async Task RaidAsync()
    {
        if (!NeedGame() || !NeedMap())
            return;
        Load();
        await Task.Delay(400);
        Steps();
        await Task.Delay(400);
        Start();
    }

    public void End()
    {
        if (!NeedGame())
            return;
        game!.RaidEnds();
        Say("raid end");
    }

    public void Cancel()
    {
        if (!NeedGame())
            return;
        game!.MatchingCancelled(Local);
        Say("matching cancelled");
    }

    /// <summary>A transit during a raid: the next map's scene loads and its raid starts.</summary>
    public async Task TransitAsync(DevMap to)
    {
        if (!NeedGame())
            return;
        Map = to;
        game!.LoadingStarts(to, Scav, Local);
        await Task.Delay(400);
        game.RaidStarts(Local);
        Say("transit to " + to.Name);
    }

    // ---- quests ----

    public IReadOnlyList<(string Id, string Name, string Trader)> QuestsMatching(string? filter, int max = 200)
    {
        if (Snapshot?.Data is not { } data)
            return [];
        var words = (filter ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return data.Tasks.Values
            .Where(t => words.All(w => t.Name.Contains(w, StringComparison.OrdinalIgnoreCase) || t.Id.StartsWith(w, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(t => t.Name, StringComparer.CurrentCulture)
            .Take(max)
            .Select(t => (t.Id, t.Name, data.TraderName(t.Trader)))
            .ToList();
    }

    public void Quest(string questId, QuestLogStatus status)
    {
        if (!NeedGame())
            return;
        var task = Snapshot?.Data?.Tasks.GetValueOrDefault(questId);
        game!.Quest(questId, status, task?.Trader);
        Say($"quest {status.ToString().ToLowerInvariant()}: {task?.Name ?? questId}");
    }

    /// <summary>A quest by its id or part of its name (a script's "quest start …").</summary>
    public string? QuestNamed(string idOrName) =>
        Snapshot?.Data?.Tasks.ContainsKey(idOrName) == true ? idOrName
            : QuestsMatching(idOrName, 1).Select(q => q.Id).FirstOrDefault();

    /// <summary>Starts up to <paramref name="count"/> quests with objectives on the chosen map.</summary>
    public void StartSomeHere(int count = 8)
    {
        if (!NeedGame() || !NeedMap() || Snapshot?.Data is not { } data)
            return;
        var mapId = data.Maps.Values.FirstOrDefault(m => m.NormalizedName == Map!.NormalizedName)?.Id;
        var here = data.Tasks.Values
            .Where(t => t.Map == mapId || (t.Objectives ?? []).Any(o => (o.Maps ?? []).Contains(mapId ?? "") || (o.Zones ?? []).Any(z => z.Map == mapId)))
            .OrderBy(t => t.MinPlayerLevel ?? 0)
            .Take(count)
            .ToList();
        foreach (var task in here)
            game!.Quest(task.Id, QuestLogStatus.Started, task.Trader);
        Say($"started {here.Count} quests on {Map!.Name}");
    }

    // ---- positions ----

    private void OnPicked((double X, double Z) at, (double X, double Z)? to)
    {
        if (to is { } end && DevPlaces.YawOf(at.X, at.Z, end.X, end.Z) is { } yaw)
            Yaw = yaw;
        var place = PlaceAt(at.X, at.Z);
        if (RecordingPath)
        {
            Path.Add(place);
            Say($"path point {Path.Count} at {place}");
            return;
        }
        Shoot(place, Yaw);
    }

    /// <summary>A world position for a spot on the shown map: its height from the shown floor (or the places nearby).</summary>
    public WorldPoint PlaceAt(double x, double z)
    {
        var scene = window.DevMapView.Scene;
        var height = scene is null ? 0
            : DevPlaces.HeightFor(scene.Definition, scene.Floor, x, z, scene.Markers.Select(m => m.Position));
        return new WorldPoint(Math.Round(x, 2), Math.Round(height + HeightOffset, 2), Math.Round(z, 2));
    }

    /// <summary>A screenshot at a spot given as fractions of the map view, as a click there would make it (a script's
    /// "place"); with a second spot, a drag for the facing.</summary>
    public void PlaceOnView(double fx, double fy, double? toFx = null, double? toFy = null)
    {
        var view = window.DevMapView;
        if (view.DevWorldAt(view.DevPointAt(fx, fy)) is not { } at)
        {
            Say("No map shown to place on.");
            return;
        }
        var to = toFx is { } tx && toFy is { } ty ? view.DevWorldAt(view.DevPointAt(tx, ty)) : null;
        OnPicked(at, to);
    }

    public void Shoot(WorldPoint p, double yaw)
    {
        if (!NeedGame())
            return;
        Yaw = yaw;
        var file = game!.Screenshot(p, yaw);
        Say($"screenshot at {p}, facing {yaw:0}° ({System.IO.Path.GetFileName(file)})");
    }

    /// <summary>The last position again (F9), as a second screenshot from the same spot.</summary>
    public void Repeat()
    {
        if (session.DevLastFix is not { } fix)
        {
            Say("No position yet to repeat.");
            return;
        }
        Shoot(fix.Position, fix.YawDegrees ?? Yaw);
    }

    public async Task AgeAsync(double minutes)
    {
        await session.DevAgeFixAsync(TimeSpan.FromMinutes(minutes));
        Say($"position aged by {minutes:0} min");
    }

    /// <summary>A screenshot at each recorded place, <paramref name="seconds"/> apart, facing the next one.</summary>
    public async Task WalkAsync(double seconds)
    {
        if (Path.Count == 0)
        {
            Say("Record a path first: switch Record path on and click places on the map.");
            return;
        }
        var points = Path.ToList();
        for (var i = 0; i < points.Count; i++)
        {
            var next = i + 1 < points.Count ? points[i + 1] : (WorldPoint?)null;
            var yaw = next is { } n ? DevPlaces.YawOf(points[i].X, points[i].Z, n.X, n.Z) ?? Yaw : Yaw;
            Shoot(points[i], yaw);
            if (i + 1 < points.Count)
                await Task.Delay(TimeSpan.FromSeconds(seconds));
        }
        Say($"walked {points.Count} places");
    }

    // ---- triggers ----

    public async Task TriggerAsync(string what)
    {
        switch (what.ToLowerInvariant())
        {
            case "report":
                window.DevShowReport();
                break;
            case "crash":
                window.DevShowCrash();
                break;
            case "update":
                window.DevShowUpdateReady("0.2.1");
                if (Snapshot?.Raid.Phase is RaidPhase.InRaid or RaidPhase.Loading)
                    Say("(the update line stays hidden in a raid, as designed: end the raid to see it)");
                break;
            case "offline":
                await session.DevFailDataAsync(new HttpRequestException("Developer view: no connection"));
                break;
            case "404":
                await session.DevFailDataAsync(new HttpRequestException("Developer view: 404", null, HttpStatusCode.NotFound));
                break;
            case "503":
                await session.DevFailDataAsync(new HttpRequestException("Developer view: 503", null, HttpStatusCode.ServiceUnavailable));
                break;
            case "nogame":
                await session.DevForgetGameAsync();
                break;
            case "reload":
                session.DevReloadData();
                break;
            default:
                Say("Unknown trigger: " + what);
                return;
        }
        Say("trigger " + what);
    }

    // ---- a script ----

    public async Task RunScriptAsync(string path)
    {
        string text;
        try
        {
            text = await File.ReadAllTextAsync(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Say($"Script {path} couldn't be read: {e.Message}");
            return;
        }
        var (steps, errors) = DevScript.Parse(text);
        foreach (var error in errors)
            Say("Script: " + error);
        // The data arrives a little after the start; map and quest steps need it.
        for (var i = 0; i < 60 && Snapshot?.Data is null; i++)
            await Task.Delay(500);
        foreach (var step in steps)
        {
            Say("script: " + step);
            try
            {
                if (!await RunAsync(step))
                    return;
            }
            catch (Exception e)
            {
                AppLog.Error($"Developer script line {step.Line} failed", e);
                Say($"line {step.Line} failed: {e.Message}");
            }
            // The app reads the logs about twice a second.
            await Task.Delay(300);
        }
    }

    // One step; false ends the script.
    private async Task<bool> RunAsync(DevStep step)
    {
        switch (step.Verb)
        {
            case "mode":
                SetMode(step.Arg(0) switch
                {
                    "pvp" or "regular" => GameMode.Pvp,
                    "seasonal" or "season" => GameMode.Seasonal,
                    _ => GameMode.Pve,
                });
                break;
            case "map":
                if (MapNamed(step.Arg(0)) is { } map)
                    await SelectMapAsync(map);
                else
                    Say("No map " + step.Arg(0));
                break;
            case "side":
                Scav = step.Arg(0).Equals("scav", StringComparison.OrdinalIgnoreCase);
                break;
            case "hosting":
                Local = step.Arg(0).Equals("local", StringComparison.OrdinalIgnoreCase);
                break;
            case "group":
                GroupPick();
                break;
            case "load":
                Load();
                break;
            case "steps":
                Steps();
                break;
            case "start":
                Start();
                break;
            case "end":
                End();
                break;
            case "cancel":
                Cancel();
                break;
            case "transit":
                if (MapNamed(step.Arg(0)) is { } to)
                    await TransitAsync(to);
                break;
            case "quest":
                var status = step.Arg(0).ToLowerInvariant() switch
                {
                    "complete" or "completed" => QuestLogStatus.Completed,
                    "fail" or "failed" => QuestLogStatus.Failed,
                    _ => QuestLogStatus.Started,
                };
                var name = string.Join(' ', step.Args.Skip(1));
                if (step.Arg(0) == "here")
                    StartSomeHere((int)step.Number(1, 8));
                else if (QuestNamed(name) is { } id)
                    Quest(id, status);
                else
                    Say("No quest " + name);
                break;
            case "pick":
                // Picks (or unpicks) a quest for the coming raid, as its pen does.
                var pickName = string.Join(' ', step.Args);
                if (QuestNamed(pickName) is { } pick)
                    await session.TogglePickAsync(pick, "dev");
                else
                    Say("No quest " + pickName);
                break;
            case "place":
                PlaceOnView(step.Number(0, 0.5), step.Number(1, 0.5),
                    step.Args.Count >= 4 ? step.Number(2) : null, step.Args.Count >= 4 ? step.Number(3) : null);
                break;
            case "pos":
                Shoot(new WorldPoint(step.Number(0), step.Number(1), step.Number(2)), step.Number(3, Yaw));
                break;
            case "repeat":
                Repeat();
                break;
            case "age":
                await AgeAsync(step.Number(0, 5));
                break;
            case "walk":
                await WalkAsync(step.Number(0, 3));
                break;
            case "trigger":
                await TriggerAsync(step.Arg(0));
                break;
            case "choose":
                // "Choose game folder…" with that folder (no picker); "choose game" is this view's own fake game.
                var folder = string.Join(' ', step.Args);
                if (folder is "" or "game")
                    folder = Game?.Root ?? "";
                Say(await session.ChooseGameFolderAsync(folder) ? "game folder chosen: " + folder : "not the game: " + folder);
                break;
            case "wait":
                await Task.Delay(TimeSpan.FromSeconds(step.Number(0, 1)));
                break;
            case "snapshot":
                await window.DevSnapshotAsync(step.Arg(0, System.IO.Path.Combine(System.IO.Path.GetTempPath(), "shturmap-devview-snapshot")));
                break;
            case "exit":
                window.DevExit();
                return false;
        }
        return true;
    }

    /// <summary>For the view: "x, y, z" of the last position, or a dash.</summary>
    public string LastPositionText() =>
        session.DevLastFix is { } fix
            ? string.Format(CultureInfo.InvariantCulture, "{0:0.0}, {1:0.0}, {2:0.0} · {3:0}° · {4:HH:mm:ss}", fix.Position.X, fix.Position.Y, fix.Position.Z, fix.YawDegrees ?? 0, fix.At)
            : "—";
}
#endif
