using Shturmap.Core.Logs;
using Shturmap.Core.Operational;
using Shturmap.Core.Quests;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Screenshots;

namespace Shturmap.Session;

// The daily and weekly operational tasks (owner, 2026-10-17: "Add dailies and weeklies via screenshot"; 2026-10-19,
// from a panel of four: "Use B, the recommended one"): read from a menu screenshot of Character › TASKS › OPERATIONAL
// in the stash's path (one tick, the menus only, the picture opened once; GameSession.Stash), kept per game mode in
// operational.json in the data folder (OperationalLedger), shown as rows where the map's work is (Plan's list rows and
// open card, the raid card). tarkov.dev's data has no such tasks: the screenshot is the only source, and its progress
// is as of the screenshot. docs/DESIGN.md §2, §4 and §8.
public sealed partial class GameSession
{
    /// <summary>
    /// Reads the Tasks screen in a menu screenshot, or null when the picture can't be read; the app's own reader when
    /// not set (tests give their own; see <see cref="StashReader"/>).
    /// </summary>
    public Func<string, CancellationToken, Task<TaskPage?>>? TasksReader { get; init; }

    private OperationalLedger _operational = new();

    // Made-up tasks were taken this run (ReadExampleTasksAsync): the file isn't written over with them.
    private bool _operationalUnsaved;

    // When the run's first made-up table was read: the later ones' countdowns run from it (ReadExampleTasksAsync).
    private DateTimeOffset? _exampleTasksSince;

    // Which readings call the cue: one for a run of screenshots (OperationalReadings).
    private readonly OperationalReadings _operationalReadings = new();

    // Wakes the watch over the tasks' expiries when a new table brings an earlier one than it waits for.
    private readonly SemaphoreSlim _operationalWake = new(0, 1);

    private string OperationalPath => Path.Combine(paths.Root, OperationalLedger.FileName);

    // At open: the file from the last run, less what ran out meanwhile.
    private void OpenOperational()
    {
        _operational = OperationalLedger.Load(OperationalPath, warning => AppLog.Warn(warning));
        _operational.Expire(DateTimeOffset.Now);
        SaveOperationalIfDirty();
    }

