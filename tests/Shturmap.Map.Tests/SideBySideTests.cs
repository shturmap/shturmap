using SkiaSharp;
using Shturmap.Core;
using Shturmap.Core.Maps;
using Shturmap.Core.Quests;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// Two symbols of the same rank at one place covered each other (the review of 2026-10-04, C6: on Streets a transit's
// diamond lay over an extract's triangle). They stand side by side now, each beside its true place by the least that
// shows both, and come back to it as the view zooms in.
public class SideBySideTests
{
    private const float Gap = 3;

    private static MapMarker Extract(double x, double z) => new("extract:gate", MarkerKind.ExtractPmc, new WorldPoint(x, 0, z), "Gate");

    private static MapMarker Transit(double x, double z) => new("transit:lab", MarkerKind.Transit, new WorldPoint(x, 0, z), "Transit to The Lab");

    private static MapRenderer.ShownMarker The(IEnumerable<MapRenderer.ShownMarker> shown, string id) => shown.Single(m => m.Marker.Id == id);

    [Fact]
    public void Two_of_one_rank_at_one_place_stand_side_by_side()
    {
        var at = new SKPoint(500, 500);
        var placed = MapRenderer.SideBySide([(at, 8.25f, 3), (at, 7.5f, 3)], Gap);
        // Left and right of the place, the earlier one on the left, apart by their half widths and the collar.
        Assert.Equal(500, placed[0].Y);
        Assert.Equal(500, placed[1].Y);
        Assert.True(placed[0].X < 500 && placed[1].X > 500);
        Assert.Equal(8.25f + 7.5f + Gap, placed[1].X - placed[0].X, 2);
        // Each by the same, and neither further than its own width.
        Assert.Equal(500 - placed[0].X, placed[1].X - 500, 2);
        Assert.True(500 - placed[0].X <= 2 * 8.25f);
    }

    [Fact]
    public void Symbols_of_different_rank_stay_where_they_are()
    {
        var at = new SKPoint(500, 500);
        // A boss over a Scav zone's ring: the octagon lies on top, nothing moves.
        Assert.Equal([at, at], MapRenderer.SideBySide([(at, 7.2f, 1), (at, 4f, 4)], Gap));
    }

    [Theory]
    [InlineData(4f)]
    [InlineData(10f)]
    [InlineData(17f)]
    public void Two_near_each_other_part_along_the_line_between_them_by_what_is_missing(float apart)
    {
        var a = new SKPoint(500, 500);
        var b = new SKPoint(500, 500 + apart);
        var placed = MapRenderer.SideBySide([(a, 7.5f, 3), (b, 7.5f, 3)], Gap);
        Assert.Equal(500, placed[0].X, 3);
        Assert.Equal(500, placed[1].X, 3);
        Assert.Equal(18, placed[1].Y - placed[0].Y, 2);
        Assert.Equal((18 - apart) / 2, a.Y - placed[0].Y, 2);
    }

    [Fact]
    public void Two_far_enough_apart_are_left_alone()
    {
        var a = new SKPoint(500, 500);
        var b = new SKPoint(518, 500);
        Assert.Equal([a, b], MapRenderer.SideBySide([(a, 7.5f, 3), (b, 7.5f, 3)], Gap));
    }

    [Fact]
    public void Three_at_one_place_all_show_and_none_goes_further_than_its_width()
    {
        var at = new SKPoint(500, 500);
        var placed = MapRenderer.SideBySide([(at, 10, 2), (at, 10, 2), (at, 10, 2)], Gap);
        Assert.All(placed, p => Assert.True(SKPoint.Distance(p, at) <= 20.01f));
        Assert.Equal(3, placed.Select(p => p.X).Distinct().Count());
    }

