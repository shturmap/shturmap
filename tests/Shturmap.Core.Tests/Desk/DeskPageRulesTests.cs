using Shturmap.App.Rules;
using Shturmap.Core.Desk;
using Shturmap.Core.Logs;

namespace Shturmap.Core.Tests.Desk;

// The desk page's layout and words (owner, 2026-10-25, decision 1 of the layout round: A, the GEAR tab mirrored and fixed;
// docs/DESIGN.md §4, *Its place*, *What it mirrors*, *The head line*; Rules.DeskPageRules), on the made-up desk
// (Rules.DeskExample) and the owner's page of the evening of 2026-10-19.
public class DeskPageRulesTests
{
    private static readonly DeskView View = DeskExample.View(1, GameMode.Pve, 1, null, DateTimeOffset.UnixEpoch);

    private static DeskRegionSheet Region(string key) => View.Sheet.Regions.Single(r => r.Region.Key == key);

    // The made-up desk's page after reading n, in reading order, as the view hands it to the layout.
    private static List<DeskRegion> Made(int reading = 1) =>
        DeskExample.View(reading, GameMode.Pve, reading, null, DateTimeOffset.UnixEpoch).Sheet.Regions.Select(r => r.Region).ToList();

    // The owner's page in hand after the evening of 2026-10-19 (docs/NEXT.md, the layout round's step 1): LOADOUT, the rig
    // five by four, the pockets four by two, the special slots three by two, the backpack five by six, the pouch three by
    // three, the stash ten by thirteen and the Keys window eleven by seven, attached to the stash.
    private static List<DeskRegion> OwnersPage() =>
    [
        new("loadout", DeskRegionKind.Loadout, 0, 0, Enumerable.Range(0, 7).Select(i => new DeskCell(0, 0, 0, 0, ["x"]) { Slot = DeskSlots.Loadout[i].Key }).ToList()),
        new("rig", DeskRegionKind.Rig, 5, 4, [new DeskCell(0, 0, 0, 0, ["x"]) { Slot = "rig" }]),
        new("pockets", DeskRegionKind.Pockets, 4, 2, []),
        new("special", DeskRegionKind.SpecialSlots, 3, 2, []),
        new("backpack", DeskRegionKind.Backpack, 5, 6, [new DeskCell(0, 0, 0, 0, ["x"]) { Slot = "backpack" }]),
        new("pouch", DeskRegionKind.Pouch, 3, 3, [new DeskCell(0, 0, 0, 0, ["x"]) { Slot = "pouch" }]),
        new("stash", DeskRegionKind.Stash, 10, 13, [new DeskCell(7, 9, 3, 2, ["box"])]),
        new("window:keys", DeskRegionKind.ContainerWindow, 11, 7, []) { Title = "Keys", AttachedTo = new DeskAnchor("stash", 7, 9) },
    ];

    // The desks the windows give (DIP beside the rail, under the head line): 900×560's, 1600×900's, a 2560 monitor's work
    // area's (the owner's), and a larger one.
    private static readonly (double W, double H)[] Desks = [(516, 440), (1216, 760), (2176, 1330), (2544, 1401)];

    // The cell size follows the desk's size alone (owner, 2026-10-25): the largest at which the three columns fit the desk's
    // width and the stash's column its height, the windows beside the stash where the width allows, else under it. At
    // 900×560's desk, 40 in one column, scrolling; at 1600×900's, 40 in three columns, the windows under the stash; at a 2560
    // monitor's, the owner's page at 56 with the Keys window in a fourth column (64 would take it under the stash, and the
    // stash's column past the desk's height), the made-up desk at 64, its windows beside the stash. Until 2026-10-25 the
    // page flowed and folded: 96 for the owner's page folded to the stash at a 2560 monitor's desk, 48 at 1600×900's.
    [Fact]
    public void The_cell_size_follows_the_desk_alone()
    {
        var small = DeskPageRules.Layout(Made(), 516, 440);
        Assert.Equal((40, true, 1), (small.Cell, small.OneColumn, small.ColumnWidths.Count));
        Assert.True(small.Height > 440);

        // 1600×900: three columns at 40, the Zhuk's and the WZ's windows under the stash; the stash's column with them is
        // taller than the desk, so the page scrolls; LOADOUT and the middle column fit.
        var mid = DeskPageRules.Layout(Made(), 1216, 760);
        Assert.Equal((40, false, 3, false), (mid.Cell, mid.OneColumn, mid.ColumnWidths.Count, mid.WindowsBeside));
        Assert.Equal(2, mid.ColumnOf("window:zhuk"));
        Assert.True(mid.Width + 2 * DeskPageRules.Margin <= 1216);
        Assert.True(mid.Held > 760);
        Assert.True(DeskPageRules.Lane + mid.ColumnHeights[0] + DeskPageRules.PageBottom <= 760);
        Assert.True(DeskPageRules.Lane + mid.ColumnHeights[1] + DeskPageRules.PageBottom <= 760);

        var owners = DeskPageRules.Layout(OwnersPage(), 2176, 1330);
        Assert.Equal((56, true, 3), (owners.Cell, owners.WindowsBeside, owners.ColumnOf("window:keys")));
        Assert.True(owners.Fits(2176, 1330));
        Assert.Equal((40, false), (DeskPageRules.Layout(OwnersPage(), 1216, 760).Cell, DeskPageRules.Layout(OwnersPage(), 1216, 760).WindowsBeside));

        var large = DeskPageRules.Layout(Made(), 2176, 1330);
        Assert.Equal((64, true, 3), (large.Cell, large.WindowsBeside, large.ColumnOf("window:zhuk")));
        Assert.True(large.Height <= 1330, "the made-up desk at a 2560 monitor's desk doesn't scroll");
        Assert.Equal(72, DeskPageRules.Layout(Made(), 2544, 1401).Cell);

        // Each size chosen fits, and the next size up doesn't (where three columns fit at all).
        foreach (var page in new[] { Made(), OwnersPage() })
        {
            foreach (var (w, h) in Desks.Skip(2))
            {
                var chosen = DeskPageRules.Layout(page, w, h);
                Assert.True(chosen.Fits(w, h), $"{w}×{h} at {chosen.Cell}");
                Assert.False(DeskPageRules.Arrange(page, chosen.Cell + 8, w).Fits(w, h), $"{w}×{h} at {chosen.Cell + 8}");
            }
        }
        // Up to 128, twice the art's own 64 px: a page of the stash alone on a desk wider than the three columns at 128.
        Assert.Equal(128, DeskPageRules.Cell([new DeskRegion("stash", DeskRegionKind.Stash, 10, 3, [])], 4000, 2000));
        // Every size a whole number of pixels at every Windows scale from 100 to 200 % in quarters, and so the gaps.
        Assert.Equal([40, 48, 56, 64, 72, 80, 88, 96, 104, 112, 120, 128], DeskPageRules.Sizes);
        Assert.All(DeskPageRules.Sizes, s => Assert.All(new[] { 1, 1.25, 1.5, 1.75, 2 }, f =>
        {
            Assert.Equal(Math.Round(s * f), s * f);
            Assert.Equal(Math.Round(DeskPageRules.BoxGap(s) * f), DeskPageRules.BoxGap(s) * f);
        }));
    }

