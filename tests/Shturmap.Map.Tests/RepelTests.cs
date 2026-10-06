using SkiaSharp;
using Shturmap.Core;
using Shturmap.Core.Maps;
using Shturmap.Core.Quests;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// Symbols that would cover each other are set apart as ggrepel sets labels apart (owner, 2026-10-06, from a panel of
// five ways: "Overlap: D"): pushed clear, kept off each other's places, pulled back toward their own; one that has to
// leave its place stands far enough off it for its leader to be followed, with a dot where it belongs.
public class RepelTests
{
    private const float Gap = 3;

    private static void NoneCovers(IReadOnlyList<(SKPoint At, float Half, int Rank, bool Moves)> symbols, SKPoint[] placed)
    {
        for (var i = 0; i < placed.Length; i++)
        {
            for (var j = i + 1; j < placed.Length; j++)
            {
                if (!symbols[i].Moves || !symbols[j].Moves)
                    continue;
                var want = symbols[i].Half + symbols[j].Half + Gap;
                Assert.True(SKPoint.Distance(placed[i], placed[j]) >= want - 0.5f,
                    $"{i} and {j} stand {SKPoint.Distance(placed[i], placed[j]):0.0} apart, {want:0.0} wanted");
            }
        }
    }

    private static MapMarker Extract(double x, double z) => new("extract:gate", MarkerKind.ExtractPmc, new WorldPoint(x, 0, z), "Gate");

    private static MapMarker Transit(double x, double z) => new("transit:lab", MarkerKind.Transit, new WorldPoint(x, 0, z), "Transit to The Lab");

