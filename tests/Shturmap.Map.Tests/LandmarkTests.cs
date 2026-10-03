using SkiaSharp;
using Shturmap.Core;
using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Data.TarkovDev;
using static Shturmap.Map.Tests.Fixtures;

namespace Shturmap.Map.Tests;

// Locks, switches, containers and hazards from tarkov.dev's data (owner, 2026-10-03: "Do B and A"; B = landmarks).
public class LandmarkTests
{
    private const string Key = "5c1e2a1e86f77431ea0ea84c";
    private const string TrunkKey = "61aa81fcb225ac1ead7957c3";

    private static ApiMap MapWith(List<ApiLock>? locks = null, List<ApiSwitch>? switches = null, List<ApiHazard>? hazards = null,
        List<ApiContainerSpot>? containers = null) =>
        new("map-1", "Test map", "the-lab", "test", null, null, 40, [], [], locks ?? [], hazards ?? [], [],
            Switches: switches ?? [], LootContainers: containers ?? []);

    private static GameData Data(ApiMap map) => new()
    {
        Mode = GameMode.Pve,
        Language = "en",
        Maps = new Dictionary<string, ApiMap> { [map.Id] = map },
        Tasks = new Dictionary<string, ApiTask>(),
        Traders = new Dictionary<string, ApiTrader>(),
        ItemNames = new Dictionary<string, string> { [Key] = "TerraGroup Labs manager's office room key", [TrunkKey] = "Car trunk key" },
        ItemShortNames = new Dictionary<string, string> { [Key] = "TGL MO" },
        MapDefinitions = [],
        CheckedAt = DateTimeOffset.UnixEpoch,
    };

    private static List<ApiPosition> Box(double x, double z, double width, double depth) =>
        [At(x, z), At(x + width, z), At(x + width, z + depth), At(x, z + depth)];

    [Fact]
    public void Locks_and_switches_are_markers_named_from_the_data()
    {
        var map = MapWith(
            locks:
            [
                new("door-1", "door", Key, false, At(10, 20)),
                new("trunk-1", "trunk", TrunkKey, true, At(30, 40)),
                new("box-1", "container", Key, false, At(50, 60)),
                new("door-2", "door", Key, false, null),
            ],
            switches:
            [
                new("s-1", "Med Elevator Power Button", At(5, 5)),
                new("s-2", "switch_00405_call_button", At(6, 6)),
                new("s-3", "Alarm Switch"),
            ]);
        var markers = MapContentBuilder.Landmarks(Data(map), map);

        var door = Assert.Single(markers, m => m.Id == "lock:door-1");
        Assert.Equal(MarkerKind.Lock, door.Kind);
        Assert.Equal("TGL MO", door.Label);
        Assert.Equal(MapContentBuilder.KeyGroup(Key), door.Group);
        // A key without a short name is called by its name; a lock that needs power says so.
        Assert.Equal("Car trunk key · needs power", Assert.Single(markers, m => m.Id == "lock:trunk-1").Label);
        Assert.DoesNotContain(markers, m => m.Id is "lock:box-1" or "lock:door-2");

        Assert.Equal("Med Elevator Power Button", Assert.Single(markers, m => m.Id == "switch:s-1").Label);
        Assert.Equal("Switch", Assert.Single(markers, m => m.Id == "switch:s-2").Label);
        Assert.DoesNotContain(markers, m => m.Id == "switch:s-3");
    }

    [Fact]
    public void A_locks_group_names_its_key()
    {
        Assert.Equal(Key, MapContentBuilder.KeyOf(MapContentBuilder.KeyGroup(Key)));
        Assert.Null(MapContentBuilder.KeyOf("boss:bossBully"));
        Assert.Null(MapContentBuilder.KeyOf(null));
    }

    [Fact]
    public void Hazards_are_trap_sized_traps_minefields_and_border_sniper_zones()
    {
        var map = MapWith(hazards:
        [
            new("hazard", "Hazard", At(0, 0), Box(0, 0, 1, 3), 2, 0),
            new("hazard", "Hazard", At(0, 0), Box(-27, -29, 54, 58), -0.6, -2.6),
            new("minefield", "DamageType_Landmine", At(0, 0), Box(100, 100, 80, 60), 1, 0),
            new("sniper", "ScavRole/Marksman", At(0, 0), Box(200, 200, 300, 40), 1, 0),
        ]);
        var zones = MapContentBuilder.Hazards(map);
        Assert.Equal(3, zones.Count);
        Assert.All(zones, z => Assert.Equal(MarkerKind.Hazard, z.Kind));
        Assert.Null(zones[0].Group);
        Assert.Equal(MapContentBuilder.MinefieldGroup, zones[1].Group);
        Assert.Equal(MapContentBuilder.SniperZoneGroup, zones[2].Group);
        Assert.Equal("SNIPER ZONE", MapRenderer.HazardLabel(zones[2]));
        Assert.Null(MapRenderer.HazardLabel(zones[1]));
    }

