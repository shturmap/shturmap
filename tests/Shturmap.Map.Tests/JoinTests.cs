using SkiaSharp;
using Shturmap.Core;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// Objectives of one quest on one spot are one marker that says how many (owner, 2026-10-06, from a panel of five ways:
// "D, count, but make it look nice"): Gratitude's Shemagh and RayBench stashed at the sawmill docks were two discs.
public class JoinTests
{
    // Two objectives ("shemagh", "raybench") of one quest, Gratitude, and their places.
    private static MapMarker Stash(string objective, double x, double z, MarkerKind kind = MarkerKind.Objective) =>
        Quest(objective, x, z, "Gratitude", kind, group: "gratitude");

    private static List<MapRenderer.ShownMarker> Shown(params MapMarker[] markers)
    {
        var (camera, scene) = Of(markers);
        return MapRenderer.Layout(camera, scene, 1).Markers.ToList();
    }

    [Fact]
    public void Two_objectives_of_one_quest_on_one_spot_are_one_marker_with_two_open()
    {
        var (camera, scene) = Of([Stash("shemagh", 0, 0), Stash("raybench", 0.4, 0.3)]);
        var layout = MapRenderer.Layout(camera, scene, 1);
        var marker = Assert.Single(layout.Markers);
        Assert.Equal(["objective:shemagh:1", "objective:raybench:1"], marker.Joined.Select(m => m.Id));
        Assert.Equal(2, MapRenderer.OpenAt(marker));
        // It stands on its place, without a leader, and is named once.
        Assert.Equal(new SKPoint(500, 500), marker.At);
        Assert.Null(marker.Leader);
        Assert.Equal("Gratitude", Assert.Single(layout.Labels).Text);
        // Its tab is the marker too.
        Assert.Equal("objective:shemagh:1", MapRenderer.HitTest(camera, scene, new SKPoint(500 + 22, 500), 1)?.Id);
    }

    [Fact]
    public void Its_name_stands_after_the_tab()
    {
        var (camera, scene) = Of([Stash("shemagh", 0, 0), Stash("raybench", 0, 0)]);
        var label = Assert.Single(MapRenderer.Layout(camera, scene, 1).Labels);
        Assert.True(label.Box.Left > 500 + 10 + 18, $"{label.Box} on the tab");
    }

    [Fact]
    public void Quests_apart_places_apart_and_maybe_places_stay_apart()
    {
        // Two quests on one spot: set apart, on leaders.
        Assert.Equal(2, Shown(Quest("a", 0, 0, "A", group: "a"), Quest("b", 0, 0, "B", group: "b")).Count);
        // Two metres apart, or a floor up: not one spot.
        Assert.Equal(2, Shown(Stash("shemagh", 0, 0), Stash("raybench", 2, 0)).Count);
        Assert.Equal(2, Shown(Stash("shemagh", 0, 0), Quest("raybench", 0, 0, "Gratitude", group: "gratitude") with { Position = new WorldPoint(0, 3, 0) }).Count);
        // A place it may be at: a "?" couldn't say which of the two is only maybe there.
        Assert.Equal(2, Shown(Stash("shemagh", 0, 0), Stash("raybench", 0, 0, MarkerKind.PossibleLocation)).Count);
    }

    [Fact]
    public void One_ticked_done_leaves_a_plain_marker_and_all_done_a_done_one()
    {
        var one = Assert.Single(Shown(Stash("shemagh", 0, 0, MarkerKind.ObjectiveDone), Stash("raybench", 0, 0)));
        Assert.Equal((MarkerKind.Objective, 1), (one.Marker.Kind, MapRenderer.OpenAt(one)));
        Assert.Equal("objective:raybench:1", one.Marker.Id);

        var all = Assert.Single(Shown(Stash("shemagh", 0, 0, MarkerKind.ObjectiveDone), Stash("raybench", 0, 0, MarkerKind.ObjectiveDone)));
        Assert.Equal(MarkerKind.ObjectiveDone, all.Marker.Kind);
    }

    [Fact]
    public void Pointing_at_either_objective_points_at_the_spot()
    {
        var (camera, scene) = Of([Stash("shemagh", 0, 0), Stash("raybench", 0, 0)]);
        scene.Focus = new HashSet<string> { "gratitude" };
        scene.FocusObjective = "raybench";
        Assert.True(Assert.Single(MapRenderer.Layout(camera, scene, 1).Markers).Pointed);
    }

    [Fact]
    public void The_marker_draws_its_count()
    {
        var (camera, scene) = Of([Stash("shemagh", 0, 0), Stash("raybench", 0, 0), Stash("water", 0, 0)]);
        using var bitmap = new SKBitmap(new SKImageInfo(1000, 1000, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
            MapRenderer.Render(canvas, camera, scene);
        // The tab, gold, right of the disc, inside its outline; its figure, dark, in it.
        var gold = Palette.Sk(Palette.Amber);
        var tab = Enumerable.Range(512, 18).SelectMany(x => Enumerable.Range(496, 9).Select(y => bitmap.GetPixel(x, y))).ToList();
        Assert.Contains(tab, p => Math.Abs(p.Red - gold.Red) <= 3 && Math.Abs(p.Green - gold.Green) <= 3 && Math.Abs(p.Blue - gold.Blue) <= 3);
        Assert.Contains(tab, p => p.Red < 60);
    }
}
