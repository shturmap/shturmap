using Shturmap.App.Rules;
using Shturmap.Core.Desk;
using Shturmap.Core.Logs;

namespace Shturmap.Core.Tests.Desk;

// The desk page's layout and words (owner, 2026-10-19, the addendum's A(2) and B1; docs/DESIGN.md §4, *Its place*, *What it
// mirrors*, *The head line and the fold*; Rules.DeskPageRules), on the made-up desk (Rules.DeskExample).
public class DeskPageRulesTests
{
    private static readonly DeskView View = DeskExample.View(1, GameMode.Pve, 1, null, DateTimeOffset.UnixEpoch);

    private static DeskRegionSheet Region(string key) => View.Sheet.Regions.Single(r => r.Region.Key == key);

    // The owner's page after the Keys window's reading of 2026-10-19: the gear folded into one line, the stash of ten by
    // thirteen open and the Keys window of eleven by seven attached to it.
    private static List<DeskRegion> OwnersPage() =>
    [
        new("loadout", DeskRegionKind.Loadout, 0, 0, Enumerable.Range(0, 7).Select(i => new DeskCell(0, 0, 0, 0, ["x"]) { Slot = DeskSlots.Loadout[i].Key }).ToList()),
        new("rig", DeskRegionKind.Rig, 6, 9, []),
        new("stash", DeskRegionKind.Stash, 10, 13, []),
        new("window:keys", DeskRegionKind.ContainerWindow, 11, 7, []) { Title = "Keys", AttachedTo = new DeskAnchor("stash", 7, 9) },
    ];

    private static readonly IReadOnlySet<string> StashAndKeys = new HashSet<string> { "stash", "window:keys" };

    // The cells fill the desk (owner, 2026-10-19, after trying step C: "there is a lot of wasted screen space … Find the
    // optimal solution"): the largest size, 40 to 128 DIP in steps of 8, at which the widest row fits the desk's width and
    // the tallest column its height, the page flowed into columns: the gear down one and on into the next, the stash of its
    // own, what follows the stash (its windows) in the shortest column beside it. On the owner's page at a 2560 monitor's
    // desk (2176 × 1330) the stash fills the height at 96, the Keys window beside it; at 1600×900's (1216 × 760) 48; at
    // 900×560's (516 × 440) 40, one column, and the page scrolls. Until 2026-10-19 the width alone: 80 at the most, the gear
    // and the stash two columns from 56.
    [Fact]
    public void The_cells_fill_the_desks_width_and_height()
    {
        var page = OwnersPage();
        var large = DeskPageRules.Layout(page, 2176, 1330, StashAndKeys);
        Assert.Equal((96, 2), (large.Cell, large.Break));
        Assert.Equal([0, 1, 0], large.Columns);
        var middle = DeskPageRules.Layout(page, 1216, 760, StashAndKeys);
        Assert.Equal((48, 2), (middle.Cell, middle.Break));
        var small = DeskPageRules.Layout(page, 516, 440, StashAndKeys);
        Assert.Equal((40, -1), (small.Cell, small.Break));
        Assert.False(small.Fits(516, 440));
        // Each layout chosen fits the desk, and the next size up doesn't, flowed or in one column.
        foreach (var (w, h) in new[] { (2176.0, 1330.0), (1216, 760), (1600, 1000), (1000, 700) })
        {
            var chosen = DeskPageRules.Layout(page, w, h, StashAndKeys);
            Assert.True(chosen.Fits(w, h), $"{w}×{h} at {chosen.Cell}");
            Assert.All(new[] { false, true }, one =>
                Assert.False(DeskPageRules.Arrange(page, StashAndKeys, chosen.Cell + 8, w, h, oneColumn: one).Fits(w, h), $"{w}×{h} at {chosen.Cell + 8}"));
        }
        // With every region open the gear flows on into a second column: the made-up desk read whole fills a 2560 monitor's
        // desk at 80 in three columns (the gear's two and the stash's, the WZ window beside the stash), and 1600×900's at 40,
        // three columns too, unscrolled (until 2026-10-19 64, two columns, scrolling).
        var made = View.Sheet.Regions.Select(r => r.Region).ToList();
        var whole = DeskPageRules.Layout(made, 2176, 1330);
        Assert.Equal(80, whole.Cell);
        Assert.Equal(3, whole.ColumnWidths.Count);
        var mid = DeskPageRules.Layout(made, 1216, 760);
        Assert.Equal((40, 3), (mid.Cell, mid.ColumnWidths.Count));
        Assert.True(mid.Fits(1216, 760));
        // 900×560's: 40, one column, scrolling.
        var narrow = DeskPageRules.Layout(made, 516, 440);
        Assert.Equal((40, -1, 1), (narrow.Cell, narrow.Break, narrow.ColumnWidths.Count));
        // Up to 128, twice the art's own 64 px: a page of one small grid on a large desk.
        Assert.Equal(128, DeskPageRules.Cell([new DeskRegion("stash", DeskRegionKind.Stash, 10, 3, [])], 2176, 1330));
        // Every size a whole number of pixels at every Windows scale from 100 to 200 % in quarters.
        Assert.Equal([40, 48, 56, 64, 72, 80, 88, 96, 104, 112, 120, 128], DeskPageRules.Sizes);
        Assert.All(DeskPageRules.Sizes, s => Assert.All(new[] { 1, 1.25, 1.5, 1.75, 2 }, f => Assert.Equal(Math.Round(s * f), s * f)));
        // A page without a stash: the gear alone, flowed.
        Assert.Equal(-1, DeskPageRules.Layout(made.Where(r => r.Kind != DeskRegionKind.Stash).ToList(), 2176, 1330).Break);
    }

