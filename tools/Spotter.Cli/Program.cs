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
    default:
        Console.WriteLine("""
            spotter-cli locate              find the game, logs, screenshots and settings on this PC
            spotter-cli replay [session]    replay a log session (default: newest) through the raid tracker
            spotter-cli data [mode] [lang]  load tarkov.dev data (mode: pve | regular | seasonal)
            """);
        break;
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
