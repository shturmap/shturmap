using SkiaSharp;
using Shturmap.Core;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// Colour carries one meaning each: green for extracts, teal for Scav extracts, sand for the player and their trail
// (cartography review, 2026-10-02).
public class RenderTests
{
    private static SKBitmap Render(MapScene scene, Camera camera)
    {
        var bitmap = new SKBitmap(new SKImageInfo(1000, 1000, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        MapRenderer.Render(canvas, camera, scene);
        return bitmap;
    }

    private static void Near(string expected, SKColor actual)
    {
        var e = SKColor.Parse(expected);
        Assert.True(Math.Abs(e.Red - actual.Red) <= 3 && Math.Abs(e.Green - actual.Green) <= 3 && Math.Abs(e.Blue - actual.Blue) <= 3, $"{actual}, expected {e}");
    }

    [Fact]
    public void A_done_objective_is_muted_ink_with_a_check()
    {
        var (camera, scene) = Of([Quest("done", 0, 0, "Dandies", MarkerKind.ObjectiveDone)]);
        using var bitmap = Render(scene, camera);
        Near("#8a8778", bitmap.GetPixel(500, 495)); // above the check
        var darkest = Enumerable.Range(497, 7).SelectMany(x => Enumerable.Range(498, 6).Select(y => bitmap.GetPixel(x, y))).Min(c => c.Red);
        Assert.True(darkest < 30, $"no check mark: darkest {darkest}");
    }

    [Fact]
    public void A_shared_extract_is_split_down_the_middle()
    {
        var (camera, scene) = Of([new MapMarker("extract:s", MarkerKind.ExtractShared, new WorldPoint(0, 0, 0), "")]);
        using var bitmap = Render(scene, camera);
        Near("#0b0c0b", bitmap.GetPixel(500, 502));
        Near("#b7b77a", bitmap.GetPixel(503, 502));
    }

    // Where symbols share a place, the one that matters more lies on top (the review of 2026-10-04, C6): a Scav
    // spawn's ring at a boss's spawn zone was drawn over the red octagon, which then read as a red ring. Whichever
    // comes first in the data, the octagon is whole: red at its centre and 3 px out, where the ring's line would run.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_boss_lies_on_top_of_a_scav_spawn_at_the_same_place(bool bossFirst)
    {
        var boss = new MapMarker("boss:1", MarkerKind.BossSpawn, new WorldPoint(0, 0, 0), "Boss 50%", "boss:one");
        var scavs = new MapMarker("scav:1", MarkerKind.ScavSpawn, new WorldPoint(0, 0, 0), "", "scav:zone");
        var (camera, scene) = Of(bossFirst ? [boss, scavs] : [scavs, boss]);
        using var bitmap = Render(scene, camera);
        foreach (var (x, y) in new[] { (500, 500), (503, 500), (497, 500), (500, 503), (500, 497), (504, 500) })
            Near(Palette.Red, bitmap.GetPixel(x, y));
    }

    // The facing cone shows as long as the cards say directions relative to the facing, and no longer (the review of
    // 2026-10-04, B9: the cone stayed for 60 s, the directions for 45 s). Its arrow stands outside the marker's ring,
    // in full sand; without the cone nothing between the ring and 22 px out is.
    [Theory]
    [InlineData(-10, true)]
    [InlineData(10, false)]
    public void The_facing_cone_goes_when_the_cards_stop_saying_ahead(int secondsPastFresh, bool cone)
    {
        var (camera, scene) = Of([]);
        scene.Player = new PlayerFix(new WorldPoint(0, 0, 0), 0, DateTime.Now - Shturmap.Core.Navigation.Facing.Fresh - TimeSpan.FromSeconds(secondsPastFresh));
        using var bitmap = Render(scene, camera);
        var arrow = false;
        for (var y = 476; y <= 524; y++)
        {
            for (var x = 476; x <= 524; x++)
            {
                var r = Math.Sqrt((x - 500) * (x - 500) + (y - 500) * (y - 500));
                arrow |= r is >= 17 and <= 22 && bitmap.GetPixel(x, y).Red > 200;
            }
        }
        Assert.Equal(cone, arrow);
        Assert.Equal(TimeSpan.FromSeconds(45), Shturmap.Core.Navigation.Facing.Fresh);
    }

    [Fact]
    public void A_loose_items_square_is_ink()
    {
        var (camera, scene) = Of([]);
        scene.Spawns = [new WorldPoint(0, 0, 0)];
        var bitmap = new SKBitmap(new SKImageInfo(1000, 1000, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
            MapRenderer.Render(canvas, camera, scene, 2);
        // At twice the size the square's edge is 3 px wide, centred 10 px left of the place.
        using (bitmap)
            Near(Palette.Ink, bitmap.GetPixel(490, 500));
    }

    [Fact]
    public void The_trail_is_drawn_in_the_players_sand()
    {
        var (camera, scene) = Of([]);
        scene.Player = new PlayerFix(new WorldPoint(0, 0, 0), null, DateTime.Now);
        scene.Trail = [new WorldPoint(-200, 0, 0)];
        using var bitmap = Render(scene, camera);
        var dot = bitmap.GetPixel(300, 500);
        Assert.True(dot.Red > 150 && dot.Red >= dot.Blue + 15, $"trail point {dot}: sand is warm and light, teal is not");
    }
}
