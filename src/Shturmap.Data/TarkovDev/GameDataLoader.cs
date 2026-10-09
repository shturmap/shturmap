using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Shturmap.Core;
using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Data.Http;

namespace Shturmap.Data.TarkovDev;

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

    /// <param name="language">The game's language code, as its settings say it ("ge" for German).</param>
    public async Task<GameData> LoadAsync(GameMode mode, string language, CancellationToken ct = default)
    {
        var fetched = new List<Task<CachedResponse>>();
        try
        {
            return await LoadAsync(mode, language, fetched, ct);
        }
        catch (Exception e) when (LoadProblem.Explain(e).Kind == LoadFailure.Unreadable)
        {
            if (ForgetWhatIsNoJson(fetched) is { Count: > 0 } forgotten)
                throw new NotDataException(forgotten, e);
            throw;
        }
    }

    private async Task<GameData> LoadAsync(GameMode mode, string language, List<Task<CachedResponse>> fetched, CancellationToken ct)
    {
        var slug = GameData.Slug(mode);
        language = ApiLanguage(language);

        var fetches = new List<Task<CachedResponse>>();
        var translations = new List<Task<CachedResponse>>();
        Task<CachedResponse> Fetch(string endpoint, TimeSpan? maxAge = null, bool translation = false)
        {
            var task = http.GetAsync(new Uri(JsonApi, endpoint), endpoint.Replace('/', '_') + ".json", maxAge ?? MaxAge, ct, json: true);
            (translation ? translations : fetches).Add(task);
            fetched.Add(task);
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
        var mapsLang = language == "en" ? mapsEn : Fetch($"{slug}/maps_{language}", translation: true);
        var tasksLang = language == "en" ? tasksEn : Fetch($"{slug}/tasks_{language}", translation: true);
        var tradersLang = language == "en" ? tradersEn : Fetch($"{slug}/traders_{language}", translation: true);
        var itemsLang = language == "en" ? itemsEn : Fetch($"{slug}/items_{language}", TimeSpan.FromHours(24), translation: true);
        var definitions = http.GetAsync(MapDefinitionsUri, "tarkov-dev_maps.json", TimeSpan.FromHours(24), ct, json: true);
        fetches.Add(definitions);
        fetched.Add(definitions);
        try
        {
            await Task.WhenAll(fetches);
        }
        catch
        {
            Observe(translations);
            throw;
        }
        string? missing = null;
        LanguageFailure? failed = null;
        try
        {
            if (await Arrived(translations))
                fetches.AddRange(translations);
            else
                missing = language; // tarkov.dev has no text in this language
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            // The texts didn't come, for a reason that says nothing about whether tarkov.dev has them (no connection,
            // a timeout, a server error, and no saved copy). The data loads in English and says why, so the session
            // can tell the player and ask again; failing the whole load left a player with no data at all for as
            // long as one translation file kept failing.
            failed = new LanguageFailure(language, LoadProblem.Explain(e));
        }
        if (missing is not null || failed is not null)
        {
            // Everything comes in English, so the data stays one language.
            language = "en";
            (mapsLang, tasksLang, tradersLang, itemsLang) = (mapsEn, tasksEn, tradersEn, itemsEn);
        }

        // Extract names arrive as the game's internal keys ("Alpinist", "RedRebel_alp") and are translated; the keys
        // tell some requirements that no field does, so they are kept. So are the English names of extracts and
        // switches: the rules that read a name's words ("(Flare)", "(Co-op)") must find them in every game language.
        var extractKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        var nameKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        var mapsData = Translated(maps.Result, mapsLang.Result, mapsEn.Result, raw =>
        {
            if (raw?["maps"] is not JsonObject all)
                return;
            foreach (var (_, map) in all)
            {
                foreach (var extract in (map?["extracts"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    if (extract["id"]?.GetValue<string>() is { } id && extract["name"]?.GetValue<string>() is { } key)
                    {
                        extractKeys.TryAdd(id, key);
                        nameKeys.TryAdd(id, key);
                    }
                }
                foreach (var toggle in (map?["switches"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    if (toggle["id"]?.GetValue<string>() is { } id && toggle["name"]?.GetValue<string>() is { } key)
                        nameKeys.TryAdd(id, key);
                }
            }
        }, alsoTranslate: ["conditions"]);
        var mapTextsEn = JsonTranslator.ReadDictionary(await File.ReadAllTextAsync(mapsEn.Result.FilePath, ct));
        var englishNames = nameKeys.ToDictionary(n => n.Key, n => JsonTranslator.Text(n.Value, mapTextsEn), StringComparer.Ordinal);
        // Kill targets and exit statuses are translated too ("Savage" becomes "Scavs", or German); the plan's effort
        // groups need the keys, which mean the same in every language.
        var objectiveFacts = new Dictionary<string, ObjectiveFacts>(StringComparer.Ordinal);
        var tasksData = Translated(tasks.Result, tasksLang.Result, tasksEn.Result, raw =>
        {
            if (raw?["tasks"] is not JsonObject all)
                return;
            foreach (var (_, task) in all)
            {
                foreach (var objective in (task?["objectives"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    if (objective["id"]?.GetValue<string>() is { } id && Facts(objective) is { } facts)
                        objectiveFacts.TryAdd(id, facts);
                }
            }
        });
        var tradersData = Translated(traders.Result, tradersLang.Result, tradersEn.Result);

        var itemTexts = JsonTranslator.ReadDictionary(await File.ReadAllTextAsync(itemsLang.Result.FilePath, ct));
        var itemNames = ItemNames(itemTexts);
        var shortNames = ItemNames(itemTexts, ShortNameSuffix);
        if (!ReferenceEquals(itemsLang.Result, itemsEn.Result))
        {
            var english = JsonTranslator.ReadDictionary(await File.ReadAllTextAsync(itemsEn.Result.FilePath, ct));
            foreach (var (id, name) in ItemNames(english))
                itemNames.TryAdd(id, name);
            foreach (var (id, name) in ItemNames(english, ShortNameSuffix))
                shortNames.TryAdd(id, name);
        }
        foreach (var questItem in Section(tasksData, "questItems", ApiJsonContext.Default.DictionaryStringApiQuestItem).Values)
            itemNames[questItem.Id] = questItem.Name;

        return new GameData
        {
            Mode = mode,
            Language = language,
            MissingLanguage = missing,
            LanguageFailure = failed,
            Maps = Section(mapsData, "maps", ApiJsonContext.Default.DictionaryStringApiMap),
            Mobs = Section(mapsData, "mobs", ApiJsonContext.Default.DictionaryStringApiMob),
            Tasks = Section(tasksData, "tasks", ApiJsonContext.Default.DictionaryStringApiTask),
            Traders = Section(tradersData, null, ApiJsonContext.Default.DictionaryStringApiTrader),
            ItemNames = itemNames,
            ItemShortNames = shortNames,
            ExtractKeys = extractKeys,
            EnglishNames = englishNames,
            ObjectiveFacts = objectiveFacts,
            MapDefinitions = MapDefinitionReader.Read(await File.ReadAllTextAsync(definitions.Result.FilePath, ct)),
            CheckedAt = fetches.Min(f => f.Result.FetchedAt),
            Offline = fetches.Any(f => f.Result.Stale),
        };
    }

    /// <summary>
    /// An objective's targets, exit statuses, exit and set kill conditions, read as it arrived (before translation), or null
    /// when it has none. A kill condition counts as set when it narrows the kill: a weapon or weapon mods, body
    /// parts, a distance above 0 (tarkov.dev writes <c>{"value":0,"compareMethod":"&gt;="}</c>, or null, for none),
    /// gear worn or not worn, a time of day (both hours 0 for none), a health effect on the player or the enemy, or
    /// zones (an area of the map, not the map itself, which is <c>maps</c>).
    /// </summary>
    internal static ObjectiveFacts? Facts(JsonObject objective)
    {
        static List<string> Strings(JsonNode? node) =>
            (node as JsonArray ?? []).Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).OfType<string>().ToList();
        static bool Listed(JsonNode? node) => node is JsonArray { Count: > 0 };
        static double Number(JsonNode? node) => node is JsonValue v && v.TryGetValue<double>(out var d) ? d : 0;

        var targets = Strings(objective["targetNames"]);
        var status = Strings(objective["exitStatus"]);
        var exit = objective["type"]?.GetValue<string>() == "extract" && objective["exitName"] is JsonValue e && e.TryGetValue<string>(out var name)
                   && name.Length > 0 ? name : null;
        var conditions = new List<string>();
        if (objective["type"]?.GetValue<string>() == "shoot")
        {
            if (Listed(objective["usingWeapon"]))
                conditions.Add("weapon");
            if (Listed(objective["usingWeaponMods"]))
                conditions.Add("weapon mods");
            if (Listed(objective["bodyParts"]))
                conditions.Add("body parts");
            if (Number(objective["distance"]?["value"]) > 0)
                conditions.Add("distance");
            if (Listed(objective["wearing"]))
                conditions.Add("wearing");
            if (Listed(objective["notWearing"]))
                conditions.Add("not wearing");
            if (Number(objective["timeFromHour"]) != 0 || Number(objective["timeUntilHour"]) != 0)
                conditions.Add("time of day");
            if (objective["playerHealthEffect"] is JsonObject)
                conditions.Add("player health");
            if (objective["enemyHealthEffect"] is JsonObject)
                conditions.Add("enemy health");
            if (Listed(objective["zones"]))
                conditions.Add("zone");
        }
        return targets.Count + status.Count + conditions.Count > 0 || exit is not null ? new ObjectiveFacts(targets, status, conditions, exit) : null;
    }

    /// <summary>
    /// Where items come from: trader offers, barters, crafts and hideout station names. Loaded after the rest, in
    /// the background: the item payload is large (17 MB) and only the item cards need it. Refreshed once a day.
    /// </summary>
    public async Task<ItemSources> LoadSourcesAsync(GameMode mode, string language, CancellationToken ct = default)
    {
        var fetched = new List<Task<CachedResponse>>();
        try
        {
            return await LoadSourcesAsync(mode, language, fetched, ct);
        }
        catch (Exception e) when (LoadProblem.Explain(e).Kind == LoadFailure.Unreadable)
        {
            if (ForgetWhatIsNoJson(fetched) is { Count: > 0 } forgotten)
                throw new NotDataException(forgotten, e);
            throw;
        }
    }

    private async Task<ItemSources> LoadSourcesAsync(GameMode mode, string language, List<Task<CachedResponse>> fetched, CancellationToken ct)
    {
        var slug = GameData.Slug(mode);
        language = ApiLanguage(language);
        var day = TimeSpan.FromHours(24);
        Task<CachedResponse> Fetch(string endpoint)
        {
            var task = http.GetAsync(new Uri(JsonApi, endpoint), endpoint.Replace('/', '_') + ".json", day, ct, json: true);
            fetched.Add(task);
            return task;
        }

        var items = Fetch($"{slug}/items");
        var barters = Fetch($"{slug}/barters");
        var crafts = Fetch($"{slug}/crafts");
        var hideout = Fetch($"{slug}/hideout");
        var hideoutEn = Fetch($"{slug}/hideout_en");
        var hideoutLang = language == "en" ? hideoutEn : Fetch($"{slug}/hideout_{language}");
        try
        {
            await Task.WhenAll(items, barters, crafts, hideout, hideoutEn);
        }
        catch
        {
            Observe([hideoutLang]);
            throw;
        }
        if (!await Arrived([hideoutLang]))
            hideoutLang = hideoutEn;

        ApiItemsEnvelope? itemsData;
        await using (var stream = File.OpenRead(items.Result.FilePath))
            itemsData = await JsonSerializer.DeserializeAsync(stream, ApiJsonContext.Default.ApiItemsEnvelope, ct);
        ApiBartersEnvelope? bartersData;
        await using (var stream = File.OpenRead(barters.Result.FilePath))
            bartersData = await JsonSerializer.DeserializeAsync(stream, ApiJsonContext.Default.ApiBartersEnvelope, ct);
        ApiCraftsEnvelope? craftsData;
        await using (var stream = File.OpenRead(crafts.Result.FilePath))
            craftsData = await JsonSerializer.DeserializeAsync(stream, ApiJsonContext.Default.ApiCraftsEnvelope, ct);
        var stations = Read(hideout.Result, hideoutLang.Result, hideoutEn.Result, null, ApiJsonContext.Default.DictionaryStringApiStation);

        return new ItemSources
        {
            Items = itemsData?.Data?.Items ?? [],
            Barters = (bartersData?.Data ?? []).Where(b => b.OfferedItem is not null).ToLookup(b => b.OfferedItem!.Item),
            Crafts = (craftsData?.Data ?? []).Where(c => c.ProductItem is not null).ToLookup(c => c.ProductItem!.Item),
            Stations = stations.Values.ToDictionary(s => s.Id, s => s.Name),
        };
    }

    /// <summary>
    /// tarkov.dev's code for the game's language (<see cref="GameLanguage"/>), English when the game names none. The
    /// game names some languages its own way, and tarkov.dev answers those with 404 (seen 2026-10-02: a German game
    /// asked for "maps_ge", and no data loaded at all).
    /// </summary>
    public static string ApiLanguage(string? gameLanguage) => GameLanguage.Common(gameLanguage) ?? "en";

    // Whether every translation arrived; false when tarkov.dev has no texts in the language, which only "not found"
    // says (the load then goes on in English). A translation that fails any other way (no connection, a timeout, 5xx;
    // with a saved copy CachedHttp has used that already) throws that failure: it is not a missing language, and
    // reading it as one put "No German texts on tarkov.dev" on a moment's 503 (the review of 2026-10-04). The game
    // data then loads in English with the failure named (GameData.LanguageFailure); the item sources fail as a
    // whole and are asked for again.
    private static async Task<bool> Arrived(IReadOnlyList<Task<CachedResponse>> translations)
    {
        try
        {
            await Task.WhenAll(translations);
            return true;
        }
        catch (Exception)
        {
            // All of them have ended. The failure worth telling comes first: awaiting them together throws whichever
            // failed first in the list, which may be the 404 beside a 503.
            var failures = translations.Where(t => t.IsFaulted).SelectMany(t => t.Exception!.InnerExceptions).ToList();
            if (failures.FirstOrDefault(e => e is not HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound }) is { } other)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(other);
            // A timeout, or the caller stopping.
            if (translations.FirstOrDefault(t => t.IsCanceled) is { } cancelled)
                await cancelled;
            return false;
        }
    }

    // A load that fails leaves the downloads it never came to await, the translations, still on their way. Their own
    // failures (usually the same lost connection) would surface later as unobserved task exceptions, which the app
    // notes as crash records and asks about at the next start: so they are looked at here.
    private static void Observe(IEnumerable<Task> tasks)
    {
        foreach (var task in tasks)
            _ = task.ContinueWith(static t => _ = t.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    // A load that couldn't read what it got (review of 2026-10-09). An answer that isn't JSON at all is no longer saved
    // (CachedHttp, json): a saved copy stays and is used, and without one the load fails as NotData. A page saved before
    // that (a captive portal's or a CDN's, sent with 200, or a body cut short) failed as unreadable, which isn't tried
    // again, and was read from the cache again at every start for as long as it counted as fresh. Such a file is
    // forgotten, so the next try or start downloads it again, and the load fails as NotData, which the session tries
    // again by itself (owner, 2026-10-09). One that is JSON, only not in the shape
    // Shturmap knows (tarkov.dev changed its format), stays, and the load stays unreadable: a download would bring the
    // same again, at every start of every Shturmap, and the saved copy is still revalidated with its ETag. A download
    // that failed is no file of the load: a good saved copy is never forgotten for a network error. Returns the names
    // of the files forgotten.
    private List<string> ForgetWhatIsNoJson(IEnumerable<Task<CachedResponse>> fetched)
    {
        var forgotten = new List<string>();
        foreach (var response in fetched.Where(t => t.IsCompletedSuccessfully).Select(t => t.Result).DistinctBy(r => r.FilePath))
        {
            if (CachedHttp.IsJson(response.FilePath))
                continue;
            http.Forget(response);
            forgotten.Add(Path.GetFileName(response.FilePath));
        }
        return forgotten;
    }

    /// <summary>The payload's "data" with every translatable string replaced by the chosen language's text.</summary>
    /// <param name="beforeTranslation">Sees the data as it arrived, with the game's keys in place of text.</param>
    /// <param name="alsoTranslate">Properties holding keys that the payload's own list misses (transit "conditions").</param>
    internal static JsonNode? Translated(CachedResponse payload, CachedResponse language, CachedResponse english,
        Action<JsonNode?>? beforeTranslation = null, IEnumerable<string>? alsoTranslate = null)
    {
        var root = JsonNode.Parse(File.ReadAllText(payload.FilePath)) ?? throw new JsonException("empty payload");
        var paths = root["translations"] is JsonArray p ? p.Select(x => x?.GetValue<string>() ?? "").ToList() : [];
        var data = root["data"];
        beforeTranslation?.Invoke(data);
        var lang = JsonTranslator.ReadDictionary(File.ReadAllText(language.FilePath));
        var en = ReferenceEquals(language, english) ? null : JsonTranslator.ReadDictionary(File.ReadAllText(english.FilePath));
        var properties = JsonTranslator.TranslatableProperties(paths);
        properties.UnionWith(alsoTranslate ?? []);
        JsonTranslator.Translate(data, properties, lang, en);
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

    private const string NameSuffix = " Name", ShortNameSuffix = " ShortName";

    // items_en maps "<id> Name" and "<id> ShortName" to text (item ids are 24 characters).
    private static Dictionary<string, string> ItemNames(Dictionary<string, string> translations, string suffix = NameSuffix)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, text) in translations)
        {
            if (key.Length == 24 + suffix.Length && key.EndsWith(suffix, StringComparison.Ordinal) && !string.IsNullOrEmpty(text))
                names[key[..24]] = text;
        }
        return names;
    }
}
