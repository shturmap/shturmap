using SkiaSharp;
using Shturmap.Core;
using Shturmap.Core.Maps;

namespace Shturmap.Map.Tests;

// Markers on other floors say how many floors away they are (cartography review, 2026-10-02).
public class FloorTests
{
    // A point inside a floor's first extent.
    private static WorldPoint On(MapLayer layer)
    {
        var extent = layer.Extents[0];
        var box = extent.Boxes.Count > 0 ? extent.Boxes[0] : new WorldBox(0, 0, 0, 0);
        var low = Math.Max(extent.Height.Min, -1000);
        var high = Math.Min(extent.Height.Max, 1000);
        return new WorldPoint((box.X1 + box.X2) / 2, (low + high) / 2, (box.Z1 + box.Z2) / 2);
    }

    [Fact]
    public void The_offset_counts_floors_in_the_maps_floor_list()
    {
        var definition = Fixtures.Definition("streets-of-tarkov");
        var scene = new MapScene(definition, null);
        var stack = scene.FloorStack;
        var ground = stack.ToList().IndexOf(null);
        Assert.True(ground > 1, "Streets has at least two floors above the ground");
        foreach (var (layer, index) in stack.Select((l, i) => (l, i)).Where(x => x.l is not null))
        {
            var p = On(layer!);
            if (FloorResolver.LayerFor(definition, p) != layer)
                continue; // a narrower floor wins at this point
            Assert.Equal(ground - index, MapRenderer.FloorOffset(scene, p));
        }
        scene.Floor = stack[0];
        Assert.Equal(-ground, MapRenderer.FloorOffset(scene, new WorldPoint(0, 0.5, 0)));
    }

    // Customs' 4th floor in the oil rig has no artwork of its own, so it isn't in the floor list; counted as the ground
    // it is drawn in, the first WI-FI camera of Provide Viewership, at 14 m on it, read as below the 2nd floor the
    // player stood on (owner, 2026-10-05: "saying it's 7m up but the icon showing down"). The places are the quest's.
    [Fact]
    public void A_floor_without_artwork_is_above_or_below_by_its_buildings_floors()
    {
        var definition = Fixtures.Definition("customs");
        var scene = new MapScene(definition, null) { Floor = null };
        var fourth = new WorldPoint(256.98, 14.13, -75.38);
        Assert.Equal("4th Floor", FloorResolver.LayerFor(definition, fourth)?.Name);
        Assert.DoesNotContain(scene.FloorStack, l => l?.Name == "4th Floor");
        // Shown the ground: three floors up (2nd, 3rd, 4th), where it said nothing.
        Assert.Equal(3, MapRenderer.FloorOffset(scene, fourth));
        // Shown the 2nd floor: two up; the second camera, on the ground of the same building, one down; the third, on
        // the 2nd floor, none.
        scene.Floor = scene.FloorStack.Single(l => l?.Name == "2nd Floor");
        Assert.Equal(2, MapRenderer.FloorOffset(scene, fourth));
        Assert.Equal(-1, MapRenderer.FloorOffset(scene, new WorldPoint(254.7, 2.54, -75.2)));
        Assert.Equal(0, MapRenderer.FloorOffset(scene, new WorldPoint(264.03, 7.03, -38.9)));
    }

