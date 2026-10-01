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
            """);
        break;
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
        Console.WriteLine($"  {p.MapName}: finish {p.Finish.Count}, progress {p.Progress.Count}, ~{p.WalkingMinutes} min walking of {p.RaidMinutes} min; bosses {string.Join(", ", p.Bosses)}");
        Console.WriteLine($"     finish:   {string.Join("; ", p.Finish.Select(q => $"[{q.Kind}] {q.Name}"))}");
        Console.WriteLine($"     progress: {string.Join("; ", p.Progress.Select(q => $"[{q.Kind}] {q.Name}"))}");
        foreach (var r in p.Requirements)
            Console.WriteLine($"     {r.Kind}: {r.Text}  (for {r.ForQuests})");
    }
}

static async Task Render(string mapName, string output, List<string> screenshots)
{
    var cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shturmap", "cache");
    var http = new CachedHttp(CachedHttp.CreateClient(), Path.Combine(cacheRoot, "tarkov-dev"));
    var data = await new GameDataLoader(http).LoadAsync(GameMode.Pve, "en");
    var map = data.MapByNormalizedName(mapName) ?? throw new ArgumentException("Unknown map " + mapName);
    var definition = data.DefinitionFor(map.NormalizedName) ?? throw new InvalidOperationException("No artwork definition for " + mapName);
    var svg = await new Shturmap.Data.Maps.ArtworkCache(new CachedHttp(CachedHttp.CreateClient(), Path.Combine(cacheRoot, "artwork")))
        .GetSvgAsync(definition.SvgPath ?? throw new InvalidOperationException("Map has no SVG"));

    var sw = Stopwatch.StartNew();
    using var artwork = Shturmap.Map.MapArtwork.Load(svg, definition, Path.Combine(cacheRoot, "pictures"));
    Console.WriteLine($"Artwork parsed in {sw.ElapsedMilliseconds} ms (viewBox {artwork.ViewBox.Width:0}×{artwork.ViewBox.Height:0})");

    // Quests from the Tasks screenshots, as an example of active quests.
    var active = data.Tasks.Values.Where(t => new[] { "audit", "dandies", "secret-message", "road-closed", "ballet-lover", "glory-to-cpsu", "revision-streets-of-tarkov" }
        .Contains(t.NormalizedName)).Select(t => t.Id).ToList();
    var content = Shturmap.Map.MapContentBuilder.Build(data, map.Id, active, new HashSet<string>());
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

    foreach (var (suffix, zoomIn) in new[] { ("", 1.0), ("-close", 3.0) })
    {
        var camera = new Shturmap.Map.Camera();
        camera.Resize(new SkiaSharp.SKSize(1600, 1000));
        camera.Fit(scene.Projection.WorldRect);
        if (zoomIn > 1 && scene.Player is { } p)
        {
            camera.CenterOn(scene.Projection.ToMap(p.Position));
            camera.ZoomAt(new SkiaSharp.SKPoint(800, 500), zoomIn);
        }
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
