using Spotter.Core.Maps;

namespace Spotter.Core.Tests;

public class MapProjectionTests
{
    [Fact]
    public void Reads_all_interactive_maps()
    {
        var keys = Fixtures.MapDefinitions.Select(m => m.Key).ToHashSet();
        foreach (var key in new[] { "customs", "factory", "ground-zero", "interchange", "lighthouse", "reserve", "shoreline",
                     "streets-of-tarkov", "the-lab", "the-labyrinth", "terminal", "woods", "icebreaker" })
            Assert.Contains(key, keys);
        Assert.True(Fixtures.Map("factory").Matches("night-factory"));
    }

    [Fact]
    public void Golden_point_streets_screenshot_lands_where_tarkov_dev_draws_it()
    {
        // A made-up position near the middle of Streets (x 40, z 120).
        var projection = new MapProjection(Fixtures.Map("streets-of-tarkov"));

        var map = projection.ToMap(40.00, 120.00);
        Assert.Equal(-34.804, map.X, 3);
        Assert.Equal(170.947, map.Y, 3);

        // StreetsOfTarkov.svg viewBox="0 0 605.32395 831.57753"
        var svg = projection.PlaceSvg(0, 0, 605.32395, 831.57753).MapToSvg(map);
        Assert.Equal(232.2, svg.X, 1);
        Assert.Equal(749.0, svg.Y, 1);
    }

    [Fact]
    public void World_to_map_round_trips_on_every_map()
    {
        foreach (var definition in Fixtures.MapDefinitions)
        {
            var projection = new MapProjection(definition);
            var (x, z) = projection.ToWorld(projection.ToMap(123.45, -67.89));
            Assert.Equal(123.45, x, 6);
            Assert.Equal(-67.89, z, 6);
        }
    }

    [Fact]
    public void Facing_south_on_a_180_degree_map_points_up()
    {
        // yaw 177.93° is roughly world −Z; Streets is rotated 180°, so the arrow points (almost) straight up.
        var projection = new MapProjection(Fixtures.Map("streets-of-tarkov"));
        var heading = projection.ScreenHeadingDegrees(new WorldPoint(40.00, 2.50, 120.00), 177.93);
        Assert.Equal(357.93, heading, 2);
    }

    [Fact]
    public void Screen_heading_handles_unequal_scale()
    {
        // Icebreaker's transform scales X by 2.0 and Y by 3.5; facing straight along an axis must stay on that axis.
        var projection = new MapProjection(Fixtures.Map("icebreaker"));
        var up = projection.ScreenHeadingDegrees(new WorldPoint(0, 0, 0), 180);
        Assert.True(up < 0.001 || up > 359.999, $"expected straight up, got {up}");
    }

    [Theory]
    [InlineData(4.09, null)]
    [InlineData(12.0, "2nd Floor")]
    [InlineData(17.5, "3rd Floor")]
    [InlineData(-8.0, "Underground")]
    public void Floor_follows_height_on_streets(double height, string? expected)
    {
        var layer = FloorResolver.LayerFor(Fixtures.Map("streets-of-tarkov"), new WorldPoint(40.00, height, 120.00));
        Assert.Equal(expected, layer?.Name);
    }

    [Fact]
    public void Floor_extents_with_boxes_only_apply_inside_them()
    {
        // Some layers only cover a building: a height match outside its boxes stays on the base layer.
        var map = Fixtures.MapDefinitions.First(m => m.Layers.Any(l => l.Extents.Any(e => e.Boxes.Count > 0)));
        var layer = map.Layers.First(l => l.Extents.Any(e => e.Boxes.Count > 0));
        var extent = layer.Extents.First(e => e.Boxes.Count > 0);
        var box = extent.Boxes[0];
        var height = double.IsFinite(extent.Height.Min) ? extent.Height.Min + 0.01 : extent.Height.Max - 0.01;

        var inside = new WorldPoint((box.X1 + box.X2) / 2, height, (box.Z1 + box.Z2) / 2);
        var outside = new WorldPoint(10_000, height, 10_000);
        Assert.True(layer.Contains(inside));
        Assert.False(layer.Contains(outside));
    }

    [Theory]
    [InlineData("streets-of-tarkov", "5th Floor,4th Floor,3rd Floor,2nd Floor,Ground,Underground")]
    [InlineData("customs", "3rd Floor,2nd Floor,Ground,Underground")] // its 4th floor is drawn in the base layer
    [InlineData("reserve", "Ground,Bunkers")]
    [InlineData("ground-zero", "3rd Floor,2nd Floor,Ground,Garage")]
    [InlineData("factory", "3rd Floor,2nd Floor,Ground,Tunnels")]
    [InlineData("woods", "")]
    public void Floors_stack_from_the_top_down(string map, string expected)
    {
        var floors = FloorResolver.Stack(Fixtures.Map(map)).Select(l => l?.Name ?? "Ground");
        Assert.Equal(expected, string.Join(",", floors));
    }
}