    // A beta tester through the owner, 2026-10-16: the arrow was hard to read on small second screens. It is a bold
    // caret, a stroked chevron of 2.2 DIP, about 9 × 5 DIP, in a dark disc ⌀ 14 with a 1.5 rim; two or more floors away
    // a 22 × 15 plate with the caret and an 11 px digit (docs/DESIGN.md, "Visual language"). Drawn at 200 %, in white
    // on the dark plate, the badge alone.
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(-3)]
    public void The_floor_arrow_is_a_bold_caret_in_a_disc_or_with_the_count_on_a_plate(int floor)
    {
        const float ui = 2;
        var marker = new MapMarker("objective:a:1", MarkerKind.Objective, new WorldPoint(0, 0, 0), "A", "quest-a");
        // The badge's centre on a pixel's centre.
        var shown = new MapRenderer.ShownMarker(marker, new SKPoint(40.5f, 80.5f), 10 * ui, SKColors.White, false, false, false, floor);
        var c = MapRenderer.FloorBadgeCenter(shown, ui);
        using var bitmap = new SKBitmap(new SKImageInfo(120, 120, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            MapRenderer.DrawFloorArrow(canvas, shown, ui);
        }
        bool Lit(int x, int y) => bitmap.GetPixel(x, y) is { Alpha: > 200, Red: > 128 };
        (int Width, int Height) Ink(Func<int, int, bool> counts)
        {
            int left = 120, top = 120, right = -1, bottom = -1;
            for (var y = 0; y < 120; y++)
                for (var x = 0; x < 120; x++)
                    if (counts(x, y))
                    {
                        left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                    }
            return right < 0 ? (0, 0) : (right - left + 1, bottom - top + 1);
        }
        // The badge with its rim: a disc ⌀ 14 + 1.5, or a plate 22 × 15 + 1.5.
        var badge = Ink((x, y) => bitmap.GetPixel(x, y).Alpha > 128);
        var (width, height) = Math.Abs(floor) > 1 ? (22 + 1.5, 15 + 1.5) : (14 + 1.5, 14 + 1.5);
        Assert.InRange(badge.Width, width * ui - 1, width * ui + 1);
        Assert.InRange(badge.Height, height * ui - 1, height * ui + 1);
        // Its rim: 1.5 DIP of the marker's colour, then the dark plate, on the line through the caret.
        var cx = (int)c.X;
        var cy = (int)c.Y;
        var from = Enumerable.Range(0, cx).First(x => bitmap.GetPixel(x, cy).Alpha > 200);
        var rim = Enumerable.Range(from, cx - from).TakeWhile(x => Lit(x, cy)).Count();
        Assert.InRange(rim, 1.5 * ui - 1, 1.5 * ui + 1);
        // The caret, centred on the disc's centre (on a plate, as far in from its left end), clear of the rim and the
        // digit: about 9 × 5 DIP.
        var caret = Ink((x, y) => SKPoint.Distance(new SKPoint(x + 0.5f, y + 0.5f), c) < 5.5f * ui && Lit(x, y));
        Assert.InRange(caret.Width, 9 * ui - 1, 9 * ui + 1);
        Assert.InRange(caret.Height, 5 * ui - 1, 5 * ui + 1);
        // An open V, stroked, not a filled triangle: lit at its point, dark inside it where a triangle would be lit.
        var up = floor > 0 ? 1 : -1;
        Assert.True(Lit(cx, cy - up * (int)ui), "the caret's point");
        Assert.False(Lit(cx, cy + up * (int)ui), "inside the caret's V");
        if (Math.Abs(floor) > 1)
        {
            // The digit, 11 px bold, right of the caret: a figure's height is 0.7 of its size.
            var box = MapRenderer.FloorBadgeBox(shown, ui);
            var digit = Ink((x, y) => x > c.X + 6 * ui && x < box.Right - 2 * ui && y > box.Top + 2 * ui && y < box.Bottom - 2 * ui && Lit(x, y));
            Assert.InRange(digit.Height, 11 * ui * 0.7 - 2, 11 * ui * 0.7 + 2);
        }
    }

    // QA, 2026-10-16: at 3× on Interchange the picked "Minute of Fame" started 0 to 1 px from its ⌃2 plate's rim, reading
    // "⌃2 Minute of Fame". A name keeps 3 DIP off every floor badge, its own too: the picked name two floors up keeps its
    // right side, moved out past the plate's margin, and a name beside a one-floor disc keeps its right as before.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void A_name_keeps_3_DIP_off_a_floor_badge_and_its_side(float ui)
    {
        // Two floors with artwork above the ground; the ground shown.
        var definition = new MapDefinition
        {
            Key = "test",
            Transform = [1, 0, 1, 0],
            Bounds = new WorldBox(-500, -500, 500, 500),
            Layers =
            [
                new MapLayer("2nd", null, "2nd", false, [new LayerExtent(new HeightRange(5, 10), [])]),
                new MapLayer("3rd", null, "3rd", false, [new LayerExtent(new HeightRange(10, 15), [])]),
            ],
        };
        MapMarker Quest(string id, double x, double y, double z, string name) =>
            new($"objective:{id}:1", MarkerKind.Objective, new WorldPoint(x, y, z), name, "quest-" + id, Shturmap.Core.Quests.ObjectiveKind.Exploration);
        var camera = new Camera();
        camera.Resize(new SKSize(1000, 1000));
        camera.Restore(new MapPoint(0, 0), 1);
        var scene = new MapScene(definition, null)
        {
            Markers = [Quest("a", 0, 12, 0, "Minute of Fame"), Quest("b", 0, 7, -150, "Big Sale")],
            Kept = new HashSet<string> { "quest-a" },
        };
        var layout = MapRenderer.Layout(camera, scene, ui);
        var badged = layout.Markers.Where(m => m.Floor != 0).OrderByDescending(m => m.Floor).ToList();
        Assert.Equal([2, 1], badged.Select(m => m.Floor));
        Assert.All(layout.Labels, label => Assert.All(badged, m =>
            Assert.False(label.Box.IntersectsWith(SKRect.Inflate(MapRenderer.FloorBadgeBox(m, ui), 3 * ui, 3 * ui)), $"{label.Text} within 3 DIP of {m.Marker.Id}'s floor badge")));
        Assert.All(layout.Labels, label => Assert.Equal(0, label.Side));
        var picked = layout.Labels.Single(l => l.Text == "Minute of Fame");
        Assert.InRange(picked.Box.Left - MapRenderer.FloorBadgeBox(badged[0], ui).Right, 3 * ui - 0.01, 3 * ui + 0.01);
    }
}