    // Under the gate: written when something changed (a table read, a task run out).
    private void SaveOperationalIfDirty()
    {
        if (!_operational.Dirty || _operationalUnsaved)
            return;
        try
        {
            _operational.Save(OperationalPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Failed("Saving the operational tasks file", e);
        }
    }

    // Under the gate: what the Tasks screen showed. The other Tasks tables are told and left; the operational table's rows
    // replace the mode's set, each on the map its location names ("Any location": every map); a trader's tab brings its
    // one task and refreshes countdowns (TakeTrader). A location that names no map of the data is left out: shown on no
    // map rather than on the wrong one. What the set did, for the readings log; null when the screen was only told.
    private OperationalTaken? TakeTasks(TaskPage page, DateTime at, double ms)
    {
        if (page.Screen == TaskScreen.TraderScreen && (page.Selected is not null || page.Rows.Count > 0))
            return TakeTrader(page, new DateTimeOffset(at), ms);
        if (page.Screen != TaskScreen.Table)
        {
            Study.Game("operational.screen", ("screen", page.Screen), ("ms", ms));
            return null;
        }
        var rows = new List<OperationalRead>(page.Rows.Count);
        var unplaced = new List<string?>();
        foreach (var row in page.Rows)
        {
            if (OperationalMap(row.Location) is not { Known: true } place)
            {
                unplaced.Add(row.Location);
                continue;
            }
            rows.Add(new OperationalRead(row.Name, place.MapId, row.TimeLeft, row.TimeVotes, row.Progress,
                row.Objectives.Select(o => new OperationalObjective(o.Text, o.Done, o.Of)).ToList()));
        }
        return TakeOperational(rows, new DateTimeOffset(at), ms, unplaced);
    }

    // Under the gate: a table's rows, from a screenshot taken at that moment (or a developer script's); what the set did.
    private OperationalTaken TakeOperational(IReadOnlyList<OperationalRead> rows, DateTimeOffset at, double ms, IReadOnlyList<string?>? unplacedAt = null)
    {
        var unplaced = unplacedAt?.Count ?? 0;
        // The set as it stood, less what ran out by now: what the reading's tasks are new against.
        var now = DateTimeOffset.Now;
        var before = _operational.Tasks(_mode).Where(t => t.ExpiresAt is not { } end || end > now).ToList();
        if (_operational.ApplyTable(_mode, at, rows) is not { } dropped)
        {
            AppLog.Info("Operational tasks: a screenshot older than the tasks kept, left");
            return new OperationalTaken { Older = true, Unplaced = unplacedAt ?? [], Set = before };
        }
        _operational.Expire(now);
        var tasks = _operational.Tasks(_mode);
        var call = _operationalReadings.Read(at, before, tasks);
        Study.Game("operational.read", ("rows", rows.Count), ("unplaced", unplaced), ("expiries", tasks.Count(t => t.ExpiresAt is not null)),
            ("dropped", dropped), ("objectives", rows.Count(r => r.Objectives.Count > 0)), ("cue", call is null ? "none" : call.Joins ? "join" : "new"), ("ms", ms));
        AppLog.Info($"Operational tasks read from a menu screenshot: {rows.Count} rows, {unplaced} on no known map, {dropped} countdowns not taken ({ms:0} ms)");
        SaveOperationalIfDirty();
        if (_operationalWake.CurrentCount == 0)
            _operationalWake.Release();
        // Feedback (owner, 2026-10-19, after trying the dev build: "the same animation as the other quests"; docs/DESIGN.md
        // §4, principle 11): the cue lists every task a run of screenshots read, one animation for all of them, and the
        // rows that arrived glow in on the rail; a reading that brings no new task calls none (OperationalReadings).
        if (call is not null && _data is { } data)
            Announce(new ViewCue(CueKind.TasksRead, "", Tasks: call.Tasks.Select(t => ShownOf(data, t)).ToList(), Joins: call.Joins));
        Publish();
        return new OperationalTaken { Rows = rows.Count, Unplaced = unplacedAt ?? [], Dropped = dropped, Set = tasks.ToList() };
    }

    // Under the gate: a trader's TASKS tab (owner, 2026-10-25: "when a daily or weekly is screenshotted from the traders
    // quest accept view, it is not added to shturmap"; docs/DESIGN.md §2). The task its header names, when it stands in
    // the list's OPERATIONAL TASKS, goes on the map its location names, as a table's row does (no location read, or one of
    // no known map: left out and counted), added to the mode's set or updating the kept task of its name and map; the
    // list's other entries refresh the countdowns of tasks kept by their names, and never add one. A task the set didn't
    // have calls the same cue as a table's, one for a run of screenshots; a reading that only refreshed countdowns, or
    // updated a kept task, calls none. The study log counts, never names. What the set did, for the readings log.
    private OperationalTaken TakeTrader(TaskPage page, DateTimeOffset at, double ms)
    {
        OperationalRead? task = null;
        var unplaced = 0;
        if (page.Selected is { } selected)
        {
            if (OperationalMap(selected.Location) is { Known: true } place)
            {
                task = new OperationalRead(selected.Name, place.MapId, selected.TimeLeft, selected.TimeVotes, selected.Progress,
                    selected.Objectives.Select(o => new OperationalObjective(o.Text, o.Done, o.Of)).ToList());
            }
            else
            {
                unplaced = 1;
            }
        }
        IReadOnlyList<string?> unplacedAt = unplaced == 1 ? [page.Selected!.Location] : [];
        // The task's own entry carries its countdown; the others refresh.
        var others = page.Rows.Where(r => task is null || !ReferenceEquals(r, page.Selected))
            .Select(r => new OperationalEntry(r.Name, r.TimeLeft, r.TimeVotes)).ToList();
        var now = DateTimeOffset.Now;
        var before = _operational.Tasks(_mode).Where(t => t.ExpiresAt is not { } end || end > now).ToList();
        if (_operational.ApplyTrader(_mode, at, task, others) is not { } applied)
        {
            Study.Game("operational.trader", ("older", true), ("ms", ms));
            AppLog.Info("Operational tasks: a trader's screenshot older than the last Tasks table, left");
            return new OperationalTaken { Trader = true, Older = true, Unplaced = unplacedAt, Set = before };
        }
        _operational.Expire(now);
        var taken = task is null || !(applied.Added || applied.Updated) ? null
            : _operational.Tasks(_mode).LastOrDefault(t => t.ReadAt == at && t.MapId == task.MapId && string.Equals(t.Name, task.Name, StringComparison.OrdinalIgnoreCase));
        var call = taken is null ? null : _operationalReadings.Read(at, before, [taken]);
        Study.Game("operational.trader", ("task", task is not null), ("unplaced", unplaced), ("added", applied.Added), ("entries", page.Rows.Count),
            ("refreshed", applied.Refreshed), ("dropped", applied.Dropped), ("cue", call is null ? "none" : call.Joins ? "join" : "new"), ("ms", ms));
        var read = applied.Added ? "1 new" : applied.Updated ? "1 kept updated" : "none";
        AppLog.Info($"Operational task read from a trader's screen: {read}, {applied.Refreshed} countdowns refreshed, {unplaced} on no known map, " +
                    $"{applied.Dropped} countdowns not taken ({ms:0} ms)");
        SaveOperationalIfDirty();
        if (_operationalWake.CurrentCount == 0)
            _operationalWake.Release();
        if (call is not null && _data is { } data)
            Announce(new ViewCue(CueKind.TasksRead, "", Tasks: call.Tasks.Select(t => ShownOf(data, t)).ToList(), Joins: call.Joins));
        Publish();
        return new OperationalTaken
        {
            Trader = true,
            Added = applied.Added,
            Updated = applied.Updated,
            Refreshed = applied.Refreshed,
            Dropped = applied.Dropped,
            Unplaced = unplacedAt,
            Set = _operational.Tasks(_mode).ToList(),
        };
    }

    // "Any location" is every map; a map's name as the game's English screen writes it ("Streets of Tarkov", "The Lab")
    // is that map, a variant drawn with the same artwork its base map's (Ground Zero 21+ is Ground Zero). Known is false
    // for a location the data names no map for.
    private OperationalPlace OperationalMap(string? location)
    {
        if (location is null || _data is null)
            return new(false, null);
        var folded = Fold(location);
        if (folded == "anylocation")
            return new(true, null);
        var data = _data;
        var map = data.Maps.Values
            .Where(m => Fold(m.Name) == folded || Fold(m.NormalizedName) == folded)
            .OrderBy(m => Planning.PickKey(data, m.NormalizedName) == m.NormalizedName ? 0 : 1)
            .ThenBy(m => m.NormalizedName.Length)
            .FirstOrDefault();
        return new(map is not null, map?.Id);
    }

    private readonly record struct OperationalPlace(bool Known, string? MapId);

    private static string Fold(string text) => new(text.ToLowerInvariant().Where(char.IsLetter).ToArray());

    // Under the gate: the mode's tasks as the rail shows them, each with its map's name as Plan counts maps (a
    // variant is its map) and the items its hand-overs name, with the catalogue's id where exactly one item has that
    // full name (the stash's rule, never against the whole catalogue, gives way here: a quoted name has nothing to
    // narrow it, so only a whole name that matches counts; else the words alone). The names are the English game's, so
    // they are matched against the items' English names whatever the app's language (QA, 2026-10-19: a German app's
    // catalogue found none); the cell then shows the item in the app's language, as any item cell does.
    private IReadOnlyList<OperationalShown> OperationalShownNow()
    {
        var tasks = _operational.Tasks(_mode);
        if (tasks.Count == 0 || _data is not { } data)
            return [];
        return tasks.Select(t => ShownOf(data, t)).ToList();
    }

    private static OperationalShown ShownOf(GameData data, OperationalTask t) =>
        new(t, t.MapId is { } id && data.Maps.GetValueOrDefault(id) is { } map ? Planning.PickKey(data, map.NormalizedName) : null,
            t.Kind == OperationalKind.HandOver ? HandOverItems(data, t) : []);

    private static IReadOnlyList<OperationalItem> HandOverItems(GameData data, OperationalTask task)
    {
        var byName = ItemsByName(data);
        return task.Objectives.SelectMany(o => OperationalWords.HandedOver(o.Text)).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new OperationalItem(name, byName.GetValueOrDefault(name))).ToList();
    }