    // The size and the shape never follow what was read (owner, 2026-10-25): the made-up desk's three readings (the first;
    // one that changed the backpack, the rest kept and dated; one that changed nothing) take one size and one shape at each
    // desk, and so does the page with its cells taken out. The size changes only with the desk's size or a region's own grid
    // (a larger backpack).
    [Fact]
    public void The_size_never_follows_what_was_read()
    {
        static List<DeskRegion> Emptied(List<DeskRegion> page) => page.Select(r => r with { Cells = [] }).ToList();
        foreach (var (w, h) in Desks)
        {
            var first = DeskPageRules.Layout(Made(1), w, h);
            Assert.Equal(first.Shape, DeskPageRules.Layout(Made(2), w, h).Shape);
            Assert.Equal(first.Shape, DeskPageRules.Layout(Made(3), w, h).Shape);
            Assert.Equal(first.Shape, DeskPageRules.Layout(Emptied(Made(2)), w, h).Shape);
        }
        // A backpack of eight by eight: the middle column two cells wider, so at a 2560 monitor's desk the windows stand under
        // the stash; a stash page of twenty rows read: the size smaller.
        var larger = Made().Select(r => r.Kind == DeskRegionKind.Backpack ? r with { Cols = 8, Rows = 8 } : r).ToList();
        Assert.True(DeskPageRules.Layout(Made(), 2176, 1330).WindowsBeside);
        Assert.False(DeskPageRules.Layout(larger, 2176, 1330).WindowsBeside);
        var longer = Made().Select(r => r.Kind == DeskRegionKind.Stash ? r with { Rows = 20 } : r).ToList();
        Assert.True(DeskPageRules.Layout(longer, 2176, 1330).Cell < DeskPageRules.Layout(Made(), 2176, 1330).Cell);
    }

    // Every region stands in its place whether or not the last screenshot showed it: the seven fixed regions in the game's
    // order, one no reading showed a placeholder (the made-up desk's POUCH), then the windows in reading order. A page of the
    // stash alone still has LOADOUT and the middle column's places, and its three columns.
    [Fact]
    public void Every_region_stands_in_its_place_read_or_not()
    {
        var blocks = DeskPageRules.Blocks(Made());
        Assert.Equal(["loadout", "rig", "pockets", "special", "backpack", "pouch", "stash", "window:zhuk", "window:wz"], blocks.Select(b => b.Key));
        Assert.Equal(["pouch"], blocks.Where(b => b.Placeholder).Select(b => b.Key));
        Assert.All(DeskPageRules.Blocks([]), b => Assert.True(b.Placeholder));
        Assert.Equal(7, DeskPageRules.Blocks([]).Count);

        var stashAlone = DeskPageRules.Layout([new DeskRegion("stash", DeskRegionKind.Stash, 10, 13, [])], 1216, 760);
        Assert.Equal((3, 0, 1, 2), (stashAlone.ColumnWidths.Count, stashAlone.ColumnOf("loadout"), stashAlone.ColumnOf("backpack"), stashAlone.ColumnOf("stash")));
        // One column: the same blocks in the same order.
        var one = DeskPageRules.Arrange(Made(), 40, oneColumn: true);
        Assert.Equal(blocks.Select(b => b.Key), one.Blocks.Select(b => b.Key));
        Assert.All(one.Columns, c => Assert.Equal(0, c));
    }

