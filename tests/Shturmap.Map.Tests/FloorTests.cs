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
}
