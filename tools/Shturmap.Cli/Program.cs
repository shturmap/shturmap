using System.Diagnostics;
using Shturmap.Core.Logs;
using Shturmap.Core.Raid;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Install;
using Shturmap.Game.Logs;
using Shturmap.Game.Settings;

var command = args.FirstOrDefault() ?? "help";
switch (command)
{
    case "locate":
        Locate();
        break;
    case "replay":
        Replay(args.ElementAtOrDefault(1));
        break;
    case "data":
        await Data(args.ElementAtOrDefault(1) ?? "pve", args.ElementAtOrDefault(2) ?? "en");
        break;
    case "render":
        await Render(args.ElementAtOrDefault(1) ?? "streets-of-tarkov", args.ElementAtOrDefault(2) ?? "map.png", args.Skip(3).ToList());
        break;
    case "watch":
        await Watch(int.TryParse(args.ElementAtOrDefault(1), out var seconds) ? seconds : 20);
        break;
    case "simulate":
        await Simulate();
        break;
    case "quests":
        await Quests(args.ElementAtOrDefault(1) ?? "pve");
        break;
    case "spawns":
        await Spawns(args.ElementAtOrDefault(1) ?? "pve");
        break;
    case "synopses":
        await Synopses(args.ElementAtOrDefault(1) ?? "pve");
        break;
    case "effort":
        await Effort(args.ElementAtOrDefault(1) ?? "pve");
        break;
    case "bring":
        await Bring(args.ElementAtOrDefault(1) ?? "pve");
        break;
    case "study":
        Study(args.ElementAtOrDefault(1));
        break;
    default:
        Console.WriteLine("""
            shturmap-cli locate              find the game, logs, screenshots and settings on this PC
            shturmap-cli replay [session]    replay a log session (default: newest) through the raid tracker
            shturmap-cli data [mode] [lang]  load tarkov.dev data (mode: pve | regular | seasonal)
            shturmap-cli render <map> <out.png> [screenshot names...]
                                            draw a map with the positions from screenshot names
            shturmap-cli watch [seconds]     run the companion headless and print what it sees
            shturmap-cli simulate            play a scripted Streets raid against a temporary fake game folder
            shturmap-cli quests [mode]       list active quests with every stored observation behind them
            shturmap-cli spawns [mode]       per map, the spawn zone markers and how far each stands from a real spawn point
            shturmap-cli synopses [mode]     every Plan row's synopsis, with fallbacks, lines over two and rule breaks flagged
            shturmap-cli effort [mode]       every Plan row's effort group and complexity, with unknown targets and types flagged
            shturmap-cli bring [mode]        every map's BRING rows (keys, items, weapons, mods, gear, exit items), with gaps flagged
            shturmap-cli study [on|off]      show or set the "Keep a study log" switch, as help sets it (Shturmap closed)
            """);
        break;
}

