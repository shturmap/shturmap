using SkiaSharp;
using Shturmap.Core;

namespace Shturmap.Map.Tests;

// Two symbols of the same rank at one place covered each other (the review of 2026-10-04, C6: on Streets a transit's
// diamond lay over an extract's triangle). Since 2026-10-06 quests, bosses and ways out are set apart on leaders
// (RepelTests); the receded ones (locks, switches, Scav zones' rings) still stand side by side, each beside its true
// place by the least that shows both, and come back to it as the view zooms in.
public class SideBySideTests
{
    private const float Gap = 3;

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
    public void Two_padlocks_at_one_door_stand_side_by_side_without_leaders()
    {
        var (camera, scene) = TestView.Of([
            new MapMarker("lock:a", MarkerKind.Lock, new WorldPoint(0, 0, 0), "A", "key:a"),
            new MapMarker("lock:b", MarkerKind.Lock, new WorldPoint(0, 0, 0), "B", "key:b"),
        ]);
        var shown = MapRenderer.ShownMarkers(camera, scene, 1);
        var (a, b) = (shown.Single(m => m.Marker.Id == "lock:a"), shown.Single(m => m.Marker.Id == "lock:b"));
        Assert.True(a.At.X < 500 && b.At.X > 500);
        Assert.Equal(5.5f + 5.5f + Gap, b.At.X - a.At.X, 2);
        Assert.All(shown, m => Assert.Null(m.Leader));
    }
}
