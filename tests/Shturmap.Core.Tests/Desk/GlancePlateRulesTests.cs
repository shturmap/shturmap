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

    // A head as it reads: its no-break spaces (it wraps only before a "·") as spaces.
    private static string Words(string head) => head.Replace('\u00A0', ' ');

    // A made-up LOOT region: a grid cols×rows with an item in the first cell of each row, under its own container's slot cell
    // where it has one (a body's part), searched or not.
    private static DeskRegionSheet Made(DeskRegionKind kind, int cols, int rows, DeskSearch search = DeskSearch.Searched, bool slot = true)
    {
        var cells = new List<DeskCell>();
        if (slot)
            cells.Add(new DeskCell(0, 0, 1, 1, ["bag"]) { Slot = "backpack" });
        for (var j = 0; j < rows && cols > 0; j++)
            cells.Add(new DeskCell(0, j, 1, 1, [$"item {j}"]));
        var key = kind switch { DeskRegionKind.LootBackpack => "loot:backpack", DeskRegionKind.LootPockets => "loot:pockets", _ => "loot" };
        var region = new DeskRegion(key, kind, cols, rows, cells) { Search = search, Title = "LOOT · DRAWER" };
        return new DeskRegionSheet(region, cells.Select(c => new DeskCellSheet(c, null, DeskKeepLevel.None, false)).ToList(), DeskPart.Loot, 0,
            search != DeskSearch.Searched);
    }

    private static DeskPage PageOf(params DeskRegionSheet[] regions) => new(At, regions.Select(r => r.Region).ToList());

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
        Assert.Equal("TACTICAL RIG", Words(GlancePlateRules.HeadLine(rig, page)));
        Assert.Equal("POCKETS · SEARCHING +", Words(GlancePlateRules.HeadLine(pockets, page)));
        Assert.Null(GlancePlateRules.State(backpack));
        Assert.Equal("BACKPACK", GlancePlateRules.HeadLine(backpack, page));
        Assert.Equal("BACKPACK · MORE BELOW +", Words(GlancePlateRules.HeadLine(backpack with { Cut = true }, page)));
        // Cut short by the plate (its room took its top rows): MORE BELOW, or the search's word where the part is being searched.
        Assert.Equal("BACKPACK · MORE BELOW +", Words(GlancePlateRules.HeadLine(backpack, page, cutShort: true)));
        Assert.Equal("POCKETS · SEARCHING +", Words(GlancePlateRules.HeadLine(pockets, page, cutShort: true)));
        // It wraps only before a "·": the words between them are joined by no-break spaces.
        Assert.Equal("POCKETS ·\u00A0SEARCHING\u00A0+", GlancePlateRules.HeadLine(pockets, page));
        Assert.Equal("TACTICAL\u00A0RIG", GlancePlateRules.HeadLine(rig, page));
        // The closed region's room: three quarters of a cell, never under 28 DIP; it counts in the region's height.
        Assert.Equal((30.0, 60.0), (GlancePlateRules.ClosedHeight(40), GlancePlateRules.ClosedHeight(80)));
        Assert.True(GlancePlateRules.RegionHeight(rig, page, 40, 400) > GlancePlateRules.RegionHeight(rig with { Search = DeskSearch.Searched }, page, 40, 400));
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
            Assert.Equal(0, chosen.CutShortCount);
            if (chosen.Cell < GlancePlateRules.LargestCell)
                Assert.False(GlancePlateRules.Arrange(body, page, chosen.Cell + 8, h).Fits(w, h), $"{w}×{h} at {chosen.Cell + 8}");
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

    // A region that fits no column whole even at 40 is drawn cut short (UI QA pass of 2026-10-19, F1: a looted body's
    // backpack, a tall grid three cells wide, was named in the ALSO line alone): its head says so, its grid's top rows as many
    // as the room takes, at least two. Whole wins where it fits: the largest size at which everything stands whole.
    [Fact]
    public void A_region_too_tall_for_the_room_is_drawn_cut_short()
    {
        // A 3×8 backpack being searched, under its slot cell, in a room 300 high: whole it takes 389 at 40.
        var backpack = Made(DeskRegionKind.LootBackpack, 3, 8, DeskSearch.Searching);
        var page = PageOf(backpack);
        Assert.True(GlancePlateRules.RegionHeight(backpack.Region, page, 40, 161) > 300);
        var laid = GlancePlateRules.Layout([backpack], page, 600, 300);
        Assert.Equal((40.0, 1, 0), (laid.Cell, laid.Shown.Count, laid.Cut.Count));
        Assert.True(laid.CutShort(0));
        Assert.Equal(1, laid.CutShortCount);
        Assert.InRange(laid.Rows[0], GlancePlateRules.LeastRows, 7);
        Assert.True(laid.Fits(600, 300), $"{laid.Width}×{laid.Height}");
        // As many rows as the room takes: one more wouldn't fit.
        var column = laid.ColumnWidths[0];
        Assert.True(GlancePlateRules.RegionHeight(backpack.Region, page, 40, column, rows: laid.Rows[0] + 1) > 300);
        // Its head: the search's word, which wins; a searched one cut short says MORE BELOW.
        Assert.Equal("BACKPACK · SEARCHING +", Words(GlancePlateRules.HeadLine(backpack.Region, page, laid.CutShort(0))));
        Assert.Equal("BACKPACK · MORE BELOW +", Words(GlancePlateRules.HeadLine(backpack.Region with { Search = DeskSearch.Searched }, page, cutShort: true)));

        // The body's shape of that picture: its pockets not searched (a closed region), the backpack beside them in a
        // column of its own, cut short; without the width for a second column, under them in the rest of the first.
        var pockets = Made(DeskRegionKind.LootPockets, 0, 0, DeskSearch.Unsearched, slot: false);
        var body = PageOf(pockets, backpack);
        var beside = GlancePlateRules.Layout([pockets, backpack], body, 600, 300);
        Assert.Equal([0, 1], beside.Columns);
        Assert.Empty(beside.Cut);
        Assert.Equal((false, true), (beside.CutShort(0), beside.CutShort(1)));
        Assert.True(beside.Fits(600, 300));
        var under = GlancePlateRules.Layout([pockets, backpack], body, 200, 300);
        Assert.Equal([0, 0], under.Columns);
        Assert.Empty(under.Cut);
        Assert.True(under.CutShort(1));
        Assert.True(under.Rows[1] < beside.Rows[1] && under.Rows[1] >= GlancePlateRules.LeastRows);
        Assert.True(under.Fits(200, 300), $"{under.Width}×{under.Height}");
    }

    // A drawer that fits stands whole, at the largest size it fits; nothing cut short.
    [Fact]
    public void A_region_that_fits_stands_whole()
    {
        var drawer = Made(DeskRegionKind.Loot, 4, 2, slot: false);
        var page = PageOf(drawer);
        var laid = GlancePlateRules.Layout([drawer], page, 600, 300);
        Assert.Equal((80.0, 0, 0), (laid.Cell, laid.CutShortCount, laid.Cut.Count));
        Assert.Equal([2], laid.Rows);
        Assert.Equal("DRAWER", GlancePlateRules.HeadLine(drawer.Region, page));
        // Whole at a smaller size wins over cut short at a larger one: the 3×8 backpack in a room it fits whole at 40.
        var backpack = Made(DeskRegionKind.LootBackpack, 3, 8);
        var tall = GlancePlateRules.Layout([backpack], PageOf(backpack), 600, 420);
        Assert.Equal(0, tall.CutShortCount);
        Assert.True(tall.Fits(600, 420));
    }

    // Where even its head, its slot and two rows don't fit, a region gives way to the ALSO line, and the regions after it.
    [Fact]
    public void A_region_without_room_for_two_rows_is_named_in_the_also_line()
    {
        var backpack = Made(DeskRegionKind.LootBackpack, 3, 8, DeskSearch.Searching);
        var page = PageOf(backpack);
        Assert.Null(GlancePlateRules.RowsIn(backpack.Region, page, 40, 161, 120));
        var laid = GlancePlateRules.Layout([backpack], page, 600, 120);
        Assert.Empty(laid.Shown);
        Assert.Equal(["loot:backpack"], laid.Cut.Select(r => r.Region.Key));
        Assert.True(laid.Height <= 120, $"{laid.Height}");
        // A region with no grid to cut (a closed one) stands whole or not at all.
        var pockets = Made(DeskRegionKind.LootPockets, 0, 0, DeskSearch.Unsearched, slot: false);
        Assert.Null(GlancePlateRules.RowsIn(pockets.Region, PageOf(pockets), 40, 161, 1000));
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
            Assert.Equal("NICHT DURCHSUCHT\u00A0+", GlancePlateRules.State(rig));
            Assert.Equal("RIG", GlancePlateRules.Name(rig, body.Sheet.Page));
            // The heads wider than a four-cell column at 40 DIP (the senior translator, 2026-10-19: they split their phrase):
            // they wrap only before the "·", and a region's height counts the second line.
            var page = body.Sheet.Page;
            var pockets = body.Sheet.Regions.Single(r => r.Region.Key == "loot:pockets").Region;
            var backpack = body.Sheet.Regions.Single(r => r.Region.Key == "loot:backpack").Region;
            var searching = GlancePlateRules.HeadLine(pockets, page);
            var below = GlancePlateRules.HeadLine(backpack, page, cutShort: true);
            Assert.Equal(("TASCHEN ·\u00A0WIRD\u00A0DURCHSUCHT\u00A0+", "RUCKSACK ·\u00A0MEHR\u00A0DARUNTER\u00A0+"), (searching, below));
            var px = DeskPageRules.HeadSize(40);
            Assert.Equal((2, 2), (GlancePlateRules.HeadLines(searching, px, 161), GlancePlateRules.HeadLines(below, px, 161)));
            Assert.Equal(1, GlancePlateRules.HeadLines(searching, px, 400));
            Assert.True(GlancePlateRules.RegionHeight(pockets, page, 40, 161) > GlancePlateRules.RegionHeight(pockets, page, 40, 400));
        }
        finally
        {
            UiLanguage.Set(UiLanguage.English);
        }
    }

    // The English heads at 40 DIP stand on one line in a four-cell column.
    [Fact]
    public void The_english_heads_stand_on_one_line_at_40()
    {
        var body = GlanceExample.Glance(2, 1, null, DateTimeOffset.UnixEpoch);
        var page = body.Sheet.Page;
        var px = DeskPageRules.HeadSize(40);
        var pockets = body.Sheet.Regions.Single(r => r.Region.Key == "loot:pockets").Region;
        var backpack = body.Sheet.Regions.Single(r => r.Region.Key == "loot:backpack").Region;
        Assert.Equal(1, GlancePlateRules.HeadLines(GlancePlateRules.HeadLine(pockets, page), px, 161));
        Assert.Equal(1, GlancePlateRules.HeadLines(GlancePlateRules.HeadLine(backpack, page, cutShort: true), px, 161));
    }
}