// The audit of what BRING lists, after a tarkov.dev or game update: every map's rows with all quests active, in the
// app's own words (classes named from the item categories), and what to look at: UNKNOWN ITEM (an id without a name),
// LIST (more than three weapons that aren't one class: shown as "X or N others"), and, over all quests, NO EXIT (an
// extract objective naming an exit no map has).
static async Task Bring(string mode)
{
    var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shturmap", "cache", "tarkov-dev");
    var gameMode = GameLogParser.ModeFrom(mode == "seasonal" ? "PvpSeason" : mode);
    var loader = new GameDataLoader(new CachedHttp(CachedHttp.CreateClient(), cache));
    var data = await loader.LoadAsync(gameMode, "en");
    var sources = await loader.LoadSourcesAsync(gameMode, "en");
    var quests = data.Tasks.Values.Select(t => Shturmap.Session.Planning.ToPlan(t, data)).ToList();
    var kinds = new Dictionary<Shturmap.Core.Planning.RequirementKind, int>();
    var flagged = 0;
    foreach (var map in Shturmap.Session.Planning.Maps(data).OrderBy(m => m.Name))
    {
        var plan = Shturmap.Core.Planning.RaidPlanner.Plan(quests, map);
        Console.WriteLine($"== {map.Name}");
        foreach (var r in plan.Requirements.OrderBy(r => r.Kind))
        {
            var view = Shturmap.Session.Planning.RequirementText(data, r, sources);
            kinds[r.Kind] = kinds.GetValueOrDefault(r.Kind) + 1;
            var flags = new List<string>();
            if (r.Alternatives.Any(id => data.ItemName(id) == "Unknown item"))
                flags.Add("UNKNOWN ITEM");
            // A weapon list shown as "X or N others": which categories it spans, against each category's size.
            if (r.Kind == Shturmap.Core.Planning.RequirementKind.Weapon && view.Text.EndsWith(" others", StringComparison.Ordinal))
                flags.Add($"LIST {r.Alternatives.Count}: " + string.Join(", ", r.Alternatives
                    .GroupBy(id => sources.Items.GetValueOrDefault(id)?.Categories?.FirstOrDefault())
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key is null ? $"? {g.Count()}" : $"{data.ItemName(g.Key)} {g.Count()}/{sources.Members[g.Key].Count()}")));
            flagged += flags.Count > 0 ? 1 : 0;
            Console.WriteLine($"  {r.Kind,-10} {view.Text} | {view.Why}" + (flags.Count > 0 ? $"  [{string.Join(", ", flags)}]" : ""));
        }
    }
    var noExit = data.Tasks.Values
        .SelectMany(t => (t.Objectives ?? []).Select(o => (Task: t, Facts: data.ObjectiveFacts.GetValueOrDefault(o.Id))))
        // Transits ("STR_TRANSIT_4") are named the same way but aren't extracts; what they take is the transit's text.
        .Where(x => x.Facts?.Exit is { } exit && !exit.Contains("TRANSIT", StringComparison.Ordinal) &&
                    !data.Maps.Values.SelectMany(m => m.Extracts ?? []).Any(e => data.ExtractKeys.GetValueOrDefault(e.Id) == exit))
        .Select(x => $"{x.Task.Name} ({x.Facts!.Exit})").Distinct().ToList();
    Console.WriteLine();
    Console.WriteLine("Rows: " + string.Join(", ", kinds.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}")) + $"; flagged {flagged}");
    Console.WriteLine($"NO EXIT: {(noExit.Count == 0 ? "none" : string.Join("; ", noExit))}");
}

// The audit to run after a tarkov.dev or game update: every quest row Plan can show (all quests active), in the plan's
// order, with its effort group and complexity (QuestEffort), and what to look at: UNKNOWN TARGET (a kill target the
// rules don't know, counted as a fight) and, over all quests, objective types the rules don't know.
static async Task Effort(string mode)
{
    var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shturmap", "cache", "tarkov-dev");
    var gameMode = GameLogParser.ModeFrom(mode == "seasonal" ? "PvpSeason" : mode);
    var data = await new GameDataLoader(new CachedHttp(CachedHttp.CreateClient(), cache)).LoadAsync(gameMode, "en");
    var quests = data.Tasks.Values.Select(t => Shturmap.Session.Planning.ToPlan(t, data)).ToList();
    var groups = new Dictionary<Shturmap.Core.Planning.EffortGroup, int>();
    var unknownTargets = new SortedSet<string>(StringComparer.Ordinal);
    var rows = 0;
    foreach (var map in Shturmap.Session.Planning.Maps(data).OrderBy(m => m.Name))
    {
        var plan = Shturmap.Core.Planning.RaidPlanner.Plan(quests, map);
        Console.WriteLine($"== {map.Name}");
        foreach (var (section, list) in new[] { ("COMPLETE", plan.Finish), ("PROGRESS", plan.Progress) })
        {
            Console.WriteLine($"  {section}");
            Shturmap.Core.Planning.EffortGroup? previous = null;
            foreach (var q in list)
            {
                var effort = Shturmap.Core.Planning.QuestEffort.Of(q);
                if (previous is { } p && p != effort.Group)
                    Console.WriteLine("    ----");
                previous = effort.Group;
                rows++;
                groups[effort.Group] = groups.GetValueOrDefault(effort.Group) + 1;
                var unknown = q.Objectives.SelectMany(o => o.Targets ?? []).Where(t => !Shturmap.Core.Planning.QuestEffort.IsKnownTarget(t)).Distinct().ToList();
                unknownTargets.UnionWith(unknown);
                Console.WriteLine($"    {effort.Group,-13} {effort.Steps} {effort.Conditions} {effort.KillBucket}  {q.Quest.Name}" +
                                  (unknown.Count > 0 ? $"  [UNKNOWN TARGET {string.Join(", ", unknown)}]" : ""));
            }
        }
    }
    var unknownTypes = data.Tasks.Values.SelectMany(t => t.Objectives ?? []).Select(o => o.Type)
        .Where(t => !Shturmap.Core.Planning.QuestEffort.IsKnownType(t)).Distinct().ToList();
    Console.WriteLine();
    Console.WriteLine($"{rows} rows: " + string.Join(", ", groups.OrderBy(g => g.Key).Select(g => $"{Shturmap.Core.Planning.QuestEffort.Label(g.Key)} {g.Value}")));
    Console.WriteLine($"Unknown kill targets: {(unknownTargets.Count == 0 ? "none" : string.Join(", ", unknownTargets))}");
    Console.WriteLine($"Unknown objective types: {(unknownTypes.Count == 0 ? "none" : string.Join(", ", unknownTypes.Select(t => t ?? "(none)")))}");
}