    [Fact]
    public void An_extract_and_the_switches_it_needs_light_each_other()
    {
        // An extract needs a lever, which a power switch unlocks, which a switch the data doesn't place unlocks.
        var map = MapWith(switches:
        [
            new("power", "Power switch", At(0, 0), [new("Unlock", "lever")]),
            new("lever", "Lever", At(5, 0)),
            new("hidden", "Hidden", null, [new("Unlock", "power")]),
            new("alarm", "Alarm", At(9, 9)),
        ]) with { Extracts = [new("d2", "D-2", "pmc", At(10, 0), null, null, null, ["lever"])] };
        var links = MapContentBuilder.ExtractSwitchLinks(map);
        Assert.Equal(["switch:lever", "switch:power"], links["extract:d2"]);
        Assert.Equal(["extract:d2"], links["switch:power"]);
        Assert.Equal(["extract:d2"], links["switch:lever"]);
        Assert.False(links.ContainsKey("switch:hidden"));
        Assert.False(links.ContainsKey("switch:alarm"));
    }

    [Fact]
    public void A_switch_listed_for_every_extract_links_none()
    {
        // Customs lists one lever for all its extracts, most of which need no switch; a lock-switch is not needed either.
        var map = MapWith(switches:
        [
            new("lever", "Lever", At(0, 0)),
            new("gate", "Gate button", At(5, 0)),
            new("console", "Console", At(7, 0), [new("Lock", "gate")]),
        ]) with
        {
            Extracts =
            [
                new("a", "Gas Station", "pmc", At(10, 0), null, null, null, ["lever"]),
                new("b", "Gate", "pmc", At(20, 0), null, null, null, ["lever", "gate"]),
            ],
        };
        var links = MapContentBuilder.ExtractSwitchLinks(map);
        Assert.Equal(["switch:gate"], links["extract:b"]);
        Assert.False(links.ContainsKey("extract:a"));
        Assert.False(links.ContainsKey("switch:lever"));
        Assert.False(links.ContainsKey("switch:console"));
    }

    [Fact]
    public void Minefields_are_left_out_over_artwork_that_draws_them()
    {
        // Customs' artwork has no minefields, so the data's show; Woods' has a "Minefield" group, so they don't.
        var minefield = new MapZone("minefield:0", MarkerKind.Hazard, [new(0, 0, 0), new(5, 0, 0), new(5, 0, 5)], MapContentBuilder.MinefieldGroup);
        var trap = minefield with { Id = "hazard:0", Group = null };
        var (_, sheet) = TestView.Of([]);
        string Svg(string inner) => $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1000 1000">{inner}</svg>""";
        MapArtwork Artwork(string svg)
        {
            var path = Path.Combine(Path.GetTempPath(), $"shturmap-mines-{Guid.NewGuid():N}.svg");
            File.WriteAllText(path, svg);
            try
            {
                return MapArtwork.Load(path, sheet.Definition);
            }
            finally
            {
                File.Delete(path);
            }
        }
        using var without = Artwork(Svg("""<g id="Roads"><rect width="10" height="10"/></g>"""));
        using var with = Artwork(Svg("""<g id="Minefield"><rect width="10" height="10"/></g>"""));
        Assert.False(without.ShowsMinefields);
        Assert.True(with.ShowsMinefields);
        Assert.True(MapRenderer.HazardShown(minefield, null));
        Assert.True(MapRenderer.HazardShown(minefield, without));
        Assert.False(MapRenderer.HazardShown(minefield, with));
        Assert.True(MapRenderer.HazardShown(trap, with));

        // Border-sniper zones: Customs, Ground Zero and Streets draw a "Sniper" group, Interchange a "Danger" group.
        var sniperZone = minefield with { Id = "sniper-zone:0", Group = MapContentBuilder.SniperZoneGroup };
        using var sniper = Artwork(Svg("""<g id="Sniper" class="danger"><rect width="10" height="10"/></g>"""));
        using var danger = Artwork(Svg("""<g id="Danger" class="danger"><rect width="10" height="10"/></g>"""));
        Assert.False(with.ShowsSniperZones);
        Assert.True(sniper.ShowsSniperZones);
        Assert.True(danger.ShowsSniperZones);
        Assert.False(sniper.ShowsMinefields);
        Assert.True(MapRenderer.HazardShown(sniperZone, with));
        Assert.False(MapRenderer.HazardShown(sniperZone, sniper));
        Assert.False(MapRenderer.HazardShown(sniperZone, danger));
        Assert.True(MapRenderer.HazardShown(minefield, sniper));
    }