    // The catalogue's full English names, each to its one id; a name two items share names none.
    private static Dictionary<string, string?> ItemsByName(GameData data)
    {
        lock (ItemNamesFor)
        {
            if (ItemNamesFor.TryGetValue(data, out var known))
                return known;
            var byName = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (id, name) in data.ItemNamesInEnglish)
                byName[name] = byName.ContainsKey(name) ? null : id;
            ItemNamesFor.AddOrUpdate(data, byName);
            return byName;
        }
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameData, Dictionary<string, string?>> ItemNamesFor = new();

    // Under the gate: the maps with operational work and none of your quests, by name, for the end of NEXT RAID's list
    // (owner, 2026-10-19, B: the planner's order and score unchanged).
    private IReadOnlyList<MapPlanView> OperationalPlans(IReadOnlyList<OperationalShown> shown)
    {
        if (_data is not { } data || shown.Count == 0)
            return [];
        var active = _quests.Values.Where(q => q.State == QuestState.Active).Select(q => q.QuestId).ToList();
        return shown.Select(s => s.MapKey).OfType<string>().Distinct(StringComparer.Ordinal)
            .Where(key => !_plan.Any(p => p.NormalizedName == key))
            .Select(key => Planning.PlanFor(data, active, key, _done))
            .OfType<MapPlanView>()
            .OrderBy(p => p.MapName, StringComparer.Create(Shturmap.Core.UiLanguage.Culture, ignoreCase: true))
            .ToList();
    }

