using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Spotter.Core.Logs;
using Spotter.Core.Maps;
using Spotter.Data.Http;

namespace Spotter.Data.TarkovDev;

/// <summary>
/// Loads tasks, maps and traders from json.tarkov.dev (plus tarkov.dev's maps.json for map geometry), translated
/// into the game's language. Cached copies are revalidated at most once an hour; offline, the cache is used.
/// </summary>
public sealed class GameDataLoader(CachedHttp http)
{
    private static readonly Uri JsonApi = new("https://json.tarkov.dev/");
    private static readonly Uri MapDefinitionsUri = new("https://raw.githubusercontent.com/the-hideout/tarkov-dev/main/src/data/maps.json");
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(1);

    public async Task<GameData> LoadAsync(GameMode mode, string language, CancellationToken ct = default)
    {
        var slug = GameData.Slug(mode);
        language = string.IsNullOrWhiteSpace(language) ? "en" : language.ToLowerInvariant();

        var fetches = new List<Task<CachedResponse>>();
        Task<CachedResponse> Fetch(string endpoint)
        {
            var task = http.GetAsync(new Uri(JsonApi, endpoint), endpoint.Replace('/', '_') + ".json", MaxAge, ct);
            fetches.Add(task);
            return task;
        }

        var maps = Fetch($"{slug}/maps");
        var tasks = Fetch($"{slug}/tasks");
        var traders = Fetch($"{slug}/traders");
        var mapsEn = Fetch($"{slug}/maps_en");
        var tasksEn = Fetch($"{slug}/tasks_en");
        var tradersEn = Fetch($"{slug}/traders_en");
        var mapsLang = language == "en" ? mapsEn : Fetch($"{slug}/maps_{language}");
        var tasksLang = language == "en" ? tasksEn : Fetch($"{slug}/tasks_{language}");
        var tradersLang = language == "en" ? tradersEn : Fetch($"{slug}/traders_{language}");
        var definitions = http.GetAsync(MapDefinitionsUri, "tarkov-dev_maps.json", TimeSpan.FromHours(24), ct);
        fetches.Add(definitions);
        await Task.WhenAll(fetches);

        return new GameData
        {
            Mode = mode,
            Language = language,
            Maps = Read(maps.Result, mapsLang.Result, mapsEn.Result, "maps", ApiJsonContext.Default.DictionaryStringApiMap),
            Tasks = Read(tasks.Result, tasksLang.Result, tasksEn.Result, "tasks", ApiJsonContext.Default.DictionaryStringApiTask),
            Traders = Read(traders.Result, tradersLang.Result, tradersEn.Result, null, ApiJsonContext.Default.DictionaryStringApiTrader),
            MapDefinitions = MapDefinitionReader.Read(await File.ReadAllTextAsync(definitions.Result.FilePath, ct)),
            CheckedAt = fetches.Min(f => f.Result.FetchedAt),
            Offline = fetches.Any(f => f.Result.Stale),
        };
    }

    /// <param name="section">The property under "data" holding the records, or null when "data" itself is the dictionary.</param>
    internal static Dictionary<string, T> Read<T>(CachedResponse payload, CachedResponse language, CachedResponse english, string? section,
        JsonTypeInfo<Dictionary<string, T>> type)
    {
        var root = JsonNode.Parse(File.ReadAllText(payload.FilePath)) ?? throw new JsonException("empty payload");
        var paths = root["translations"] is JsonArray p ? p.Select(x => x?.GetValue<string>() ?? "").ToList() : [];
        var data = root["data"];
        var records = section is null ? data : data?[section];
        var lang = JsonTranslator.ReadDictionary(File.ReadAllText(language.FilePath));
        var en = ReferenceEquals(language, english) ? null : JsonTranslator.ReadDictionary(File.ReadAllText(english.FilePath));
        JsonTranslator.Translate(records, JsonTranslator.TranslatableProperties(paths), lang, en);
        return records?.Deserialize(type) ?? [];
    }
}
