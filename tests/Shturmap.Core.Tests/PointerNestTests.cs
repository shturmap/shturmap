using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

/// <summary>
/// Linked elements may lie inside one another (docs/DESIGN.md §4, "Visual language", linked highlight): the innermost
/// one around the pointer is the one pointed at, and leaving it gives the pointer back to the one around it. Before,
/// the highlight knew one element, and leaving an inner one let go of the row the pointer was still on (the review of
/// 2026-10-04).
/// </summary>
public class PointerNestTests
{
    // Elements named by their place in the tree: "block/line" lies inside "block".
    private static PointerNest<string> Nest() => new((inner, outer) => inner.StartsWith(outer + "/", StringComparison.Ordinal));

    [Fact]
    public void In_nothing_nothing_is_pointed_at()
    {
        var nest = Nest();
        Assert.Null(nest.Top);
        Assert.Empty(nest.Outward());
    }

    [Fact]
    public void Entering_an_inner_element_points_at_it_and_leaving_it_returns_to_the_outer_one()
    {
        var nest = Nest();
        nest.Enter("block");
        Assert.Equal("block", nest.Top);
        nest.Enter("block/line");
        Assert.Equal("block/line", nest.Top);
        nest.Leave("block/line");
        Assert.Equal("block", nest.Top);
        nest.Leave("block");
        Assert.Null(nest.Top);
    }

    [Fact]
    public void The_innermost_counts_whichever_was_entered_first()
    {
        // One move of the pointer from outside into the line: Windows may tell the line before the block.
        var nest = Nest();
        nest.Enter("block/line");
        nest.Enter("block");
        Assert.Equal("block/line", nest.Top);
        Assert.Equal(["block/line", "block"], nest.Outward());
    }

    [Fact]
    public void Three_deep_it_walks_back_out_one_at_a_time()
    {
        var nest = Nest();
        nest.Enter("block");
        nest.Enter("block/line");
        nest.Enter("block/line/key");
        Assert.Equal("block/line/key", nest.Top);
        Assert.Equal(["block/line/key", "block/line", "block"], nest.Outward());
        nest.Leave("block/line/key");
        Assert.Equal("block/line", nest.Top);
        nest.Leave("block/line");
        Assert.Equal("block", nest.Top);
    }

    [Fact]
    public void Moving_between_two_inner_elements_stays_on_the_newer_one()
    {
        // The next line is entered before the last one is left.
        var nest = Nest();
        nest.Enter("block");
        nest.Enter("block/a");
        nest.Enter("block/b");
        Assert.Equal("block/b", nest.Top);
        nest.Leave("block/a");
        Assert.Equal("block/b", nest.Top);
        Assert.Equal(["block/b", "block"], nest.Outward());
    }

    [Fact]
    public void Of_two_rows_side_by_side_the_one_entered_last_counts()
    {
        var nest = Nest();
        nest.Enter("row1");
        nest.Enter("row1/cell");
        nest.Enter("row2");
        Assert.Equal("row2", nest.Top);
        Assert.Equal(["row2"], nest.Outward());
        // The first row's late goodbyes change nothing.
        nest.Leave("row1/cell");
        nest.Leave("row1");
        Assert.Equal("row2", nest.Top);
    }

    [Fact]
    public void Leaving_the_outer_one_first_keeps_the_inner_one_until_it_is_left_too()
    {
        var nest = Nest();
        nest.Enter("row");
        nest.Enter("row/cell");
        nest.Leave("row");
        Assert.Equal("row/cell", nest.Top);
        nest.Leave("row/cell");
        Assert.Null(nest.Top);
    }

    [Fact]
    public void Entering_twice_is_entering_once_and_clearing_lets_go_of_everything()
    {
        var nest = Nest();
        nest.Enter("row");
        nest.Enter("row");
        nest.Leave("row");
        Assert.Null(nest.Top);
        nest.Enter("row");
        nest.Enter("row/cell");
        nest.Clear();
        Assert.Null(nest.Top);
    }
}
