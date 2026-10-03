using Shturmap.Core.Maps;

namespace Shturmap.Core.Tests;

// Maps without usable artwork (The Lab, Labyrinth, Icebreaker) are drawn as a metric sheet (docs/DESIGN.md §3).
public class MapSheetTests
{
    [Theory]
    [InlineData("the-lab")]
    [InlineData("the-labyrinth")]
    [InlineData("icebreaker")]
    public void Maps_without_an_svg_are_the_three_sheet_maps(string key) => Assert.Null(Fixtures.Map(key).SvgPath);

    [Fact]
    public void Lab_labels_keep_their_heights()
    {
        var parking = Fixtures.Map("the-lab").Labels.First(l => l.Text == "Parking");
        Assert.Equal(new HeightRange(-0.8, 2), parking.Height);
        Assert.All(Fixtures.Map("streets-of-tarkov").Labels.Where(l => l.Height is null), l => Assert.NotEmpty(l.Text));
    }

    [Fact]
    public void Lab_floors_come_from_its_tile_layers()
    {
        // No SvgLayer on any floor: the stack is built from the tile layers, top first, the base as null.
        var stack = FloorResolver.Stack(Fixtures.Map("the-lab"));
        Assert.Equal(["Second Level", "Ground", "Technical"], stack.Select(l => l?.Name ?? "Ground"));
    }

    [Fact]
    public void Grid_lines_sit_on_whole_metres_across_the_bounds()
    {
        var bounds = Fixtures.Map("the-lab").Bounds; // x -287 … -80, z -477 … -193
        var lines = SchematicGrid.Lines(bounds, 10, 5);
        var vertical = lines.Where(l => l.X1 == l.X2).ToList();
        var horizontal = lines.Where(l => l.Z1 == l.Z2).ToList();
        Assert.Equal(21, vertical.Count);   // -280 … -80
        Assert.Equal(28, horizontal.Count); // -470 … -200
        Assert.All(lines, l => Assert.Equal(0, (l.X1 == l.X2 ? l.X1 : l.Z1) % 10));
        Assert.Equal([-250.0, -200.0, -150.0, -100.0], vertical.Where(l => l.Major).Select(l => l.X1));
        Assert.All(vertical, l => Assert.Equal((-477.0, -193.0), (l.Z1, l.Z2)));
    }

    [Fact]
    public void A_sheet_without_tiles_keeps_ten_metres_the_same_length_both_ways()
    {
        // A map with neither SVG nor tiles is drawn with one scale. (Icebreaker, stretched X by 2 and Y by 3.5 for its
        // tile render, stands in for such a map here.)
        var projection = MapProjection.For(Fixtures.Map("icebreaker") with { TilePath = null });
        var origin = projection.ToMap(0, 0);
        var alongX = projection.ToMap(10, 0) - origin;
        var alongZ = projection.ToMap(0, 10) - origin;
        Assert.Equal(Math.Sqrt(alongX.X * alongX.X + alongX.Y * alongX.Y), Math.Sqrt(alongZ.X * alongZ.X + alongZ.Y * alongZ.Y), 6);
        Assert.Equal(10 * Math.Sqrt(2 * 3.5), Math.Abs(alongX.X + alongX.Y), 6);
    }

    [Fact]
    public void Tile_maps_keep_tarkov_devs_transform_so_markers_land_on_the_render()
    {
        // Icebreaker's render is stretched: its markers must be too (docs/DESIGN.md §3, "Maps without SVG artwork").
        var map = Fixtures.Map("icebreaker");
        var projection = MapProjection.For(map);
        Assert.Same(map, projection.Map);
        var origin = projection.ToMap(0, 0);
        Assert.Equal(20, Math.Abs((projection.ToMap(10, 0) - origin).X), 6);
        Assert.Equal(35, Math.Abs((projection.ToMap(0, 10) - origin).Y), 6);
    }

    [Fact]
    public void Maps_with_artwork_keep_tarkov_devs_transform()
    {
        var streets = Fixtures.Map("streets-of-tarkov");
        Assert.Same(streets, MapProjection.For(streets).Map);
        Assert.Equal(new MapProjection(streets).ToMap(40.00, 120.00), MapProjection.For(streets).ToMap(40.00, 120.00));
    }

    [Theory]
    [InlineData("the-lab")]
    [InlineData("the-labyrinth")]
    [InlineData("icebreaker")]
    public void Labels_and_bounds_land_on_the_sheet(string key)
    {
        var map = Fixtures.Map(key);
        var projection = MapProjection.For(map);
        var sheet = projection.WorldRect;
        Assert.True(sheet.Width > 0 && sheet.Height > 0);
        foreach (var label in map.Labels)
        {
            var at = projection.ToMap(label.X, label.Z);
            Assert.InRange(at.X, sheet.Left - 0.5, sheet.Right + 0.5);
            Assert.InRange(at.Y, sheet.Top - 0.5, sheet.Bottom + 0.5);
        }
    }
}