// The help panel's "Keep a study log" switch, in the app's own settings; for setting it without opening the app.
static void Study(string? value)
{
    using var store = new Shturmap.Data.Progress.ProgressStore(Shturmap.Session.AppPaths.Default.Database);
    const string key = Shturmap.Session.GameSession.StudySetting;
    if (value is "on" or "off")
        store.SetSetting(key, value);
    else if (value is not null)
        throw new ArgumentException("Use: shturmap-cli study [on|off]");
    Console.WriteLine($"Study log: {store.GetSetting(key) ?? "off (not set)"}");
}

// The audit to run after a tarkov.dev or game update: every quest row Plan can show (all quests active), its synopsis,
// and what to look at: FALLBACK (a text starting with no known verb, shown as written), LONG (more than the two lines
// the row shows) and BREAK (QuestSynopsis.Problems: a word not in the text, a condition left out, a cut mid-phrase).
static async Task Synopses(string mode)
{
    var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shturmap", "cache", "tarkov-dev");
    var gameMode = GameLogParser.ModeFrom(mode == "seasonal" ? "PvpSeason" : mode);
    var data = await new GameDataLoader(new CachedHttp(CachedHttp.CreateClient(), cache)).LoadAsync(gameMode, "en");
    var quests = data.Tasks.Values.Select(Shturmap.Session.Planning.ToPlan).ToList();
    // The row's second line: Bahnschrift 12.5 (NoteText) in the Plan row's text column, 221 px less the bring cells
    // (18 px each, 2 apart; past three a "+N").
    using var typeface = SkiaSharp.SKFontManager.Default.MatchFamily("Bahnschrift") ?? SkiaSharp.SKTypeface.Default;
    using var font = new SkiaSharp.SKFont(typeface, 12.5f);
    static double Width(int cells) => 221 - cells switch { 0 => 18, <= 3 => cells * 18 + (cells - 1) * 2, _ => 3 * 18 + 2 * 2 + 4 + 14 };
    int Lines(string text, double width)
    {
        var lines = 0;
        var line = "";
        foreach (var word in text.Split(' '))
        {
            var next = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && font.MeasureText(next) > width)
            {
                lines++;
                line = word;
            }
            else
            {
                line = next;
            }
        }
        return line.Length > 0 ? lines + 1 : lines;
    }

    int rows = 0, fallbacks = 0, longRows = 0, breaks = 0;
    foreach (var map in Shturmap.Session.Planning.Maps(data).OrderBy(m => m.Name))
    {
        var plan = Shturmap.Core.Planning.RaidPlanner.Plan(quests, map);
        Console.WriteLine($"== {map.Name}");
        foreach (var q in plan.Finish.Concat(plan.Progress))
        {
            var line = Shturmap.Session.Planning.Synopsis(data, q, map);
            if (line is null)
                continue;
            rows++;
            var cells = plan.Requirements.Count(r => r.ForQuests.Contains(q.Quest.Id));
            var lines = Lines(line.Text, Width(cells));
            var problems = Shturmap.Core.Quests.QuestSynopsis.Problems(line);
            var flags = new List<string>();
            if (line.HasFallback)
            {
                fallbacks++;
                flags.Add("FALLBACK");
            }
            if (lines > 2)
            {
                longRows++;
                flags.Add($"LONG {lines}");
            }
            if (problems.Count > 0)
            {
                breaks++;
                flags.Add("BREAK");
            }
            Console.WriteLine($"  {q.Quest.Name} | {line.Text}{(flags.Count > 0 ? "  [" + string.Join(", ", flags) + "]" : "")}");
            foreach (var p in problems)
                Console.WriteLine($"      {p}");
        }
    }
    Console.WriteLine($"{rows} rows: {fallbacks} with a fallback, {longRows} longer than two lines, {breaks} breaking a rule");
}

