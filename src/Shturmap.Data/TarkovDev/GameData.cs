using Shturmap.Core;
using Shturmap.Core.Logs;
using Shturmap.Core.Maps;

namespace Shturmap.Data.TarkovDev;

/// <summary>A language whose texts couldn't be loaded, and why (<see cref="GameData.LanguageFailure"/>).</summary>
/// <param name="Language">tarkov.dev's code for the language asked for ("de").</param>
public sealed record LanguageFailure(string Language, LoadProblem Why);

/// <summary>Everything Shturmap knows about the game world for one mode and language.</summary>
public sealed class GameData
{
    public required GameMode Mode { get; init; }

    public required string Language { get; init; }

    /// <summary>The language asked for when tarkov.dev gave no texts in it and English came instead, or null.</summary>
    public string? MissingLanguage { get; init; }

    /// <summary>
    /// Set when the texts of the language asked for couldn't be loaded and English came instead: which language, and
    /// why (no connection, a timeout, a server error, with no saved copy). Unlike <see cref="MissingLanguage"/>,
    /// tarkov.dev may well have them: the session says so and asks again later.
    /// </summary>
    public LanguageFailure? LanguageFailure { get; init; }

    public required IReadOnlyDictionary<string, ApiMap> Maps { get; init; }

    public required IReadOnlyDictionary<string, ApiTask> Tasks { get; init; }

    public required IReadOnlyDictionary<string, ApiTrader> Traders { get; init; }

    /// <summary>Bosses and other AI types by id ("bossBoar" → "Kaban").</summary>
    public IReadOnlyDictionary<string, ApiMob> Mobs { get; init; } = new Dictionary<string, ApiMob>();

    /// <summary>Item and quest-item names by id, in the game's language.</summary>
    public IReadOnlyDictionary<string, string> ItemNames { get; init; } = new Dictionary<string, string>();

    public string ItemName(string id) => ItemNames.TryGetValue(id, out var name) ? name : DataTexts.ItemUnknown;

    /// <summary>Items' short names by id, as the game prints them on an item ("TGL MO", "314 marked").</summary>
    public IReadOnlyDictionary<string, string> ItemShortNames { get; init; } = new Dictionary<string, string>();

    /// <summary>An item's short name, else its full name.</summary>
    public string ItemShortName(string id) => ItemShortNames.TryGetValue(id, out var name) ? name : ItemName(id);

