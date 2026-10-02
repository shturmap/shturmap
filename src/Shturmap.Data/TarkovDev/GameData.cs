using Shturmap.Core;
using Shturmap.Core.Logs;
using Shturmap.Core.Maps;

namespace Shturmap.Data.TarkovDev;

/// <summary>Everything Shturmap knows about the game world for one mode and language.</summary>
public sealed class GameData
{
    public required GameMode Mode { get; init; }

    public required string Language { get; init; }

    /// <summary>The language asked for when tarkov.dev gave no texts in it and English came instead, or null.</summary>
    public string? MissingLanguage { get; init; }

    public required IReadOnlyDictionary<string, ApiMap> Maps { get; init; }

    public required IReadOnlyDictionary<string, ApiTask> Tasks { get; init; }

    public required IReadOnlyDictionary<string, ApiTrader> Traders { get; init; }

    /// <summary>Bosses and other AI types by id ("bossBoar" → "Kaban").</summary>
    public IReadOnlyDictionary<string, ApiMob> Mobs { get; init; } = new Dictionary<string, ApiMob>();

    /// <summary>Item and quest-item names by id, in the game's language.</summary>
    public IReadOnlyDictionary<string, string> ItemNames { get; init; } = new Dictionary<string, string>();

    public string ItemName(string id) => ItemNames.TryGetValue(id, out var name) ? name : "Unknown item";

    /// <summary>Extracts' internal names by id ("Alpinist" for Cliff Descent), from before translation.</summary>
    public IReadOnlyDictionary<string, string> ExtractKeys { get; init; } = new Dictionary<string, string>();

    /// <summary>Bosses that can spawn on a map, strongest chance first (AI PMC squads are left out).</summary>
    public IReadOnlyList<(string Name, double Chance)> BossesOn(string mapId) =>
        Maps.TryGetValue(mapId, out var map) && map.Bosses is { } bosses
            ? bosses.Where(b => b.Mob.StartsWith("boss", StringComparison.Ordinal))
                .GroupBy(b => b.Mob)
                .Select(g => (Name: Mobs.TryGetValue(g.Key, out var mob) ? mob.Name : g.Key, Chance: g.Max(b => b.SpawnChance)))
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
