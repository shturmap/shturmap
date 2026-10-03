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

    [Theory]
    [InlineData(0, "69 m")]
    [InlineData(4.5, "69 m · 4 MIN")]
    public void The_guide_plate_says_the_cards_distance_and_the_fixs_age(double minutes, string plate)
    {
        var (camera, scene) = Of([Quest("near", 69, 0, "Revision", group: "revision"), Quest("far", -300, 0, "Revision", group: "revision")]);
        camera.ZoomAt(new SkiaSharp.SKPoint(500, 500), 4);
        scene.Player = new PlayerFix(new WorldPoint(0, 0, 0), null, DateTime.Now - TimeSpan.FromMinutes(minutes));
        scene.Kept = new HashSet<string> { "revision" };
        var guide = MapRenderer.Layout(camera, scene, 1).Guide!;
        Assert.Equal(69, guide.Metres, 3);
        Assert.Equal(plate, guide.Plate);
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
