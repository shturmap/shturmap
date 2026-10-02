using SkiaSharp;
using Shturmap.Core;
using Shturmap.Core.Maps;
using Shturmap.Core.Quests;

namespace Shturmap.Map.Tests;

// Labels are placed by priority after every symbol, each trying four positions (cartography review, 2026-10-02).
public class LabelTests
{
    // One metre is one pixel; world x grows to the right, world z upward; the view's centre is the origin.
    private static (Camera Camera, MapScene Scene) View(IReadOnlyList<MapMarker> markers, params MapLabel[] names)
    {
        var definition = new MapDefinition
        {
            Key = "test",
            Transform = [1, 0, 1, 0],
            Bounds = new WorldBox(-500, -500, 500, 500),
            Labels = names,
        };
        var camera = new Camera();
        camera.Resize(new SKSize(1000, 1000));
        camera.Restore(new MapPoint(0, 0), 1);
        return (camera, new MapScene(definition, null) { Markers = markers });
    }

    private static MapMarker Quest(string id, double x, double z, string name) =>
        new($"objective:{id}:1", MarkerKind.Objective, new WorldPoint(x, 0, z), name, "quest-" + id, ObjectiveKind.Exploration);

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