    // A placeholder counts like a region read (owner, 2026-10-25): a grid region's dashed frame five cells by two (POCKETS
    // four, SPECIAL SLOTS three, as wide as the game sets theirs), a container's empty slot box beside it; a region the last
    // screenshot didn't show counts as read.
    [Fact]
    public void A_placeholder_and_a_kept_region_count_like_a_region_read()
    {
        var pouch = DeskPageRules.Blocks(Made()).Single(b => b.Kind == DeskRegionKind.Pouch);
        Assert.True(pouch.Placeholder);
        Assert.Equal(2 * 40 + 8 + 5 * 40 + 1, DeskPageRules.BlockWidth(pouch, 40));
        Assert.Equal(81, DeskPageRules.ContentHeight(pouch, 40));
        var read = new DeskBlock("pouch", DeskRegionKind.Pouch, new DeskRegion("pouch", DeskRegionKind.Pouch, 5, 2, []));
        Assert.Equal(DeskPageRules.BlockWidth(read, 40), DeskPageRules.BlockWidth(pouch, 40));
        Assert.Equal(DeskPageRules.BlockHeight(read, 40, 400), DeskPageRules.BlockHeight(pouch, 40, 400));
        var special = new DeskBlock("special", DeskRegionKind.SpecialSlots, null);
        Assert.Equal((3 * 56 + 1, 2 * 56 + 1), (DeskPageRules.BlockWidth(special, 56), DeskPageRules.ContentHeight(special, 56)));
        Assert.Equal(4 * 56 + 1, DeskPageRules.BlockWidth(new DeskBlock("pockets", DeskRegionKind.Pockets, null), 56));
        // The stash never read: its column still ten cells wide.
        Assert.Equal(10 * 40 + 1, DeskPageRules.BlockWidth(new DeskBlock("stash", DeskRegionKind.Stash, null), 40));

        // Reading 2 kept every region but the backpack from reading 1: each the same block as read.
        var kept = DeskExample.Page(2);
        Assert.Equal(["loadout", "pockets", "rig", "special", "stash", "window:wz", "window:zhuk"],
            kept.Regions.Where(r => DeskPageRules.Kept(r, kept)).Select(r => r.Key).Order(StringComparer.Ordinal));
        Assert.Empty(DeskExample.Page(1).Regions.Where(r => DeskPageRules.Kept(r, DeskExample.Page(1))));
    }

    // A region a reading made from its heading alone, no grid and no cell (code review of 2026-10-25: a POCKETS, SPECIAL
    // SLOTS or STASH of 0×0, as the layout gives one on a first reading when the region's frames are all under a window,
    // was drawn as a bare head, POCKETS and SPECIAL SLOTS a DIP wide): it stands as a region never read, the placeholder,
    // and the page takes the shape it takes with those regions never read; it counts nothing. A container's 0×0 region is
    // its slot seen empty, its grid gone with it (docs/DESIGN.md §4): read, its empty slot box with no grid.
    [Fact]
    public void A_region_read_from_its_heading_alone_stands_as_never_read()
    {
        static bool Bare(DeskRegionKind kind) => kind is DeskRegionKind.Pockets or DeskRegionKind.SpecialSlots or DeskRegionKind.Stash;
        foreach (var page in new[] { OwnersPage(), Made() })
        {
            var headed = page.Select(r => Bare(r.Kind) ? r with { Cols = 0, Rows = 0, Cells = [], Parts = [] } : r).ToList();
            var never = page.Where(r => !Bare(r.Kind)).ToList();
            var blocks = DeskPageRules.Blocks(headed);
            Assert.Equal(DeskPageRules.Blocks(never).Select(b => (b.Key, b.Placeholder)), blocks.Select(b => (b.Key, b.Placeholder)));
            Assert.All(blocks.Where(b => Bare(b.Kind)), b => Assert.True(b.Placeholder, b.Key));
            // Counted as the placeholder: POCKETS four cells wide, SPECIAL SLOTS three, the stash ten; the page's shape and
            // its columns' heights as with those regions never read, at every desk.
            Assert.Equal((4 * 40 + 1, 3 * 40 + 1, 10 * 40 + 1),
                (DeskPageRules.BlockWidth(blocks[2], 40), DeskPageRules.BlockWidth(blocks[3], 40), DeskPageRules.BlockWidth(blocks[6], 40)));
            foreach (var (w, h) in Desks)
            {
                var drawn = DeskPageRules.Layout(headed, w, h);
                var placeheld = DeskPageRules.Layout(never, w, h);
                Assert.Equal(placeheld.Shape, drawn.Shape);
                Assert.Equal(placeheld.ColumnHeights, drawn.ColumnHeights);
            }
        }
        // Only where it has neither grid nor cell: a POCKETS read empty, or one with a cell and no grid, stays read.
        Assert.False(DeskPageRules.HeadingAlone(new DeskRegion("pockets", DeskRegionKind.Pockets, 4, 2, [])));
        Assert.False(DeskPageRules.HeadingAlone(new DeskRegion("pockets", DeskRegionKind.Pockets, 0, 0, [new DeskCell(0, 0, 1, 1, ["x"])])));
        // A backpack whose slot was seen empty: read, its empty slot box beside no grid; LOADOUT, which has no grid, read too.
        var emptied = OwnersPage().Select(r => r.Kind == DeskRegionKind.Backpack ? r with { Cols = 0, Rows = 0, Cells = [] } : r).ToList();
        Assert.False(DeskPageRules.Blocks(emptied).Single(b => b.Kind == DeskRegionKind.Backpack).Placeholder);
        Assert.False(DeskPageRules.Blocks([new DeskRegion("loadout", DeskRegionKind.Loadout, 0, 0, [])]).Single(b => b.Kind == DeskRegionKind.Loadout).Placeholder);

        // It counts nothing: the made-up desk with its POCKETS and SPECIAL SLOTS made from their headings alone counts ON YOU
        // what it counts with them read, less theirs, and the stash as before.
        var facts = DeskExample.View(1, GameMode.Pve, 1, null, DateTimeOffset.UnixEpoch).FactsOf;
        static bool Paired(DeskRegionKind kind) => kind is DeskRegionKind.Pockets or DeskRegionKind.SpecialSlots;
        var made = DeskExample.Page(1);
        var full = DeskSheet.Of(made, facts, DeskFleaRules.Default);
        var bare = DeskSheet.Of(made with { Regions = made.Regions.Select(r => Paired(r.Kind) ? r with { Cols = 0, Rows = 0, Cells = [], Parts = [] } : r).ToList() },
            facts, DeskFleaRules.Default);
        var theirs = full.Regions.Where(r => Paired(r.Region.Kind)).Sum(r => r.Total);
        Assert.True(theirs > 0);
        Assert.All(bare.Regions.Where(r => Paired(r.Region.Kind)), r => Assert.Equal((0, 0.0), (r.Cells.Count, r.Total)));
        Assert.Equal(full.Totals.OnYou - theirs, bare.Totals.OnYou, 3);
        Assert.Equal(full.Totals.Stash, bare.Totals.Stash);
    }