static async Task Quests(string mode)
{
    var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shturmap", "cache", "tarkov-dev");
    var gameMode = GameLogParser.ModeFrom(mode == "seasonal" ? "PvpSeason" : mode);
    var data = await new GameDataLoader(new CachedHttp(CachedHttp.CreateClient(), cache)).LoadAsync(gameMode, "en");
    using var store = new Shturmap.Data.Progress.ProgressStore(Shturmap.Session.AppPaths.Default.Database);
    var stored = store.Load(gameMode);
    // As the app does: only the game's log (and manual changes) count; older scan and import rows are ignored.
    var observations = stored.Where(o => o.Source is Shturmap.Core.Quests.ObservationSource.Log or Shturmap.Core.Quests.ObservationSource.Manual).ToList();
    var statuses = Shturmap.Core.Quests.QuestProgress.Resolve(observations,
        id => data.Tasks.GetValueOrDefault(id)?.TaskRequirements?.Select(r => new Shturmap.Core.Quests.QuestRequirement(r.Task, r.Status ?? [])) ?? [],
        id => data.Tasks.GetValueOrDefault(id)?.Name ?? id);
    var bySource = stored.GroupBy(o => o.Source).Select(g => $"{g.Key} {g.Count()}");
    Console.WriteLine($"{stored.Count} stored observations ({string.Join(", ", bySource)}); {observations.Count} used");
    foreach (var status in statuses.Values.Where(s => s.State == Shturmap.Core.Quests.QuestState.Active && data.Tasks.ContainsKey(s.QuestId))
                 .OrderBy(s => data.Tasks[s.QuestId].Name))
    {
        Console.WriteLine($"{data.Tasks[status.QuestId].Name}");
        foreach (var o in observations.Where(o => o.QuestId == status.QuestId).OrderBy(o => o.At))
            Console.WriteLine($"    {o.At:yyyy-MM-dd HH:mm} {o.State,-9} {o.Source,-9} {o.Evidence}");
    }
}

static async Task Simulate()
{
    // A fake game: a Logs session the script appends to, and a Screenshots folder it drops files into.
    var root = Directory.CreateTempSubdirectory("shturmap-sim-").FullName;
    var start = DateTime.Now;
    var session = $"log_{start:yyyy.MM.dd_HH-mm-ss}_1.1.5.1.47510";
    var logs = Path.Combine(root, "Logs");
    Directory.CreateDirectory(Path.Combine(logs, session));
    var appLog = Path.Combine(logs, session, $"{start:yyyy.MM.dd_HH-mm-ss}_1.1.5.1.47510 application_000.log");
    var shots = Path.Combine(root, "Screenshots");
    Directory.CreateDirectory(shots);
    void Log(string message) => File.AppendAllText(appLog, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}|1.1.5.1.47510|Info|application|{message}\r\n");
    void Shot(double x, double y, double z, string quaternion) =>
        File.WriteAllText(Path.Combine(shots, FormattableString.Invariant($"{DateTime.Now:yyyy-MM-dd[HH-mm]}_{x:0.00}, {y:0.00}, {z:0.00}_{quaternion}_14.13 (0).png")), "");

    var install = new InstallCandidate(InstallKind.Manual, root, logs, start, "simulation", null);
    var settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Battlestate Games", "Escape from Tarkov", "Settings");
    var locations = new GameLocations(install, [install], shots, settingsFolder);
    var paths = new Shturmap.Session.AppPaths(Path.Combine(root, "app"), Shturmap.Session.AppPaths.Default.CacheRoot);

    Log("Session mode: Pve");
    Log("PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0");
    await using var game = new Shturmap.Session.GameSession(paths, locations);
    game.Notice += m => Console.WriteLine($"  notice: {m.Text}");
    await game.StartAsync();
    for (var i = 0; i < 50 && game.Snapshot.Data is null; i++)
        await Task.Delay(200);

    void Print(string step)
    {
        var s = game.Snapshot;
        Console.WriteLine($"[{step}] {s.Raid.Phase} on {s.Map?.Name} as {s.Raid.Side}; fix {(s.Fix is null ? "none" : s.Fix.Position + $" facing {s.Fix.YawDegrees:0}°")}; floor {s.Floor?.Name ?? "ground"}; trail {s.Trail.Count}");
        foreach (var o in s.Objectives.Where(o => o.HasPlace).Take(5))
            Console.WriteLine($"      {o.Distance,6:0} m {(o.Direction is { } d ? Shturmap.Core.Navigation.Bearing.Describe(d) : ""),-12} {o.QuestName}: {o.Text}");
        foreach (var e in step == "raid started" ? s.Extracts : s.Extracts.Take(3))
            Console.WriteLine($"      {e.Distance,6:0} m {(e.Direction is { } d ? Shturmap.Core.Navigation.Bearing.Describe(d) : ""),-12} {e.Name} ({e.Kind}){(e.Needs.Length > 0 ? "  — " + e.Needs : "")}");
    }

    Print("menu");
    Log("scene preset path:maps/city_preset.bundle rcid:city.scenespreset.asset");
    Log("TRACE-NetworkGameCreate profileStatus: 'Profileid: 000000000000000000000003, Status: Busy, RaidMode: Online, Location: TarkovStreets, shortId: SIMRAD'");
    Log("GameStarting:80.26(1.7) real:95.46(2.73) diff:15.19");
    await Task.Delay(1500);
    Log("GameStarted:90.6(10.33) real:107.49(12.02) diff:16.89");
    await Task.Delay(2500);
    Print("raid started");

    Shot(-60.00, 3.50, 300.00, "-0.02500, 0.23500, -0.00500, -0.97150");
    await Task.Delay(1500);
    Print("first screenshot");
    Shot(40.00, 2.50, 120.00, "0.01000, 0.99900, -0.04000, 0.02000");
    await Task.Delay(1500);
    Print("second screenshot");
    Shot(40.00, 12.4, 120.00, "0.01000, 0.99900, -0.04000, 0.02000");
    await Task.Delay(1500);
    Print("upstairs");

    Log("PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0");
    await Task.Delay(2500);
    Print("raid over");
    try
    {
        Directory.Delete(root, true);
    }
    catch (IOException)
    {
    }
}