    // The gear flows down a column and on into the next where the desk's height ends, in reading order, a row of POCKETS and
    // SPECIAL SLOTS kept whole; the stash heads a column of its own; a window of the stash's goes in the shortest column
    // beside the stash where it fits, else under the stash.
    [Fact]
    public void The_page_flows_into_columns_in_reading_order()
    {
        var made = View.Sheet.Regions.Select(r => r.Region).ToList();
        var keys = made.Select(r => r.Key).ToList();
        var flowed = DeskPageRules.Arrange(made, null, 48, 1216, 760);
        var columnOf = flowed.Blocks.Select((b, i) => (Key: ((DeskBlock.Region)b).Key, Column: flowed.Columns[i])).ToDictionary(x => x.Key, x => x.Column);
        // Reading order kept: no region in a column left of one before it, but what follows the stash.
        var gear = keys.TakeWhile(k => k != "stash").ToList();
        Assert.True(gear.Zip(gear.Skip(1)).All(p => columnOf[p.First] <= columnOf[p.Second]));
        Assert.Equal(columnOf["pockets"], columnOf["special"]);
        Assert.True(columnOf["stash"] > columnOf[gear[^1]]);
        // Without a height the gear stays one column, the stash beside it.
        var tall = DeskPageRules.Arrange(made, null, 48);
        Assert.Equal(2, tall.ColumnWidths.Count);
        // The owner's page: the Keys window beside the stash, in the gear's column, where it fits; under it on a desk too
        // narrow for that.
        var page = OwnersPage();
        Assert.Equal([0, 1, 0], DeskPageRules.Arrange(page, StashAndKeys, 48, 2176, 760).Columns);
        Assert.Equal([0, 1, 1], DeskPageRules.Arrange(page, StashAndKeys, 48, 1000, 2000).Columns);
    }

    // The marks and the heads grow with the cell: the figure 10.5 px at 40 to 15 at 80, the count 10 to 14.5, the source's
    // glyph 9 to 14, a trader's portrait 14 at 56 to 20 at 80, the notch and the "?" plate a DIP a step from 56; the
    // regions' heads 11, 12 and 13 px.
    [Fact]
    public void The_marks_and_the_heads_scale_with_the_cell()
    {
        var sizes = DeskPageRules.Sizes.Take(6).ToList();
        Assert.Equal([10.5, 11, 12, 13, 14, 15], sizes.Select(DeskCells.FigureSizeAt));
        Assert.Equal([10, 10.5, 11.5, 12.5, 13.5, 14.5], sizes.Select(DeskCells.SmallSizeAt));
        Assert.Equal([9, 10, 11, 12, 13, 14], sizes.Select(DeskCells.GlyphAt));
        Assert.Equal([14, 16, 18, 20], sizes.Skip(2).Select(DeskCells.PortraitAt));
        Assert.Equal([11, 11, 11, 12, 13, 14], sizes.Select(DeskCells.NotchAt));
        Assert.Equal([12, 12, 12, 13, 14, 15], sizes.Select(DeskCells.PlateAt));
        Assert.Equal([11, 11, 12, 12, 13, 13], sizes.Select(DeskPageRules.HeadSize));
        // On up to 128 a pixel a step (the figure 21 px at 128), the heads a pixel every two steps up to 16.
        Assert.Equal(21, DeskCells.FigureSizeAt(128));
        Assert.Equal([14, 14, 15, 15, 16, 16], DeskPageRules.Sizes.Skip(6).Select(DeskPageRules.HeadSize));
        // From 56 up the change stands as a figure, as at 56 (*The cell*).
        var cell = new DeskCellSheet(new DeskCell(0, 0, 2, 1, ["x"]),
            new DeskFigure(DeskSource.Flea, 13_000, 13_000, null) { Arrow = DeskArrow.Down, Change48h = -12 }, DeskKeepLevel.None, false);
        Assert.All(DeskPageRules.Sizes.Skip(2), s => Assert.Equal("↓\u00A0−12%", DeskCells.Of(cell, s).Change));
        Assert.Equal("↓", DeskCells.Of(cell, 48).Change);
    }

