using SkiaSharp;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// A quest just completed: its places ring out in gold with a check and go (owner, 2026-10-07).
public class CompletedTests
{
    private static int Gold(SKBitmap bitmap, int x, int y, int r = 12)
    {
        var amber = Palette.Sk(Palette.Amber);
        var count = 0;
        for (var i = x - r; i <= x + r; i++)
            for (var j = y - r; j <= y + r; j++)
            {
                var c = bitmap.GetPixel(i, j);
                if (Math.Abs(c.Red - amber.Red) < 30 && Math.Abs(c.Green - amber.Green) < 30 && Math.Abs(c.Blue - amber.Blue) < 30)
                    count++;
            }
        return count;
    }

    private static SKBitmap Render(Camera camera, MapScene scene)
    {
        var bitmap = new SKBitmap(1000, 1000);
        using var canvas = new SKCanvas(bitmap);
        MapRenderer.Render(canvas, camera, scene);
        return bitmap;
    }

    [Fact]
    public void A_completed_quests_places_ring_out_and_then_are_gone()
    {
        var (camera, scene) = Of([]);
        var place = Quest("a", 100, 100, "Gratitude");
        // On the test sheet a metre is a pixel: (100, 100) is drawn at (600, 400).
        scene.Leaving = [(place, DateTime.Now)];
        Assert.True(scene.LeavingNow);
        using (var now = Render(camera, scene))
            Assert.True(Gold(now, 600, 400) > 40, "the gold disc is drawn");

        scene.Leaving = [(place, DateTime.Now - MapScene.LeaveLength - TimeSpan.FromSeconds(0.1))];
        Assert.False(scene.LeavingNow);
        using (var later = Render(camera, scene))
            Assert.Equal(0, Gold(later, 600, 400));
    }

    [Fact]
    public void Without_animation_effects_the_places_simply_go()
    {
        var (camera, scene) = Of([]);
        scene.Pulse = false;
        scene.Leaving = [(Quest("a", 100, 100, "Gratitude"), DateTime.Now)];
        Assert.False(scene.LeavingNow);
        using var bitmap = Render(camera, scene);
        Assert.Equal(0, Gold(bitmap, 600, 400));
    }

    [Fact]
    public void Help_lists_it_where_a_quest_has_a_place()
    {
        Assert.Contains(LegendSymbol.Completed, MapLegend.On(Of([Quest("a", 0, 0, "Quest")]).Scene));
        Assert.DoesNotContain(LegendSymbol.Completed, MapLegend.On(Of([]).Scene));
    }
}
