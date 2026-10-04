using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Data.Tests;

// A boss's name in a line of text is linked to its spawn zones on the map (the review of 2026-10-04, E3): the data's
// list of a map's bosses comes with the ids the markers' groups are made of. A hand-made map: no payload is stored.
public class BossMobsTests
{
    private static GameData Data() => new()
    {
        Mode = GameMode.Pve,
        Language = "en",
        Maps = new Dictionary<string, ApiMap>
        {
            ["map-1"] = new("map-1", "Test map", "test", "test", null, null, 40, [], [], [], [],
            [
                new ApiBoss("bossKolontay", 0.5), new ApiBoss("bossBoar", 0.6), new ApiBoss("bossBoar", 0.75),
                // AI squads are in the same list of the data, and are left out of the line.
                new ApiBoss("pmcBot", 1), new ApiBoss("exUsec", 1),
            ]),
        },
        Tasks = new Dictionary<string, ApiTask>(),
        Traders = new Dictionary<string, ApiTrader>(),
        Mobs = new Dictionary<string, ApiMob> { ["bossBoar"] = new("bossBoar", "Kaban"), ["bossKolontay"] = new("bossKolontay", "Kollontay") },
        MapDefinitions = Array.Empty<MapDefinition>(),
        CheckedAt = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public void A_maps_bosses_come_with_their_ids_strongest_chance_first()
    {
        var data = Data();
        Assert.Equal(new[] { ("bossBoar", "Kaban", 0.75), ("bossKolontay", "Kollontay", 0.5) }, data.BossMobsOn("map-1").ToArray());
        // The line's own list is the same, without the ids.
        Assert.Equal(new[] { ("Kaban", 0.75), ("Kollontay", 0.5) }, data.BossesOn("map-1").ToArray());
        Assert.Empty(data.BossMobsOn("another-map"));
    }
}
