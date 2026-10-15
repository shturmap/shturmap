using SkiaSharp;
using Shturmap.Core;
using Shturmap.Core.Maps;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// Labels are placed by priority after every symbol, each trying four positions (cartography review, 2026-10-02).
public class LabelTests
{
    private static (Camera Camera, MapScene Scene) View(IReadOnlyList<MapMarker> markers, params MapLabel[] names) => Of(markers, names);

    [Fact]
    public void Candidates_go_right_left_above_below()
    {
        var at = new SKPoint(100, 100);
        var boxes = MapRenderer.LabelCandidates(at, 10, 40, 10).ToList();
        Assert.Equal(4, boxes.Count);
        Assert.True(boxes[0].Left >= 110);
        Assert.True(boxes[1].Right <= 90);
        Assert.True(boxes[2].Bottom <= 90 && Math.Abs(boxes[2].MidX - 100) < 0.01);
        Assert.True(boxes[3].Top >= 110 && Math.Abs(boxes[3].MidX - 100) < 0.01);
    }

    [Fact]
    public void A_label_moves_left_when_a_symbol_takes_its_right()
    {
        var (camera, scene) = View([Quest("a", 0, 0, "Ballet Lover"), new MapMarker("extract:1", MarkerKind.ExtractPmc, new WorldPoint(30, 0, 0), "")]);
        var label = Assert.Single(MapRenderer.Layout(camera, scene, 1).Labels);
        Assert.True(label.Box.Right < 500, $"label at {label.Box}");
    }

    [Fact]
    public void Map_names_give_way_to_marker_labels()
    {
        var boss = new MapMarker("boss:bossBoar:Car Dealership", MarkerKind.BossSpawn, new WorldPoint(0, 0, 0), "Kaban 75%", "boss:bossBoar");
        // "Lexos" sits right of the diamond, where Kaban's label goes first.
        var (camera, scene) = View([boss], new MapLabel(40, 0, "Lexos", 0, 70));
        var layout = MapRenderer.Layout(camera, scene, 1);
        Assert.Equal("Kaban 75%", Assert.Single(layout.Labels).Text);
        Assert.Empty(layout.Names);
    }

    // The review of 2026-10-04 (C5): a boss's label was red where every other label is ink. The red octagon is the
    // danger sign; the label reads like the others, and takes its marker's colour only while pointed at, as theirs do.
    [Fact]
    public void A_bosss_label_is_ink_like_every_other_at_rest()
    {
        var boss = new MapMarker("boss:bossBoar:Z", MarkerKind.BossSpawn, new WorldPoint(0, 0, 0), "Kaban 75% · 50% here", "boss:bossBoar");
        var (camera, scene) = View([boss, Quest("q", 0, 200, "Dandies"), new MapMarker("extract:1", MarkerKind.ExtractPmc, new WorldPoint(0, 0, -200), "Gate")]);
        var labels = MapRenderer.Layout(camera, scene, 1).Labels;
        Assert.Equal(3, labels.Count);
        Assert.All(labels, l => Assert.Equal(SKColor.Parse(Palette.Ink), l.Color));
        Assert.Equal("Kaban 75% · 50% here", labels.Single(l => l.Of.Marker.Kind == MarkerKind.BossSpawn).Text);

        scene.Focus = new HashSet<string> { "boss:bossBoar" };
        var pointed = MapRenderer.Layout(camera, scene, 1).Labels.Single(l => l.Of.Marker.Kind == MarkerKind.BossSpawn);
        Assert.Equal(SKColor.Parse(Palette.Red), pointed.Color);
    }

    [Fact]
    public void Bosses_are_labelled_before_quests()
    {
        // Each one's only free side is the same spot between them.
        var boss = new MapMarker("boss:bossBoar:Z", MarkerKind.BossSpawn, new WorldPoint(0, 0, 0), "Kaban 75%", "boss:bossBoar");
        var quest = Quest("q", 90, 0, "Dandies");
        var blockers = new[]
        {
            new MapMarker("extract:l", MarkerKind.ExtractPmc, new WorldPoint(-25, 0, 0), ""),
            new MapMarker("extract:u", MarkerKind.ExtractPmc, new WorldPoint(0, 0, 22), ""),
            new MapMarker("extract:d", MarkerKind.ExtractPmc, new WorldPoint(0, 0, -22), ""),
            new MapMarker("extract:r", MarkerKind.ExtractPmc, new WorldPoint(120, 0, 0), ""),
            new MapMarker("extract:qu", MarkerKind.ExtractPmc, new WorldPoint(90, 0, 25), ""),
            new MapMarker("extract:qd", MarkerKind.ExtractPmc, new WorldPoint(90, 0, -25), ""),
        };
        var (camera, scene) = View([quest, boss, .. blockers]);
        var layout = MapRenderer.Layout(camera, scene, 1);
        Assert.Contains(layout.Labels, l => l.Text == "Kaban 75%");
        Assert.DoesNotContain(layout.Labels, l => l.Text == "Dandies");
    }

