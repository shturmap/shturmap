using SkiaSharp;
using Shturmap.Core;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// The raid replay (owner, 2026-10-07: "C, but encode raid time to the color of the pen stroke. Use a fitting color map
// based on the apps design"; docs/DESIGN.md "Map drawing", *The raid replay*).
public class ReplayTests
{
    private static ReplayFix Fix(double minute, double x, double z) => new(minute, new WorldPoint(x, 0, z));

    private static RaidReplay Raid(params ReplayFix[] fixes) => new("test", "Test map", fixes, 33);

    [Fact]
    public void A_raid_replays_with_three_positions_over_five_minutes_or_more()
    {
        Assert.True(Raid(Fix(0, 0, 0), Fix(3, 10, 0), Fix(5, 20, 0)).Plays);
        // Two positions are a dot or two, not a raid; three within a few minutes say nothing of it either.
        Assert.False(Raid(Fix(0, 0, 0), Fix(20, 10, 0)).Plays);
        Assert.False(Raid(Fix(10, 0, 0), Fix(12, 10, 0), Fix(14.9, 20, 0)).Plays);
        Assert.False(new RaidReplay("test", "Test map", [Fix(0, 0, 0), Fix(3, 10, 0), Fix(6, 20, 0)], 0).Plays);
    }

    [Theory]
    [InlineData(31.2, "LAST SEEN · 1 MIN BEFORE THE END")]
    [InlineData(30.9, "LAST SEEN · 2 MIN BEFORE THE END")]
    [InlineData(32.5, "LAST SEEN · JUST BEFORE THE END")]
    public void The_last_position_says_how_long_before_the_end_it_was_taken(double last, string text) =>
        Assert.Equal(text, Raid(Fix(0, 0, 0), Fix(10, 10, 0), Fix(last, 20, 0)).LastSeenText);

    [Fact]
    public void A_positions_minute_says_in_as_the_raid_clock_does() // bare minutes read as a time to get somewhere
    {
        Assert.Equal("8 MIN IN", RaidReplay.MinuteText(8.79));
        Assert.Equal("0 MIN IN", RaidReplay.MinuteText(0.07));
    }

    [Fact]
    public void The_raid_plays_in_eleven_seconds_whatever_its_length()
    {
        Assert.Equal(0, ReplayTiming.MinuteAt(TimeSpan.FromSeconds(-1), 33));
        Assert.Equal(16.5, ReplayTiming.MinuteAt(TimeSpan.FromSeconds(5.5), 33), 6);
        Assert.Equal(33, ReplayTiming.MinuteAt(TimeSpan.FromSeconds(20), 33));
        Assert.Equal(11.0 / 40, ReplayTiming.SecondsPerMinute(40), 6);
        // Entrance, the band going down, the raid, the hold, the fade: about 17 s in all (owner, 2026-10-08: longer).
        Assert.InRange(ReplayTiming.Total.TotalSeconds, 17, 18);
    }

    [Fact]
    public void The_pen_runs_from_faint_sand_to_full_sand_and_widens()
    {
        var sand = Palette.Sk(Palette.Sand);
        var ground = Palette.Sk(Palette.Ground);
        Assert.Equal(sand, ReplayInk.At(1));
        // 22 % sand at the raid's start: sand mixed into the ground, opaque, the same hue.
        var start = ReplayInk.At(0);
        Assert.Equal(255, start.Alpha);
        Assert.Equal(Math.Round(ground.Red + (sand.Red - ground.Red) * 0.22), start.Red, 0);
        // Lighter as the raid goes on, never darker.
        var lightness = Enumerable.Range(0, 11).Select(i => ReplayInk.At(i / 10.0)).Select(c => c.Red + c.Green + c.Blue).ToList();
        Assert.Equal(lightness.Order(), lightness);
        Assert.Equal(1.4f, ReplayInk.Width(0));
        Assert.Equal(3.2f, ReplayInk.Width(1));
        Assert.Equal(3.2f, ReplayInk.Width(2)); // never past the end
    }

    // Draws the scene into a picture the size of the test view.
    private static SKBitmap Render(Camera camera, MapScene scene)
    {
        var bitmap = new SKBitmap(1000, 1000);
        using var canvas = new SKCanvas(bitmap);
        MapRenderer.Render(canvas, camera, scene);
        return bitmap;
    }

    // How much of the pen's sand lies in a box around a point (screen pixels).
    private static int Sandy(SKBitmap bitmap, int x, int y, int r = 4)
    {
        var count = 0;
        for (var i = x - r; i <= x + r; i++)
            for (var j = y - r; j <= y + r; j++)
            {
                var c = bitmap.GetPixel(i, j);
                if (c.Red > 60 && c.Red > c.Blue + 8)
                    count++;
            }
        return count;
    }

    [Fact]
    public void The_pen_is_drawn_up_to_the_playhead_and_no_further()
    {
        var (camera, scene) = Of([]);
        // On the test sheet a metre is a pixel: x to the right, z upward from the middle.
        scene.Replay = new RaidReplay("test", "Test map", [Fix(0, -200, 0), Fix(10, 0, 0), Fix(20, 200, 0)], 30);
        scene.ReplayMinute = 10;
        using var halfway = Render(camera, scene);
        Assert.True(Sandy(halfway, 400, 500) > 5, "the first leg is drawn");
        Assert.True(Sandy(halfway, 500, 500) > 5, "the second position is drawn");
        Assert.Equal(0, Sandy(halfway, 600, 500));
        Assert.Equal(0, Sandy(halfway, 700, 500));

        // At the end every leg is there, the last brighter than the first.
        scene.ReplayDone = true;
        using var end = Render(camera, scene);
        Assert.True(Sandy(end, 600, 500) > 5, "the last leg is drawn");
        var early = end.GetPixel(350, 500);
        var late = end.GetPixel(650, 500);
        Assert.True(late.Red + late.Green + late.Blue > early.Red + early.Green + early.Blue, $"early {early}, late {late}");
    }

    [Fact]
    public void The_map_recedes_under_the_replay()
    {
        var (camera, scene) = Of([Quest("a", 300, 300, "Somewhere")]);
        using var before = Render(camera, scene);
        scene.Replay = new RaidReplay("test", "Test map", [Fix(0, -200, 0), Fix(10, 0, 0), Fix(20, 200, 0)], 30);
        scene.ReplayDone = true;
        using var during = Render(camera, scene);
        // The quest's marker, far from the pen, is darker under the replay.
        var a = before.GetPixel(800, 200);
        var b = during.GetPixel(800, 200);
        Assert.True(b.Red + b.Green + b.Blue < a.Red + a.Green + a.Blue, $"before {a}, during {b}");
    }

    [Fact]
    public void Help_lists_the_replay_while_it_plays_on_the_map()
    {
        var (_, scene) = Of([]);
        Assert.DoesNotContain(LegendSymbol.Replay, MapLegend.On(scene));
        scene.Replay = Raid(Fix(0, 0, 0), Fix(3, 10, 0), Fix(6, 20, 0));
        Assert.Contains(LegendSymbol.Replay, MapLegend.On(scene));
    }
}
