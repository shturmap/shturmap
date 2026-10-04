using SkiaSharp;
using Shturmap.Core;
using Shturmap.Core.Maps;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// A frame's layout stands while nothing it is made from changed (the review of 2026-10-04, A17: a pulse or a ping
// draws the map again about 60 times a second, and each frame placed every marker and label anew). Whatever feeds
// the layout must give a new one: a layout kept too long shows markers where they no longer are.
public class LayoutCacheTests
{
    private static (Camera Camera, MapScene Scene) View()
    {
        var (camera, scene) = Of(
        [
            Quest("a", 0, 0, "Ballet Lover"),
            Quest("b", 120, 40, "Dandies"),
            new MapMarker("extract:1", MarkerKind.ExtractPmc, new WorldPoint(-150, 0, 60), "Gate"),
            new MapMarker("lock:1", MarkerKind.Lock, new WorldPoint(60, 0, -80), "Key", "key:one"),
        ]);
        scene.Player = new PlayerFix(new WorldPoint(10, 0, 10), 0, DateTime.Now);
        return (camera, scene);
    }

    [Fact]
    public void A_still_map_keeps_its_layout_from_frame_to_frame()
    {
        var (camera, scene) = View();
        var first = MapRenderer.LayoutOf(camera, scene, 1);
        Assert.Same(first, MapRenderer.LayoutOf(camera, scene, 1));
        // Pointing at a quest places its markers anew, once.
        scene.Focus = new HashSet<string> { "quest-a" };
        var pointed = MapRenderer.LayoutOf(camera, scene, 1);
        Assert.NotSame(first, pointed);
        scene.Dim = 0.4f;
        var eased = MapRenderer.LayoutOf(camera, scene, 1);
        // What a pulse, a ping and the dimming's easing change is drawn, not placed.
        scene.Dim = 1f;
        scene.PingSince = DateTime.Now;
        scene.InRaid = true;
        scene.Trail = [new WorldPoint(0, 0, 0)];
        scene.Spawns = [new WorldPoint(5, 0, 5)];
        scene.Gliding = true;
        Assert.Same(eased, MapRenderer.LayoutOf(camera, scene, 1));
    }

    public static TheoryData<string> Changes() =>
        new("pan", "zoom", "resize", "scale", "markers", "zones", "player", "kept", "quest keys", "focus", "objective", "labels", "floor");

    [Theory]
    [MemberData(nameof(Changes))]
    public void Whatever_feeds_the_layout_gives_a_new_one(string change)
    {
        var (camera, scene) = View();
        scene.Focus = new HashSet<string> { "quest-a" };
        var ui = 1f;
        var before = MapRenderer.LayoutOf(camera, scene, ui);
        switch (change)
        {
            case "pan": camera.Pan(30, 0); break;
            case "zoom": camera.ZoomAt(new SKPoint(500, 500), 1.5); break;
            case "resize": camera.Resize(new SKSize(900, 700)); break;
            case "scale": ui = 1.5f; break;
            case "markers": scene.Markers = [.. scene.Markers, Quest("c", -60, -60, "Audit")]; break;
            case "zones": scene.Zones = [new MapZone("zone:a:1", MarkerKind.Objective, [new WorldPoint(0, 0, 0), new WorldPoint(10, 0, 0), new WorldPoint(10, 0, 10)], "quest-a")]; break;
            case "player": scene.Player = new PlayerFix(new WorldPoint(40, 0, 40), 0, DateTime.Now); break;
            case "kept": scene.Kept = new HashSet<string> { "quest-b" }; break;
            case "quest keys": scene.QuestKeys = new Dictionary<string, IReadOnlyList<string>> { ["quest-a"] = ["key:one"] }; break;
            case "focus": scene.Focus = new HashSet<string> { "quest-b" }; break;
            case "objective": scene.FocusObjective = "a"; break;
            case "labels": scene.ShowLabels = false; break;
            case "floor": scene.Floor = new MapLayer("2nd", null, null, false, []); break;
        }
        var after = MapRenderer.LayoutOf(camera, scene, ui);
        Assert.NotSame(before, after);
        // And it is the layout a fresh placing gives.
        var fresh = MapRenderer.Layout(camera, scene, ui);
        Assert.Equal(fresh.Markers.Select(m => (m.Marker.Id, m.At, m.R, m.Selected, m.Kept, m.Focused)), after.Markers.Select(m => (m.Marker.Id, m.At, m.R, m.Selected, m.Kept, m.Focused)));
        Assert.Equal(fresh.Labels.Select(l => (l.Text, l.Box, l.Color)), after.Labels.Select(l => (l.Text, l.Box, l.Color)));
    }

    [Fact]
    public void The_last_focus_leaves_the_layout_when_its_dimming_has_faded_out()
    {
        var (camera, scene) = View();
        scene.Focus = new HashSet<string> { "quest-a" };
        scene.Dim = 1f;
        scene.Focus = new HashSet<string>();
        var fading = MapRenderer.LayoutOf(camera, scene, 1);
        Assert.Contains(fading.Markers, m => m.Focused);
        scene.Dim = 0.5f;
        Assert.Same(fading, MapRenderer.LayoutOf(camera, scene, 1));
        scene.Dim = 0f;
        var rest = MapRenderer.LayoutOf(camera, scene, 1);
        Assert.NotSame(fading, rest);
        Assert.DoesNotContain(rest.Markers, m => m.Focused);
    }

    [Fact]
    public async Task A_position_turning_a_minute_old_gets_its_age_tag_placed()
    {
        var (camera, scene) = View();
        // A label right of the player, where the age tag goes once the position is a minute old.
        scene.Markers = [Quest("near", 34, 10, "Road Closed")];
        scene.Player = new PlayerFix(new WorldPoint(10, 0, 10), 0, DateTime.Now - MapRenderer.PlayerOld + TimeSpan.FromSeconds(1));
        var young = MapRenderer.LayoutOf(camera, scene, 1);
        Assert.Same(young, MapRenderer.LayoutOf(camera, scene, 1));
        await Task.Delay(1400, TestContext.Current.CancellationToken);
        Assert.NotSame(young, MapRenderer.LayoutOf(camera, scene, 1));
    }

    [Fact]
    public void The_marker_under_the_pointer_is_found_in_the_layout_on_screen()
    {
        var (camera, scene) = View();
        var layout = MapRenderer.LayoutOf(camera, scene, 1);
        var quest = layout.Markers.Single(m => m.Marker.Id == "objective:b:1");
        Assert.Equal("objective:b:1", MapRenderer.HitTest(camera, scene, quest.At, 1)?.Id);
        Assert.Same(layout, MapRenderer.LayoutOf(camera, scene, 1));
        // After the map moved, the marker is where it is drawn now.
        camera.Pan(200, 0);
        Assert.Null(MapRenderer.HitTest(camera, scene, quest.At, 1));
        Assert.Equal("objective:b:1", MapRenderer.HitTest(camera, scene, new SKPoint(quest.At.X + 200, quest.At.Y), 1)?.Id);
    }
}
