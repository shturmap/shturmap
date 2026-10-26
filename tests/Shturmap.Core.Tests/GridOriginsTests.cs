using Shturmap.Core.Maps;

namespace Shturmap.Core.Tests;

// Each map's A1 is fixed in the app (owner, 2026-10-26; docs/DESIGN.md §4, "Map drawing", *The grid*): a reference is only
// worth saying if every player's Shturmap draws the same grid, so it never follows tarkov.dev's bounds by itself.
public class GridOriginsTests
{
    // The table as frozen on 2026-10-26, from tarkov.dev's maps.json of that day (the test fixture). A change here is a
    // change of every reference on that map for every player: only for a map whose own grid was wrong, never because
    // tarkov.dev's bounds moved (docs/UPDATES.md).
    private static readonly Dictionary<string, (double X, double Z)> AsFrozen = new()
    {
        ["customs"] = (700, -400),
        ["factory"] = (100, 100),
        ["ground-zero"] = (300, -200),
        ["icebreaker"] = (100, -100),
        ["interchange"] = (600, -500),
        ["lighthouse"] = (600, -1000),
        ["reserve"] = (300, -300),
        ["shoreline"] = (600, -500),
        ["streets-of-tarkov"] = (400, -300),
        ["terminal"] = (500, -600),
        ["the-lab"] = (-300, -500),
        ["the-labyrinth"] = (-100, -100),
        ["woods"] = (700, -1000),
    };

    [Fact]
    public void Every_map_in_the_data_has_its_a1()
    {
        var missing = Fixtures.MapDefinitions.Where(m => !GridOrigins.Frozen.ContainsKey(m.Key)).Select(m => m.Key).ToList();
        Assert.True(missing.Count == 0, "No A1 for " + string.Join(", ", missing) + ": add each to GridOrigins (shturmap-cli grid prints it) and to this test");
        Assert.All(Fixtures.MapDefinitions, m => Assert.True(SquadGrid.For(m).Fixed));
    }

    [Fact]
    public void The_table_does_not_change_silently() =>
        Assert.Equal(AsFrozen.OrderBy(e => e.Key, StringComparer.Ordinal), GridOrigins.Frozen.OrderBy(e => e.Key, StringComparer.Ordinal));

    [Fact]
    public void Each_a1_is_a_corner_of_100_metre_squares()
    {
        foreach (var (key, (x, z)) in GridOrigins.Frozen)
        {
            Assert.True(x % 100 == 0 && z % 100 == 0, $"{key}: A1 at ({x}, {z}) isn't on the 100 m lines");
        }
    }

    [Fact]
    public void Every_map_lies_inside_its_lettered_grid()
    {
        // No part of a map's bounds left of A, above row 1 or past Z: the whole map has references. Should tarkov.dev's
        // bounds ever grow past A1, the owner decides; the table isn't moved by itself.
        foreach (var map in Fixtures.MapDefinitions)
        {
            var grid = SquadGrid.For(map);
            var (u1, v1) = grid.Along(map.Bounds.X1, map.Bounds.Z1);
            var (u2, v2) = grid.Along(map.Bounds.X2, map.Bounds.Z2);
            Assert.True(Math.Min(u1, u2) >= grid.OriginU && Math.Min(v1, v2) >= grid.OriginV, $"{map.Key} starts before A1");
            Assert.True(Math.Max(u1, u2) <= grid.OriginU + SquadGrid.Letters.Length * SquadGrid.Square, $"{map.Key} runs past Z");
        }
    }

    [Fact]
    public void Bounds_that_move_keep_the_frozen_grid()
    {
        // tarkov.dev widens Customs' bounds by 150 m to the left as drawn (world +x on a map rotated 180°): the grid stays
        // where it was, so a reference said on an install with the old data still means the same place.
        var customs = Fixtures.Map("customs");
        var wider = customs with { Bounds = customs.Bounds with { X1 = customs.Bounds.X1 + 150 } };
        Assert.Equal(SquadGrid.For(customs).Corner, SquadGrid.For(wider).Corner);
        Assert.Equal(SquadGrid.For(customs).At(100, 50), SquadGrid.For(wider).At(100, 50));
        Assert.NotEqual(SquadGrid.FromBounds(customs).Corner, SquadGrid.FromBounds(wider).Corner);
    }
}