    // The windows stand in a fourth column right of the stash where the width allows, else under the stash; the three
    // fixed columns never move for a window.
    [Fact]
    public void The_windows_stand_beside_the_stash_where_the_width_allows_else_under_it()
    {
        var page = OwnersPage();
        var beside = DeskPageRules.Arrange(page, 56, 2176);
        Assert.True(beside.WindowsBeside);
        Assert.Equal([0, 1, 1, 1, 1, 1, 2, 3], beside.Columns);
        var under = DeskPageRules.Arrange(page, 56, 2000);
        Assert.False(under.WindowsBeside);
        Assert.Equal([0, 1, 1, 1, 1, 1, 2, 2], under.Columns);
        // The fixed columns as wide either way; the stash's as wide as the window under it.
        Assert.Equal(beside.ColumnWidths.Take(2), under.ColumnWidths.Take(2));
        Assert.Equal(11 * 56 + 1, under.ColumnWidths[2]);
        Assert.Equal(10 * 56 + 1, beside.ColumnWidths[2]);
        // Without a window, three columns and no fourth.
        Assert.Equal(3, DeskPageRules.Arrange(page.Where(r => r.Kind != DeskRegionKind.ContainerWindow).ToList(), 56, 4000).ColumnWidths.Count);
    }

    // LOADOUT as the game's slot grid (owner, 2026-10-25): the boxes two cells square in three columns a cell apart, in the
    // game's rows (EARPIECE · HEADWEAR · FACE COVER; ARMBAND over DOGTAG · BODY ARMOR · EYEWEAR; ON SLING · HOLSTER; ON BACK
    // · SHEATH), the weapons' boxes five cells wide, the armband's and the dog tag's two short boxes in one box's room, each
    // box under its name; eight cells wide.
    [Fact]
    public void The_loadout_is_the_games_slot_grid()
    {
        var boxes = DeskPageRules.LoadoutBoxes(40);
        Assert.Equal(DeskSlots.Loadout.Select(s => s.Key).Order(), boxes.Select(b => b.Slot).Order());
        var at = boxes.ToDictionary(b => b.Slot);
        var room = DeskPageRules.SlotHeadRoom(40);
        Assert.Equal(15, room);
        Assert.Equal((0, 120, 240), (at["earpiece"].X, at["headwear"].X, at["facecover"].X));
        Assert.All(["earpiece", "headwear", "facecover", "bodyarmor", "eyewear", "holster", "sheath"], s => Assert.Equal((80.0, 80.0), (at[s].Width, at[s].Height)));
        Assert.All(["onsling", "onback"], s => Assert.Equal((200.0, 80.0, 0.0), (at[s].Width, at[s].Height, at[s].X)));
        // The rows: each a name and a box, 8 DIP apart.
        Assert.Equal([15, 118, 221, 324], new[] { "headwear", "bodyarmor", "onsling", "onback" }.Select(s => at[s].Y));
        Assert.Equal((at["onsling"].Y, 240), (at["holster"].Y, at["holster"].X));
        // The armband over the dog tag, both within the body armour's band, each under its name.
        Assert.Equal(at["bodyarmor"].Y, at["armband"].Y);
        Assert.Equal(at["armband"].Y + at["armband"].Height + DeskPageRules.ShortGap + room, at["dogtag"].Y);
        Assert.True(at["dogtag"].Y + at["dogtag"].Height <= at["bodyarmor"].Y + at["bodyarmor"].Height);
        // Every box within the grid: eight cells wide, four rows high.
        Assert.All(boxes, b => Assert.True(b.X + b.Width <= DeskPageRules.LoadoutCells * 40 && b.Y + b.Height <= DeskPageRules.LoadoutHeight(40)));
        Assert.Equal(4 * (15 + 80) + 3 * 8, DeskPageRules.LoadoutHeight(40));
        // The window's text size grows the names' room, and the grid with it.
        Assert.True(DeskPageRules.LoadoutHeight(40, 1.5) > DeskPageRules.LoadoutHeight(40));
    }

