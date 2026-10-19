using Shturmap.App.Rules;
using Shturmap.Core.Desk;

namespace Shturmap.Core.Tests.Desk;

// The in-raid glance's plate (owner, 2026-10-19: from its panel, Variant 1, with the looted container's or body's layout
// mirrored; docs/DESIGN.md §4, *The in-raid glance*; Rules.GlancePlateRules), on the made-up readings (Rules.GlanceExample):
// 1, a drawer of three items; 2, a body with its slots, an UNSEARCHED rig, pockets being searched and a searched backpack.
public class GlancePlateRulesTests
{
    private static readonly DateTimeOffset At = new(2026, 1, 1, 14, 20, 0, TimeSpan.Zero);

    private static RaidGlance Drawer => GlanceExample.Glance(1, 1, null, At);

    private static RaidGlance Body => GlanceExample.Glance(2, 2, null, At);

    // The player's inventory alone: the made-up gear without a LOOT pane.
    private static RaidGlance OwnGear()
    {
        var page = GlanceExample.Page(2);
        page = page with { Regions = page.Regions.Where(r => !r.IsLoot).ToList() };
        var sheet = DeskSheet.Of(page, id => DeskExample.Facts.GetValueOrDefault(id), DeskFleaRules.Default, DeskExample.Keep);
        return new RaidGlance(sheet, null, At, 3);
    }

    private static DeskRegionSheet Region(RaidGlance glance, string key) => glance.Sheet.Regions.Single(r => r.Region.Key == key);

    // The loot is drawn, never the player's own gear (it is known; the glance is about the loot): the LOOT pane's regions in
    // the game's order, a body's slots first. Without a LOOT pane, nothing.
    [Fact]
    public void The_plate_draws_the_loot_and_never_the_players_own_regions()
    {
        Assert.Equal(["loot:slots", "loot:rig", "loot:pockets", "loot:backpack"], GlancePlateRules.Drawn(Body).Select(r => r.Region.Key));
        Assert.Equal(["loot"], GlancePlateRules.Drawn(Drawer).Select(r => r.Region.Key));
        Assert.Contains(Body.Sheet.Regions, r => r.Part == DeskPart.OnYou);
        Assert.All(GlancePlateRules.Drawn(Body), r => Assert.Equal(DeskPart.Loot, r.Part));
        Assert.Empty(GlancePlateRules.Drawn(OwnGear()));
        Assert.Equal("LOOT · BODY", Body.Loot);
    }

    // Variant 1's head: FROM YOUR SCREENSHOT over the LOOT total, ON YOU and the "?" cells under it; a total with contents
    // unseen ends in "+" (the drawer's wallet; the body's rig not searched). Without a LOOT pane, ON YOU is the title and the
    // line says nothing was looted on the screen.
    [Fact]
    public void The_head_says_the_loot_and_what_is_on_you()
    {
        var drawer = GlancePlateRules.HeadOf(Drawer);
        var totals = Drawer.Sheet.Totals;
        Assert.Equal(("FROM YOUR SCREENSHOT", "LOOT"), (drawer.Eyebrow, drawer.Word));
        Assert.Equal(DeskNumbers.Total(totals.Loot, true), drawer.Total);
        Assert.EndsWith("+ ₽", drawer.Total, StringComparison.Ordinal);
        Assert.Equal("ON YOU " + DeskNumbers.Total(totals.OnYou, totals.OnYouPlus), drawer.Line);
        Assert.Equal("1 unsure", drawer.Unsure);
        // The GPU, the wallet and the keycard at its least: what each would bring.
        Assert.Equal(Region(Drawer, "loot").Total, totals.Loot);

        var body = GlancePlateRules.HeadOf(Body);
        Assert.True(Body.Sheet.Totals.LootPlus);
        Assert.Equal("2 unsure", body.Unsure);

        var own = OwnGear();
        var alone = GlancePlateRules.HeadOf(own);
        Assert.Equal(("ON YOU", "Your gear · nothing looted on this screen", (string?)null), (alone.Word, alone.Line, alone.Unsure));
        Assert.Equal(DeskNumbers.Total(own.Sheet.Totals.OnYou, own.Sheet.Totals.OnYouPlus), alone.Total);
    }

