using Shturmap.Core;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// Several quests picked for the coming raid: all in the kept look, the guide to the nearest, nothing else dimmed
// (owner, 2026-10-03: "Still it should show all other quest markers").
public class PickTests
{
    [Fact]
    public void The_guide_leads_to_the_nearest_place_of_any_pick_and_never_to_an_unpicked_quest()
    {
        var (camera, scene) = Of([Quest("a", 300, 0, "A", group: "a"), Quest("b", 50, 0, "B", group: "b"), Quest("c", 20, 0, "C", group: "c")]);
        scene.Player = new PlayerFix(new WorldPoint(0, 0, 0), null, DateTime.Now);
        scene.Kept = new HashSet<string> { "a", "b" };
        Assert.Equal(50, MapRenderer.Layout(camera, scene, 1).Guide!.Metres, 3);
    }

    [Fact]
    public void Every_pick_is_drawn_kept_and_nothing_steps_back()
    {
        var (camera, scene) = Of([Quest("a", 100, 0, "A", group: "a"), Quest("b", -100, 0, "B", group: "b"), Quest("c", 0, 100, "C", group: "c")]);
        scene.Kept = new HashSet<string> { "a", "b" };
        scene.Dim = 1;
        Assert.False(scene.HasHighlight);
        Assert.Empty(scene.ShownFocus);
        var markers = MapRenderer.Layout(camera, scene, 1).Markers;
        Assert.Equal(["A", "B"], markers.Where(m => m.Kept).Select(m => m.Marker.Label).Order());
        Assert.All(markers, m => Assert.False(m.Focused));
    }

    [Fact]
    public void Without_picks_there_is_no_guide()
    {
        var (camera, scene) = Of([Quest("a", 100, 0, "A", group: "a")]);
        scene.Player = new PlayerFix(new WorldPoint(0, 0, 0), null, DateTime.Now);
        Assert.Null(MapRenderer.Layout(camera, scene, 1).Guide);
        Assert.DoesNotContain(MapRenderer.Layout(camera, scene, 1).Markers, m => m.Kept);
    }

    [Fact]
    public void Pointing_at_a_quest_still_steps_the_others_back()
    {
        var (_, scene) = Of([Quest("a", 100, 0, "A", group: "a"), Quest("b", -100, 0, "B", group: "b")]);
        scene.Kept = new HashSet<string> { "b" };
        scene.Focus = new HashSet<string> { "a" };
        Assert.True(scene.HasHighlight);
        Assert.Equal(["a"], scene.ShownFocus);
    }
}