    // LOADOUT is a strip of slot cells: no grid under it, whatever a reading says, and its slots don't widen the gear's
    // column (QA pass of 2026-10-19, F1 and F2: a reading's loadout of eleven by one drew eleven empty cells and pushed the
    // stash's tenth column past a window 1600 wide).
    [Fact]
    public void The_loadout_draws_no_grid_whatever_a_reading_says()
    {
        var read = new DeskRegion("loadout", DeskRegionKind.Loadout, 11, 1, [new DeskCell(0, 0, 1, 1, ["x"]) { Slot = "earpiece" }]);
        Assert.Equal((0, 0), DeskPageRules.Grid(read));
        var rig = new DeskRegion("rig", DeskRegionKind.Rig, 5, 2, []);
        var stash = new DeskRegion("stash", DeskRegionKind.Stash, 10, 13, []);
        Assert.Equal(6 * 56 + 1, DeskPageRules.ColumnWidth([read, rig], 56, 6));
        Assert.Equal((5, 2), DeskPageRules.Grid(rig));
        // The desk of a window 1600 by 900 (1216 by 760 DIP beside the rail): two columns, the gear's six cells and the
        // stash's ten.
        Assert.Equal(2, DeskPageRules.ColumnBreak([read, rig, stash], 1216, 760));
    }

    // POCKETS and SPECIAL SLOTS stand side by side: the gear's column holds both (the pockets' four cells, the special
    // slots' three and the gap between them), so the special slots aren't cut at the column's edge.
    [Fact]
    public void The_gear_column_holds_pockets_and_special_slots_side_by_side()
    {
        var pockets = new DeskRegion("pockets", DeskRegionKind.Pockets, 4, 2, []);
        var special = new DeskRegion("special", DeskRegionKind.SpecialSlots, 3, 2, []);
        Assert.Equal(7 * 56 + DeskPageRules.RowGap + 1, DeskPageRules.ColumnWidth([pockets, special], 56, 6));
        Assert.Equal(6 * 40 + 1, DeskPageRules.ColumnWidth([special], 40, 6));
    }

    // Two columns only where both fit the desk: a gear column as wide as its widest grid, the stash's ten cells beside it.
    [Fact]
    public void Two_columns_only_where_both_fit()
    {
        var wide = new DeskRegion("backpack", DeskRegionKind.Backpack, 12, 4, []);
        var stash = new DeskRegion("stash", DeskRegionKind.Stash, 10, 13, []);
        // At 40 DIP side by side they take 12 + 10 cells, the gap and the margins: 942 DIP; the page 13 rows high.
        Assert.Equal(-1, DeskPageRules.ColumnBreak([wide, stash], 941, 600));
        Assert.Equal(1, DeskPageRules.ColumnBreak([wide, stash], 942, 600));
    }

