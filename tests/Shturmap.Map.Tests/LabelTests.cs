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
