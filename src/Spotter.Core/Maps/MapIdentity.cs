namespace Spotter.Core.Maps;

/// <summary>A map as tarkov.dev's API names it.</summary>
/// <param name="Id">Game id, e.g. "5714dc692459777137212e12".</param>
/// <param name="NormalizedName">e.g. "streets-of-tarkov"; also the key into maps.json.</param>
/// <param name="NameId">The game's location id, e.g. "TarkovStreets" or "bigmap".</param>
/// <param name="ScenePath">e.g. "maps/city_preset.bundle", exactly as the game logs it.</param>
public sealed record MapIdentity(string Id, string NormalizedName, string NameId, string? ScenePath, string Name);

/// <summary>
/// Works out which map the logs are talking about: scene path first, then the location id, then a small
/// alias list for names seen in logs that the API does not carry.
/// </summary>
public sealed class MapResolver(IEnumerable<MapIdentity> maps)
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // rcid / location ids seen in real logs → normalizedName
        ["bigmap"] = "customs",
        ["city"] = "streets-of-tarkov",
        ["shopping_mall"] = "interchange",
        ["factory_day"] = "factory",
        ["factory_night"] = "night-factory",
        ["sandbox_sl"] = "ground-zero-tutorial",
        ["sandbox_start"] = "ground-zero-tutorial",
        ["rezerv_base"] = "reserve",
    };

    private readonly List<MapIdentity> _maps = maps.ToList();

    public IReadOnlyList<MapIdentity> Maps => _maps;

    public MapIdentity? Resolve(string? scenePath, string? locationId = null)
    {
        if (!string.IsNullOrEmpty(scenePath) &&
            _maps.FirstOrDefault(m => string.Equals(m.ScenePath, scenePath, StringComparison.OrdinalIgnoreCase)) is { } byScene)
            return byScene;

        if (!string.IsNullOrEmpty(locationId) &&
            _maps.FirstOrDefault(m => string.Equals(m.NameId, locationId, StringComparison.OrdinalIgnoreCase)) is { } byId)
            return byId;

        foreach (var candidate in new[] { locationId, SceneStem(scenePath) })
        {
            if (candidate is not null && Aliases.TryGetValue(candidate, out var normalized) &&
                _maps.FirstOrDefault(m => m.NormalizedName == normalized) is { } byAlias)
                return byAlias;
        }
        return null;
    }

    public MapIdentity? ByNormalizedName(string normalizedName) =>
        _maps.FirstOrDefault(m => string.Equals(m.NormalizedName, normalizedName, StringComparison.OrdinalIgnoreCase));

    public MapIdentity? ById(string id) => _maps.FirstOrDefault(m => m.Id == id);

    // "maps/city_preset.bundle" → "city"
    private static string? SceneStem(string? scenePath)
    {
        if (string.IsNullOrEmpty(scenePath))
            return null;
        var name = scenePath.Split('/').Last();
        foreach (var suffix in new[] { ".bundle", "_preset", "-preset" })
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                name = name[..^suffix.Length];
        }
        return name;
    }
}
