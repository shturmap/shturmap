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

    // At the smallest window the bar lay over a lit extract on Streets, whose name then had no room (owner, 2026-10-15): a
    // symbol under it moves it up by what it takes, 2 px clear of the symbol's box, and the labels give way to it there.
    [Fact]
    public void The_scale_bar_steps_up_from_a_symbol_under_it()
    {
        var (camera, scene) = Of([]);
        var home = MapRenderer.Layout(camera, scene, 1).Scale;
        Assert.Equal(new SkiaSharp.SKPoint(18, 934), home.Origin);

        // A lit extract on the bar's middle mark (68, 930 on screen).
        scene.Markers = [new MapMarker("extract:a", MarkerKind.ExtractPmc, new WorldPoint(-432, 0, -430), "Crash Site")];
        scene.ExitsListed = new HashSet<string> { "extract:a" };
        var layout = MapRenderer.Layout(camera, scene, 1);
        var m = layout.Markers.Single();
        var symbol = new SkiaSharp.SKRect(m.At.X - m.Reach, m.At.Y - m.Reach, m.At.X + m.Reach, m.At.Y + m.Reach);
        var bar = layout.Scale;
        Assert.False(bar.Box.IntersectsWith(symbol), $"the bar {bar.Box} over the extract {symbol}");
        Assert.Equal(symbol.Top - 2, bar.Box.Bottom, 3);
        Assert.Equal(18, bar.Origin.X);
        Assert.Equal((home.Metres, home.Pixels), (bar.Metres, bar.Pixels));
        // Its name has room again, clear of the bar.
        var name = Assert.Single(layout.Labels);
        Assert.False(name.Box.IntersectsWith(bar.Box), $"{name.Box} under the bar {bar.Box}");

        // A receded symbol, a Scav zone's ring, it may cover, as other symbols do.
        scene.Markers = [new MapMarker("spawn:scav:1", MarkerKind.ScavSpawn, new WorldPoint(-432, 0, -430), "")];
        Assert.Equal(home.Origin, MapRenderer.Layout(camera, scene, 1).Scale.Origin);

        // A crowd up the corner, no free place within four of its heights: it stays where it stands.
        scene.Markers = [.. Enumerable.Range(0, 7).Select(i => Quest($"q{i}", i % 2 == 0 ? -460 : -400, -430 + 24 * i, "", group: $"q{i}"))];
        Assert.Equal(home.Origin, MapRenderer.Layout(camera, scene, 1).Scale.Origin);
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
        // Other quests' markers all along the line, every 24 px: touching, so none is set apart off the line (Repel).
        var crowd = Enumerable.Range(2, 14).Select(i => Quest($"q{i}", i * 24, 0, "", group: $"other-{i}"));
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

    // In the language in use, English in tests, whatever the PC's own number format (until 2026-10-10 the thread's
    // culture wrote them: "1,2 km" inside English texts on a German Windows).
    [Fact]
    public void Distances_read_like_the_cards() => Assert.Equal(["69 m", "999 m", "1.2 km"], new[] { 69.4, 999.4, 1234 }.Select(MapRenderer.DistanceText));
}
