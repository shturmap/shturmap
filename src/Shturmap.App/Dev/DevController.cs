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

    /// <summary>The map shown as a pick in the MAP list shows it, also in a raid, where it is a look (the label says the
    /// raid's map); the raid commands keep their map (QA pass of 2026-10-15: the look label had no script step).</summary>
    public async Task LookAtAsync(DevMap map)
    {
        await session.SelectMapAsync(map.NormalizedName);
        Say("look at " + map.Name);
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
            // By its name in the data's language, its id, or tarkov.dev's normalized name, the same in every language
            // ("insomnia"), so one script finds the quest in German too.
            .Where(t => words.All(w => t.Name.Contains(w, StringComparison.OrdinalIgnoreCase) || t.Id.StartsWith(w, StringComparison.OrdinalIgnoreCase)
                || t.NormalizedName?.Contains(w, StringComparison.OrdinalIgnoreCase) == true))
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
                Say(window.DevShowUpdateReady() is { } version ? $"update {version} downloaded, as far as this run knows" : "(no updater yet: try again once the session has started)");
                if (Snapshot?.Raid.Phase is RaidPhase.InRaid or RaidPhase.Loading)
                    Say("(the update band and its cue wait for the menus, as designed: end the raid to see them)");
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

    /// <summary>Plays a script's steps in order; true when its "exit" ended the app.</summary>
    public async Task<bool> RunScriptAsync(string path)
    {
        string text;
        try
        {
            text = await File.ReadAllTextAsync(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Say($"Script {path} couldn't be read: {e.Message}");
            return false;
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
                    return true;
            }
            catch (Exception e)
            {
                AppLog.Error($"Developer script line {step.Line} failed", e);
                Say($"line {step.Line} failed: {e.Message}");
            }
            // The app reads the logs about twice a second. The pointer's steps write nothing for it to read, and
            // what they check is a matter of tenths of a second: they follow each other at once (DevScript.FollowsAtOnce).
            if (!DevScript.FollowsAtOnce(step))
                await Task.Delay(300);
        }
        return false;
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
                if (MapNamed(step.Arg(0)) is not { } map)
                    Say("No map " + step.Arg(0));
                else if (step.Arg(1).Equals("look", StringComparison.OrdinalIgnoreCase))
                    await LookAtAsync(map);
                else
                    await SelectMapAsync(map);
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
            case "trader":
                // A trader's focus held, as a click on its portrait in the strip does; alone, let go (owner, 2026-10-17).
                if (window.DevHoldTrader(step.Args.Count == 0 ? null : string.Join(' ', step.Args)) is { } noTrader)
                    Say($"trader {string.Join(' ', step.Args)}: {noTrader}");
                break;
            case "tick":
                // Ticks (or unticks) an objective as done, as the box on its quest card does: the quest, then which of
                // its objectives, counted from 1 in the card's order. For this session only: a script leaves no ticks.
                var tickName = string.Join(' ', step.Args.Take(Math.Max(1, step.Args.Count - 1)));
                var number = (int)step.Number(step.Args.Count - 1, 1);
                if (QuestNamed(tickName) is { } tickQuest && Snapshot?.Data?.Tasks.GetValueOrDefault(tickQuest)?.Objectives is { } all
                    && number >= 1 && number <= all.Count)
                {
                    await session.ToggleTickAsync(all[number - 1].Id, "dev", save: false);
                    Say($"ticked objective {number} of {tickName}");
                }
                else
                {
                    Say($"No objective {number} of a quest {tickName}");
                }
                break;
            case "show":
                // Its card held: where --show-quest puts it, or "at row" at the quest's own row in the rail, as a click on
                // the row holds it (owner, 2026-10-15, D10: a card from a row near the window's foot).
                if (DevScript.ShowOf(step) is not { } show)
                    break;
                if (!show.AtRow)
                    window.DevShowQuest(show.Quest);
                else if (QuestNamed(show.Quest) is not { } rowQuest)
                    Say("No quest " + show.Quest);
                else if (window.DevHoldAtRow(rowQuest) is { } noRow)
                    Say($"show {show.Quest} at row: {noRow}");
                break;
            case "focus":
                // The keyboard focus on a control, as Tab moves it there: a snapshot shows its focus ring (QA pass,
                // 2026-10-15).
                if (DevScript.FocusOf(step) is { } part && await window.DevFocusAsync(part) is { } noFocus)
                    Say($"focus {step.Arg(0)}: {noFocus}");
                break;
            case "hover":
                // The pointer, as far as the linked highlight goes: onto a quest's block in the lists, and from there
                // into its n-th objective's line, its first need cell ("cell") or a gold line that is a key ("key");
                // alone, out of the innermost thing it is in. The code the pointer's own events call.
                if (step.Args.Count == 0)
                {
                    window.DevHover(null);
                    break;
                }
                var hoverPart = step.Args.Count > 1 && step.Args[^1] is "cell" or "key" ? step.Args[^1] : null;
                var hoverNumbered = hoverPart is null && step.Args.Count > 1 && int.TryParse(step.Args[^1], out _);
                var hoverName = string.Join(' ', hoverPart is not null || hoverNumbered ? step.Args.Take(step.Args.Count - 1) : step.Args);
                if (QuestNamed(hoverName) is not { } hoverQuest)
                {
                    Say("No quest " + hoverName);
                    break;
                }
                var hoverAll = Snapshot?.Data?.Tasks.GetValueOrDefault(hoverQuest)?.Objectives ?? [];
                var hoverNumber = hoverNumbered ? (int)step.Number(step.Args.Count - 1) : 0;
                if (hoverNumbered && (hoverNumber < 1 || hoverNumber > hoverAll.Count))
                {
                    Say($"No objective {hoverNumber} of a quest {hoverName}");
                    break;
                }
                if (window.DevHover(hoverQuest, hoverNumbered ? hoverAll[hoverNumber - 1].Id : null, hoverPart) is { } missing)
                    Say($"hover {hoverName}: {missing}");
                break;
            case "point":
                // Points at a quest, at one of its objectives (counted from 1 in the card's order, as "tick" counts
                // them) or at an item, as the pointer on its line would; without arguments, at nothing again.
                if (step.Args.Count == 0)
                {
                    window.DevPoint(null);
                    break;
                }
                if (step.Args.Count > 1 && step.Arg(0).Equals("item", StringComparison.OrdinalIgnoreCase))
                {
                    var itemName = string.Join(' ', step.Args.Skip(1));
                    var names = Snapshot?.Data?.ItemNames;
                    var item = names?.ContainsKey(itemName) == true ? itemName
                        : names?.Where(n => n.Value.Contains(itemName, StringComparison.OrdinalIgnoreCase)).OrderBy(n => n.Value.Length).Select(n => n.Key).FirstOrDefault();
                    if (item is null)
                        Say("No item " + itemName);
                    else
                        window.DevPoint(new Controls.Focus(new HashSet<string>(), Item: item));
                    break;
                }
                var numbered = step.Args.Count > 1 && int.TryParse(step.Args[^1], out _);
                var pointName = string.Join(' ', numbered ? step.Args.Take(step.Args.Count - 1) : step.Args);
                if (QuestNamed(pointName) is not { } pointQuest)
                {
                    Say("No quest " + pointName);
                    break;
                }
                var pointAll = Snapshot?.Data?.Tasks.GetValueOrDefault(pointQuest)?.Objectives ?? [];
                var pointNumber = numbered ? (int)step.Number(step.Args.Count - 1) : 0;
                if (numbered && (pointNumber < 1 || pointNumber > pointAll.Count))
                {
                    Say($"No objective {pointNumber} of a quest {pointName}");
                    break;
                }
                window.DevPoint(new Controls.Focus(new HashSet<string> { pointQuest }, Objective: numbered ? pointAll[pointNumber - 1].Id : null));
                break;
            case "pointer":
                // The mouse onto the map at fractions of its size, through the code its own moves call (with the grid, the
                // square under it lit and its reference in the readout); "leave", off the map again.
                Say(window.DevPointerOnMap(DevScript.PointerOf(step)));
                break;
            case "place":
                PlaceOnView(step.Number(0, 0.5), step.Number(1, 0.5),
                    step.Args.Count >= 4 ? step.Number(2) : null, step.Args.Count >= 4 ? step.Number(3) : null);
                break;
            case "pos":
                Shoot(new WorldPoint(step.Number(0), step.Number(1), step.Number(2)), step.Number(3, Yaw));
                break;
            case "exits":
                // The game's extract list as a screenshot taken now shows it: the session takes it from where text
                // recognition hands over its reading, so the window calls it out as a real one (the roll call).
                var rows = DevScript.ExitsOf(step);
                if (await session.DevReadExitsAsync(rows) is { } noList)
                    Say(noList);
                else
                    Say(rows is null ? "extract list: every extract of the map" : "extract list: " + string.Join(", ", rows.Select(r => r.Marked ? r.Name + " ???" : r.Name)));
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
                // "Choose game folder…" with that folder (no picker); "choose game" is this view's own fake game;
                // "choose auto" is settings' FIND AUTOMATICALLY.
                var folder = string.Join(' ', step.Args);
                if (folder == "auto")
                {
                    await session.FindGameAutomaticallyAsync();
                    Say("game folder: found automatically");
                    break;
                }
                if (folder is "" or "game")
                    folder = Game?.Root ?? "";
                Say(await session.ChooseGameFolderAsync(folder) ? "game folder chosen: " + folder : "not the game: " + folder);
                break;
            case "whatsnew":
                // The card with the pointer on a line or off it; a tap on a line; the pointer off the line it is on
                // (DevScript.WhatsNewOf; a click without a line doesn't get past Parse).
                var whatsNew = DevScript.WhatsNewOf(step) ?? (DevScript.WhatsNewAct.Point, 0);
                var version = DevScript.WhatsNewVersionOf(step);
                var noWhatsNew = whatsNew.Act switch
                {
                    DevScript.WhatsNewAct.Click => window.DevWhatsNewClick(whatsNew.Line, version),
                    DevScript.WhatsNewAct.Leave => window.DevWhatsNewLeave(),
                    _ => window.DevWhatsNew(whatsNew.Line, version),
                };
                if (noWhatsNew is not null)
                    Say(noWhatsNew);
                break;
            case "replay":
                if (await window.DevReplayAsync() is { } noReplay)
                    Say(noReplay);
                break;
            case "stash":
                // SEEN IN YOUR STASH with the example items, as a reading shows it.
                if (window.DevStashCue() is { } noStash)
                    Say(noStash);
                break;
            case "desk":
                // The made-up stash desk: a reading, a cell pointed at, pinned, clicked or picked, the copy's rule, or PLAN
                // (step B, 2026-10-19; step C, 2026-10-19; the fold's "open" gone with the fold, 2026-10-25; CLEAR and its
                // UNDO, 2026-10-26, gone 2026-10-27).
                if (DevScript.DeskOf(step) is { } desk)
                    Say(desk.Probe ? await window.DevDeskProbedAsync(desk) : window.DevDesk(desk));
                break;
            case "glance":
                // The made-up in-raid glance: a raid screenshot of an inventory screen, its reading as the session publishes it
                // (owner, 2026-10-19; Rules.GlanceExample).
                Say(window.DevGlance(DevScript.GlanceOf(step) ?? 1));
                break;
            case "tasks":
                // The OPERATIONAL table as a screenshot taken now shows it: made-up tasks, from where the reading hands
                // its rows over on (owner, 2026-10-19, B).
                var (taskCount, taskEnded) = DevScript.TasksOf(step) ?? (3, false);
                if (await session.ReadExampleTasksAsync(taskCount, taskEnded) is { } noTasks)
                    Say(noTasks);
                else
                    Say($"operational table: {taskCount} tasks" + (taskEnded ? " and one about to end" : ""));
                break;
            case "textsize":
                // Settings' Text size, chosen as its row chooses it (QA pass of 2026-10-19, F4): a snapshot before and after
                // pictures the change while Shturmap runs.
                if (DevScript.TextSizeOf(step) is { } size)
                    Say(window.DevTextSize(size));
                break;
            case "language":
                // Settings' Language for this run, chosen as its row chooses it (release QA pass of 2026-10-19, F1): a
                // snapshot before and after pictures the switch with a desk's cards up, which a fresh start can't.
                if (DevScript.LanguageOf(step) is { } culture)
                    Say(await window.DevLanguageAsync(culture));
                break;
            case "tour":
                // "tour end": as Esc does. A --dev-view session's settings are new, so the tour opened by itself.
                Say(window.DevEndTour() ? "tour ended" : "no tour to end");
                break;
            case "trail":
                // The pointer's last places in the main window, as pairs of x and y, for the cards to tell where it
                // is heading (CardAim). No pointer moves: the card stack is told what its window would tell it.
                var places = new List<Windows.Foundation.Point>();
                for (var i = 0; i + 1 < step.Args.Count; i += 2)
                    places.Add(new Windows.Foundation.Point(step.Number(i), step.Number(i + 1)));
                window.DevTrail(places);
                break;
            case "cards":
                // Which cards are open in the main window, written to the app log with its time: a snapshot takes
                // too long to catch what holds for a tenth of a second.
                var open = window.DevCards();
                Say("cards: " + open);
                AppLog.Info("Developer view: cards: " + open);
                break;
            case "wait":
                await Task.Delay(TimeSpan.FromSeconds(step.Number(0, 1)));
                break;
            case "snapshot":
                await window.DevSnapshotAsync(DevScript.Folder(step.Arg(0, System.IO.Path.Combine(System.IO.Path.GetTempPath(), "shturmap-devview-snapshot"))));
                break;
            case "exit":
                await window.DevExitAsync();
                return false;
            case "note":
                // A note on a quest, as the field on its card adds one ("note Dandies \"Behind the bar\"", then "show
                // Dandies" for its card). For this session only, as "tick": a script leaves no notes.
                if (DevScript.Note(step) is not { } note)
                    Say("note <quest id or name> <text>");
                else if (QuestNamed(note.Quest) is { } noteQuest)
                    await session.AddQuestNoteAsync(noteQuest, note.Text, save: false);
                else
                    Say("No quest " + note.Quest);
                break;
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