    // Each region under the head the game's pane gives it: a body's slots under BODY, its parts under the desk's heads, a
    // container's grid under its kind; a part whose contents went unseen says so with "+": not searched (in its closed
    // region, it has no grid), being searched or cut by the pane's scrolling (after its head, its grid drawn).
    [Fact]
    public void Each_region_is_headed_as_the_game_shows_it()
    {
        var page = Body.Sheet.Page;
        Assert.Equal(["BODY", "TACTICAL RIG", "POCKETS", "BACKPACK"], GlancePlateRules.Drawn(Body).Select(r => GlancePlateRules.Name(r.Region, page)));
        Assert.Equal("DRAWER", GlancePlateRules.Name(Region(Drawer, "loot").Region, Drawer.Sheet.Page));
        Assert.Equal("LOOT", GlancePlateRules.Name(Region(Drawer, "loot").Region with { Title = "LOOT" }, Drawer.Sheet.Page));

        var rig = Region(Body, "loot:rig").Region;
        var pockets = Region(Body, "loot:pockets").Region;
        var backpack = Region(Body, "loot:backpack").Region;
        Assert.Equal("UNSEARCHED +", GlancePlateRules.State(rig));
        Assert.False(GlancePlateRules.HasGrid(rig));
        Assert.Equal("TACTICAL RIG", GlancePlateRules.HeadLine(rig, page));
        Assert.Equal("POCKETS · SEARCHING +", GlancePlateRules.HeadLine(pockets, page));
        Assert.Null(GlancePlateRules.State(backpack));
        Assert.Equal("BACKPACK", GlancePlateRules.HeadLine(backpack, page));
        Assert.Equal("BACKPACK · MORE BELOW +", GlancePlateRules.HeadLine(backpack with { Cut = true }, page));
        // The closed region's room: three quarters of a cell, never under 28 DIP; it counts in the region's height.
        Assert.Equal((30.0, 60.0), (GlancePlateRules.ClosedHeight(40), GlancePlateRules.ClosedHeight(80)));
        Assert.True(GlancePlateRules.RegionHeight(rig, 40, 400) > GlancePlateRules.RegionHeight(rig with { Search = DeskSearch.Searched }, 40, 400));
    }

    // The "?" cells counted: a cell that fits several items and wasn't picked (the body's helmet and armband); a pick makes
    // it one.
    [Fact]
    public void The_unsure_cells_are_the_ones_that_fit_several()
    {
        var drawn = GlancePlateRules.Drawn(Body);
        Assert.Equal(2, GlancePlateRules.Unsure(drawn));
        var picked = drawn.Select(r => r with
        {
            Cells = r.Cells.Select(c => c.Cell.Slot == "headwear" ? c with { Cell = c.Cell with { Pick = c.Cell.Ids[0] } } : c).ToList(),
        });
        Assert.Equal(1, GlancePlateRules.Unsure(picked));
    }

