using Spotter.Core.Logs;
using Spotter.Core.Maps;

namespace Spotter.Data.TarkovDev;

/// <summary>Everything Spotter knows about the game world for one mode and language.</summary>
public sealed class GameData
{
    public required GameMode Mode { get; init; }

    public required string Language { get; init; }

    public required IReadOnlyDictionary<string, ApiMap> Maps { get; init; }

    public required IReadOnlyDictionary<string, ApiTask> Tasks { get; init; }

    public required IReadOnlyDictionary<string, ApiTrader> Traders { get; init; }

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

    public static string Slug(GameMode mode) => mode switch
    {
        GameMode.Pve => "pve",
        GameMode.Seasonal => "pvp-season",
        _ => "regular",
    };
}