    [Fact]
    public void An_extract_and_a_transit_at_one_spot_are_both_drawn_and_both_found()
    {
        var (camera, scene) = TestView.Of([Extract(0, 0), Transit(0, 0)]);
        var shown = MapRenderer.ShownMarkers(camera, scene, 1);
        var (extract, transit) = (The(shown, "extract:gate"), The(shown, "transit:lab"));
        Assert.True(extract.At.X < 500 && transit.At.X > 500);
        Assert.Equal(MapRenderer.RestHalf(extract.Marker, 1) + MapRenderer.RestHalf(transit.Marker, 1) + Gap, transit.At.X - extract.At.X, 2);

        // The pointer finds each where it is drawn.
        Assert.Equal("extract:gate", MapRenderer.HitTest(camera, scene, extract.At, 1)?.Id);
        Assert.Equal("transit:lab", MapRenderer.HitTest(camera, scene, transit.At, 1)?.Id);

        // Both symbols show: green on the left, violet on the right.
        using var bitmap = new SKBitmap(new SKImageInfo(1000, 1000, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
            MapRenderer.Render(canvas, camera, scene);
        Near(Palette.Green, bitmap.GetPixel((int)Math.Round(extract.At.X), 502));
        Near(Palette.Violet, bitmap.GetPixel((int)Math.Round(transit.At.X), 500));
    }

    private static void Near(string expected, SKColor actual)
    {
        var e = SKColor.Parse(expected);
        Assert.True(Math.Abs(e.Red - actual.Red) <= 3 && Math.Abs(e.Green - actual.Green) <= 3 && Math.Abs(e.Blue - actual.Blue) <= 3, $"{actual}, expected {e}");
    }

    // In a crowded corner the pair's names both show: the second a line under the first, as the pair's caption. On
    // Streets the transit's name was lost when its diamond moved off the extract's triangle.
    [Fact]
    public void Both_names_of_a_pair_show_also_where_only_the_place_below_is_free()
    {
        MapMarker Boss(string id, double x, double z) => new("boss:" + id, MarkerKind.BossSpawn, new WorldPoint(x, 0, z), "", "boss:" + id);
        // Bosses left, right and above take those places from both names.
        var (camera, scene) = TestView.Of([Extract(0, 0), Transit(0, 0), Boss("left", -34, 0), Boss("right", 34, 0), Boss("above", 0, 25)]);
        var labels = MapRenderer.Layout(camera, scene, 1).Labels;
        var gate = labels.Single(l => l.Text == "Gate");
        var transit = labels.Single(l => l.Text == "Transit to The Lab");
        Assert.True(gate.Box.Top > 500 && transit.Box.Top >= gate.Box.Bottom, $"{gate.Box} and {transit.Box}");
        Assert.True(The(MapRenderer.ShownMarkers(camera, scene, 1), "transit:lab").Beside);
    }

    [Fact]
    public void Zooming_in_brings_each_back_to_its_own_place()
    {
        // Two metres apart in the world: one pixel at this zoom, far more than their widths at 16 times it.
        var (camera, scene) = TestView.Of([Extract(0, 0), Transit(2, 0)]);
        var near = MapRenderer.ShownMarkers(camera, scene, 1);
        Assert.True(The(near, "transit:lab").At.X - The(near, "extract:gate").At.X > 15);
        Assert.NotEqual(500, The(near, "extract:gate").At.X);

        camera.ZoomAt(new SKPoint(500, 500), 16);
        var far = MapRenderer.ShownMarkers(camera, scene, 1);
        Assert.Equal(camera.ToScreen(scene.Projection.ToMap(new WorldPoint(0, 0, 0))), The(far, "extract:gate").At);
        Assert.Equal(camera.ToScreen(scene.Projection.ToMap(new WorldPoint(2, 0, 0))), The(far, "transit:lab").At);
    }

    [Fact]
    public void Pointing_at_one_of_them_moves_neither()
    {
        var (camera, scene) = TestView.Of([Extract(0, 0), Transit(0, 0)]);
        var rest = MapRenderer.ShownMarkers(camera, scene, 1);
        scene.Focus = new HashSet<string> { "transit:lab" };
        var pointed = MapRenderer.ShownMarkers(camera, scene, 1);
        Assert.Equal(The(rest, "extract:gate").At, The(pointed, "extract:gate").At);
        Assert.Equal(The(rest, "transit:lab").At, The(pointed, "transit:lab").At);
    }

    [Fact]
    public void Places_of_one_objective_still_merge_and_two_quests_at_one_place_part()
    {
        MapMarker Place(string quest, string objective, int n, double x) =>
            new($"objective:{objective}:{n}", MarkerKind.Objective, new WorldPoint(x, 0, 0), quest, "quest-" + quest, ObjectiveKind.Exploration);
        var (camera, scene) = TestView.Of([Place("a", "one", 1, 0), Place("a", "one", 2, 4), Place("b", "two", 1, 0)]);
        var shown = MapRenderer.ShownMarkers(camera, scene, 1);
        Assert.Equal(2, shown.Count);
        var merged = shown.Single(m => m.Marker.Group == "quest-a");
        var other = shown.Single(m => m.Marker.Group == "quest-b");
        Assert.Equal(2, merged.Count);
        Assert.Equal(20 + Gap, SKPoint.Distance(merged.At, other.At), 1);
    }

    [Fact]
    public void The_guide_ends_on_the_symbol_where_it_is_drawn()
    {
        var mine = Quest("mine", 0, 0, "Ballet Lover");
        var (camera, scene) = TestView.Of([mine, Quest("other", 0, 0, "Dandies")]);
        scene.Player = new PlayerFix(new WorldPoint(-300, 0, 0), 0, DateTime.Now);
        scene.Kept = new HashSet<string> { "quest-mine" };
        var layout = MapRenderer.Layout(camera, scene, 1);
        var drawn = The(layout.Markers, mine.Id);
        Assert.NotEqual(500, drawn.At.X);
        Assert.Equal(drawn.At, layout.Guide!.To);
        // The distance on the plate stays the place's own.
        Assert.Equal(300, layout.Guide.Metres, 3);
    }
}
