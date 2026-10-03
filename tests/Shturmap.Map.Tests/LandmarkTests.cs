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
    public void Hazards_are_trap_sized_traps_and_minefields_of_any_size()
    {
        var map = MapWith(hazards:
        [
            new("hazard", "Hazard", At(0, 0), Box(0, 0, 1, 3), 2, 0),
            new("hazard", "Hazard", At(0, 0), Box(-27, -29, 54, 58), -0.6, -2.6),
            new("minefield", "DamageType_Landmine", At(0, 0), Box(100, 100, 80, 60), 1, 0),
            new("sniper", "ScavRole/Marksman", At(0, 0), Box(200, 200, 5, 5), 1, 0),
        ]);
        var zones = MapContentBuilder.Hazards(map);
        Assert.Equal(2, zones.Count);
        Assert.All(zones, z => Assert.Equal(MarkerKind.Hazard, z.Kind));
        Assert.Null(zones[0].Group);
        Assert.Equal(MapContentBuilder.MinefieldGroup, zones[1].Group);
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
