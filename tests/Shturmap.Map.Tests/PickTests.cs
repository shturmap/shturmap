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

    // Picks are level 1, "drawn last" (the review of 2026-10-04: in a raid the ways out don't step back either, and
    // drawn in one group with them the picks came first and lay under other quests' markers).
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_pick_is_never_covered_by_another_quests_marker(bool inRaid)
    {
        var (camera, scene) = Of([
            new MapMarker("extract:e", MarkerKind.ExtractPmc, new WorldPoint(-200, 0, 0), "Exit"),
            Quest("a", 0, 0, "A", group: "a"),
            Quest("b", 16, 0, "B", group: "b"),
        ]);
        scene.InRaid = inRaid;
        scene.Kept = new HashSet<string> { "a" };
        using var bitmap = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(1000, 1000, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul));
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        MapRenderer.Render(canvas, camera, scene);
        // Where the two discs overlap, clear of both glyphs (the pick's own centre holds its glyph): the pick's cyan,
        // not the other quest's gold.
        var pixel = bitmap.GetPixel(510, 496);
        var cyan = MapRenderer.Kept;
        Assert.True(Math.Abs(pixel.Red - cyan.Red) <= 3 && Math.Abs(pixel.Green - cyan.Green) <= 3 && Math.Abs(pixel.Blue - cyan.Blue) <= 3,
            $"{pixel} where the pick ({cyan}) and the other quest's marker overlap");
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

    // An objective the player ticked as done is no place to go to (owner, 2026-10-04): in a picked quest it keeps the
    // quiet done look, without the pick's size, ring and name, and the guide line leads past it to an open place.
    [Fact]
    public void A_done_objective_of_a_picked_quest_keeps_its_quiet_look()
    {
        var (camera, scene) = Of([Quest("open", 300, 0, "A", group: "a"), Quest("done", 20, 0, "A", MarkerKind.ObjectiveDone, group: "a")]);
        scene.Player = new PlayerFix(new WorldPoint(0, 0, 0), null, DateTime.Now);
        scene.Kept = new HashSet<string> { "a" };
        var layout = MapRenderer.Layout(camera, scene, 1);
        var done = layout.Markers.Single(m => m.Marker.Kind == MarkerKind.ObjectiveDone);
        var open = layout.Markers.Single(m => m.Marker.Kind == MarkerKind.Objective);
        Assert.True(open.Kept);
        Assert.False(done.Kept);
        Assert.False(done.Selected);
        Assert.Equal(10, done.R);
        Assert.DoesNotContain(layout.Labels, l => l.Of == done);
        Assert.Equal(300, layout.Guide!.Metres, 3);

        // Pointed at, it is still no bigger and still unnamed.
        scene.Kept = new HashSet<string>();
        scene.Focus = new HashSet<string> { "a" };
        var pointed = MapRenderer.Layout(camera, scene, 1);
        Assert.Equal(10, pointed.Markers.Single(m => m.Marker.Kind == MarkerKind.ObjectiveDone).R);
        Assert.Equal(12, pointed.Markers.Single(m => m.Marker.Kind == MarkerKind.Objective).R);
    }
}