static async Task Watch(int seconds)
{
    await using var session = new Shturmap.Session.GameSession(Shturmap.Session.AppPaths.Default);
    var last = "";
    session.Notice += notice => Console.WriteLine($"{DateTime.Now:HH:mm:ss} NOTICE {notice.Text}");
    session.Changed += s =>
    {
        var line = $"{s.Mode} | {s.Raid.Phase} {s.Map?.Name} side={s.Raid.Side} | fix={(s.Fix is null ? "-" : s.Fix.Position.ToString())} floor={s.Floor?.Name ?? "base"} | " +
                   $"active quests={s.ActiveQuestCount} objectives here={s.Objectives.Count} extracts={s.Extracts.Count} | {s.Logs.Text}, {s.Screenshots.Text}, {s.DataHealth.Text}";
        if (line != last)
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} {line}");
        last = line;
    };
    var sw = Stopwatch.StartNew();
    await session.StartAsync();
    Console.WriteLine($"started in {sw.ElapsedMilliseconds} ms");
    await Task.Delay(TimeSpan.FromSeconds(seconds));
    var snap = session.Snapshot;
    Console.WriteLine($"--- after {seconds}s: {snap.Quests.Values.Count(q => q.State == Shturmap.Core.Quests.QuestState.Active)} active, " +
                      $"{snap.Quests.Values.Count(q => q.State == Shturmap.Core.Quests.QuestState.Completed && !q.IsImplied)} completed, " +
                      $"{snap.Quests.Values.Count(q => q.IsImplied)} implied complete");
    foreach (var q in snap.Quests.Values.Where(q => q.State == Shturmap.Core.Quests.QuestState.Active).Take(40))
        Console.WriteLine($"  active: {snap.Data?.Tasks.GetValueOrDefault(q.QuestId)?.Name ?? q.QuestId} ({q.Source}, {q.At:dd.MM HH:mm})");
    foreach (var o in snap.Objectives.Take(8))
        Console.WriteLine($"  here: [{o.Kind}] {o.QuestName}: {o.Text} {(o.Distance is { } d ? $"{d:0} m" : "")}{(o.Needs is { } n ? "  — " + n : "")}");
    Console.WriteLine($"--- next raid (last: {snap.LastRaid?.MapName} {snap.LastRaid?.Duration:mm\\:ss} {snap.LastRaid?.Side}); any map: {string.Join("; ", snap.AnyMap.Select(q => $"[{q.Kind}] {q.Name}"))}");
    foreach (var p in snap.Plan)
    {
        Console.WriteLine($"  {p.MapName}: finish {p.Finish.Count}, progress {p.Progress.Count}, {p.RaidMinutes} min raid; bosses {string.Join(", ", p.Bosses)}");
        Console.WriteLine($"     finish:   {string.Join("; ", p.Finish.Select(q => $"[{q.Kind}] {q.Name}"))}");
        Console.WriteLine($"     progress: {string.Join("; ", p.Progress.Select(q => $"[{q.Kind}] {q.Name}"))}");
        foreach (var r in p.Requirements)
            Console.WriteLine($"     {r.Kind}: {r.Text}  (for {r.ForQuests})");
    }
}

