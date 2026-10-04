using Shturmap.Core;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// The map states distances: the guide line carries the card's number, a scale bar follows the zoom (cartography review, 2026-10-02).
public class DistanceTests
{
    [Theory]
    [InlineData(1.0, 100)]
    [InlineData(0.5, 200)]
    [InlineData(2.6, 25)]
    [InlineData(0.05, 2000)]
    [InlineData(100.0, 1)]
    [InlineData(1.3, 50)]
    public void The_scale_bar_takes_the_longest_round_length_that_fits(double pixelsPerMetre, double metres) =>
        Assert.Equal(metres, MapRenderer.ScaleBarMetres(pixelsPerMetre, 120));

    [Fact]
    public void The_scale_bar_follows_the_zoom()
    {
        var (camera, scene) = Of([]);
        Assert.Equal(100, MapRenderer.Layout(camera, scene, 1).Scale.Metres);
        camera.ZoomAt(new SkiaSharp.SKPoint(500, 500), 4);
        var scale = MapRenderer.Layout(camera, scene, 1).Scale;
        Assert.Equal(25, scale.Metres);
        Assert.Equal(100, scale.Pixels, 3);
    }

    // A render stretched along one axis has no one scale: the bar is true the way it lies (the review of 2026-10-04:
    // it took the mean of both axes, which made "50 m" span 69 m on Icebreaker).
    [Fact]
    public void On_a_stretched_map_the_scale_bar_is_true_along_the_bar()
    {
        var scene = new MapScene(Fixtures.Definition("icebreaker"), null);
        var camera = new Camera();
        camera.Resize(new SkiaSharp.SKSize(1600, 1000));
        camera.Fit(scene.Projection.WorldRect);
        var bar = MapRenderer.Layout(camera, scene, 1).Scale;

        // The bar's two ends, back in the world, are as many metres apart as it says.
        var from = scene.Projection.ToWorld(camera.ToMap(bar.Origin));
        var to = scene.Projection.ToWorld(camera.ToMap(new SkiaSharp.SKPoint(bar.Origin.X + bar.Pixels, bar.Origin.Y)));
        Assert.Equal(bar.Metres, Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Z - from.Z, 2)), 3);
        // Icebreaker's transform: 2.0 map units a metre across the screen, 3.5 down it. The bar takes the first, not
        // their mean (2.75).
        Assert.Equal(2.0 * camera.Zoom, bar.Pixels / bar.Metres, 6);
    }

    [Theory]
    [InlineData(0, "69 m")]
    [InlineData(4.5, "69 m")] // the position's age isn't said beside a distance: it read as minutes to get there
    public void The_guide_plate_says_the_cards_distance_and_nothing_else(double minutes, string plate)
    {
        var (camera, scene) = Of([Quest("near", 69, 0, "Revision", group: "revision"), Quest("far", -300, 0, "Revision", group: "revision")]);
        camera.ZoomAt(new SkiaSharp.SKPoint(500, 500), 4);
        scene.Player = new PlayerFix(new WorldPoint(0, 0, 0), null, DateTime.Now - TimeSpan.FromMinutes(minutes));
        scene.Kept = new HashSet<string> { "revision" };
        var guide = MapRenderer.Layout(camera, scene, 1).Guide!;
        Assert.Equal(69, guide.Metres, 3);
        Assert.Equal(plate, guide.Plate);
    }

    // The plate stood on a boss marker in a snapshot (the review of 2026-10-04): it gives way along the line.
    [Fact]
    public void The_guide_plate_gives_way_to_a_symbol_under_the_lines_middle()
    {
        // The player at the middle of the view, the pick 400 m to the right: the line's middle is at (700, 500).
        var pick = Quest("far", 400, 0, "Revision", group: "revision");
        var boss = new MapMarker("boss:1", MarkerKind.BossSpawn, new WorldPoint(200, 0, 0), "Kaban 75%", "boss-zone");
        var (camera, scene) = Of([pick]);
        scene.Player = new PlayerFix(new WorldPoint(0, 0, 0), null, DateTime.Now);
        scene.Kept = new HashSet<string> { "revision" };
        var free = MapRenderer.Layout(camera, scene, 1).Guide!.PlateBox;
        Assert.Equal(700, free.MidX, 3);

        scene.Markers = [pick, boss];
        var layout = MapRenderer.Layout(camera, scene, 1);
        var symbol = layout.Markers.Single(m => m.Marker == boss);
        var box = layout.Guide!.PlateBox;
        Assert.False(box.IntersectsWith(new SkiaSharp.SKRect(symbol.At.X - symbol.Reach, symbol.At.Y - symbol.Reach, symbol.At.X + symbol.Reach, symbol.At.Y + symbol.Reach)),
            $"the plate {box} covers the boss marker at {symbol.At}");
        // Still on the line, toward the place, and no further than it has to go.
        Assert.Equal(500, box.MidY, 3);
        Assert.InRange(box.MidX, 701, 700 + symbol.Reach + box.Width / 2 + 6);
        Assert.Equal("400 m", layout.Guide.Plate);
        // No label of another marker goes under it either.
        Assert.All(layout.Labels, l => Assert.False(l.Box.IntersectsWith(box), $"{l.Text} under the plate"));
    }

    [Fact]
    public void With_no_free_place_on_the_line_the_plate_keeps_the_middle()
    {
        // Other quests' markers all along the line, every 20 px.
        var crowd = Enumerable.Range(2, 17).Select(i => Quest($"q{i}", i * 20, 0, "", group: $"other-{i}"));
        var (camera, scene) = Of([Quest("far", 400, 0, "Revision", group: "revision"), .. crowd]);
        scene.Player = new PlayerFix(new WorldPoint(0, 0, 0), null, DateTime.Now);
        scene.Kept = new HashSet<string> { "revision" };
        Assert.Equal(700, MapRenderer.Layout(camera, scene, 1).Guide!.PlateBox.MidX, 3);
    }

    [Fact]
    public void A_short_guide_carries_no_plate()
    {
        var (camera, scene) = Of([Quest("near", 20, 0, "Revision", group: "revision")]);
        scene.Player = new PlayerFix(new WorldPoint(0, 0, 0), null, DateTime.Now);
        scene.Kept = new HashSet<string> { "revision" };
        Assert.Null(MapRenderer.Layout(camera, scene, 1).Guide!.Plate);
    }

    [Fact]
    public void Distances_read_like_the_cards() => Assert.Equal(["69 m", "999 m", $"{1.2:0.0} km"], new[] { 69.4, 999.4, 1234 }.Select(MapRenderer.DistanceText));
}