    /// <summary>
    /// For snapshots and the developer view: as if a screenshot of the Tasks screen's OPERATIONAL table taken now had shown
    /// made-up tasks (a script's "tasks", "--operational" with a fake game; owner, 2026-10-19, B, so the QA pass can
    /// picture the rows without a picture): from where the reading hands its rows over on, every step is a real
    /// screenshot's (the checks, the cue, the rows' glow), except that the operational tasks' file isn't written
    /// from then on in this run: made-up tasks never reach a player's file. In order, up to <paramref name="count"/>
    /// of: a survival on Streets of Tarkov ending in 25 minutes, a kill with its condition on Customs, a weekly's two
    /// hand-overs on any map, a kill on Shoreline whose countdown went unread, a line of no known kind on Woods, and,
    /// repeating kinds on other maps so the cue's last place says "+N" (QA pass of 2026-10-19, F3), a survival on
    /// Interchange, a kill on Lighthouse and a weekly's kill on Reserve, and a kill on Ground Zero at the game's 0 % with
    /// two counted objectives, so no single count: the rail's "0%" (UI QA re-check of 94591a5, F3). With <paramref name="ended"/>, one more on Woods
    /// that runs out three seconds later ("1 ended at …"). Says why not when nothing is read: no data, or reading menu
    /// screenshots turned off.
    /// </summary>
    public async Task<string?> ReadExampleTasksAsync(int count, bool ended)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_readStash)
                return "Reading menu screenshots is off.";
            if (_data is null)
                return "No data yet.";
            var data = _data;
            // The maps named, or where the data has no such map, the next of its maps by name: a made-up task is never
            // taken for an any-map one.
            var spare = new Queue<string>(data.Maps.Values.OrderBy(m => m.NormalizedName, StringComparer.Ordinal).Select(m => m.Id));
            string? Map(string name) => data.MapByNormalizedName(name)?.Id ?? (spare.Count > 0 ? spare.Dequeue() : null);
            // The countdowns run from the run's first made-up table on, as the game's do: the same table again (a script's
            // second "tasks") is the same tasks with the same ends, as a real screenshot taken later shows them.
            var now = DateTimeOffset.Now;
            _exampleTasksSince ??= now;
            var gone = now - _exampleTasksSince.Value;
            TimeSpan Left(TimeSpan left) => left - gone > TimeSpan.Zero ? left - gone : TimeSpan.FromSeconds(1);
            List<OperationalRead> rows =
            [
                new("Exit the location", Map("streets-of-tarkov"), Left(TimeSpan.FromMinutes(25)), 5, 57,
                    [new OperationalObjective("Survive and extract from the location", 4, 7)]),
                new("Elimination", Map("customs"), Left(new TimeSpan(8, 14, 11)), 4, 0,
                    [new OperationalObjective("Eliminate Scavs with a headshot from a distance of more than 50m", 0, 3)]),
                new("Find and transfer", null, Left(new TimeSpan(2, 3, 0, 0)), 3, null,
                    [new OperationalObjective("Hand over the found in raid item: \"SSD drive\""), new OperationalObjective("Hand over the found in raid item: \"CPU fan\"")]),
                new("Elimination", Map("shoreline"), null, 0, 20, [new OperationalObjective("Eliminate PMC operatives", 1, 5)]),
                new("Assignment", Map("woods"), Left(new TimeSpan(8, 14, 11)), 3, 0, [new OperationalObjective("Mark the made-up spots", 0, 2)]),
                new("Exit the location", Map("interchange"), Left(new TimeSpan(14, 30, 0)), 4, 0,
                    [new OperationalObjective("Survive and extract from the location", 0, 2)]),
                new("Elimination", Map("lighthouse"), Left(new TimeSpan(20, 5, 0)), 4, 0, [new OperationalObjective("Eliminate Scavs", 0, 8)]),
                new("Elimination", Map("reserve"), Left(new TimeSpan(3, 20, 0, 0)), 3, 0, [new OperationalObjective("Eliminate PMC operatives", 0, 10)]),
                new("Elimination", Map("ground-zero"), Left(new TimeSpan(1, 6, 0, 0)), 3, 0,
                    [new OperationalObjective("Eliminate Scavs", 0, 5), new OperationalObjective("Eliminate PMC operatives", 0, 2)]),
            ];
            rows = rows.Take(Math.Clamp(count, 0, rows.Count)).ToList();
            if (ended)
                rows.Add(new("Exit the location", Map("woods"), TimeSpan.FromSeconds(3), 5, null, []));
            AppLog.Info($"Example operational tasks: a table of {rows.Count} rows, as a screenshot taken now would show it; the file isn't written this run");
            _operationalUnsaved = true;
            TakeOperational(rows, now, 0);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    // While tasks are kept: each runs out at its time (said under the eyebrow, "1 ended at 22:35"), and the rows' times
    // are words of the minute ("25 MIN LEFT"), so the rail is published again each minute.
    private async Task WatchOperationalAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            var wait = TimeSpan.FromMinutes(1);
            await _gate.WaitAsync();
            try
            {
                var now = DateTimeOffset.Now;
                var gone = _operational.Expire(now);
                if (gone > 0)
                {
                    Study.Game("operational.ended", ("tasks", gone));
                    AppLog.Info($"Operational tasks: {gone} ran out");
                }
                SaveOperationalIfDirty();
                if (gone > 0 || _operational.Tasks(_mode).Count > 0 || _operational.Ended(_mode) is not null)
                    Publish();
                if (_operational.NextExpiry() is { } next && next - now < wait)
                    wait = next - now < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : next - now + TimeSpan.FromMilliseconds(50);
            }
            finally
            {
                _gate.Release();
            }
            try
            {
                await _operationalWake.WaitAsync(wait, _stop.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