    // At the smallest window a name placed left of its symbol by the map's left edge lost its first letters ("ansit to
    // Interchange" on Streets; owner, 2026-10-15): no label starts past the edge. One above or below slides in to start
    // there, still over or under its symbol; one beside it isn't a place; with no side left the label is dropped.
    [Fact]
    public void A_candidate_past_the_left_edge_slides_in_over_or_under_its_symbol_or_is_no_place()
    {
        var at = new SKPoint(20, 100);
        var boxes = MapRenderer.LabelCandidates(at, 10, 60, 10).ToList();
        Assert.Equal(boxes[0], MapRenderer.Inward(boxes[0], at, 2));
        Assert.Null(MapRenderer.Inward(boxes[1], at, 2));
        foreach (var box in boxes.Skip(2))
        {
            var inward = MapRenderer.Inward(box, at, 2)!.Value;
            Assert.Equal(SKRect.Create(2, box.Top, box.Width, box.Height), inward);
            Assert.True(inward.Left <= at.X && inward.Right >= at.X);
        }
        // A symbol itself past the edge: nothing over or under it stays whole; on its right, its name may.
        var off = new SKPoint(-5, 100);
        var offBoxes = MapRenderer.LabelCandidates(off, 10, 60, 10).ToList();
        Assert.Equal(offBoxes[0], MapRenderer.Inward(offBoxes[0], off, 2));
        Assert.All(offBoxes.Skip(1), b => Assert.Null(MapRenderer.Inward(b, off, 2)));
        // Clear of the edge, nothing changes.
        Assert.All(MapRenderer.LabelCandidates(new SKPoint(200, 100), 10, 60, 10), b => Assert.Equal(b, MapRenderer.Inward(b, new SKPoint(200, 100), 2)));
    }

    [Fact]
    public void No_label_starts_past_the_maps_left_edge()
    {
        // 30 px in from the left edge, a symbol on its right: its name would go left of it, past the edge.
        var transit = new MapMarker("transit:i", MarkerKind.Transit, new WorldPoint(-470, 0, 0), "Transit to Interchange");
        var right = new MapMarker("extract:r", MarkerKind.ExtractPmc, new WorldPoint(-440, 0, 0), "");
        var (camera, scene) = View([transit, right]);
        var label = Assert.Single(MapRenderer.Layout(camera, scene, 1).Labels);
        // Above it instead, slid in to start at the edge, still over its symbol.
        Assert.Equal(MapRenderer.LabelEdge, label.Box.Left, 3);
        Assert.True(label.Box.Bottom < 490 && label.Box.Right > 30, $"{label.Box}");

        // With above and below taken too, it is dropped rather than cut.
        var (crowdedCamera, crowded) = View([transit, right, new MapMarker("extract:u", MarkerKind.ExtractPmc, new WorldPoint(-470, 0, 22), ""),
            new MapMarker("extract:d", MarkerKind.ExtractPmc, new WorldPoint(-470, 0, -22), "")]);
        Assert.Empty(MapRenderer.Layout(crowdedCamera, crowded, 1).Labels);
        // Without the edge it would have gone on the left: the same crowd in the middle of the view.
        var (middleCamera, middle) = View([.. crowded.Markers.Select(m => m with { Position = new WorldPoint(m.Position.X + 470, 0, m.Position.Z) })]);
        Assert.True(Assert.Single(MapRenderer.Layout(middleCamera, middle, 1).Labels).Box.Right < 500);
    }

    // A made-up name as long as the tour's German picked quest's (owner, 2026-10-15, E3: at full size it read
    // "Lieferung aus der Vergan" at the map's right edge, past the 150 DIP the view keeps there, Camera.LabelRoom).
    private const string LongName = "Eine Lieferung aus ferner Vergangenheit";

