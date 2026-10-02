using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Map.Tests;

internal static class Fixtures
{
    private static readonly Lazy<IReadOnlyList<MapDefinition>> Definitions =
        new(() => MapDefinitionReader.Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "tarkov-dev", "maps.json"))));

    /// <summary>A map definition from tarkov.dev's maps.json (MIT, see tests/fixtures/tarkov-dev/README.md).</summary>
    public static MapDefinition Definition(string key) => Definitions.Value.Single(m => m.Key == key);

    /// <summary>Game data holding one hand-made map: no tarkov.dev payload is stored in the repository.</summary>
    public static GameData With(ApiMap map, params ApiMob[] mobs) => new()
    {
        Mode = GameMode.Pve,
        Language = "en",
        Maps = new Dictionary<string, ApiMap> { [map.Id] = map },
        Tasks = new Dictionary<string, ApiTask>(),
        Traders = new Dictionary<string, ApiTrader>(),
        Mobs = mobs.ToDictionary(m => m.Id),
        MapDefinitions = Definitions.Value,
        CheckedAt = DateTimeOffset.UnixEpoch,
    };

    public static ApiMap TestMap(string normalizedName, List<ApiSpawn>? spawns = null, List<ApiBoss>? bosses = null) =>
        new("map-1", "Test map", normalizedName, "test", null, null, 40, [], [], [], [], bosses ?? [], Spawns: spawns ?? []);

    public static ApiPosition At(double x, double z, double y = 0) => new(x, y, z);
}

internal static class TestView
{
    /// <summary>A sheet where one metre is one pixel: world x grows to the right, world z upward, the origin in the middle.</summary>
    public static (Camera Camera, MapScene Scene) Of(IReadOnlyList<MapMarker> markers, params Shturmap.Core.Maps.MapLabel[] names)
    {
        var definition = new MapDefinition
        {
            Key = "test",
            Transform = [1, 0, 1, 0],
            Bounds = new WorldBox(-500, -500, 500, 500),
            Labels = names,
        };
        var camera = new Camera();
        camera.Resize(new SkiaSharp.SKSize(1000, 1000));
        camera.Restore(new MapPoint(0, 0), 1);
        return (camera, new MapScene(definition, null) { Markers = markers });
    }

    public static MapMarker Quest(string id, double x, double z, string name, MarkerKind kind = MarkerKind.Objective, string? group = null) =>
        new($"objective:{id}:1", kind, new Shturmap.Core.WorldPoint(x, 0, z), name, group ?? "quest-" + id, Shturmap.Core.Quests.ObjectiveKind.Exploration);
}
