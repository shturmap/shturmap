using SkiaSharp;
using Shturmap.Core;
using Shturmap.Core.Quests;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// Places of one objective that would overlap merge into one marker with their count (cartography review, 2026-10-02).
public class ClusterTests
{
    private static MapMarker Place(string objective, int n, double x, double z, MarkerKind kind = MarkerKind.PossibleLocation) =>
        new($"objective:{objective}:{n}", kind, new WorldPoint(x, 0, z), "Following the Bread Crumbs", "bread-crumbs", ObjectiveKind.FindInRaid);

    [Fact]
    public void Overlapping_places_of_one_objective_merge_at_their_medoid()
    {
        var (camera, scene) = Of([Place("tape", 1, 0, 0), Place("tape", 2, 5, 0), Place("tape", 3, 30, 0)]);
        var marker = Assert.Single(MapRenderer.ShownMarkers(camera, scene, 1));
        Assert.Equal(3, marker.Count);
        Assert.Equal("objective:tape:2", marker.Marker.Id);
    }

    [Fact]
    public void Places_of_different_objectives_stay_apart()
    {
        var (camera, scene) = Of([Place("tape", 1, 0, 0), Place("key", 1, 5, 0)]);
        Assert.All(MapRenderer.ShownMarkers(camera, scene, 1), m => Assert.Equal(1, m.Count));
    }

    [Fact]
    public void Groups_split_as_the_view_zooms_in()
    {
        var (camera, scene) = Of([Place("tape", 1, 0, 0), Place("tape", 2, 12, 0)]);
        Assert.Single(MapRenderer.ShownMarkers(camera, scene, 1));
        camera.ZoomAt(new SKPoint(500, 500), 8);
        Assert.Equal(2, MapRenderer.ShownMarkers(camera, scene, 1).Count);
    }

    [Fact]
    public void The_largest_group_carries_the_label()
    {
        var (camera, scene) = Of([Place("tape", 1, 100, 0), Place("tape", 2, 0, 0), Place("tape", 3, 4, 0), Place("tape", 4, 8, 0)]);
        var label = Assert.Single(MapRenderer.Layout(camera, scene, 1).Labels);
        Assert.Equal(3, label.Of.Count);
    }

    [Fact]
    public void Pointing_finds_the_cluster()
    {
        var (camera, scene) = Of([Place("tape", 1, 0, 0), Place("tape", 2, 5, 0), Place("tape", 3, 10, 0)]);
        Assert.Equal("objective:tape:2", MapRenderer.HitTest(camera, scene, new SKPoint(505, 500), 1)?.Id);
    }
}