    // A grid's compartments stand apart as the game draws them (QA pass of 2026-10-19, F3: the Zhuk's ten pockets of one by
    // two read as twenty equal cells): a cell stands in by half the gap on each side its compartment's edge runs along.
    [Fact]
    public void A_grids_compartments_stand_apart()
    {
        var zhuk = new DeskRegion("window:zhuk", DeskRegionKind.ContainerWindow, 5, 4, [])
        {
            Parts = [.. Enumerable.Range(0, 10).Select(k => new DeskRect(k % 5, k / 5 * 2, 1, 2))],
        };
        Assert.Equal((2, 2, 2, 2), DeskPageRules.Inset(zhuk, new DeskRect(3, 0, 1, 2), DeskCells.Large));
        Assert.Equal((1, 1, 1, 0), DeskPageRules.Inset(zhuk, new DeskRect(3, 0, 1, 1), DeskCells.Small));
        Assert.Equal((1, 0, 1, 1), DeskPageRules.Inset(zhuk, new DeskRect(3, 3, 1, 1), DeskCells.Small));
        // A grid of one compartment: its cells stand on its lines.
        var backpack = new DeskRegion("backpack", DeskRegionKind.Backpack, 5, 7, []) { Parts = [new DeskRect(0, 0, 5, 7)] };
        Assert.Equal((0, 0, 0, 0), DeskPageRules.Inset(backpack, new DeskRect(0, 0, 1, 2), DeskCells.Large));
    }

    [Fact]
    public void Pockets_and_special_slots_share_a_row()
    {
        Assert.True(DeskPageRules.SameRow(DeskRegionKind.Pockets, DeskRegionKind.SpecialSlots));
        Assert.False(DeskPageRules.SameRow(DeskRegionKind.Rig, DeskRegionKind.Pockets));
    }

    [Fact]
    public void Each_region_stands_under_its_own_head_in_reading_order()
    {
        Assert.Equal(
            ["LOADOUT", "TACTICAL RIG", "POCKETS", "SPECIAL SLOTS", "BACKPACK", "ZHUK · IN THE BACKPACK", "POUCH", "STASH", "WZ"],
            View.Sheet.Regions.Select(r => DeskPageRules.Head(r.Region, View.Sheet.Page)));
        Assert.Equal("ZHUK ▸", DeskPageRules.FoldName(Region("window:zhuk").Region));
        Assert.Equal("POUCH ▸", DeskPageRules.FoldName(Region("pouch").Region));
        Assert.Equal("UNCHANGED", DeskPageRules.Unchanged);
    }

    // A window attached to none says so, and, its containers split between the player and the stash, that it counts in
    // neither total.
    [Fact]
    public void A_window_attached_to_none_says_where_its_containers_are()
    {
        Assert.Equal("Not attached: WZ ×2 on the page, on you and in the stash; counted in neither", DeskPageRules.NotAttached(Region("window:wz")));
        Assert.Null(DeskPageRules.NotAttached(Region("window:zhuk")));
        Assert.Null(DeskPageRules.NotAttached(Region("stash")));
        var alone = Region("window:wz") with { Part = DeskPart.Stash, Region = Region("window:wz").Region with { SameName = 3, SameNameIn = ["stash"] } };
        Assert.Equal("Not attached: WZ ×3 on the page", DeskPageRules.NotAttached(alone));
        var none = alone with { Region = alone.Region with { SameName = 0 } };
        Assert.Equal("Not attached: no WZ on the page", DeskPageRules.NotAttached(none));
    }

    [Fact]
    public void The_head_line_says_both_totals_and_the_keeps()
    {
        var totals = new DeskTotals(1_010_000, true, 6_840_000, false, 3, 2, 4);
        Assert.Equal("ON YOU 1.01\u00A0M+\u00A0₽ · STASH 6.84\u00A0M\u00A0₽ · KEEP 3 · 2 · 4", DeskPageRules.HeadLine(totals, totals.OnYou, totals.Stash));
        // Counting up: the totals given apart, the "+" and the keeps as they end.
        Assert.Equal("ON YOU 505k+\u00A0₽ · STASH 3.42\u00A0M\u00A0₽ · KEEP 3 · 2 · 4", DeskPageRules.HeadLine(totals, 505_000, 3_420_000));
    }
}

[Collection("UiLanguage")]
public class DeskPageRulesInGermanTests
{
    [Fact]
    public void A_windows_head_says_its_region_in_german_grammar()
    {
        try
        {
            UiLanguage.Set(UiLanguage.German);
            var view = DeskExample.View(1, GameMode.Pve, 1, null, DateTimeOffset.UnixEpoch);
            var zhuk = view.Sheet.Regions.Single(r => r.Region.Key == "window:zhuk").Region;
            Assert.Equal("ZHUK · IM RUCKSACK", DeskPageRules.Head(zhuk, view.Sheet.Page));
            Assert.StartsWith("AN DIR ", DeskPageRules.HeadLine(view.Sheet.Totals, 0, 0), StringComparison.Ordinal);
        }
        finally
        {
            UiLanguage.Set(UiLanguage.English);
        }
    }
}
