using Shturmap.Core.Maps;

namespace Shturmap.Core.Tests;

// tarkov.dev's tile renders of The Lab, Labyrinth and Icebreaker (docs/DESIGN.md §3, "Maps without SVG artwork").
public class TileGridTests
{
    [Fact]
    public void A_point_lies_in_the_tile_found_for_it_at_every_zoom()
    {
        foreach (var tileSize in new[] { 175, 256 })
        {
            foreach (var p in new[] { new MapPoint(155.3, 95.7), new MapPoint(0.01, 255.99), new MapPoint(-12.5, -140.1) })
            {
                for (var z = 0; z <= 6; z++)
                {
                    var (x, y) = TileGrid.TileAt(p, tileSize, z);
                    var rect = TileGrid.TileRect(tileSize, z, x, y);
                    Assert.InRange(p.X, rect.Left, rect.Right);
                    Assert.InRange(p.Y, rect.Top, rect.Bottom);
                    Assert.Equal(tileSize / Math.Pow(2, z), rect.Width, 9);
                }
            }
        }
    }

    [Fact]
    public void Lab_extracts_land_on_the_tiles_whose_rooms_were_checked_by_eye()
    {
        // Positions from tarkov.dev's data; the tiles and the spots in them were looked at in the render (2026-10-03):
        // the medical block's elevator room on the Technical level, the hangar's floor on the ground floor.
        var projection = MapProjection.For(Fixtures.Map("the-lab"));
        var lab = Fixtures.Map("the-lab");
        Assert.Equal(175, lab.TileSize);

        var elevator = projection.ToMap(-112.3, -344.04);
        Assert.Equal((7, 11), TileGrid.TileAt(elevator, lab.TileSize, 4));
        var hangar = projection.ToMap(-170.49, -218.87);
        Assert.Equal((14, 8), TileGrid.TileAt(hangar, lab.TileSize, 4));

        // Where in the 256-pixel image: the elevator room's middle, as marked on the downloaded tile.
        var rect = TileGrid.TileRect(lab.TileSize, 4, 7, 11);
        Assert.Equal(159, (elevator.X - rect.Left) / rect.Width * 256, 1.0);
        Assert.Equal(206, (elevator.Y - rect.Top) / rect.Height * 256, 1.0);
    }

    [Fact]
    public void A_view_needs_the_tiles_it_overlaps_inside_the_bounds_only()
    {
        // 256 / 2² = 64 map units a tile at zoom 2.
        var view = new MapRect(10, 10, 130, 70);
        var bounds = new MapRect(0, 0, 256, 256);
        Assert.Equal([(0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1)], TileGrid.Visible(view, bounds, 256, 2));

        // A view edge exactly on a tile edge doesn't take the next tile; bounds cut what lies outside.
        Assert.Equal([(0, 0)], TileGrid.Visible(new MapRect(0, 0, 64, 64), bounds, 256, 2));
        Assert.Equal([(3, 3)], TileGrid.Visible(new MapRect(200, 200, 400, 400), bounds, 256, 2));
        Assert.Empty(TileGrid.Visible(new MapRect(300, 300, 400, 400), bounds, 256, 2));
    }

    [Theory]
    [InlineData(16, 256, 256, 4)]  // a map unit is 16 screen pixels: zoom 4 tiles are exactly as sharp
    [InlineData(15, 256, 256, 4)]  // a little less: still zoom 4 rather than a soft zoom 3
    [InlineData(9, 256, 256, 3)]   // zoom 3 (8 px a unit) is at most 1.19× soft: taken
    [InlineData(16, 175, 256, 4)]  // The Lab: 175-unit tiles of 256-pixel images are sharper per zoom
    [InlineData(0.1, 256, 256, 1)] // clamped to the coarsest published level
    [InlineData(4000, 256, 256, 5)] // and to the finest
    public void The_zoom_level_matches_the_screen_within_the_published_levels(double perUnit, int tileSize, int image, int expected) =>
        Assert.Equal(expected, TileGrid.ZoomFor(perUnit, tileSize, image, 1, 5));

    [Fact]
    public void Tiles_are_named_and_kept_by_map_layer_zoom_and_position()
    {
        var tile = new TileKey("https://assets.tarkov.dev/maps/labs_v4/technical/{z}/{x}/{y}.png", 4, 7, 11);
        Assert.Equal("https://assets.tarkov.dev/maps/labs_v4/technical/4/7/11.png", tile.Url);
        Assert.Equal(Path.Combine("the-lab", "technical", "4", "7_11.png"), TileGrid.CacheKey("the-lab", tile));
        Assert.Equal(new TileKey(tile.Template, 3, 3, 5), tile.Parent);
        var negative = new TileKey(tile.Template, 2, -1, -3);
        Assert.Equal((-1, -2), (negative.Parent.X, negative.Parent.Y));
    }
}
