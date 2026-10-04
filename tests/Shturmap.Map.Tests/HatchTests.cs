using SkiaSharp;

namespace Shturmap.Map.Tests;

// A hazard area's hatch was drawn across the area's whole box for every frame, also the part outside the window:
// thousands of lines for a border zone when zoomed in (the review of 2026-10-04, A37). Only the lines that cross the
// part in view are drawn now, at the places they had.
public class HatchTests
{
    // The loop the hatch was drawn with: every line across the whole box.
    private static List<float> Before(SKRect box, float step)
    {
        var lines = new List<float>();
        for (var x = box.Left - box.Height; x < box.Right; x += step)
            lines.Add(x + box.Bottom);
        return lines;
    }

    private static float Sum((SKPoint From, SKPoint To) line) => line.From.X + line.From.Y;

    [Fact]
    public void An_area_wholly_in_view_keeps_every_line()
    {
        var box = new SKRect(100, 200, 400, 350);
        var lines = MapRenderer.HatchLines(box, new SKRect(0, 0, 1000, 1000), 5).ToList();
        var before = Before(box, 5);
        Assert.Equal(before, lines.Select(Sum).Take(before.Count).ToList());
        // At most the one line more that only touches the box's far corner.
        Assert.InRange(lines.Count - before.Count, 0, 1);
        // Each is a 45° line across the box's height.
        Assert.All(lines, l =>
        {
            Assert.Equal(box.Bottom, l.From.Y);
            Assert.Equal(box.Top, l.To.Y);
            Assert.Equal(box.Height, l.To.X - l.From.X, 3);
        });
    }

    [Fact]
    public void A_large_area_gets_only_the_lines_that_cross_the_part_in_view()
    {
        // A border zone at a deep zoom: 40,000 by 30,000 px, of which a 1600 by 900 window shows a part.
        var box = new SKRect(-20000, -15000, 20000, 15000);
        var seen = new SKRect(0, 0, 1600, 900);
        var lines = MapRenderer.HatchLines(box, seen, 5).ToList();
        Assert.Equal(14000, Before(box, 5).Count);
        Assert.InRange(lines.Count, 500, 502);
        // At the places the whole box's lines have, so the hatch doesn't swim as the view moves.
        var before = Before(box, 5).ToHashSet();
        Assert.All(lines, l => Assert.Contains(Sum(l), before));
        // And every line that crosses the part in view is among them.
        Assert.Equal(before.Count(c => c >= seen.Left + seen.Top && c <= seen.Right + seen.Bottom), lines.Count);
        Assert.All(lines, l => Assert.True(l.From.Y == seen.Bottom && l.To.Y == seen.Top));
    }

    [Fact]
    public void An_area_out_of_view_gets_none()
    {
        Assert.Empty(MapRenderer.HatchLines(new SKRect(2000, 2000, 3000, 3000), new SKRect(0, 0, 1600, 900), 5));
        Assert.Empty(MapRenderer.HatchLines(new SKRect(0, 0, 100, 100), new SKRect(0, 0, 1600, 900), 0));
    }
}
