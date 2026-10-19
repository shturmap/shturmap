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

    // 40 DIP cells at 900×560 (a desk about 500 across), 56 from a desk 600 across; the game's two columns from 980, where
    // both fit: the made-up desk's gear column holds POCKETS and SPECIAL SLOTS side by side (417 DIP), so its columns
    // break from a desk 1038 across.
    [Fact]
    public void The_cell_and_the_columns_follow_the_desks_width()
    {
        Assert.Equal(40, DeskPageRules.Cell(500));
        Assert.Equal(40, DeskPageRules.Cell(599));
        Assert.Equal(56, DeskPageRules.Cell(600));
        var regions = View.Sheet.Regions.Select(r => r.Region).ToList();
        Assert.Equal(-1, DeskPageRules.ColumnBreak(regions, 979));
        var gear = regions.Where(r => r.Kind != DeskRegionKind.SpecialSlots).ToList();
        Assert.Equal(DeskRegionKind.Stash, gear[DeskPageRules.ColumnBreak(gear, 980)].Kind);
        Assert.Equal(-1, DeskPageRules.ColumnBreak(regions, 1037));
        var at = DeskPageRules.ColumnBreak(regions, 1038);
        Assert.Equal(DeskRegionKind.Stash, regions[at].Kind);
        // The gear stands left of it: everything before the stash in reading order.
        Assert.DoesNotContain(regions.Take(at), r => r.Kind == DeskRegionKind.Stash);
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
        // The desk of a window 1600 wide (1216 DIP beside the rail): two columns, the gear's six cells and the stash's ten.
        Assert.Equal(2, DeskPageRules.ColumnBreak([read, rig, stash], 1216));
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
        Assert.Equal(-1, DeskPageRules.ColumnBreak([wide, stash], 1216));
        Assert.Equal(1, DeskPageRules.ColumnBreak([wide, stash], 1300));
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
