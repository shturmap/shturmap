using Shturmap.App.Rules;
using Shturmap.Core.Desk;
using Shturmap.Core.Logs;

namespace Shturmap.Core.Tests.Desk;

// A window's stem through its cell's grid (docs/DESIGN.md §4, *A window's stem and light*; Rules.DeskPageRules.StemRoute):
// it never crosses a cell an item covers (UI QA re-check, 2026-10-19, F2: on the owner's Keys reading it ran along a row
// line straight across a 3×3 case's picture). The way runs along the lattice's lines: an item larger than one cell covers
// its inner ones, an item's edges and an empty cell's lines are free, and so is the region's outside edge. Points are a
// lattice line's i and j, from the cell's edge to the grid's edge on the window's side; made-up pages.
public class DeskStemTests
{
    private static DeskRegion Page(int cols, int rows, params DeskRect[] items) =>
        new("stash", DeskRegionKind.Stash, cols, rows, items.Select(r => new DeskCell(r.I, r.J, r.W, r.H, ["x"])).ToList());

    private static (double I, double J)[] Way(params (double I, double J)[] corners) => corners;

    private static DeskRegion WithAnchor(DeskRegion page, DeskRect anchor) =>
        page with { Cells = [.. page.Cells, new DeskCell(anchor.I, anchor.J, anchor.W, anchor.H, ["box"])] };

    // Nothing in the way: as before. Out of the cell's lower corner on the window's side, straight along that row line to
    // the grid's edge (1×1 items beside it passed along their edges); a cell at that edge from the middle of its side,
    // however tall. The made-up desk's Zhuk (four by three at the backpack's top left): at 1600×900, its window in the
    // gear's next column, out of its lower right corner along the Grizzly's bottom edge (tools/qa/desk.md, *first*); at
    // 2560×1440, its window under the backpack, out of its left edge's middle.
    [Fact]
    public void With_nothing_in_the_way_the_stem_leaves_the_lower_corner_as_before()
    {
        var anchor = new DeskRect(3, 1, 1, 1);
        var page = WithAnchor(Page(6, 4, new(0, 1, 1, 1), new(1, 1, 1, 1), new(2, 1, 1, 1), new(4, 1, 1, 1), new(5, 2, 1, 1), new(2, 2, 1, 1)), anchor);
        Assert.Equal(Way((3, 2), (0, 2)), DeskPageRules.StemRoute(page, anchor, toLeft: true));
        Assert.Equal(Way((4, 2), (6, 2)), DeskPageRules.StemRoute(page, anchor, toLeft: false));

        var tall = new DeskRect(0, 0, 1, 3);
        Assert.Equal(Way((0, 1.5)), DeskPageRules.StemRoute(WithAnchor(Page(4, 4), tall), tall, toLeft: true));
        var last = new DeskRect(2, 1, 2, 2);
        Assert.Equal(Way((4, 2.0)), DeskPageRules.StemRoute(WithAnchor(Page(4, 4), last), last, toLeft: false));

        var view = DeskExample.View(1, GameMode.Pve, 1, null, DateTimeOffset.UnixEpoch);
        var backpack = view.Sheet.Regions.Single(r => r.Region.Key == "backpack").Region;
        var zhuk = backpack.Cells.Single(c => !c.IsSlot && c.I == 0 && c.J == 0).Rect;
        Assert.Equal(new DeskRect(0, 0, 4, 3), zhuk);
        Assert.Equal(Way((4, 3), (6, 3)), DeskPageRules.StemRoute(backpack, zhuk, toLeft: false));
        Assert.Equal(Way((0, 1.5)), DeskPageRules.StemRoute(backpack, zhuk, toLeft: true));
    }

    // A window beside a page whose row lines by the cell lie under a 3×3 item (an item's footprint covers its inner lines):
    // the stem runs down the column line beside the cell to the line below the item and along it; at an equal distance
    // above and below, below, as the lower corner was. On a page like the owner's Keys reading (a box of three by two
    // beside a 3×3 case, both from row 9): out of the box's upper corner along the case's top edge where that line is free
    // to the grid's edge; where an item covers it further on, down to the line under the case, the nearest free one.
    [Fact]
    public void A_row_line_under_a_large_item_takes_the_line_below_it()
    {
        var anchor = new DeskRect(4, 2, 1, 1);
        var page = WithAnchor(Page(8, 6, new DeskRect(1, 1, 3, 3)), anchor);
        Assert.Equal(Way((4, 3), (4, 4), (0, 4)), DeskPageRules.StemRoute(page, anchor, toLeft: true));

        var box = new DeskRect(7, 9, 3, 2);
        DeskRect[] owners = [new(0, 0, 2, 2), new(4, 7, 3, 2), new(7, 7, 3, 2), new(4, 9, 3, 3)];
        Assert.Equal(Way((7, 9), (0, 9)), DeskPageRules.StemRoute(WithAnchor(Page(10, 13, owners), box), box, toLeft: true));
        var blocked = WithAnchor(Page(10, 13, [.. owners, new(1, 8, 2, 2)]), box);
        Assert.Equal(Way((7, 11), (7, 12), (0, 12)), DeskPageRules.StemRoute(blocked, box, toLeft: true));
    }

