using Shturmap.Core.Maps;

namespace Shturmap.Core.Tests;

internal static class Fixtures
{
    public static string PathTo(params string[] parts) =>
        Path.Combine([AppContext.BaseDirectory, "fixtures", .. parts]);

    private static readonly Lazy<IReadOnlyList<MapDefinition>> Definitions =
        new(() => MapDefinitionReader.Read(File.ReadAllText(PathTo("tarkov-dev", "maps.json"))));

    public static IReadOnlyList<MapDefinition> MapDefinitions => Definitions.Value;

    public static MapDefinition Map(string key) => MapDefinitions.Single(m => m.Key == key);

    // tarkov.dev's map ids, normalized names, location ids and scene paths (json.tarkov.dev/pve/maps, 2026-10-01)
    public static IReadOnlyList<MapIdentity> MapIdentities { get; } =
    [
        new("55f2d3fd4bdc2d5f408b4567", "factory", "factory4_day", "maps/factory_day_preset.bundle", "Factory"),
        new("56f40101d2720b2a4d8b45d6", "customs", "bigmap", "maps/customs_preset.bundle", "Customs"),
        new("5704e3c2d2720bac5b8b4567", "woods", "Woods", "maps/woods_preset.bundle", "Woods"),
        new("5704e4dad2720bb55b8b4567", "lighthouse", "Lighthouse", "maps/lighthouse_preset.bundle", "Lighthouse"),
        new("5704e554d2720bac5b8b456e", "shoreline", "Shoreline", "maps/shoreline_preset.bundle", "Shoreline"),
        new("5704e5fad2720bc05b8b4567", "reserve", "RezervBase", "maps/rezerv_base_preset.bundle", "Reserve"),
        new("5714dbc024597771384a510d", "interchange", "Interchange", "maps/shopping_mall.bundle", "Interchange"),
        new("5714dc692459777137212e12", "streets-of-tarkov", "TarkovStreets", "maps/city_preset.bundle", "Streets of Tarkov"),
        new("59fc81d786f774390775787e", "night-factory", "factory4_night", "maps/factory_night_preset.bundle", "Night Factory"),
        new("5b0fc42d86f7744a585f9105", "the-lab", "laboratory", "maps/laboratory_preset.bundle", "The Lab"),
        new("653e6760052c01c1c805532f", "ground-zero", "Sandbox", "maps/sandbox_preset.bundle", "Ground Zero"),
        new("65b8d6f5cdde2479cb2a3125", "ground-zero-21", "Sandbox_high", "maps/sandbox_high_preset.bundle", "Ground Zero 21+"),
        new("65cc8f81a9aac3e77d0cfd3e", "terminal", "Terminal", "maps/terminal_preset.bundle", "Terminal"),
        new("6733700029c367a3d40b02af", "the-labyrinth", "Labyrinth", "maps/labyrinth_preset.bundle", "The Labyrinth"),
        new("68236e8153654e8c1200798a", "ground-zero-tutorial", "Sandbox_start", "maps/sandbox_start_preset.bundle", "Ground Zero"),
        new("69af492a4819ea4ba10a69c5", "icebreaker", "Icebreaker", "maps/icebreaker.bundle", "Icebreaker"),
        new("6a294a5b5eb5f9a1700417b7", "the-lab-dark", "laboratory_dark", "maps/laboratory_dark_preset.bundle", "The Lab (dark)"),
    ];
}
