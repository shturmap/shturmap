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

    // The cells scale with the desk's width (owner, 2026-10-20: "We should utilize the whole screen"): 40 to 80 DIP in steps
    // of 8, the largest at which the page's widest row fits. One column of ten cells and the margins: 40 at 900×560 (its
    // desk is 516 across), 48 from 517, 56 from 597, up to 80 from 837. The game's two columns where both fit at 56 or more:
    // the made-up desk's gear column holds POCKETS and SPECIAL SLOTS side by side (7 cells and a gap), so from a desk 1038
    // across at 56, 1174 at 64 (1600×900's desk, 1216, stands there), 1446 at 80 (a 2560 monitor's).
    [Fact]
    public void The_cell_and_the_columns_follow_the_desks_width()
    {
        var regions = View.Sheet.Regions.Select(r => r.Region).ToList();
        var stash = regions.FindIndex(r => r.Kind == DeskRegionKind.Stash);
        Assert.Equal((40, -1), DeskPageRules.Layout(regions, 516));
        Assert.Equal((48, -1), DeskPageRules.Layout(regions, 517));
        Assert.Equal((48, -1), DeskPageRules.Layout(regions, 596));
        Assert.Equal((56, -1), DeskPageRules.Layout(regions, 597));
        Assert.Equal((72, -1), DeskPageRules.Layout(regions, 836));
        Assert.Equal((80, -1), DeskPageRules.Layout(regions, 837));
        Assert.Equal((80, -1), DeskPageRules.Layout(regions, 1037));
        Assert.Equal((56, stash), DeskPageRules.Layout(regions, 1038));
        Assert.Equal((56, stash), DeskPageRules.Layout(regions, 1173));
        Assert.Equal((64, stash), DeskPageRules.Layout(regions, 1216));
        Assert.Equal((72, stash), DeskPageRules.Layout(regions, 1310));
        Assert.Equal((80, stash), DeskPageRules.Layout(regions, 1446));
        Assert.Equal((80, stash), DeskPageRules.Layout(regions, 2176));
        Assert.Equal(64, DeskPageRules.Cell(regions, 1216));
        // Without the special slots the gear's column is six cells: two columns from 958.
        var gear = regions.Where(r => r.Kind != DeskRegionKind.SpecialSlots).ToList();
        Assert.Equal(-1, DeskPageRules.ColumnBreak(gear, 957));
        Assert.Equal(DeskRegionKind.Stash, gear[DeskPageRules.ColumnBreak(gear, 958)].Kind);
        // The gear stands left of it: everything before the stash in reading order.
        var at = DeskPageRules.ColumnBreak(regions, 1038);
        Assert.DoesNotContain(regions.Take(at), r => r.Kind == DeskRegionKind.Stash);
        // Every size a whole number of pixels at every Windows scale from 100 to 200 % in quarters.
        Assert.All(DeskPageRules.Sizes, s => Assert.All(new[] { 1, 1.25, 1.5, 1.75, 2 }, f => Assert.Equal(Math.Round(s * f), s * f)));
        // A page without a stash stands in one column.
        Assert.Equal((80, -1), DeskPageRules.Layout(gear.Where(r => r.Kind != DeskRegionKind.Stash).ToList(), 2176));
    }

    // The marks and the heads grow with the cell: the figure 10.5 px at 40 to 15 at 80, the count 10 to 14.5, the source's
    // glyph 9 to 14, a trader's portrait 14 at 56 to 20 at 80, the notch and the "?" plate a DIP a step from 56; the
    // regions' heads 11, 12 and 13 px.
    [Fact]
    public void The_marks_and_the_heads_scale_with_the_cell()
    {
        Assert.Equal([10.5, 11, 12, 13, 14, 15], DeskPageRules.Sizes.Select(DeskCells.FigureSizeAt));
        Assert.Equal([10, 10.5, 11.5, 12.5, 13.5, 14.5], DeskPageRules.Sizes.Select(DeskCells.SmallSizeAt));
        Assert.Equal([9, 10, 11, 12, 13, 14], DeskPageRules.Sizes.Select(DeskCells.GlyphAt));
        Assert.Equal([14, 16, 18, 20], DeskPageRules.Sizes.Skip(2).Select(DeskCells.PortraitAt));
        Assert.Equal([11, 11, 11, 12, 13, 14], DeskPageRules.Sizes.Select(DeskCells.NotchAt));
        Assert.Equal([12, 12, 12, 13, 14, 15], DeskPageRules.Sizes.Select(DeskCells.PlateAt));
        Assert.Equal([11, 11, 12, 12, 13, 13], DeskPageRules.Sizes.Select(DeskPageRules.HeadSize));
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