    // A slot's name over its box fits the box's width: letter-spaced where it fits, without the spacing, then as large as
    // the box takes it, never drawn under 8 px (the German FACE COVER over a box of 80 DIP). The fit is made at the size the
    // text is drawn, so a larger text size grows a long name as far as its box allows (UI QA pass of step 2, F5: in half
    // pixels as written it stopped short of the box).
    [Fact]
    public void A_slots_name_fits_its_box()
    {
        const string facecover = "GESICHTSBEDECKUNG";
        static double Wide(double drawn) => facecover.Length * drawn * 0.53;
        Assert.Equal((9.0, 100), DeskPageRules.SlotHead("HEADWEAR", 80, 40));
        Assert.Equal((9.0, 100), DeskPageRules.SlotHead("KOPFBEDECKUNG", 80, 40));
        Assert.Equal((8.875, 0), DeskPageRules.SlotHead(facecover, 80, 40));
        Assert.Equal((10.0, 100), DeskPageRules.SlotHead(facecover, 112, 56));
        // Measured as drawn (Bahnschrift's capitals about 0.51 em), it takes its row's 9 px without the spacing.
        Assert.Equal((9.0, 0), DeskPageRules.SlotHead(facecover, 80, 40, wide: (at, ems) => facecover.Length * at * (0.51 + ems)));
        // At 125 % text it is set smaller as written and drawn as large as the box takes it, to a 64th of a pixel as written:
        // larger than at 100 %, never under 8.
        static double Drawn(double s) => TextSize.Scaled(s, 1.25);
        var (size, spacing) = DeskPageRules.SlotHead(facecover, 80, 40, Drawn);
        Assert.Equal(0, spacing);
        Assert.True(Wide(Drawn(size)) <= 80, $"{size}");
        Assert.True(Wide(Drawn(size + 1.0 / 64)) > 80, $"{size} stops short of the box");
        Assert.True(Drawn(size) > 8.5, $"drawn {Drawn(size)}, under its size at 100 %");
        // A name too long for its box even at 8 px stands at 8 px.
        var (least, _) = DeskPageRules.SlotHead(facecover, 40, 40, Drawn);
        Assert.InRange(Drawn(least), DeskCells.LeastFigure, DeskCells.LeastFigure + 0.05);
        // At 150 % WinUI's curve draws no text of 4 px or more under 11 px: the name stands at the least it can, as written
        // 4 px, drawn 11.1, wider than its box (the text grows, the box doesn't; DESIGN.md §4, *Text size*).
        Assert.Equal((4.0, 0), DeskPageRules.SlotHead(facecover, 80, 40, s => TextSize.Scaled(s, 1.5)));
    }

    // A region the last screenshot didn't show (owner, 2026-10-25): older than the page in hand, its head says since when,
    // "BACKPACK · 23:08" on the day, the date before the time on another; a region of a file written before its time was
    // kept is the page's.
    [Fact]
    public void A_region_the_last_screenshot_didnt_show_says_since_when()
    {
        static DateTimeOffset Local(int year, int month, int day, int hour, int minute)
        {
            var at = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
            return new DateTimeOffset(at, TimeZoneInfo.Local.GetUtcOffset(at));
        }
        var now = Local(2026, 1, 1, 23, 20);
        var backpack = new DeskRegion("backpack", DeskRegionKind.Backpack, 5, 6, []) { ReadAt = Local(2026, 1, 1, 23, 8) };
        var stash = new DeskRegion("stash", DeskRegionKind.Stash, 10, 13, []) { ReadAt = Local(2026, 1, 1, 23, 15) };
        var page = new DeskPage(Local(2026, 1, 1, 23, 15), [backpack, stash]);
        Assert.True(DeskPageRules.Kept(backpack, page));
        Assert.False(DeskPageRules.Kept(stash, page));
        // The "·" and the date go to the next line together where a head wraps: no-break spaces after it and within the date.
        Assert.Equal("BACKPACK · 23:08", DeskPageRules.HeadOf(backpack, page, now));
        Assert.Equal("STASH", DeskPageRules.HeadOf(stash, page, now));
        Assert.Equal("BACKPACK · 31 DEC 23:08", DeskPageRules.HeadOf(backpack with { ReadAt = Local(2025, 12, 31, 23, 8) }, page, now));
        Assert.False(DeskPageRules.Kept(backpack with { ReadAt = DateTimeOffset.MinValue }, page));
        // A window's head with its date.
        var keys = new DeskRegion("window:keys", DeskRegionKind.ContainerWindow, 11, 7, []) { Title = "Keys", AttachedTo = new DeskAnchor("stash", 7, 9), ReadAt = backpack.ReadAt };
        Assert.Equal("KEYS · IN THE STASH · 23:08", DeskPageRules.HeadOf(keys, page with { Regions = [backpack, stash, keys] }, now));
    }

    // A head takes the room of its dated form from the first reading (UI QA pass of step 2, F1: after reading 2 at 40 DIP
    // SPECIAL SLOTS' head, dated, wrapped to a second line and everything under POCKETS and SPECIAL SLOTS moved 13 px down):
    // its lines are those of its name with the longest date the language writes, however it is dated, so a date comes into
    // room kept for it and nothing under a head moves.
    [Fact]
    public void A_dated_head_wraps_into_room_kept_for_it()
    {
        // SPECIAL SLOTS, three cells wide at 40: its name on one line, its dated form on two, and its room two from the first
        // reading on.
        var special = DeskPageRules.BlockWidth(new DeskBlock("special", DeskRegionKind.SpecialSlots, null), 40);
        Assert.Equal(1, DeskPageRules.HeadLines("SPECIAL SLOTS", 40, special));
        Assert.Equal(2, DeskPageRules.HeadLines("SPECIAL SLOTS · 1 JAN 14:02", 40, special));
        Assert.Equal(2, DeskPageRules.HeadRoomLines("SPECIAL SLOTS", 40, special));
        var widest = DeskPageRules.WidestSince();
        Assert.Equal("SPECIAL SLOTS · " + widest, DeskPageRules.Reserved("SPECIAL SLOTS"));
        Assert.Matches("^28 [A-Z]{3,4}\\.? 23:58$", widest);
        HeadsKeepTheirRoom();

        // The page's columns as tall at reading 2 (every region but the backpack kept and dated) as at reading 1, at every
        // desk and text size: no block moves when the dates come.
        foreach (var scale in new[] { 1, 1.25, 1.5 })
        {
            foreach (var (w, h) in Desks)
            {
                var first = DeskExample.View(1, GameMode.Pve, 1, null, DateTimeOffset.UnixEpoch).Sheet.Page;
                var second = DeskExample.View(2, GameMode.Pve, 2, null, DateTimeOffset.UnixEpoch).Sheet.Page;
                var before = DeskPageRules.Layout(first.Regions, w, h, scale, r => DeskPageRules.Head(r, first));
                var after = DeskPageRules.Layout(second.Regions, w, h, scale, r => DeskPageRules.Head(r, second));
                Assert.Equal(before.Shape, after.Shape);
                Assert.Equal(before.ColumnHeights, after.ColumnHeights);
            }
        }
    }

