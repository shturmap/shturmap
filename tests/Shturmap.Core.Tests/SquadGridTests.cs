using Shturmap.Core.Maps;

namespace Shturmap.Core.Tests;

// The squad grid's geometry (owner, 2026-10-26: E, the squad grid; docs/DESIGN.md §4, "Map drawing", *The grid*): 100 m
// squares on the map as drawn, A to Z without I and O from the left, rows from 1 at the top, the ninths as on a number pad.
public class SquadGridTests
{
    public static TheoryData<string> Maps() => [.. Fixtures.MapDefinitions.Select(m => m.Key)];

    [Fact]
    public void Columns_are_lettered_a_to_z_without_i_and_o()
    {
        Assert.Equal(24, SquadGrid.Letters.Length);
        Assert.DoesNotContain('I', SquadGrid.Letters);
        Assert.DoesNotContain('O', SquadGrid.Letters);
        Assert.Equal(["A", "B", "H", "J", "N", "P", "Z"], new[] { 0, 1, 7, 8, 12, 13, 23 }.Select(SquadGrid.Letter));
        Assert.Equal("?", SquadGrid.Letter(24));
        Assert.Equal("?", SquadGrid.Letter(-1));
    }

    [Fact]
    public void The_ninths_are_a_number_pad_seven_at_the_top_left_three_at_the_bottom_right()
    {
        // across 0..2 from the left, down 0..2 from the top: 7 8 9 / 4 5 6 / 1 2 3, as on a keyboard's number pad.
        int[][] pad = [[7, 8, 9], [4, 5, 6], [1, 2, 3]];
        for (var down = 0; down < 3; down++)
        {
            for (var across = 0; across < 3; across++)
                Assert.Equal(pad[down][across], SquadGrid.Keypad(across, down));
        }
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void Every_maps_rotation_is_a_multiple_of_90_degrees_so_the_lines_stand_square(string key) =>
        Assert.Equal(0, Fixtures.Map(key).CoordinateRotation % 90);

    [Theory]
    [MemberData(nameof(Maps))]
    public void The_origin_square_is_a1_and_its_ninths_read_as_on_a_number_pad(string key)
    {
        var grid = SquadGrid.For(Fixtures.Map(key));
        string At(double u, double v)
        {
            var (x, z) = grid.World(grid.OriginU + u, grid.OriginV + v);
            return grid.At(x, z)!.Value.ToString();
        }

        Assert.Equal("A1-7", At(5, 5));
        Assert.Equal("A1-9", At(95, 5));
        Assert.Equal("A1-5", At(50, 50));
        Assert.Equal("A1-1", At(5, 95));
        Assert.Equal("A1-3", At(95, 95));
        Assert.Equal("B3-5", At(150, 250));
        Assert.Equal("C1-8", At(250, 10));
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void Right_and_down_on_the_grid_are_right_and_down_on_the_map_as_drawn(string key)
    {
        var map = Fixtures.Map(key);
        var grid = SquadGrid.For(map);
        var projection = MapProjection.For(map);
        MapPoint On(double u, double v)
        {
            var (x, z) = grid.World(u, v);
            return projection.ToMap(x, z);
        }

        var corner = On(grid.OriginU, grid.OriginV);
        var right = On(grid.OriginU + 100, grid.OriginV);
        var down = On(grid.OriginU, grid.OriginV + 100);
        Assert.True(right.X > corner.X && Math.Abs(right.Y - corner.Y) < 1e-6, $"{key}: U doesn't run right");
        Assert.True(down.Y > corner.Y && Math.Abs(down.X - corner.X) < 1e-6, $"{key}: V doesn't run down");
        // The map's extent starts in A1 or right of and below it, never left of A or above row 1.
        var bounds = projection.WorldRect;
        Assert.True(bounds.Left >= corner.X - 1e-6 && bounds.Top >= corner.Y - 1e-6, $"{key} starts before A1");
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void Every_line_lies_on_a_world_multiple_of_100_metres(string key)
    {
        var grid = SquadGrid.For(Fixtures.Map(key));
        for (var k = 0; k <= grid.Columns; k++)
        {
            for (var j = 0; j <= grid.Rows; j++)
            {
                var (x, z) = grid.World(grid.OriginU + k * SquadGrid.Square, grid.OriginV + j * SquadGrid.Square);
                Assert.Equal(0, Math.IEEERemainder(x, 100), 9);
                Assert.Equal(0, Math.IEEERemainder(z, 100), 9);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void Every_map_fits_in_the_24_columns(string key)
    {
        var grid = SquadGrid.For(Fixtures.Map(key));
        Assert.InRange(grid.Columns, 1, SquadGrid.Letters.Length);
        Assert.True(grid.Rows >= 1);
        // Each corner of the bounds has a reference.
        var b = Fixtures.Map(key).Bounds;
        foreach (var (x, z) in new[] { (b.X1, b.Z1), (b.X1, b.Z2), (b.X2, b.Z1), (b.X2, b.Z2) })
            Assert.NotNull(grid.At(x, z));
    }

    [Fact]
    public void Past_the_edge_of_the_lettered_grid_there_is_no_reference_but_past_the_last_row_it_goes_on()
    {
        var grid = SquadGrid.For(Fixtures.Map("streets-of-tarkov"));
        GridReference? At(double u, double v)
        {
            var (x, z) = grid.World(grid.OriginU + u, grid.OriginV + v);
            return grid.At(x, z);
        }

        Assert.Null(At(-1, 50));
        Assert.Null(At(50, -1));
        Assert.Null(At(24 * 100 + 1, 50));
        Assert.Equal("Z1-5", At(23 * 100 + 50, 50)!.Value.ToString());
        // Streets is 7 squares across and 9 down: one past both still reads.
        Assert.Equal((7, 9), (grid.Columns, grid.Rows));
        Assert.Equal("H10-5", At(7 * 100 + 50, 9 * 100 + 50)!.Value.ToString());
    }

    [Fact]
    public void The_letters_skip_i_and_o_on_the_map()
    {
        var grid = SquadGrid.For(Fixtures.Map("shoreline"));
        string At(double u)
        {
            var (x, z) = grid.World(grid.OriginU + u, grid.OriginV + 50);
            return grid.At(x, z)!.Value.Square;
        }

        Assert.Equal("H1", At(750));
        Assert.Equal("J1", At(850));
        Assert.Equal("N1", At(1250));
        Assert.Equal("P1", At(1350));
        // Shoreline, the widest map, is 17 squares across: A to S.
        Assert.Equal(17, grid.Columns);
        Assert.Equal("S1", At(1650));
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void World_and_grid_metres_round_trip(string key)
    {
        var grid = SquadGrid.For(Fixtures.Map(key));
        foreach (var (x, z) in new[] { (123.45, -67.89), (-512.5, 300.25), (0.0, 0.0) })
        {
            var (u, v) = grid.Along(x, z);
            var (bx, bz) = grid.World(u, v);
            Assert.Equal(x, bx, 9);
            Assert.Equal(z, bz, 9);
        }
        // Each ninth's middle names the ninth it is the middle of.
        for (var column = 0; column < grid.Columns; column++)
        {
            for (var row = 0; row < grid.Rows; row++)
            {
                for (var digit = 1; digit <= 9; digit++)
                {
                    var (across, down) = ((digit - 1) % 3, 2 - (digit - 1) / 3);
                    var (x, z) = grid.World(grid.OriginU + column * 100 + (across + 0.5) * SquadGrid.Third,
                        grid.OriginV + row * 100 + (down + 0.5) * SquadGrid.Third);
                    Assert.Equal(new GridReference(column, row + 1, digit), grid.At(x, z));
                }
            }
        }
    }

    [Fact]
    public void A_reference_reads_as_players_say_it()
    {
        Assert.Equal("D5-8", new GridReference(3, 5, 8).ToString());
        Assert.Equal("D5", new GridReference(3, 5).ToString());
        Assert.Equal("J12", new GridReference(8, 12, 1).Square);
    }

    [Fact]
    public void Factory_lies_on_four_squares()
    {
        // As the panel found (docs/NEXT.md, 2026-10-26): A1 to B2.
        var grid = SquadGrid.For(Fixtures.Map("factory"));
        Assert.Equal((2, 2), (grid.Columns, grid.Rows));
    }

    [Fact]
    public void Icebreakers_squares_are_100_metres_each_way_though_its_render_stretches_them()
    {
        // tarkov.dev's transform is kept for Icebreaker (its render is stretched 1.75 times along one axis): its squares
        // are oblong on screen and still 100 m each way in the world.
        var map = Fixtures.Map("icebreaker");
        var grid = SquadGrid.For(map);
        var projection = MapProjection.For(map);
        var (x0, z0) = grid.World(grid.OriginU, grid.OriginV);
        var (x1, z1) = grid.World(grid.OriginU + 100, grid.OriginV);
        var (x2, z2) = grid.World(grid.OriginU, grid.OriginV + 100);
        Assert.Equal(100, Math.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0)), 9);
        Assert.Equal(100, Math.Sqrt((x2 - x0) * (x2 - x0) + (z2 - z0) * (z2 - z0)), 9);
        var across = projection.ToMap(x1, z1).X - projection.ToMap(x0, z0).X;
        var down = projection.ToMap(x2, z2).Y - projection.ToMap(x0, z0).Y;
        Assert.Equal(1.75, down / across, 6);
    }

    [Fact]
    public void A_map_without_an_entry_starts_at_its_bounds()
    {
        // A map tarkov.dev adds: its A1 is the square that holds its bounds' left and top edge, until it gets its entry.
        var map = new MapDefinition
        {
            Key = "made-up-map",
            Transform = [1, 0, 1, 0],
            CoordinateRotation = 180,
            Bounds = new WorldBox(250, -130, -170, 410),
        };
        var grid = SquadGrid.For(map);
        Assert.False(grid.Fixed);
        // Rotated 180°: right on screen is world −x, down is world +z (as on Streets).
        Assert.Equal((-300.0, -200.0), (grid.OriginU, grid.OriginV));
        Assert.Equal((300.0, -200.0), grid.Corner);
        Assert.Equal((5, 7), (grid.Columns, grid.Rows));
        Assert.Equal("A1-7", grid.At(295, -195)!.Value.ToString());
    }
}
