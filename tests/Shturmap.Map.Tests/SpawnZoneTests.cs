using Shturmap.Core;
using Shturmap.Data.TarkovDev;
using static Shturmap.Map.Tests.Fixtures;

namespace Shturmap.Map.Tests;

// Spawns are drawn one marker per zone, at the centroid of the zone's points, split into groups where the centroid
// would stand away from them (owner, 2026-10-02).
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
    public void A_zone_whose_centroid_stands_among_its_points_stays_one_marker()
    {
        // 75 m end to end, but the centroid is 12.5 m from the nearest point.
        var map = TestMap("customs", [Scav("Z", 0, 0), Scav("Z", 25, 0), Scav("Z", 50, 0), Scav("Z", 75, 0)]);
        Assert.Equal(new WorldPoint(37.5, 0, 0), Assert.Single(MapContentBuilder.SpawnZones(With(map), map)).Position);
    }

    [Fact]
    public void A_zone_in_two_places_gets_a_marker_in_each()
    {
        // Customs' sniper zone lists two points 228 m apart; one marker at their centroid stood 114 m from both.
        var map = TestMap("customs", [Scav("Z", 0, 0), Scav("Z", 10, 0), Scav("Z", 20, 0), Scav("Z", 228, 0)]);
        var markers = MapContentBuilder.SpawnZones(With(map), map);
        Assert.Equal(["scav:Z", "scav:Z:2"], markers.Select(m => m.Id));
        Assert.Equal([new WorldPoint(10, 0, 0), new WorldPoint(228, 0, 0)], markers.Select(m => m.Position));
    }

    [Fact]
    public void A_zone_reaching_another_floor_gets_a_marker_on_each()
    {
        // Reserve: a zone reaching from the ground into the bunker below put its marker between the two.
        var map = TestMap("reserve", [Scav("Z", 0, 0), Scav("Z", 10, 0), Scav("Z", 0, 0, -10), Scav("Z", 10, 0, -10)]);
        Assert.Equal([0.0, -10.0], MapContentBuilder.SpawnZones(With(map), map).Select(m => m.Position.Y));
    }

    [Fact]
    public void A_split_boss_zone_says_its_chances_once()
    {
        var map = TestMap("woods", bosses: [new("bossKojaniy", 0.75, [new("Lumber Mill", 0.5, [At(0, 0), At(10, 0), At(150, 0)])])]);
        var markers = MapContentBuilder.SpawnZones(With(map, new ApiMob("bossKojaniy", "Shturman")), map);
        Assert.Equal(["Shturman 75% · 50% here", ""], markers.Select(m => m.Label));
        Assert.Equal([new WorldPoint(5, 0, 0), new WorldPoint(150, 0, 0)], markers.Select(m => m.Position));
        Assert.All(markers, m => Assert.Equal("boss:bossKojaniy", m.Group));
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
            new("pmcUSEC", 0.4, [new("Somewhere", 1, [At(500, 500)])]),
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

    [Fact]
    public void Ai_squads_are_spawn_markers_and_ai_pmcs_are_not()
    {
        // Lighthouse's Chalet lists Rogue groups at 100, 90, 50 and 50 %, and AI PMCs at the same place.
        var chalet = new List<ApiPosition> { At(0, 0), At(10, 0) };
        var map = TestMap("lighthouse", bosses:
        [
            new("exUsecFree", 1.0, [new("Zone_Chalet", 1, chalet)]),
            new("exUsecFree", 0.9, [new("Zone_Chalet", 1, chalet)]),
            new("exUsecFree", 0.5, [new("Zone_Chalet", 1, chalet)]),
            new("exUsecFree", 0.5, [new("Zone_Chalet", 1, chalet)]),
            new("pmcUSEC", 0.5, [new("Zone_Chalet", 1, chalet)]),
            new("pmcBEAR", 0.5, [new("Zone_Chalet", 1, chalet)]),
        ]);
        var data = With(map, new ApiMob("exUsecFree", "Rogue"), new ApiMob("pmcUSEC", "USEC"), new ApiMob("pmcBEAR", "BEAR"));
        var marker = Assert.Single(MapContentBuilder.SpawnZones(data, map));
        Assert.Equal(MarkerKind.BossSpawn, marker.Kind);
        Assert.Equal("Rogue 100%", marker.Label);
        Assert.Equal("boss:exUsecFree", marker.Group);
    }

    // One format for a boss's chances, on the map as in Plan's line ("Kollontay 75%"): the chance on the map, then what
    // applies to the place when it is less (2026-10-09; The Lab's 2nd floor said "Raider 60%, 45%, 35%" until then).
    [Fact]
    public void Several_groups_of_a_squad_say_its_chance_on_the_map_and_the_best_one_here()
    {
        var floor = new List<ApiPosition> { At(0, 0), At(10, 0) };
        var basement = new List<ApiPosition> { At(200, 0, -10), At(210, 0, -10) };
        var map = TestMap("laboratory", bosses:
        [
            new("pmcBot", 0.6, [new("Floor2", 1, floor)]),
            new("pmcBot", 0.45, [new("Floor2", 1, floor)]),
            new("pmcBot", 0.35, [new("Floor2", 1, floor)]),
            new("pmcBot", 0.45, [new("Basement", 1, basement)]),
            new("pmcBot", 0.4, [new("Basement", 1, basement)]),
        ]);
        var markers = MapContentBuilder.SpawnZones(With(map, new ApiMob("pmcBot", "Raider")), map);
        Assert.Equal(["Raider 60%", "Raider 60% · 45% here"], markers.Select(m => m.Label));
    }

    [Fact]
    public void Several_names_and_shares_stay_apart_in_a_label() =>
        Assert.Equal("Reshala 75% · 33% here / Knight 25% / Raider 40% · 30% here / Rogue 100%",
            MapContentBuilder.SpawnLabel(
            [
                new("Reshala", 75, 33), new("Knight", 25, null), new("Raider", 40, 30), new("Raider", 40, 20), new("Knight", 25, null),
                new("Rogue", 100, null), new("Rogue", 100, 90),
            ]));
}