    [Fact]
    public void A_hazard_over_artwork_hatches_only_where_it_draws_the_map()
    {
        // Customs' minefields run past the drawn map into the empty space around it (owner, 2026-10-03: "big white
        // rectangles"). Here the artwork draws its left half only.
        var (camera, sheet) = TestView.Of([]);
        var path = Path.Combine(Path.GetTempPath(), $"shturmap-ground-{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1000 1000"><rect width="500" height="1000" fill="#808080"/></svg>""");
        MapArtwork artwork;
        try
        {
            artwork = MapArtwork.Load(path, sheet.Definition);
        }
        finally
        {
            File.Delete(path);
        }
        using var _ = artwork;
        var scene = new MapScene(sheet.Definition, artwork);
        SKPoint At(double x, double y) => camera.ToScreen(scene.Placement.SvgToMap(x, y));
        Assert.True(artwork.OnGround(new SKPoint(250, 500)));
        Assert.False(artwork.OnGround(new SKPoint(750, 500)));

        SKBitmap Draw()
        {
            var bitmap = new SKBitmap(1000, 1000);
            using var canvas = new SKCanvas(bitmap);
            MapRenderer.Render(canvas, camera, scene, 1);
            return bitmap;
        }
        // How many pixels of a 20-pixel square around a point the hazard changed.
        static int Changed(SKBitmap before, SKBitmap after, SKPoint at)
        {
            var n = 0;
            for (var x = (int)at.X - 10; x < (int)at.X + 10; x++)
                for (var y = (int)at.Y - 10; y < (int)at.Y + 10; y++)
                    n += before.GetPixel(x, y) != after.GetPixel(x, y) ? 1 : 0;
            return n;
        }
        using var bare = Draw();
        scene.Zones = [new MapZone("minefield:0", MarkerKind.Hazard, [new(-5000, 0, -5000), new(5000, 0, -5000), new(5000, 0, 5000), new(-5000, 0, 5000)], MapContentBuilder.MinefieldGroup)];
        using var hatched = Draw();
        Assert.True(Changed(bare, hatched, At(250, 500)) > 20);
        Assert.Equal(0, Changed(bare, hatched, At(750, 500)));
    }

    [Fact]
    public void Containers_are_their_places_once_each()
    {
        var map = MapWith(containers: [new("safe", At(1, 2)), new("pc", At(1, 2)), new("crate", At(3, 4)), new("crate", null)]);
        Assert.Equal([new WorldPoint(1, 0, 2), new WorldPoint(3, 0, 4)], MapContentBuilder.Containers(map));
        Assert.Equal(2, MapContentBuilder.Build(Data(map), map.Id, [], new HashSet<string>()).Containers.Count);
    }

    // ---- what shows when ----

    private static MapMarker Lock(double x, double z, double y = 0) =>
        new($"lock:{x}", MarkerKind.Lock, new WorldPoint(x, y, z), "TGL MO", MapContentBuilder.KeyGroup(Key));

    // A scene with artwork: a blank SVG over the test sheet's extent.
    private static MapScene WithArtwork(IReadOnlyList<MapMarker> markers)
    {
        var (_, sheet) = TestView.Of(markers);
        var path = Path.Combine(Path.GetTempPath(), $"shturmap-landmark-{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1000 1000"><rect width="1000" height="1000" fill="#808080"/></svg>""");
        try
        {
            return new MapScene(sheet.Definition, MapArtwork.Load(path, sheet.Definition)) { Markers = markers };
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void On_artwork_landmarks_show_from_a_zoom_and_on_a_sheet_always()
    {
        var markers = new[] { Lock(0, 0) };
        var (camera, sheet) = TestView.Of(markers);
        Assert.True(sheet.IsSheet);
        Assert.Single(MapRenderer.ShownMarkers(camera, sheet, 1));

        var artwork = WithArtwork(markers);
        Assert.False(artwork.IsSheet);
        Assert.Empty(MapRenderer.ShownMarkers(camera, artwork, 1));
        camera.ZoomAt(new SKPoint(500, 500), (float)MapRenderer.LandmarkFromZoom * 1.1f);
        Assert.Single(MapRenderer.ShownMarkers(camera, artwork, 1));
    }

    [Fact]
    public void Pointing_at_a_key_shows_the_locks_it_opens_at_any_zoom()
    {
        var markers = new[] { Lock(0, 0), Lock(100, 0) with { Group = MapContentBuilder.KeyGroup(TrunkKey) } };
        var (camera, _) = TestView.Of(markers);
        var artwork = WithArtwork(markers);
        artwork.Focus = new HashSet<string> { MapContentBuilder.KeyGroup(Key) };
        var shown = Assert.Single(MapRenderer.ShownMarkers(camera, artwork, 1));
        Assert.Equal(MapContentBuilder.KeyGroup(Key), shown.Marker.Group);
        Assert.True(shown.Selected);
    }

    [Fact]
    public void A_landmarks_label_shows_close_up_or_when_pointed_at()
    {
        var (camera, scene) = TestView.Of([Lock(0, 0)]);
        Assert.DoesNotContain(MapRenderer.Layout(camera, scene, 1).Labels, l => l.Text == "TGL MO");

        scene.Focus = new HashSet<string> { MapContentBuilder.KeyGroup(Key) };
        Assert.Contains(MapRenderer.Layout(camera, scene, 1).Labels, l => l.Text == "TGL MO");

        scene.Focus = new HashSet<string>();
        camera.ZoomAt(new SKPoint(500, 500), (float)MapRenderer.LandmarkLabelFromZoom * 1.1f);
        Assert.Contains(MapRenderer.Layout(camera, scene, 1).Labels, l => l.Text == "TGL MO");
    }

    [Fact]
    public void Landmarks_on_another_floor_show_only_when_pointed_at()
    {
        var definition = Definition("streets-of-tarkov");
        var scene = new MapScene(definition, null);
        // A point inside some floor's first extent that resolves to that floor (as in FloorTests).
        var at = scene.FloorStack.OfType<MapLayer>()
            .Select(layer =>
            {
                var extent = layer.Extents[0];
                var box = extent.Boxes.Count > 0 ? extent.Boxes[0] : new WorldBox(0, 0, 0, 0);
                return new WorldPoint((box.X1 + box.X2) / 2, (Math.Max(extent.Height.Min, -1000) + Math.Min(extent.Height.Max, 1000)) / 2, (box.Z1 + box.Z2) / 2);
            })
            .First(p => MapRenderer.FloorOffset(scene, p) != 0);
        var marker = new MapMarker("lock:up", MarkerKind.Lock, at, "TGL MO", MapContentBuilder.KeyGroup(Key));
        scene.Markers = [marker];
        var camera = new Camera();
        camera.Resize(new SKSize(1600, 1000));
        camera.Fit(scene.Projection.WorldRect);
        camera.CenterOn(scene.Projection.ToMap(at));

        Assert.Empty(MapRenderer.ShownMarkers(camera, scene, 1));
        scene.Focus = new HashSet<string> { MapContentBuilder.KeyGroup(Key) };
        Assert.Single(MapRenderer.ShownMarkers(camera, scene, 1));
    }

    [Fact]
    public void Container_dots_draw_on_a_sheet_and_never_on_artwork()
    {
        static SKColor Centre(MapScene scene, Camera camera)
        {
            using var bitmap = new SKBitmap(1000, 1000);
            using (var canvas = new SKCanvas(bitmap))
                MapRenderer.Render(canvas, camera, scene, 1);
            return bitmap.GetPixel(500, 500);
        }

        var (camera, sheet) = TestView.Of([]);
        var plain = Centre(sheet, camera);
        sheet.Containers = [new WorldPoint(0, 0, 0)];
        Assert.NotEqual(plain, Centre(sheet, camera));

        var artwork = WithArtwork([]);
        var bare = Centre(artwork, camera);
        artwork.Containers = [new WorldPoint(0, 0, 0)];
        Assert.Equal(bare, Centre(artwork, camera));
    }
}