    private static MapRenderer.ShownMarker The(IEnumerable<MapRenderer.ShownMarker> shown, string id) => shown.Single(m => m.Marker.Id == id);

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(12)]
    public void A_crowd_at_one_place_opens_until_none_covers_another(int count)
    {
        var at = new SKPoint(500, 500);
        var symbols = Enumerable.Range(0, count).Select(_ => (at, 10f, 2, true)).ToList();
        NoneCovers(symbols, MapRenderer.Repel(symbols, Gap, MapRenderer.PlaceClear, MapRenderer.LeaderLeast));
    }

    [Fact]
    public void A_crowd_of_near_places_opens_too_and_leaves_every_place_in_sight()
    {
        var symbols = Enumerable.Range(0, 14)
            .Select(i => (new SKPoint(500 + 6 * MathF.Cos(i * 1.3f) * (i % 3), 500 + 5 * MathF.Sin(i * 0.7f) * (i % 4)), 10f, 2, true))
            .ToList();
        var placed = MapRenderer.Repel(symbols, Gap, MapRenderer.PlaceClear, MapRenderer.LeaderLeast);
        NoneCovers(symbols, placed);
        // No symbol stands on another's place: every dot at the end of a leader shows.
        for (var i = 0; i < placed.Length; i++)
        {
            for (var j = 0; j < placed.Length; j++)
            {
                if (i != j)
                    Assert.True(SKPoint.Distance(placed[i], symbols[j].Item1) >= 10 + MapRenderer.PlaceClear - 0.5f, $"{i} on {j}'s place");
            }
        }
    }

    [Fact]
    public void A_lesser_symbol_between_two_that_keep_their_places_gets_out()
    {
        // A padlock (rank 5) right between two quests' places (rank 2) that touch it from both sides.
        var symbols = new List<(SKPoint, float, int, bool)>
        {
            (new SKPoint(500, 489), 10f, 2, true), (new SKPoint(500, 500.5f), 5.5f, 5, true), (new SKPoint(501, 512), 10f, 2, true),
        };
        var placed = MapRenderer.Repel(symbols, Gap);
        NoneCovers(symbols, placed);
        // The two quests keep theirs.
        Assert.Equal(new SKPoint(500, 489), placed[0]);
        Assert.Equal(new SKPoint(501, 512), placed[2]);
    }

    [Fact]
    public void One_that_has_to_leave_its_place_stands_a_leader_off_it_one_only_nudged_keeps_to_it()
    {
        // Two quests on one spot: both have to leave it. Two that only touch: nudged apart by a pixel or two.
        var spot = new SKPoint(300, 300);
        var placed = MapRenderer.Repel([(spot, 10, 2, true), (spot, 10, 2, true), (new SKPoint(600, 300), 10, 2, true), (new SKPoint(621, 300), 10, 2, true)],
            Gap, MapRenderer.PlaceClear, MapRenderer.LeaderLeast);
        Assert.All(placed[..2], p => Assert.True(SKPoint.Distance(p, spot) >= 10 + Gap + MapRenderer.LeaderLeast - 0.5f, $"{p} too near its place"));
        Assert.True(SKPoint.Distance(placed[2], new SKPoint(600, 300)) < 3);
        Assert.True(SKPoint.Distance(placed[3], new SKPoint(621, 300)) < 3);
    }

    [Fact]
    public void An_extract_and_a_transit_at_one_spot_are_both_drawn_both_found_and_lead_to_it()
    {
        var (camera, scene) = Of([Extract(0, 0), Transit(0, 0)]);
        var shown = MapRenderer.ShownMarkers(camera, scene, 1);
        var (extract, transit) = (The(shown, "extract:gate"), The(shown, "transit:lab"));
        var spot = new SKPoint(500, 500);
        // The earlier one goes to the left, and both stand off the spot by a leader, which ends there.
        Assert.True(extract.At.X < transit.At.X);
        Assert.Equal(spot, extract.Leader);
        Assert.Equal(spot, transit.Leader);
        Assert.All([extract, transit], m => Assert.True(SKPoint.Distance(m.At, spot) >= MapRenderer.RestHalf(m.Marker, 1) + Gap + MapRenderer.LeaderLeast - 0.5f));

        // The pointer finds each where it is drawn, and the dot at the spot is a symbol of it.
        Assert.Equal("extract:gate", MapRenderer.HitTest(camera, scene, extract.At, 1)?.Id);
        Assert.Equal("transit:lab", MapRenderer.HitTest(camera, scene, transit.At, 1)?.Id);
        Assert.Contains(MapRenderer.HitTest(camera, scene, spot, 1)?.Id, new[] { "extract:gate", "transit:lab" });

        // Both symbols show in their colours.
        using var bitmap = new SKBitmap(new SKImageInfo(1000, 1000, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
            MapRenderer.Render(canvas, camera, scene);
        Near(Palette.Green, bitmap.GetPixel((int)Math.Round(extract.At.X), (int)Math.Round(extract.At.Y) + 2));
        Near(Palette.Violet, bitmap.GetPixel((int)Math.Round(transit.At.X), (int)Math.Round(transit.At.Y)));
    }

    private static void Near(string expected, SKColor actual)
    {
        var e = SKColor.Parse(expected);
        Assert.True(Math.Abs(e.Red - actual.Red) <= 3 && Math.Abs(e.Green - actual.Green) <= 3 && Math.Abs(e.Blue - actual.Blue) <= 3, $"{actual}, expected {e}");
    }

    [Fact]
    public void Zooming_in_brings_each_back_to_its_own_place()
    {
        // Two metres apart in the world: two pixels at this zoom, far more than their widths at 16 times it.
        var (camera, scene) = Of([Extract(0, 0), Transit(2, 0)]);
        var near = MapRenderer.ShownMarkers(camera, scene, 1);
        Assert.True(The(near, "transit:lab").At.X - The(near, "extract:gate").At.X > 15);
        Assert.NotEqual(500, The(near, "extract:gate").At.X);

        camera.ZoomAt(new SKPoint(500, 500), 16);
        var far = MapRenderer.ShownMarkers(camera, scene, 1);
        Assert.Equal(camera.ToScreen(scene.Projection.ToMap(new WorldPoint(0, 0, 0))), The(far, "extract:gate").At);
        Assert.Equal(camera.ToScreen(scene.Projection.ToMap(new WorldPoint(2, 0, 0))), The(far, "transit:lab").At);
        Assert.All(far, m => Assert.Null(m.Leader));
    }

    [Fact]
    public void Pointing_at_one_of_them_or_picking_moves_none()
    {
        var (camera, scene) = Of([Extract(0, 0), Transit(0, 0), Quest("a", 3, 0, "A"), Quest("b", 0, 4, "B")]);
        var rest = MapRenderer.ShownMarkers(camera, scene, 1);
        scene.Focus = new HashSet<string> { "transit:lab" };
        var pointed = MapRenderer.ShownMarkers(camera, scene, 1);
        scene.Focus = new HashSet<string>();
        scene.Kept = new HashSet<string> { "quest-a" };
        var picked = MapRenderer.ShownMarkers(camera, scene, 1);
        foreach (var m in rest)
        {
            Assert.Equal(m.At, The(pointed, m.Marker.Id).At);
            Assert.Equal(m.At, The(picked, m.Marker.Id).At);
        }
    }

    [Fact]
    public void Panning_moves_every_symbol_and_leader_alike()
    {
        var (camera, scene) = Of([Extract(0, 0), Transit(0, 0), Quest("a", 3, 0, "A"), Quest("b", 0, 4, "B"), Quest("c", 60, 0, "C")]);
        var before = MapRenderer.Layout(camera, scene, 1).Markers;
        camera.Pan(37, -12);
        var after = MapRenderer.Layout(camera, scene, 1).Markers;
        foreach (var m in before)
        {
            var moved = The(after, m.Marker.Id);
            Assert.Equal(m.At.X + 37, moved.At.X, 3);
            Assert.Equal(m.At.Y - 12, moved.At.Y, 3);
            Assert.Equal(m.Leader is null, moved.Leader is null);
        }
    }

    [Fact]
    public void Places_of_one_objective_still_merge_and_two_quests_at_one_place_both_show()
    {
        MapMarker Place(string quest, string objective, int n, double x) =>
            new($"objective:{objective}:{n}", MarkerKind.Objective, new WorldPoint(x, 0, 0), quest, "quest-" + quest, ObjectiveKind.Exploration);
        var (camera, scene) = Of([Place("a", "one", 1, 0), Place("a", "one", 2, 4), Place("b", "two", 1, 0)]);
        var shown = MapRenderer.ShownMarkers(camera, scene, 1);
        Assert.Equal(2, shown.Count);
        var merged = shown.Single(m => m.Marker.Group == "quest-a");
        var other = shown.Single(m => m.Marker.Group == "quest-b");
        Assert.Equal(2, merged.Count);
        // The cluster's count badge counts in its width (MapRenderer.RepelHalf).
        Assert.True(SKPoint.Distance(merged.At, other.At) >= 13 + 10 + Gap - 0.5f);
    }

    [Fact]
    public void No_label_covers_a_leader_or_its_dot()
    {
        var (camera, scene) = Of([Extract(0, 0), Transit(0, 0), Quest("a", 2, 2, "A long quest name"), Quest("b", -3, 1, "Another long name")]);
        var layout = MapRenderer.Layout(camera, scene, 1);
        Assert.Contains(layout.Markers, m => m.Leader is not null);
        foreach (var m in layout.Markers.Where(m => m.Leader is not null))
        {
            for (var t = 0f; t <= 1; t += 0.05f)
            {
                var p = new SKPoint(m.Leader!.Value.X + (m.At.X - m.Leader.Value.X) * t, m.Leader.Value.Y + (m.At.Y - m.Leader.Value.Y) * t);
                Assert.All(layout.Labels, l => Assert.False(l.Box.Contains(p), $"{l.Text} over {m.Marker.Id}'s leader"));
            }
        }
    }

    [Fact]
    public void The_guide_ends_on_the_symbol_where_it_is_drawn()
    {
        var mine = Quest("mine", 0, 0, "Ballet Lover");
        var (camera, scene) = Of([mine, Quest("other", 0, 0, "Dandies")]);
        scene.Player = new PlayerFix(new WorldPoint(-300, 0, 0), 0, DateTime.Now);
        scene.Kept = new HashSet<string> { "quest-mine" };
        var layout = MapRenderer.Layout(camera, scene, 1);
        var drawn = The(layout.Markers, mine.Id);
        Assert.NotEqual(500, drawn.At.X);
        Assert.Equal(drawn.At, layout.Guide!.To);
        // The distance on the plate stays the place's own.
        Assert.Equal(300, layout.Guide.Metres, 3);
    }
}