// Spawn markers stand at the centroid of a group of real spawn points; this shows how far each is from the nearest
// point of its kind, so a data change that leaves a marker where nothing spawns is seen.
static async Task Spawns(string mode)
{
    var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shturmap", "cache", "tarkov-dev");
    var gameMode = GameLogParser.ModeFrom(mode == "seasonal" ? "PvpSeason" : mode);
    var data = await new GameDataLoader(new CachedHttp(CachedHttp.CreateClient(), cache)).LoadAsync(gameMode, "en");
    foreach (var map in data.Maps.Values.OrderBy(m => m.Name))
    {
        var scav = (map.Spawns ?? []).Where(s => s.Position is not null && s.Sides?.Contains("scav") == true).ToList();
        var points = new Dictionary<Shturmap.Map.MarkerKind, List<Shturmap.Core.WorldPoint>>
        {
            [Shturmap.Map.MarkerKind.ScavSpawn] = scav.Where(s => s.Categories?.Any(c => c is "bot" or "all") == true && s.Categories?.Contains("sniper") != true)
                .Select(s => s.Position!.ToWorld()).ToList(),
            [Shturmap.Map.MarkerKind.SniperSpawn] = scav.Where(s => s.Categories?.Contains("bot") == true && s.Categories?.Contains("sniper") == true)
                .Select(s => s.Position!.ToWorld()).ToList(),
            [Shturmap.Map.MarkerKind.BossSpawn] = (map.Bosses ?? []).Where(b => b.Mob.StartsWith("boss", StringComparison.Ordinal))
                .SelectMany(b => b.SpawnLocations ?? []).SelectMany(l => l.Positions ?? []).Select(p => p.ToWorld()).ToList(),
        };
        var markers = Shturmap.Map.MapContentBuilder.SpawnZones(data, map);
        if (markers.Count == 0)
            continue;
        var far = markers.Select(m => (m, p: points[m.Kind].MinBy(p => p.HorizontalDistanceTo(m.Position) + Math.Abs(p.Y - m.Position.Y))))
            .Select(x => (x.m, d: x.p.HorizontalDistanceTo(x.m.Position), dy: x.p.Y - x.m.Position.Y)).ToList();
        var zones = scav.Select(s => (s.Categories?.Contains("sniper") == true, s.ZoneName)).Distinct().Count();
        Console.WriteLine($"{map.Name} ({map.NormalizedName}): {zones} Scav and sniper zones; {markers.Count(m => m.Kind == Shturmap.Map.MarkerKind.ScavSpawn)} Scav, " +
                          $"{markers.Count(m => m.Kind == Shturmap.Map.MarkerKind.SniperSpawn)} sniper, {markers.Count(m => m.Kind == Shturmap.Map.MarkerKind.BossSpawn)} boss; " +
                          $"farthest {far.Max(x => x.d):0} m across, {far.Max(x => Math.Abs(x.dy)):0.0} m up or down");
        foreach (var (m, d, dy) in far.Where(x => x.d > 20 || Math.Abs(x.dy) > 3))
            Console.WriteLine($"    {m.Id}: {d:0} m from the nearest point, {dy:+0.0;-0.0;0.0} m in height");
    }
}

