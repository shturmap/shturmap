using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Spotter.Core.Logs;
using Spotter.Core.Maps;
using Spotter.Data.Http;

namespace Spotter.Data.TarkovDev;

/// <summary>
/// Loads tasks, maps, traders and item names from json.tarkov.dev (plus tarkov.dev's maps.json for map geometry),
/// translated into the game's language. Cached copies are revalidated at most once an hour; offline, the cache is
/// used. Nothing is bundled: see docs/DESIGN.md §3.
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
        Task<CachedResponse> Fetch(string endpoint, TimeSpan? maxAge = null)
        {
            var task = http.GetAsync(new Uri(JsonApi, endpoint), endpoint.Replace('/', '_') + ".json", maxAge ?? MaxAge, ct);
            fetches.Add(task);
            return task;
        }

        var maps = Fetch($"{slug}/maps");
        var tasks = Fetch($"{slug}/tasks");
        var traders = Fetch($"{slug}/traders");
        var mapsEn = Fetch($"{slug}/maps_en");
        var tasksEn = Fetch($"{slug}/tasks_en");
        var tradersEn = Fetch($"{slug}/traders_en");
        // Item names only (1.6 MB); the full item payload is ten times larger and not needed.
        var itemsEn = Fetch($"{slug}/items_en", TimeSpan.FromHours(24));
        var mapsLang = language == "en" ? mapsEn : Fetch($"{slug}/maps_{language}");
        var tasksLang = language == "en" ? tasksEn : Fetch($"{slug}/tasks_{language}");
        var tradersLang = language == "en" ? tradersEn : Fetch($"{slug}/traders_{language}");
        var itemsLang = language == "en" ? itemsEn : Fetch($"{slug}/items_{language}", TimeSpan.FromHours(24));
        var definitions = http.GetAsync(MapDefinitionsUri, "tarkov-dev_maps.json", TimeSpan.FromHours(24), ct);
        fetches.Add(definitions);
        await Task.WhenAll(fetches);

        var mapsData = Translated(maps.Result, mapsLang.Result, mapsEn.Result);
        var tasksData = Translated(tasks.Result, tasksLang.Result, tasksEn.Result);
        var tradersData = Translated(traders.Result, tradersLang.Result, tradersEn.Result);

        var itemNames = ItemNames(JsonTranslator.ReadDictionary(await File.ReadAllTextAsync(itemsLang.Result.FilePath, ct)));
        if (!ReferenceEquals(itemsLang.Result, itemsEn.Result))
        {
            foreach (var (id, name) in ItemNames(JsonTranslator.ReadDictionary(await File.ReadAllTextAsync(itemsEn.Result.FilePath, ct))))
                itemNames.TryAdd(id, name);
        }
        foreach (var questItem in Section(tasksData, "questItems", ApiJsonContext.Default.DictionaryStringApiQuestItem).Values)
            itemNames[questItem.Id] = questItem.Name;

        return new GameData
        {
            Mode = mode,
            Language = language,
            Maps = Section(mapsData, "maps", ApiJsonContext.Default.DictionaryStringApiMap),
            Mobs = Section(mapsData, "mobs", ApiJsonContext.Default.DictionaryStringApiMob),
            Tasks = Section(tasksData, "tasks", ApiJsonContext.Default.DictionaryStringApiTask),
            Traders = Section(tradersData, null, ApiJsonContext.Default.DictionaryStringApiTrader),
            ItemNames = itemNames,
            MapDefinitions = MapDefinitionReader.Read(await File.ReadAllTextAsync(definitions.Result.FilePath, ct)),
            CheckedAt = fetches.Min(f => f.Result.FetchedAt),
            Offline = fetches.Any(f => f.Result.Stale),
        };
    }

    /// <summary>The payload's "data" with every translatable string replaced by the chosen language's text.</summary>
    internal static JsonNode? Translated(CachedResponse payload, CachedResponse language, CachedResponse english)
    {
        var root = JsonNode.Parse(File.ReadAllText(payload.FilePath)) ?? throw new JsonException("empty payload");
        var paths = root["translations"] is JsonArray p ? p.Select(x => x?.GetValue<string>() ?? "").ToList() : [];
        var data = root["data"];
        var lang = JsonTranslator.ReadDictionary(File.ReadAllText(language.FilePath));
        var en = ReferenceEquals(language, english) ? null : JsonTranslator.ReadDictionary(File.ReadAllText(english.FilePath));
        JsonTranslator.Translate(data, JsonTranslator.TranslatableProperties(paths), lang, en);
        return data;
    }

    /// <param name="name">The property under "data" holding the records, or null when "data" itself is the dictionary.</param>
    internal static Dictionary<string, T> Section<T>(JsonNode? data, string? name, JsonTypeInfo<Dictionary<string, T>> type)
    {
        var records = name is null ? data : data?[name];
        return records is JsonObject ? records.Deserialize(type) ?? [] : [];
    }

    internal static Dictionary<string, T> Read<T>(CachedResponse payload, CachedResponse language, CachedResponse english, string? section,
        JsonTypeInfo<Dictionary<string, T>> type) =>
        Section(Translated(payload, language, english), section, type);

    // items_en maps "<id> Name" and "<id> ShortName" to text.
    private static Dictionary<string, string> ItemNames(Dictionary<string, string> translations)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, text) in translations)
        {
            if (key.Length == 29 && key.EndsWith(" Name", StringComparison.Ordinal) && !string.IsNullOrEmpty(text))
                names[key[..24]] = text;
        }
        return names;
    }
}