    // Every row line out of the cell blocked toward the window, and the column line beside it too, above and below: the
    // stem runs round the region's outside edge, which no item covers (here down the cell's far side to the bottom edge and
    // along it), with the fewest turns, then the shortest way.
    [Fact]
    public void Where_every_line_is_blocked_the_stem_runs_round_the_outside_edge()
    {
        var anchor = new DeskRect(3, 2, 1, 1);
        var page = WithAnchor(Page(5, 5, new(2, 1, 2, 1), new(2, 3, 2, 1), new(0, 0, 2, 5)), anchor);
        Assert.Equal(Way((4, 3), (4, 5), (0, 5)), DeskPageRules.StemRoute(page, anchor, toLeft: true));
    }

    // Whatever the page, the way crosses no item's picture: every step on a line no item covers, axis by axis, from the
    // cell's edge to the grid's edge on the window's side. On the made-up desk's grids and on packed pages of random items.
    [Fact]
    public void The_stem_never_crosses_an_item()
    {
        var view = DeskExample.View(1, GameMode.Pve, 1, null, DateTimeOffset.UnixEpoch);
        var pages = view.Sheet.Regions.Select(r => r.Region).Where(r => DeskPageRules.Grid(r).Cols > 0 && r.Cells.Any(c => !c.IsSlot)).ToList();
        var random = new Random(7);
        for (var n = 0; n < 40; n++)
            pages.Add(Packed(random, random.Next(4, 11), random.Next(4, 14)));
        foreach (var page in pages)
        {
            foreach (var cell in page.Cells.Where(c => !c.IsSlot))
            {
                foreach (var toLeft in new[] { true, false })
                {
                    var route = DeskPageRules.StemRoute(page, cell.Rect, toLeft);
                    Assert.NotNull(route);
                    var edge = toLeft ? 0 : page.Cols;
                    Assert.Equal(edge, route[^1].I);
                    if (route.Count == 1)
                    {
                        Assert.Equal(toLeft ? cell.I : cell.I + cell.W, edge);
                        continue;
                    }
                    Assert.True(OnEdge(cell.Rect, route[0]), $"{page.Key} {cell.Rect}: starts at {route[0]}");
                    for (var k = 1; k < route.Count; k++)
                        Assert.False(Crosses(page, route[k - 1], route[k]), $"{page.Key} {cell.Rect} {(toLeft ? "left" : "right")}: {route[k - 1]} to {route[k]}");
                }
            }
        }
    }

    private static bool OnEdge(DeskRect cell, (double I, double J) p) =>
        (p.I == cell.I || p.I == cell.I + cell.W) && p.J >= cell.J && p.J <= cell.J + cell.H
        || (p.J == cell.J || p.J == cell.J + cell.H) && p.I >= cell.I && p.I <= cell.I + cell.W;

    // Whether a leg runs inside an item: along a row line strictly between an item's top and bottom, over its columns, or
    // a column line strictly between its sides, over its rows.
    private static bool Crosses(DeskRegion page, (double I, double J) from, (double I, double J) to)
    {
        Assert.True(from.I == to.I || from.J == to.J, $"{from} to {to} is not along a line");
        foreach (var item in page.Cells.Where(c => !c.IsSlot))
        {
            if (from.J == to.J && item.J < from.J && from.J < item.J + item.H
                && Math.Max(Math.Min(from.I, to.I), item.I) < Math.Min(Math.Max(from.I, to.I), item.I + item.W))
                return true;
            if (from.I == to.I && item.I < from.I && from.I < item.I + item.W
                && Math.Max(Math.Min(from.J, to.J), item.J) < Math.Min(Math.Max(from.J, to.J), item.J + item.H))
                return true;
        }
        return false;
    }

    // A page packed with random items of one to four by one to four cells where they fit, the rest empty.
    private static DeskRegion Packed(Random random, int cols, int rows)
    {
        var taken = new bool[cols, rows];
        var items = new List<DeskRect>();
        for (var tries = 0; tries < cols * rows; tries++)
        {
            int w = random.Next(1, 5), h = random.Next(1, 5), i = random.Next(0, cols - w + 1), j = random.Next(0, rows - h + 1);
            if (i < 0 || j < 0 || Enumerable.Range(i, w).Any(x => Enumerable.Range(j, h).Any(y => taken[x, y])))
                continue;
            foreach (var x in Enumerable.Range(i, w))
            {
                foreach (var y in Enumerable.Range(j, h))
                    taken[x, y] = true;
            }
            items.Add(new DeskRect(i, j, w, h));
        }
        return Page(cols, rows, [.. items]) with { Key = $"packed {cols}×{rows}" };
    }
}
