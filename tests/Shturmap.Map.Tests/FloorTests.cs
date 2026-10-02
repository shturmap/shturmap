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
}