    // The cells take the largest of the desk's sizes, 80 at the most, at which the regions fit the plate's room, flowed into
    // columns where one is too short; where even 40 doesn't fit, the last regions give way to one line, the first stand.
    [Fact]
    public void The_cells_take_the_largest_size_that_fits_and_the_rest_gives_way()
    {
        var body = GlancePlateRules.Drawn(Body);
        var page = Body.Sheet.Page;
        // A 2560 monitor's map column: 80, never more, nothing cut.
        var wide = GlancePlateRules.Layout(body, page, 2000, 560);
        Assert.Equal((80.0, 0), (wide.Cell, wide.Cut.Count));
        Assert.True(wide.Fits(2000, 560));
        Assert.Equal(80, GlancePlateRules.Layout(GlancePlateRules.Drawn(Drawer), Drawer.Sheet.Page, 2000, 2000).Cell);
        // Each layout chosen fits its room, and the next size up doesn't; the regions stand in reading order, column by
        // column.
        foreach (var (w, h) in new[] { (1150.0, 300.0), (1150, 420), (700, 420), (2000, 560) })
        {
            var chosen = GlancePlateRules.Layout(body, page, w, h);
            Assert.True(chosen.Fits(w, h), $"{w}×{h} at {chosen.Cell}");
            Assert.Empty(chosen.Cut);
            if (chosen.Cell < GlancePlateRules.LargestCell)
                Assert.False(GlancePlateRules.Arrange(body, chosen.Cell + 8, h).Fits(w, h), $"{w}×{h} at {chosen.Cell + 8}");
            Assert.Equal(chosen.Columns.Order(), chosen.Columns);
        }
        // 1600×900's room (about 1150 by 300 under the head): in columns.
        Assert.True(GlancePlateRules.Layout(body, page, 1150, 300).ColumnWidths.Count > 1);
        // 900×560's (about 450 by 130): not even the body's slots fit with the line under them; everything is named in it.
        var small = GlancePlateRules.Layout(body, page, 452, 129);
        Assert.Equal((40.0, 0, 4), (small.Cell, small.Shown.Count, small.Cut.Count));
        // A little more room: the first regions stand, the rest give way, all within the room.
        var some = GlancePlateRules.Layout(body, page, 452, 260);
        Assert.InRange(some.Shown.Count, 1, 3);
        Assert.Equal(body.Take(some.Shown.Count).Select(r => r.Region.Key), some.Shown.Select(r => r.Region.Key));
        Assert.Equal(body.Skip(some.Shown.Count).Select(r => r.Region.Key), some.Cut.Select(r => r.Region.Key));
        Assert.True(some.Height <= 260, $"{some.Height}");
        // Nothing to draw takes no room.
        Assert.Equal(0, GlancePlateRules.Layout([], page, 452, 129).Height);
    }

    // The cut line in the fold line's words: ALSO, then each region that gave way with what it holds.
    [Fact]
    public void The_cut_line_names_what_had_no_room()
    {
        var body = GlancePlateRules.Drawn(Body);
        var (word, names) = GlancePlateRules.CutLine(body.Skip(2), Body.Sheet.Page);
        var backpack = body[3];
        Assert.Equal("ALSO", word);
        Assert.Equal(["POCKETS " + DeskNumbers.Total(body[2].Total, true), "BACKPACK " + DeskNumbers.Total(backpack.Total, backpack.Plus)], names);
        Assert.False(backpack.Plus);
    }

    // The plate stands about 6 s and fades over 0.4 s (the panel's B); its cells never above 80 DIP, one of the desk's sizes.
    [Fact]
    public void The_plate_stands_six_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(6), GlancePlateRules.Stands);
        Assert.Equal(TimeSpan.FromSeconds(0.4), GlancePlateRules.FadesOut);
        Assert.Contains(GlancePlateRules.LargestCell, DeskPageRules.Sizes);
    }
}

[Collection("UiLanguage")]
public class GlancePlateRulesInGermanTests
{
    [Fact]
    public void The_head_and_the_states_in_german()
    {
        try
        {
            UiLanguage.Set(UiLanguage.German);
            var body = GlanceExample.Glance(2, 1, null, DateTimeOffset.UnixEpoch);
            var head = GlancePlateRules.HeadOf(body);
            Assert.Equal(("AUS DEINEM SCREENSHOT", "LOOT", "2 unsicher"), (head.Eyebrow, head.Word, head.Unsure));
            Assert.StartsWith("AN DIR ", head.Line, StringComparison.Ordinal);
            var rig = body.Sheet.Regions.Single(r => r.Region.Key == "loot:rig").Region;
            Assert.Equal("NICHT DURCHSUCHT +", GlancePlateRules.State(rig));
            Assert.Equal("RIG", GlancePlateRules.Name(rig, body.Sheet.Page));
        }
        finally
        {
            UiLanguage.Set(UiLanguage.English);
        }
    }
}
