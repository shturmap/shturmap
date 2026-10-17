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
    // The boss is lit here, by a picked quest that targets it (drawn with the rest, not after it as the pointed-at is): at
    // rest it is hollow (BossRestTests).
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_boss_lies_on_top_of_a_scav_spawn_at_the_same_place(bool bossFirst)
    {
        var boss = new MapMarker("boss:1", MarkerKind.BossSpawn, new WorldPoint(0, 0, 0), "Boss 50%", "boss:one");
        var scavs = new MapMarker("scav:1", MarkerKind.ScavSpawn, new WorldPoint(0, 0, 0), "", "scav:zone");
        var (camera, scene) = Of(bossFirst ? [boss, scavs] : [scavs, boss]);
        scene.QuestBosses = new Dictionary<string, IReadOnlyList<string>> { ["quest-k"] = ["boss:one"] };
        scene.Kept = new HashSet<string> { "quest-k" };
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

    // A picked or pointed-at name at the map's right edge is drawn whole or trimmed with "…", never cut by the edge
    // (owner, 2026-10-15, E3: in German the tour's chapter 4 read "Lieferung aus der Vergan" there at full size). A
    // made-up name as long, its symbol 150 px in from the edge, as a framing leaves the place furthest right
    // (Camera.LabelRoom): no pixel of the name's colour in the map's last two columns, and as many of them as the name
    // has drawn in the middle of the map, or, crowded on its other sides, the dots of the "…" at its end.
    [Fact]
    public void A_picked_name_at_the_right_edge_is_drawn_whole_or_with_an_ellipsis_never_cut()
    {
        const string name = "Eine Lieferung aus ferner Vergangenheit";
        static int Lit(SKBitmap bitmap, SKColor color, SKRect box)
        {
            var n = 0;
            for (var y = Math.Max(0, (int)box.Top); y < Math.Min(bitmap.Height, (int)Math.Ceiling(box.Bottom)); y++)
            {
                for (var x = Math.Max(0, (int)box.Left); x < Math.Min(bitmap.Width, (int)Math.Ceiling(box.Right)); x++)
                {
                    var c = bitmap.GetPixel(x, y);
                    n += Math.Abs(c.Red - color.Red) + Math.Abs(c.Green - color.Green) + Math.Abs(c.Blue - color.Blue) <= 60 ? 1 : 0;
                }
            }
            return n;
        }
        var edge = SKRect.Create(998, 0, 2, 1000);
        (Camera, MapScene, MapRenderer.PlacedLabel) Picked(params MapMarker[] markers)
        {
            var (camera, scene) = Of(markers);
            scene.Kept = new HashSet<string> { "quest-q" };
            return (camera, scene, MapRenderer.Layout(camera, scene, 1).Labels.Single(l => l.Of.Marker.Group == "quest-q"));
        }

        var (middleCamera, middle, inMiddle) = Picked(Quest("q", 0, 0, name));
        using var middleBitmap = Render(middle, middleCamera);
        var whole = Lit(middleBitmap, inMiddle.Color, inMiddle.Box);
        Assert.True(whole > 100, $"{whole} pixels of the name's colour");

        var (camera, scene, atEdge) = Picked(Quest("q", 350, 0, name));
        using var bitmap = Render(scene, camera);
        Assert.Equal(name, atEdge.Text);
        Assert.InRange(Lit(bitmap, atEdge.Color, atEdge.Box), whole * 0.9, whole * 1.1);
        Assert.Equal(0, Lit(bitmap, atEdge.Color, edge));

        var (crowdedCamera, crowded, trimmed) = Picked(Quest("q", 350, 0, name),
            new MapMarker("extract:l", MarkerKind.ExtractPmc, new WorldPoint(325, 0, 0), ""),
            new MapMarker("extract:u", MarkerKind.ExtractPmc, new WorldPoint(350, 0, 22), ""),
            new MapMarker("extract:d", MarkerKind.ExtractPmc, new WorldPoint(350, 0, -22), ""));
        using var crowdedBitmap = Render(crowded, crowdedCamera);
        Assert.EndsWith("…", trimmed.Text);
        Assert.Equal(0, Lit(crowdedBitmap, trimmed.Color, edge));
        Assert.True(Lit(crowdedBitmap, trimmed.Color, SKRect.Create(trimmed.Box.Right - 8, trimmed.Box.Top, 8, trimmed.Box.Height)) > 0, "no ellipsis drawn");
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