static async Task Render(string mapName, string output, List<string> screenshots)
{
    var cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shturmap", "cache");
    var http = new CachedHttp(CachedHttp.CreateClient(), Path.Combine(cacheRoot, "tarkov-dev"));
    var data = await new GameDataLoader(http).LoadAsync(GameMode.Pve, "en");
    var map = data.MapByNormalizedName(mapName) ?? throw new ArgumentException("Unknown map " + mapName);
    var definition = data.DefinitionFor(map.NormalizedName) ?? throw new InvalidOperationException("No map definition for " + mapName);

    // Maps without an SVG (The Lab, Labyrinth, Icebreaker) render as the schematic sheet.
    var sw = Stopwatch.StartNew();
    Shturmap.Map.MapArtwork? artwork = null;
    if (definition.SvgPath is { } svgUrl)
    {
        var svg = await new Shturmap.Data.Maps.ArtworkCache(new CachedHttp(CachedHttp.CreateClient(), Path.Combine(cacheRoot, "artwork"))).GetSvgAsync(svgUrl);
        artwork = Shturmap.Map.MapArtwork.Load(svg, definition, Path.Combine(cacheRoot, "pictures"));
        Console.WriteLine($"Artwork parsed in {sw.ElapsedMilliseconds} ms (viewBox {artwork.ViewBox.Width:0}×{artwork.ViewBox.Height:0})");
    }
    else
    {
        Console.WriteLine("No artwork for this map: drawing the schematic sheet");
    }
    using var artworkScope = artwork;

    // Quests from the Tasks screenshots, as an example of active quests; on maps where none of them has anything,
    // every quest with something on the map.
    var active = data.Tasks.Values.Where(t => new[] { "audit", "dandies", "secret-message", "road-closed", "ballet-lover", "glory-to-cpsu", "revision-streets-of-tarkov" }
        .Contains(t.NormalizedName)).Select(t => t.Id).ToList();
    var content = Shturmap.Map.MapContentBuilder.Build(data, map.Id, active, new HashSet<string>());
    if (content.Objectives.All(o => o.Places.Count == 0))
        content = Shturmap.Map.MapContentBuilder.Build(data, map.Id, data.Tasks.Keys.ToList(), new HashSet<string>());
    var scene = new Shturmap.Map.MapScene(definition, artwork) { Markers = content.Markers, Zones = content.Zones };

    var fixes = screenshots.Select(s => Shturmap.Core.Screenshots.ScreenshotName.TryParse(s, out var info) ? info : null)
        .Where(i => i?.Position is not null).ToList();
    if (fixes.Count > 0)
    {
        var last = fixes[^1]!;
        scene.Player = new Shturmap.Map.PlayerFix(last.Position!.Value, last.YawDegrees, last.TakenAt);
        scene.Trail = fixes.SkipLast(1).Select(f => f!.Position!.Value).ToList();
        scene.Floor = Shturmap.Core.Maps.FloorResolver.LayerFor(definition, last.Position.Value);
    }
    scene.Selected = content.Objectives.FirstOrDefault(o => o.Places.Count > 0)?.Quest.Id;

    // "-ping": a new position pinging, 0.6 s in; "-edge": the same with the player out of view (the edge arrow).
    foreach (var (suffix, zoomIn, ping, away) in new[] { ("", 1.0, false, 0f), ("-close", 3.0, false, 0f), ("-ping", 3.0, true, 0f), ("-edge", 3.0, true, 1100f) })
    {
        var camera = new Shturmap.Map.Camera();
        camera.Resize(new SkiaSharp.SKSize(1600, 1000));
        camera.Fit(scene.Projection.WorldRect);
        if (zoomIn > 1 && scene.Player is { } p)
        {
            camera.CenterOn(scene.Projection.ToMap(p.Position));
            camera.ZoomAt(new SkiaSharp.SKPoint(800, 500), zoomIn);
            camera.Pan(-away, -away * 0.3f);
        }
        scene.PingSince = ping ? DateTime.Now - TimeSpan.FromSeconds(0.6) : null;
        sw.Restart();
        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(1600, 1000));
        Shturmap.Map.MapRenderer.Render(surface.Canvas, camera, scene);
        var drawMs = sw.Elapsed.TotalMilliseconds;
        var path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, Path.GetFileNameWithoutExtension(output) + suffix + ".png");
        using var image = surface.Snapshot();
        using var png = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
        await File.WriteAllBytesAsync(path, png.ToArray());
        Console.WriteLine($"{path}: drawn in {drawMs:0.0} ms (CPU raster)");
    }
    Console.WriteLine($"{content.Markers.Count} markers, {content.Zones.Count} zones; objectives here: " +
                      string.Join("; ", content.Objectives.Select(o => $"{o.Quest.Name}: {o.Objective.Description} ({o.Places.Count} places)")));
}