    // One longer than LitNameMost, made up too (tarkov.dev's longest, German, is 307 px in the bold label face).
    private const string LongerName = "Der Weg der Überlebenden - Ungeschützt, aber ziemlich gefährlich";

    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);

    // Its left, above and below taken, 150 px in from the right edge (screen x 850).
    private static MapMarker[] AroundFar =>
    [
        new("extract:l", MarkerKind.ExtractPmc, new WorldPoint(325, 0, 0), ""),
        new("extract:u", MarkerKind.ExtractPmc, new WorldPoint(350, 0, 22), ""),
        new("extract:d", MarkerKind.ExtractPmc, new WorldPoint(350, 0, -22), ""),
    ];

    // The mirror of the left edge's rule, for a picked or pointed-at name at the right edge: one above or below its symbol
    // slides left to end there, one beside it isn't a place.
    [Fact]
    public void A_lit_candidate_past_the_right_edge_slides_in_over_or_under_its_symbol_or_is_no_place()
    {
        var at = new SKPoint(980, 100);
        var boxes = MapRenderer.LabelCandidates(at, 10, 60, 10).ToList();
        Assert.Null(MapRenderer.Inward(boxes[0], at, 2, 998));
        Assert.Equal(boxes[1], MapRenderer.Inward(boxes[1], at, 2, 998));
        foreach (var box in boxes.Skip(2))
        {
            var inward = MapRenderer.Inward(box, at, 2, 998)!.Value;
            Assert.Equal(SKRect.Create(998 - box.Width, box.Top, box.Width, box.Height), inward);
            Assert.True(inward.Left <= at.X && inward.Right >= at.X);
        }
        // Without a right edge (every other label) it stays where it is.
        Assert.All(boxes, b => Assert.Equal(b, MapRenderer.Inward(b, at, 2)));
    }

    [Fact]
    public void A_picked_or_pointed_at_name_at_the_right_edge_goes_left_of_its_symbol_whole()
    {
        // 150 px in from the right edge, as a framing leaves the place furthest right.
        var (camera, scene) = View([Quest("far", 350, 0, LongName)]);
        // Not lit, it keeps the rule of the room on the right: it goes right and runs past the edge, as before.
        var rest = Assert.Single(MapRenderer.Layout(camera, scene, 1).Labels);
        Assert.Equal(0, rest.Side);
        Assert.True(rest.Box.Right > 1000, $"{rest.Box}");

        scene.Kept = new HashSet<string> { "quest-far" };
        var picked = Assert.Single(MapRenderer.Layout(camera, scene, 1).Labels);
        Assert.Equal(LongName, picked.Text);
        Assert.Equal(1, picked.Side);
        Assert.True(picked.Box.Right < 850 && picked.Box.Left >= MapRenderer.LabelEdge, $"{picked.Box}");

        scene.Kept = new HashSet<string>();
        scene.Focus = new HashSet<string> { "quest-far" };
        var pointed = Assert.Single(MapRenderer.Layout(camera, scene, 1).Labels);
        Assert.Equal((LongName, 1), (pointed.Text, pointed.Side));
    }

    [Fact]
    public void A_lit_name_with_no_other_side_free_stays_right_trimmed_with_an_ellipsis_at_the_edge()
    {
        var (camera, scene) = View([Quest("far", 350, 0, LongName), .. AroundFar]);
        scene.Kept = new HashSet<string> { "quest-far" };
        var label = Assert.Single(MapRenderer.Layout(camera, scene, 1).Labels);
        Assert.Equal(0, label.Side);
        Assert.EndsWith("…", label.Text);
        Assert.StartsWith(label.Text[..^1], LongName);
        Assert.True(label.Text.Length > 10, label.Text);
        Assert.True(label.Box.Left > 850 && label.Box.Right <= 1000 - MapRenderer.LabelEdge, $"{label.Box}");
    }

    [Fact]
    public void A_lit_name_longer_than_the_limit_moves_in_trimmed_to_it_and_reads_whole_where_its_right_has_room()
    {
        var (camera, scene) = View([Quest("far", 350, 0, LongerName)]);
        scene.Kept = new HashSet<string> { "quest-far" };
        var moved = Assert.Single(MapRenderer.Layout(camera, scene, 1).Labels);
        Assert.Equal(1, moved.Side);
        Assert.EndsWith("…", moved.Text);
        Assert.InRange(moved.Box.Width, MapRenderer.LitNameMost - 20, MapRenderer.LitNameMost);

        var (middleCamera, middle) = View([Quest("mid", -300, 0, LongerName)]);
        middle.Kept = new HashSet<string> { "quest-mid" };
        var whole = Assert.Single(MapRenderer.Layout(middleCamera, middle, 1).Labels);
        Assert.Equal((LongerName, 0), (whole.Text, whole.Side));
        Assert.True(whole.Box.Width > MapRenderer.LitNameMost, $"{whole.Box}");

        // A map 500 px wide (the smallest window's): 40 % of it, 200 px.
        var (smallCamera, small) = View([Quest("far", 100, 0, LongName)]);
        smallCamera.Resize(new SKSize(500, 1000));
        small.Kept = new HashSet<string> { "quest-far" };
        var narrow = Assert.Single(MapRenderer.Layout(smallCamera, small, 1).Labels);
        Assert.Equal(1, narrow.Side);
        Assert.EndsWith("…", narrow.Text);
        Assert.True(narrow.Box.Width <= 500 * MapRenderer.LitNameShare && narrow.Box.Left >= MapRenderer.LabelEdge, $"{narrow.Box}");
    }

    // No label moves while the view moves (the rule Steady while the view moves): a lit name panned toward the right edge
    // keeps its right, trimmed with "…" as it reaches the edge, rather than going over to its left in the middle of the
    // pan; with the view still it is placed afresh, as the scale bar is.
    [Fact]
    public void A_lit_name_keeps_its_side_while_the_view_moves_and_is_placed_afresh_once_still()
    {
        var (camera, scene) = View([Quest("far", 200, 0, LongName)]);
        scene.Kept = new HashSet<string> { "quest-far" };
        var first = Assert.Single(MapRenderer.LayoutOf(camera, scene, 1, T0).Labels);
        Assert.Equal((LongName, 0), (first.Text, first.Side));
        var trimmed = 0;
        for (var k = 1; k <= 15; k++)
        {
            camera.Pan(10, 0);
            var label = Assert.Single(MapRenderer.LayoutOf(camera, scene, 1, T0).Labels);
            Assert.Equal(0, label.Side);
            Assert.Equal(first.Box.Left + 10 * k, label.Box.Left, 2);
            Assert.True(label.Box.Right <= 1000 - MapRenderer.LabelEdge, $"frame {k}: {label.Box}");
            Assert.True(label.Text == LongName || (label.Text.EndsWith('…') && LongName.StartsWith(label.Text[..^1], StringComparison.Ordinal)), label.Text);
            trimmed += label.Text == LongName ? 0 : 1;
        }
        Assert.True(trimmed >= 5, $"{trimmed} frames trimmed: the pan doesn't reach the edge");
        // The view still: on its left, whole, as a fresh layout of that view has it.
        var still = Assert.Single(MapRenderer.LayoutOf(camera, scene, 1, T0).Labels);
        Assert.Equal((LongName, 1), (still.Text, still.Side));
        var (fresh, freshScene) = View([Quest("far", 200, 0, LongName)]);
        freshScene.Kept = scene.Kept;
        fresh.Restore(camera.Center, camera.Zoom);
        Assert.Equal(Assert.Single(MapRenderer.Layout(fresh, freshScene, 1, T0).Labels).Box, still.Box);
        // Panned back, it keeps its left until the view is still again.
        camera.Pan(-150, 0);
        Assert.Equal(1, Assert.Single(MapRenderer.LayoutOf(camera, scene, 1, T0).Labels).Side);
        Assert.Equal(0, Assert.Single(MapRenderer.LayoutOf(camera, scene, 1, T0).Labels).Side);
    }

    [Fact]
    public void Trimmed_keeps_the_longest_start_that_fits_with_an_ellipsis()
    {
        using var font = new SKFont(SKTypeface.Default, 13);
        Assert.Equal("Abc", MapRenderer.Trimmed(font, "Abc", 1000));
        var width = font.MeasureText("Lieferung aus…");
        Assert.Equal("Lieferung aus…", MapRenderer.Trimmed(font, "Lieferung aus der Vergangenheit", width));
        // No space or dash before the ellipsis.
        Assert.Equal("Weg…", MapRenderer.Trimmed(font, "Weg - Biest", font.MeasureText("Weg - …")));
        Assert.Null(MapRenderer.Trimmed(font, "Lieferung", 1));
    }

    [Fact]
    public void A_name_is_said_once_per_neighbourhood()
    {
        var (camera, scene) = View([Quest("a", 0, 0, "Abandoned Cargo"), Quest("b", 0, 120, "Abandoned Cargo"), Quest("c", 400, 0, "Abandoned Cargo")]);
        Assert.Equal(2, MapRenderer.Layout(camera, scene, 1).Labels.Count(l => l.Text == "Abandoned Cargo"));
    }

    [Fact]
    public void No_label_covers_a_symbol()
    {
        var markers = Enumerable.Range(0, 60).Select(i => Quest($"q{i}", (i % 10) * 45 - 200, (i / 10) * 35 - 100, $"Quest number {i}")).ToList();
        var (camera, scene) = View(markers);
        var layout = MapRenderer.Layout(camera, scene, 1);
        foreach (var label in layout.Labels)
            Assert.All(layout.Markers, m => Assert.False(label.Box.IntersectsWith(new SKRect(m.At.X - m.Reach, m.At.Y - m.Reach, m.At.X + m.Reach, m.At.Y + m.Reach)), $"{label.Text} covers {m.Marker.Id}"));
    }
}