    // Every head of the page keeps the room of any date its language writes: each fixed region's at every cell size in its
    // block's width and at the three text sizes, each month at a day of one and of two figures (the English here, the
    // German below).
    internal static void HeadsKeepTheirRoom()
    {
        var dates = Enumerable.Range(1, 12).SelectMany(m => new[] { 1, 9, 10, 28, 31 }.Where(d => d <= DateTime.DaysInMonth(2026, m))
            .Select(d => new DateTime(2026, m, d, 23, 58, 0, DateTimeKind.Unspecified))).ToList();
        foreach (var size in DeskPageRules.Sizes)
        {
            foreach (var kind in DeskPageRules.Fixed)
            {
                var width = DeskPageRules.BlockWidth(new DeskBlock(DeskRegion.KeyOf(kind), kind, null), size);
                var name = DeskPageRules.Name(kind);
                foreach (var scale in new[] { 1, 1.25, 1.5 })
                {
                    var room = DeskPageRules.HeadRoomLines(name, size, width, scale);
                    Assert.All(dates, at =>
                    {
                        var since = new DateTimeOffset(at, TimeZoneInfo.Local.GetUtcOffset(at));
                        var dated = RuleTexts.DeskHeadSince(head: name, time: DeskPageRules.Since(since, since.AddDays(3)));
                        Assert.True(DeskPageRules.HeadLines(dated, size, width, scale) <= room, $"{dated} at {size}, ×{scale}");
                    });
                }
            }
        }
    }

    // The scroll mark (UI QA pass of step 2, F3; the coordinator's rule): "MORE BELOW ▾" at the desk's foot while the page
    // runs on past it, the room under the page not counted; gone at its end and where the page fits.
    [Fact]
    public void The_scroll_mark_says_the_page_runs_on()
    {
        Assert.True(DeskPageRules.MoreBelow(2000, 440, 0));
        Assert.True(DeskPageRules.MoreBelow(2000, 440, 1500));
        Assert.False(DeskPageRules.MoreBelow(2000, 440, 1560), "scrolled to the end");
        // The room under the page alone below the foot: nothing more to see.
        Assert.False(DeskPageRules.MoreBelow(440 + DeskPageRules.PageBottom, 440, 0));
        Assert.False(DeskPageRules.MoreBelow(300, 440, 0));

        // The made-up desk at 900×560 (one column) and at 1600×900 (its windows under the stash) runs past the desk; at a
        // 2560 monitor's desk it doesn't.
        foreach (var (w, h, more) in new[] { (516.0, 440.0, true), (1216.0, 760.0, true), (2176.0, 1330.0, false) })
        {
            var page = DeskPageRules.Layout(Made(), w, h);
            Assert.Equal(more, DeskPageRules.MoreBelow(page.Height, h, 0));
        }
    }

    // The page in screens for the layout check (UI QA pass of step 2, F4): from its top, each the desk's room less the scroll
    // mark's band further down, so what the mark covers at one screen's foot stands in the next, the last at the page's end.
    [Fact]
    public void The_page_is_checked_in_screens()
    {
        Assert.Equal([0, 404, 808, 1212, 1560], DeskPageRules.ScreenOffsets(2000, 440, 36));
        Assert.Equal([0], DeskPageRules.ScreenOffsets(400, 440, 36));
        Assert.Equal([0], DeskPageRules.ScreenOffsets(440 + DeskPageRules.PageBottom, 440, 36));
        // Every DIP of the page under some screen's foot less the band, but for the last, which shows the page's end.
        var offsets = DeskPageRules.ScreenOffsets(2000, 440, 36);
        for (var k = 1; k < offsets.Count; k++)
            Assert.True(offsets[k] <= offsets[k - 1] + 440 - 36);
        Assert.False(DeskPageRules.MoreBelow(2000, 440, offsets[^1]));
    }

    // A reading's page comes in cell by cell (*Fun*): what it changed, every region on the mode's first; nothing for a
    // republish, and nothing for a reading that changed nothing.
    [Fact]
    public void A_readings_changed_regions_come_in_and_nothing_else()
    {
        string[] keys = ["loadout", "rig", "backpack", "stash"];
        var all = keys.ToHashSet();
        Assert.Equal(all, DeskPageRules.Entering(keys, new HashSet<string> { "backpack" }, first: true, newReading: true));
        Assert.Equal(new HashSet<string> { "backpack" }, DeskPageRules.Entering(keys, new HashSet<string> { "backpack" }, first: false, newReading: true));
        Assert.Empty(DeskPageRules.Entering(keys, new HashSet<string>(), first: false, newReading: true));
        Assert.Empty(DeskPageRules.Entering(keys, new HashSet<string> { "backpack" }, first: false, newReading: false));
    }

