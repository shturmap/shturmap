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
