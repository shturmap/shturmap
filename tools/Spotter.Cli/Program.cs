using System.Diagnostics;
using Spotter.Core.Logs;
using Spotter.Core.Raid;
using Spotter.Data.Http;
using Spotter.Data.TarkovDev;
using Spotter.Game.Install;
using Spotter.Game.Logs;
using Spotter.Game.Settings;

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
    case "ocr":
        await Ocr(args.Skip(1).ToList());
        break;
    default:
        Console.WriteLine("""
            spotter-cli locate              find the game, logs, screenshots and settings on this PC
            spotter-cli replay [session]    replay a log session (default: newest) through the raid tracker
            spotter-cli data [mode] [lang]  load tarkov.dev data (mode: pve | regular | seasonal)
            spotter-cli ocr <png>...        read Tasks-screen screenshots and match them to PvE quests
            """);
        break;
}

static async Task Ocr(List<string> files)
{
    var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spotter", "cache", "tarkov-dev");
    var data = await new GameDataLoader(new CachedHttp(CachedHttp.CreateClient(), cache)).LoadAsync(GameMode.Pve, "en");
    var candidates = data.Tasks.Values
        .Select(t => new Spotter.Core.Quests.QuestCandidate(t.Id, t.Name,
            t.Map is not null && data.Maps.TryGetValue(t.Map, out var m) ? [m.Name] : []))
        .ToList();
    var recognizer = Spotter.Ocr.TextRecognizer.Create("en") ?? throw new InvalidOperationException("No OCR language installed.");
    // Experiments: SPOTTER_OCR_SCALE=2.5, SPOTTER_OCR_INTERP=Linear|Cubic|Fant|NearestNeighbor
    var scale = double.TryParse(Environment.GetEnvironmentVariable("SPOTTER_OCR_SCALE"), System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 2.0;
    var interpolation = Enum.TryParse<Windows.Graphics.Imaging.BitmapInterpolationMode>(Environment.GetEnvironmentVariable("SPOTTER_OCR_INTERP"), out var i)
        ? i : Windows.Graphics.Imaging.BitmapInterpolationMode.Cubic;
    var reader = new Spotter.Ocr.TasksScreenReader(recognizer, scale, interpolation);
    foreach (var file in files)
    {
        var screen = await reader.ReadAsync(Path.GetFullPath(file));
        Console.WriteLine($"{Path.GetFileName(file)}: {(screen is null ? "not a Tasks screen" : $"{screen.Tab} tab, {screen.Rows.Count} rows, {screen.Elapsed.TotalMilliseconds:0} ms")}");
        foreach (var row in screen?.Rows ?? [])
        {
            var match = Spotter.Core.Quests.QuestNameMatcher.Match(row.Name, row.Location, candidates);
            Console.WriteLine($"  {row.Name,-32} | {row.Location,-20} | {row.Status,-8} → {match.Verdict,-17} {match.Quest?.Name} ({match.Score:0.00})");
        }
    }
}

static async Task Data(string mode, string language)
{
    var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spotter", "cache", "tarkov-dev");
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