    // The marks and the heads grow with the cell: the figure 10.5 px at 40 to 15 at 80, the count 10 to 14.5, the source's
    // glyph 9 to 14, a trader's portrait 14 at 56 to 20 at 80, the notch and the "?" plate a DIP a step from 56; the
    // regions' heads 11, 12 and 13 px, the slots' names two pixels under them.
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
        Assert.Equal([9, 9, 10, 10, 11, 11], sizes.Select(DeskPageRules.SlotHeadSize));
        // On up to 128 a pixel a step (the figure 21 px at 128), the heads a pixel every two steps up to 16.
        Assert.Equal(21, DeskCells.FigureSizeAt(128));
        Assert.Equal([14, 14, 15, 15, 16, 16], DeskPageRules.Sizes.Skip(6).Select(DeskPageRules.HeadSize));
        // From 56 up the change stands as a figure, as at 56 (*The cell*).
        var cell = new DeskCellSheet(new DeskCell(0, 0, 2, 1, ["x"]),
            new DeskFigure(DeskSource.Flea, 13_000, 13_000, null) { Arrow = DeskArrow.Down, Change48h = -12 }, DeskKeepLevel.None, false);
        Assert.All(DeskPageRules.Sizes.Skip(2), s => Assert.Equal("↓ −12%", DeskCells.Of(cell, s).Change));
        Assert.Equal("↓", DeskCells.Of(cell, 48).Change);
        // A slot's box gives its marks the box's width: a "?" helmet's range in a box of 80 DIP keeps its figure.
        var helmet = new DeskCellSheet(new DeskCell(0, 0, 0, 0, ["a", "b"]) { Slot = "headwear" },
            new DeskFigure(DeskSource.Flea, 34_000, 52_000, null) { Unsure = true, High = 52_000 }, DeskKeepLevel.None, false);
        Assert.NotNull(DeskCells.Of(helmet, 40, box: 80).Figure);
    }

    // LOADOUT draws no grid, whatever a reading says, and its column is eight cells wide whatever its slots (QA pass of
    // 2026-10-19, F1 and F2: a reading's loadout of eleven by one drew eleven empty cells and pushed the stash's tenth
    // column past a window 1600 wide).
    [Fact]
    public void The_loadout_draws_no_grid_whatever_a_reading_says()
    {
        var read = new DeskRegion("loadout", DeskRegionKind.Loadout, 11, 1, [new DeskCell(0, 0, 1, 1, ["x"]) { Slot = "earpiece" }]);
        Assert.Equal((0, 0), DeskPageRules.Grid(read));
        Assert.Equal(8 * 56 + 1, DeskPageRules.BlockWidth(new DeskBlock("loadout", DeskRegionKind.Loadout, read), 56));
        Assert.Equal(DeskPageRules.LoadoutHeight(56), DeskPageRules.ContentHeight(new DeskBlock("loadout", DeskRegionKind.Loadout, read), 56));
        var rig = new DeskRegion("rig", DeskRegionKind.Rig, 5, 2, []);
        Assert.Equal((5, 2), DeskPageRules.Grid(rig));
        // A container's row: its slot box, the gap and its grid, as tall as the taller.
        var block = new DeskBlock("rig", DeskRegionKind.Rig, rig);
        Assert.Equal(2 * 56 + 12 + 5 * 56 + 1, DeskPageRules.BlockWidth(block, 56));
        Assert.Equal(113, DeskPageRules.ContentHeight(block, 56));
        Assert.Equal(112, DeskPageRules.ContentHeight(block with { Region = rig with { Rows = 1 } }, 56));
        Assert.Equal(6 * 56 + 1, DeskPageRules.ContentHeight(block with { Region = rig with { Rows = 6 } }, 56));
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

    // POCKETS and SPECIAL SLOTS stand side by side, each as wide as its grid: the middle column holds both and the gap.
    [Fact]
    public void Pockets_and_special_slots_share_a_row()
    {
        Assert.True(DeskPageRules.SameRow(DeskRegionKind.Pockets, DeskRegionKind.SpecialSlots));
        Assert.False(DeskPageRules.SameRow(DeskRegionKind.Rig, DeskRegionKind.Pockets));
        var page = new List<DeskRegion>
        {
            new("pockets", DeskRegionKind.Pockets, 4, 2, []),
            new("special", DeskRegionKind.SpecialSlots, 3, 2, []),
        };
        Assert.Equal(4 * 56 + 1 + DeskPageRules.RowGap + 3 * 56 + 1, DeskPageRules.Arrange(page, 56).ColumnWidths[1]);
    }

    [Fact]
    public void Each_region_stands_under_its_own_head_in_reading_order()
    {
        Assert.Equal(
            ["LOADOUT", "TACTICAL RIG", "POCKETS", "SPECIAL SLOTS", "BACKPACK", "ZHUK · IN THE BACKPACK", "STASH", "WZ"],
            View.Sheet.Regions.Select(r => DeskPageRules.Head(r.Region, View.Sheet.Page)));
        Assert.Equal("POUCH", DeskPageRules.Name(DeskRegionKind.Pouch));
        Assert.Equal("NOT IN A SCREENSHOT YET", RuleTexts.DeskNotRead);
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
        Assert.Equal("ON YOU 1.01 M+ ₽ · STASH 6.84 M ₽ · KEEP 3 · 2 · 4", DeskPageRules.HeadLine(totals, totals.OnYou, totals.Stash, OwnersPage()));
        // Counting up: the totals given apart, the "+" and the keeps as they end.
        Assert.Equal("ON YOU 505k+ ₽ · STASH 3.42 M ₽ · KEEP 3 · 2 · 4", DeskPageRules.HeadLine(totals, 505_000, 3_420_000, OwnersPage()));
    }

    // A part never read is left out of the head line (owner, 2026-10-26, decision 5: the made-up desk's reading 4 said "ON
    // YOU 3.20 M+ ₽ · STASH 0 ₽ · KEEP 0 · 2 · 1" under a STASH that said it was never read): STASH until the page holds a
    // stash read, ON YOU until it holds a gear page read; KEEP stays. Read is what the page doesn't draw as never read, the
    // dashed frame's rule: a region of its heading alone is not read, a container's slot seen empty and a LOADOUT of no
    // slot are.
    [Fact]
    public void The_head_line_leaves_out_a_part_never_read()
    {
        const char nb = (char)0xA0;
        var made =DeskExample.View(4, GameMode.Pve, 4, null, DateTimeOffset.UnixEpoch);
        Assert.Equal($"ON YOU 3.20{nb}M+{nb}₽ · KEEP 0 · 2 · 1",
            DeskPageRules.HeadLine(made.Sheet.Totals, made.Sheet.Totals.OnYou, made.Sheet.Totals.Stash, made.Sheet.Page.Regions));

        var totals = new DeskTotals(1_010_000, true, 6_840_000, false, 3, 2, 4);
        string Line(IReadOnlyList<DeskRegion> page) => DeskPageRules.HeadLine(totals, totals.OnYou, totals.Stash, page);
        static bool Gear(DeskRegion r) => r.Kind is not (DeskRegionKind.Stash or DeskRegionKind.ContainerWindow);
        static DeskRegion Bare(DeskRegion r) => r with { Cols = 0, Rows = 0, Cells = [], Parts = [] };
        var owners = OwnersPage();
        DeskRegion Of(DeskRegionKind kind) => owners.Single(r => r.Kind == kind);
        var stash = Of(DeskRegionKind.Stash);
        var keys = Of(DeskRegionKind.ContainerWindow);

        // Both read; the stash never read, none on the page or one of its heading alone; the gear never read; neither.
        var both = $"ON YOU 1.01{nb}M+{nb}₽ · STASH 6.84{nb}M{nb}₽ · KEEP 3 · 2 · 4";
        var onYou = $"ON YOU 1.01{nb}M+{nb}₽ · KEEP 3 · 2 · 4";
        var inStash = $"STASH 6.84{nb}M{nb}₽ · KEEP 3 · 2 · 4";
        Assert.Equal(both, Line(owners));
        Assert.Equal(onYou, Line(owners.Where(Gear).ToList()));
        Assert.Equal(onYou, Line([.. owners.Where(Gear), Bare(stash)]));
        Assert.Equal(inStash, Line([stash, keys]));
        Assert.Equal(inStash, Line([stash, Bare(Of(DeskRegionKind.Pockets)), Bare(Of(DeskRegionKind.SpecialSlots))]));
        Assert.Equal("KEEP 3 · 2 · 4", Line([]));
        Assert.Equal("KEEP 3 · 2 · 4", Line([Bare(stash), Bare(Of(DeskRegionKind.Pockets)), keys with { AttachedTo = null }]));

        // Any one gear region read is a gear page read: a LOADOUT of no slot read, a backpack whose slot was seen empty (its
        // grid gone with it), the pockets read empty.
        Assert.Equal(both, Line([Of(DeskRegionKind.Loadout) with { Cells = [] }, stash]));
        Assert.Equal(both, Line([Bare(Of(DeskRegionKind.Backpack)), stash]));
        Assert.Equal(both, Line([Of(DeskRegionKind.Pockets), stash]));

        // One rule for the frame and the head line: a part is read where the page draws a region of it as read.
        List<List<DeskRegion>> pages = [owners, Made(1), Made(4), owners.Where(Gear).ToList(), [stash, keys], []];
        foreach (var page in pages)
        {
            var blocks = DeskPageRules.Blocks(page);
            Assert.Equal((blocks.Any(b => b.Kind is not (DeskRegionKind.Stash or DeskRegionKind.ContainerWindow) && !b.Placeholder),
                !blocks.Single(b => b.Kind == DeskRegionKind.Stash).Placeholder), DeskPageRules.PartsRead(page));
        }
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
            Assert.StartsWith("AN DIR ", DeskPageRules.HeadLine(view.Sheet.Totals, 0, 0, view.Sheet.Page.Regions), StringComparison.Ordinal);
            // A part never read left out, in German too (owner, 2026-10-26, decision 5).
            var totals = new DeskTotals(3_200_000, true, 6_840_000, false, 0, 2, 1);
            var page = DeskExample.Page(1).Regions;
            string Line(IEnumerable<DeskRegion> regions) => DeskPageRules.HeadLine(totals, totals.OnYou, totals.Stash, regions.ToList());
            Assert.Equal("AN DIR 3,20 M+ ₽ · STASH 6,84 M ₽ · BEHALTEN 0 · 2 · 1", Line(page));
            Assert.Equal("AN DIR 3,20 M+ ₽ · BEHALTEN 0 · 2 · 1", Line(DeskExample.Page(4).Regions));
            Assert.Equal("STASH 6,84 M ₽ · BEHALTEN 0 · 2 · 1", Line(page.Where(r => r.Kind == DeskRegionKind.Stash)));
            Assert.Equal("BEHALTEN 0 · 2 · 1", Line([]));
            // A kept region's date as German writes it, in capitals.
            var at = new DateTime(2025, 12, 31, 23, 8, 0, DateTimeKind.Unspecified);
            var readAt = new DateTimeOffset(at, TimeZoneInfo.Local.GetUtcOffset(at));
            Assert.Equal("31. DEZ., 23:08", DeskPageRules.Since(readAt, readAt.AddDays(2)));
            Assert.Equal("23:08", DeskPageRules.Since(readAt, readAt.AddMinutes(5)));
            // Every German head keeps the room of its longest date, "28. SEPT., 23:58" (UI QA pass of step 2, F1: at 125 %
            // TASCHEN's dated head wrapped and moved what was under it).
            Assert.Equal("28. SEPT., 23:58", DeskPageRules.WidestSince());
            DeskPageRulesTests.HeadsKeepTheirRoom();
        }
        finally
        {
            UiLanguage.Set(UiLanguage.English);
        }
    }
}
