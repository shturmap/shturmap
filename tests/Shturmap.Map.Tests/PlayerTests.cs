using SkiaSharp;
using Shturmap.Core;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// The player stays at full strength; its age is said by a dashed ring and a tag (cartography review, 2026-10-02).
public class PlayerTests
{
    [Theory]
    [InlineData(1.2, "1 MIN")]
    [InlineData(4.9, "4 MIN")]
    [InlineData(59.9, "59 MIN")]
    [InlineData(95, "1 H")]
    public void The_age_reads_in_whole_units(double minutes, string text) => Assert.Equal(text, MapRenderer.AgeText(TimeSpan.FromMinutes(minutes)));

    [Fact]
    public void Labels_keep_clear_of_the_player_and_its_age_tag()
    {
        var markers = Enumerable.Range(0, 24).Select(i => Quest($"q{i}", (i % 6) * 22 - 50, (i / 6) * 20 - 30, $"Quest {i}")).ToList();
        var (camera, scene) = Of(markers);
        scene.Player = new PlayerFix(new WorldPoint(0, 0, 0), null, DateTime.Now - TimeSpan.FromMinutes(4));
        var layout = MapRenderer.Layout(camera, scene, 1);
        // The player is drawn at the view's centre; ring and tag reach from 15 px left of it to 60 px right.
        var player = new SKRect(500 - 15, 500 - 15, 500 + 60, 500 + 15);
        Assert.NotEmpty(layout.Labels);
        Assert.All(layout.Labels, l => Assert.False(l.Box.IntersectsWith(player), $"{l.Text} at {l.Box}"));
    }
}
