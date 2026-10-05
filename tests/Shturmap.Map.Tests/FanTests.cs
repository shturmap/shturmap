using Shturmap.Core;
using SkiaSharp;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// Symbols that cover each other (owner, 2026-10-05, from the overlap panel: "Go for F"): nothing grows when picked or
// pointed at, and a stack opens on a ring where the pointer rests on it (MapScene.FanAt), each symbol on a hairline
// to its own place.
public class FanTests
{
    private static readonly WorldPoint Spot = new(0, 0, 0);

    // A quest's place right on an extract, and another quest's a little way off.
    private static (Camera, MapScene) Stack() => Of([
        Quest("a", 0, 0, "A", group: "a"),
        new MapMarker("extract:e", MarkerKind.ExtractPmc, Spot, "Exit"),
        Quest("far", 200, 0, "Far", group: "far"),
    ]);

    [Fact]
    public void Only_a_symbol_another_lies_well_over_opens()
    {
        var (camera, scene) = Stack();
        var markers = MapRenderer.Layout(camera, scene, 1).Markers;
        Assert.True(MapRenderer.Stacked(markers, "objective:a:1", 1));
        Assert.True(MapRenderer.Stacked(markers, "extract:e", 1));
        Assert.False(MapRenderer.Stacked(markers, "objective:far:1", 1));
    }

    [Fact]
    public void An_opened_stack_stands_on_a_ring_each_on_a_line_to_its_place()
    {
        var (camera, scene) = Stack();
        var before = MapRenderer.Layout(camera, scene, 1).Markers;
        var home = before.Single(m => m.Marker.Id == "objective:a:1").At;
        scene.FanAt = "objective:a:1";
        scene.FanProgress = 1;
        var markers = MapRenderer.Layout(camera, scene, 1).Markers;
        var opened = markers.Where(m => m.Home is not null).ToList();
        Assert.Equal(["extract:e", "objective:a:1"], opened.Select(m => m.Marker.Id).Order());
        // Out of each other's way, around the place the pointer rested on, each remembering its own.
        Assert.True(SKPoint.Distance(opened[0].At, opened[1].At) > opened[0].Reach + opened[1].Reach - 2);
        Assert.All(opened, m => Assert.Equal(home, m.FanHub));
        Assert.All(opened, m => Assert.True(SKPoint.Distance(m.At, home) > m.Reach));
        // The one far off stays where it is; the plate takes in the ring.
        Assert.Null(markers.Single(m => m.Marker.Id == "objective:far:1").Home);
        var plate = MapRenderer.FanPlate(markers, 1)!.Value;
        Assert.All(opened, m => Assert.True(SKPoint.Distance(m.At, plate.Hub) + m.Reach <= plate.Radius));
    }

    [Fact]
    public void Opening_eases_out_from_the_symbols_places_and_closing_puts_them_back()
    {
        var (camera, scene) = Stack();
        var home = MapRenderer.Layout(camera, scene, 1).Markers.Single(m => m.Marker.Id == "extract:e").At;
        scene.FanAt = "objective:a:1";
        scene.FanProgress = 0;
        Assert.Equal(home, MapRenderer.Layout(camera, scene, 1).Markers.Single(m => m.Marker.Id == "extract:e").At);
        scene.FanProgress = 0.5f;
        var half = MapRenderer.Layout(camera, scene, 1).Markers.Single(m => m.Marker.Id == "extract:e").At;
        scene.FanProgress = 1;
        var open = MapRenderer.Layout(camera, scene, 1).Markers.Single(m => m.Marker.Id == "extract:e").At;
        // Eased out: past halfway at half the time.
        Assert.True(SKPoint.Distance(home, half) > SKPoint.Distance(home, open) / 2);
        scene.FanAt = null;
        var closed = MapRenderer.Layout(camera, scene, 1).Markers.Single(m => m.Marker.Id == "extract:e");
        Assert.Equal(home, closed.At);
        Assert.Null(closed.Home);
        Assert.Null(MapRenderer.FanPlate(MapRenderer.Layout(camera, scene, 1).Markers, 1));
    }

    [Fact]
    public void A_pick_and_what_is_pointed_at_keep_their_rest_size()
    {
        var (camera, scene) = Stack();
        scene.Kept = new HashSet<string> { "a" };
        scene.Focus = new HashSet<string> { "extract:e" };
        var markers = MapRenderer.Layout(camera, scene, 1).Markers;
        Assert.Equal(10, markers.Single(m => m.Marker.Id == "objective:a:1").R);
        Assert.Equal(7.5f, markers.Single(m => m.Marker.Id == "extract:e").R);
    }
}
