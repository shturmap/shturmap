using Shturmap.Core;
using Shturmap.Data.TarkovDev;
using static Shturmap.Map.Tests.Fixtures;

namespace Shturmap.Map.Tests;

// Spawns are drawn one marker per zone, at the centroid of the zone's points (owner, 2026-10-02).
public class SpawnZoneTests
{
    private static ApiSpawn Scav(string zone, double x, double z, double y = 0) => new(At(x, z, y), ["scav"], ["bot"], zone);

    [Fact]
    public void Each_scav_zone_is_one_marker_at_the_centroid_of_its_points()
    {
        var map = TestMap("streets-of-tarkov", [Scav("ZoneA", 0, 0), Scav("ZoneA", 10, 0, 2), Scav("ZoneA", 20, 30, 4), Scav("ZoneB", 100, 100)]);
        var markers = MapContentBuilder.SpawnZones(With(map), map).Where(m => m.Kind == MarkerKind.ScavSpawn).ToList();
        Assert.Equal(2, markers.Count);
        var a = markers.Single(m => m.Id == "scav:ZoneA");
        Assert.Equal(new WorldPoint(10, 2, 10), a.Position);
        Assert.Equal("", a.Label);
    }

    [Fact]
    public void A_point_listed_twice_does_not_pull_the_centroid()
    {
        var map = TestMap("customs", [Scav("Z", 0, 0), Scav("Z", 0, 0), Scav("Z", 30, 0)]);
        Assert.Equal(new WorldPoint(15, 0, 0), MapContentBuilder.SpawnZones(With(map), map).Single().Position);
    }

    [Fact]
    public void Sniper_points_make_their_own_zones_and_only_bot_snipers_count()
    {
        var map = TestMap("customs",
        [
            Scav("ZoneCustoms", 0, 0),
            new(At(50, 50, 10), ["scav"], ["bot", "sniper"], "ZoneCustoms"),
            new(At(54, 50, 10), ["scav"], ["bot", "sniper"], "ZoneCustoms"),
            // Ground Zero lists player spawns tagged "sniper" for every side: not sniper Scavs.
            new(At(0, 80), ["all"], ["player", "sniper"], "ZoneSandbox"),
        ]);
        var markers = MapContentBuilder.SpawnZones(With(map), map);
        var sniper = Assert.Single(markers, m => m.Kind == MarkerKind.SniperSpawn);
        Assert.Equal(new WorldPoint(52, 10, 50), sniper.Position);
        Assert.Equal("Sniper", sniper.Label);
        Assert.Equal(new WorldPoint(0, 0, 0), Assert.Single(markers, m => m.Kind == MarkerKind.ScavSpawn).Position);
    }

    [Fact]
    public void A_boss_with_several_zones_says_both_chances()
    {
        var map = TestMap("streets-of-tarkov", bosses:
        [
            new("bossKolontay", 0.75, [new("Klimov Shopping Mall", 0.5, [At(0, 0), At(10, 0)]), new("Ministry of the Interior Academy", 0.5, [At(300, 300)])]),
            new("bossBoar", 0.75, [new("Car Dealership", 1, [At(-200, 0), At(-210, 10), At(-190, 20)])]),
            new("pmcBot", 0.4, [new("Somewhere", 1, [At(500, 500)])]),
        ]);
        var markers = MapContentBuilder.SpawnZones(With(map, new ApiMob("bossKolontay", "Kollontay"), new ApiMob("bossBoar", "Kaban")), map);
        Assert.Equal(["Kollontay 75% · 50% here", "Kollontay 75% · 50% here", "Kaban 75%"], markers.Select(m => m.Label));
        Assert.Equal(new WorldPoint(5, 0, 0), markers[0].Position);
        Assert.Equal(new WorldPoint(-200, 0, 10), markers[2].Position);
        Assert.All(markers, m => Assert.Equal(MarkerKind.BossSpawn, m.Kind));
        Assert.Equal("boss:bossKolontay", markers[0].Group);
    }

    [Fact]
    public void Bosses_sharing_a_zone_share_one_marker_and_repeated_entries_count_once()
    {
        var stronghold = new List<ApiPosition> { At(0, 0), At(20, 0) };
        var map = TestMap("customs", bosses:
        [
            new("bossBully", 0.75, [new("Dorms", 0.33, [At(100, 0)]), new("Stronghold", 0.33, stronghold)]),
            new("bossKnight", 0.25, [new("Stronghold", 1, stronghold)]),
            new("bossKnight", 0.25, [new("Stronghold", 1, stronghold)]),
        ]);
        var markers = MapContentBuilder.SpawnZones(With(map, new ApiMob("bossBully", "Reshala"), new ApiMob("bossKnight", "Knight")), map);
        Assert.Equal(2, markers.Count);
        var shared = markers.Single(m => m.Position == new WorldPoint(10, 0, 0));
        Assert.Equal("Reshala 75% · 33% here / Knight 25%", shared.Label);
        Assert.Equal("boss:bossBully+bossKnight", shared.Group);
        Assert.Equal(["bossBully", "bossKnight"], MapContentBuilder.BossesOf(shared.Group!));
    }

    [Fact]
    public void Built_content_has_no_marker_per_spawn_point()
    {
        var spawns = Enumerable.Range(0, 40).Select(i => Scav(i < 20 ? "ZoneA" : "ZoneB", i * 7, i % 5)).ToList();
        var map = TestMap("streets-of-tarkov", spawns);
        var content = MapContentBuilder.Build(With(map), map.Id, [], new HashSet<string>());
        Assert.Equal(2, content.Markers.Count(m => m.Kind == MarkerKind.ScavSpawn));
    }
}
