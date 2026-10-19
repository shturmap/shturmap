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

    // 40 DIP cells at 900×560 (a desk about 500 across), 56 from a desk 600 across; the game's two columns from 980.
    [Fact]
    public void The_cell_and_the_columns_follow_the_desks_width()
    {
        Assert.Equal(40, DeskPageRules.Cell(500));
        Assert.Equal(40, DeskPageRules.Cell(599));
        Assert.Equal(56, DeskPageRules.Cell(600));
        var regions = View.Sheet.Regions.Select(r => r.Region).ToList();
        Assert.Equal(-1, DeskPageRules.ColumnBreak(regions, 979));
        var at = DeskPageRules.ColumnBreak(regions, 980);
        Assert.Equal(DeskRegionKind.Stash, regions[at].Kind);
        // The gear stands left of it: everything before the stash in reading order.
        Assert.DoesNotContain(regions.Take(at), r => r.Kind == DeskRegionKind.Stash);
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
        Assert.Equal("ON YOU 1.01 M+ ₽ · STASH 6.84 M ₽ · KEEP 3 · 2 · 4", DeskPageRules.HeadLine(totals, totals.OnYou, totals.Stash));
        // Counting up: the totals given apart, the "+" and the keeps as they end.
        Assert.Equal("ON YOU 505k+ ₽ · STASH 3.42 M ₽ · KEEP 3 · 2 · 4", DeskPageRules.HeadLine(totals, 505_000, 3_420_000));
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