    /// <summary>Extracts' internal names by id ("Alpinist" for Cliff Descent), from before translation.</summary>
    public IReadOnlyDictionary<string, string> ExtractKeys { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Extracts' and switches' English names by id, whatever the game's language. A rule that reads a name's words
    /// ("(Flare)", "(Co-op)", a switch named after its extract) reads these: other languages translate the words away
    /// or drop them (German has "Mira-Allee" for "Mira Ave (Flare)").
    /// </summary>
    public IReadOnlyDictionary<string, string> EnglishNames { get; init; } = new Dictionary<string, string>();

    /// <summary>An extract's or switch's English name: as recorded at load, else the name it carries (English data).</summary>
    public string EnglishName(string id, string? name) => EnglishNames.TryGetValue(id, out var english) ? english : name ?? "";

    /// <summary>
    /// Extracts' names by id in the game's own language, when the data is in another (the player chose another language
    /// for Shturmap, or the pseudo-language): the game's extract list in a screenshot names them so. Empty when the data
    /// is in the game's language, the game's is English, or its texts couldn't be had.
    /// </summary>
    public IReadOnlyDictionary<string, string> GameNames { get; init; } = new Dictionary<string, string>();

    /// <summary>tarkov.dev's code for the language of <see cref="GameNames"/> ("de"), or null when there are none.</summary>
    public string? GameNamesLanguage { get; init; }

    /// <summary>Objectives' kill targets, exit statuses and set kill conditions by objective id, from before translation.</summary>
    public IReadOnlyDictionary<string, ObjectiveFacts> ObjectiveFacts { get; init; } = new Dictionary<string, ObjectiveFacts>();

    /// <summary>Bosses that can spawn on a map, strongest chance first (AI PMC squads are left out).</summary>
    public IReadOnlyList<(string Name, double Chance)> BossesOn(string mapId) =>
        BossMobsOn(mapId).Select(b => (b.Name, b.Chance)).ToList();

    /// <summary>The same list with each boss's id in the data ("bossBoar"), which its markers' groups are made of:
    /// a boss's name in a line of text can then light its spawn zones on the map.</summary>
    public IReadOnlyList<(string Mob, string Name, double Chance)> BossMobsOn(string mapId) =>
        Maps.TryGetValue(mapId, out var map) && map.Bosses is { } bosses
            ? bosses.Where(b => b.Mob.StartsWith("boss", StringComparison.Ordinal))
                .GroupBy(b => b.Mob)
                .Select(g => (Mob: g.Key, Name: Mobs.TryGetValue(g.Key, out var mob) ? mob.Name : g.Key, Chance: g.Max(b => b.SpawnChance)))
                .OrderByDescending(b => b.Chance)
                .ToList()
            : [];

    public required IReadOnlyList<MapDefinition> MapDefinitions { get; init; }

    /// <summary>When the data was last confirmed current with tarkov.dev.</summary>
    public required DateTimeOffset CheckedAt { get; init; }

    /// <summary>True if tarkov.dev could not be reached and cached data is shown.</summary>
    public bool Offline { get; init; }

    public MapResolver CreateResolver() =>
        new(Maps.Values.Select(m => new MapIdentity(m.Id, m.NormalizedName, m.NameId, m.ScenePath, m.Name)));

    /// <summary>
    /// The artwork definition for a map. Variants without their own entry use their base map's
    /// ("ground-zero-tutorial" → "ground-zero").
    /// </summary>
    public MapDefinition? DefinitionFor(string normalizedName) =>
        MapDefinitions.FirstOrDefault(d => d.Matches(normalizedName)) ??
        MapDefinitions.Where(d => normalizedName.StartsWith(d.Key + "-", StringComparison.OrdinalIgnoreCase))
            .MaxBy(d => d.Key.Length);

    public ApiMap? MapByNormalizedName(string normalizedName) =>
        Maps.Values.FirstOrDefault(m => string.Equals(m.NormalizedName, normalizedName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Map ids drawn with the same artwork (e.g. Ground Zero and Ground Zero 21+).</summary>
    public IReadOnlySet<string> MapIdsSharing(string normalizedName)
    {
        var definition = DefinitionFor(normalizedName);
        return Maps.Values
            .Where(m => definition is null ? m.NormalizedName == normalizedName : definition.Matches(m.NormalizedName))
            .Select(m => m.Id)
            .ToHashSet();
    }

    public string TraderName(string? traderId) =>
        traderId is not null && Traders.TryGetValue(traderId, out var t) ? t.Name : "";

    private Dictionary<string, List<(string MapId, WorldPoint Position)>>? _spawns;

    /// <summary>Where an item can spawn as loose loot, per map (from the maps payload).</summary>
    public IReadOnlyList<(string MapId, WorldPoint Position)> SpawnsOf(string itemId)
    {
        var spawns = LazyInitializer.EnsureInitialized(ref _spawns, () =>
        {
            var index = new Dictionary<string, List<(string, WorldPoint)>>(StringComparer.Ordinal);
            foreach (var map in Maps.Values)
            {
                foreach (var spawn in map.LootLoose ?? [])
                {
                    if (spawn.Position is not { } p)
                        continue;
                    foreach (var item in spawn.Items ?? [])
                    {
                        if (!index.TryGetValue(item, out var list))
                            index[item] = list = [];
                        list.Add((map.Id, p.ToWorld()));
                    }
                }
            }
            return index;
        });
        return spawns.TryGetValue(itemId, out var found) ? found : [];
    }

    public static string Slug(GameMode mode) => mode switch
    {
        GameMode.Pve => "pve",
        GameMode.Seasonal => "pvp-season",
        _ => "regular",
    };
}