static async Task Data(string mode, string language)
{
    var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shturmap", "cache", "tarkov-dev");
    var loader = new GameDataLoader(new CachedHttp(CachedHttp.CreateClient(), cache));
    var gameMode = GameLogParser.ModeFrom(mode == "seasonal" ? "PvpSeason" : mode);
    var sw = Stopwatch.StartNew();
    var data = await loader.LoadAsync(gameMode, language);
    Console.WriteLine($"Loaded {gameMode} ({data.Language}) in {sw.ElapsedMilliseconds} ms{(data.Offline ? " from cache (offline)" : "")}: " +
                      $"{data.Tasks.Count} tasks, {data.Maps.Count} maps, {data.Traders.Count} traders, {data.MapDefinitions.Count} map definitions");
    foreach (var map in data.Maps.Values.OrderBy(m => m.Name))
    {
        var definition = data.DefinitionFor(map.NormalizedName);
        Console.WriteLine($"  {map.Name,-22} {map.ScenePath,-36} extracts={map.Extracts?.Count ?? 0,-3} artwork={(definition?.SvgPath is not null ? "svg" : definition?.TilePath is not null ? "tiles" : "NONE")}");
    }
    if (data.Tasks.Values.FirstOrDefault(t => t.NormalizedName == "audit") is { } audit)
    {
        Console.WriteLine($"Example: {audit.Name} ({data.TraderName(audit.Trader)}), level {audit.MinPlayerLevel}");
        foreach (var o in audit.Objectives ?? [])
            Console.WriteLine($"  - {o.Description} [{o.Type}] {string.Join("; ", (o.PossibleLocations ?? []).SelectMany(l => l.Positions ?? []).Select(p => $"({p.X:0.#}, {p.Y:0.#}, {p.Z:0.#})"))}");
    }
}

static GameLocations Locate()
{
    var env = new WindowsGameEnvironment();
    var found = new InstallLocator(env).Locate();
    Console.WriteLine("Candidates:");
    foreach (var c in found.Candidates)
        Console.WriteLine($"  [{(c.IsValid ? "ok" : "--")}] {c.Kind,-11} {c.Root}  ({c.Found}){(c.Rejected is null ? "" : " - " + c.Rejected)}");
    Console.WriteLine($"Chosen:      {found.Install?.Kind} {found.Install?.Root}");
    Console.WriteLine($"Logs:        {found.LogsFolder} (newest session {found.Install?.NewestSession:yyyy-MM-dd HH:mm})");
    Console.WriteLine($"Screenshots: {found.ScreenshotsFolder} (exists: {Directory.Exists(found.ScreenshotsFolder)})");
    var settings = new GameSettingsReader(env).Read(found.SettingsFolder);
    Console.WriteLine($"Settings:    screenshot key {(settings.ScreenshotKeys.Count == 0 ? "NOT BOUND" : string.Join(" or ", settings.ScreenshotKeys))}, language {settings.Language}");
    return found;
}

static void Replay(string? session)
{
    var found = new InstallLocator(new WindowsGameEnvironment()).Locate();
    if (found.LogsFolder is null)
    {
        Console.WriteLine("No logs folder found.");
        return;
    }
    session ??= LogTailer.NewestSession(found.LogsFolder);
    var tracker = new RaidTracker();
    foreach (var e in LogTailer.ReadSession(Path.Combine(found.LogsFolder, session!)))
    {
        switch (tracker.Apply(e))
        {
            case ModeChanged m:
                Console.WriteLine($"{e.At:HH:mm:ss} mode {m.State.Mode}");
                break;
            case RaidLoading l:
                Console.WriteLine($"{e.At:HH:mm:ss} loading {l.State.ScenePath}");
                break;
            case RaidStarted s:
                Console.WriteLine($"{e.At:HH:mm:ss} raid started on {s.State.LocationId ?? s.State.ScenePath} as {s.State.Side}");
                break;
            case RaidEnded r:
                Console.WriteLine($"{e.At:HH:mm:ss} back in menu{(r.Previous.RaidStartedAt is { } st ? $" after {(r.At - st):mm\\:ss}" : " (load abandoned)")}");
                break;
        }
        if (e is QuestEvent q)
            Console.WriteLine($"{e.At:HH:mm:ss} quest {q.QuestId} {q.Status}");
    }
}
