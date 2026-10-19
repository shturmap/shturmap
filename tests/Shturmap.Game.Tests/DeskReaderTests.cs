using Shturmap.Core.Desk;
using Shturmap.Game.Screenshots;
using SkiaSharp;

namespace Shturmap.Game.Tests;

// The stash desk's reading (owner, 2026-10-19; docs/DESIGN.md §2, "For the stash desk"): the GEAR tab's regions under
// their headings, the middle column's compartments, the stash, a container's floating window over it, each cell named
// against the catalogue at a fit of 0.9 with the footprint as the tie-break. The first tests need no picture (the label's
// match, the printed condition) or draw their own (the frames, a whole GEAR tab with made-up names, read with Windows'
// recognition like the stash reader's tests); the real screenshots are read in tests\Shturmap.Session.Tests
// (DeskRealPictureTests), which skip without them.
public sealed class DeskReaderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("shturmap-desk-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Id(int n) => n.ToString("D24");

    private static DeskCandidate Item(int n, string shortName, int w = 1, int h = 1, DeskHolds holds = DeskHolds.Nothing, string? name = null,
        DeskSlotKind slots = DeskSlotKind.Any) =>
        new(Id(n), shortName, name ?? shortName, w, h, holds, slots);

    // ---- the label's match ----

    [Fact]
    public void A_label_one_letter_in_ten_off_still_names_an_item_and_one_in_five_doesnt()
    {
        var match = new DeskMatch([Item(1, "Vantressimo"), Item(2, "Quenvik")]);
        // One letter off in eleven: 0.91.
        var (fit, best) = match.Match("Vantressimc");
        Assert.True(fit >= DeskMatch.Enough);
        Assert.Equal(Id(1), Assert.Single(best).ItemId);
        // One letter off in seven: 0.86, enough for the quest path's 0.8, not for the desk.
        Assert.True(match.Match("Quenvak").Fit < DeskMatch.Enough);
    }

    [Fact]
    public void Items_of_one_short_name_are_one_of_n()
    {
        var match = new DeskMatch([Item(1, "Bandage"), Item(2, "Bandage"), Item(3, "Bandit")]);
        var (fit, best) = match.Match("Bandage");
        Assert.Equal(1, fit);
        Assert.Equal([Id(1), Id(2)], best.Select(c => c.ItemId).Order());
    }

    [Fact]
    public void A_name_with_digits_must_agree_in_them()
    {
        var match = new DeskMatch([Item(1, "Marl 214"), Item(2, "Marl 206")]);
        Assert.Equal(Id(2), Assert.Single(match.Match("Marl 206").Best).ItemId);
        Assert.Equal(Id(1), Assert.Single(match.Match("Marl 214").Best).ItemId);
    }

    // The measurement of 2026-10-19: "Weapons" fit 49 sealed weapon cases by the cut-label rule; a label is cut only
    // when it doesn't fit its cell, and seven letters fit a cell five wide.
    [Fact]
    public void A_short_label_on_a_wide_cell_is_no_cut_name()
    {
        var match = new DeskMatch([Item(1, "Weapons", 5, 2), Item(2, "Weapons sealed", 5, 2), Item(3, "Weapons sealed", 5, 2)]);
        Assert.Equal(Id(1), Assert.Single(match.Match("Weapons", cells: 5).Best).ItemId);
        // On a one-wide cell the same label may be cut: all three.
        Assert.Equal(3, match.Match("Weapons", cells: 1).Best.Count);
    }

    // A pocket's item bound to a quick slot carries the slot's number over its label's start: "6 splint" for "Alu
    // splint". The end alone names nothing when another item's name ends alike.
    [Fact]
    public void A_label_cut_by_a_quick_slots_number_is_one_of_the_names_ending_so()
    {
        var match = new DeskMatch([Item(1, "Alu splint"), Item(2, "Splint"), Item(3, "Esmarch")]);
        var (fit, best) = match.Match("6 splint");
        Assert.Equal(1, fit);
        Assert.Equal([Id(1), Id(2)], best.Select(c => c.ItemId).Order());
        Assert.Equal("splint", DeskMatch.QuickSlotCut("6 splint"));
        Assert.Null(DeskMatch.QuickSlotCut("Alu splint"));
    }

    // The recognition may leave the quick slot's number out ("splint" for "6 | Alu splint"): where the reading saw the
    // number's plate, the label is a name's end all the same (QA pass of 2026-10-19, F4).
    [Fact]
    public void A_label_under_a_quick_slots_plate_is_a_names_end_even_without_its_number()
    {
        var match = new DeskMatch([Item(1, "Alu splint"), Item(2, "Splint"), Item(3, "CALOK-B")]);
        Assert.Equal([Id(2)], match.Match("splint").Best.Select(c => c.ItemId));
        Assert.Equal([Id(1), Id(2)], match.Match("splint", cutAtStart: true).Best.Select(c => c.ItemId).Order());
        var (fit, best) = match.Match("ALOK-B", cutAtStart: true);
        Assert.Equal(1, fit);
        Assert.Equal(Id(3), Assert.Single(best).ItemId);
        Assert.True(match.Match("ALOK-B").Fit < DeskMatch.Enough);
    }

    // A slot takes items of its kind only: the label a gun shares with its magazine, stock and grip names the gun on
    // the sling (QA pass of 2026-10-19, F4: "one of 5", a magazine's art at ~11–80k).
    [Fact]
    public void A_slots_label_names_the_items_of_its_kind_only()
    {
        var match = new DeskMatch([
            Item(1, "Varkel-12", 4, 1, DeskHolds.Mods, slots: DeskSlotKind.Weapon),
            Item(2, "Varkel-12", 1, 2, slots: DeskSlotKind.None), // its magazine
            Item(3, "Varkel-12", 2, 1, slots: DeskSlotKind.None), // its stock
            Item(4, "Varkel-12", 1, 1, slots: DeskSlotKind.Headwear),
        ]);
        Assert.Equal(4, match.Match("Varkel-12").Best.Count);
        Assert.Equal(Id(1), Assert.Single(match.Match("Varkel-12", kind: DeskSlotKind.Weapon).Best).ItemId);
        Assert.Equal(Id(4), Assert.Single(match.Match("Varkel-12", kind: DeskSlotKind.Headwear).Best).ItemId);
        Assert.Empty(match.Match("Varkel-12", kind: DeskSlotKind.Knife).Best);
    }

    [Fact]
    public void A_windows_title_names_the_containers_by_short_or_full_name()
    {
        var match = new DeskMatch([
            Item(1, "Zorblat", 4, 3, DeskHolds.Contents, "Zorblat chest harness (Black)"),
            Item(2, "Zorblat", 4, 3, DeskHolds.Contents, "Zorblat chest harness (Grey)"),
            Item(3, "Zorblat", 3, 3), // a body armour of the same short name: no grid, no window
            Item(4, "Ostrel", holds: DeskHolds.Contents, name: "Ostrel case"),
        ]);
        Assert.Equal([Id(1), Id(2)], match.Titled("Zorblat").Select(c => c.ItemId).Order());
        Assert.Equal(Id(4), Assert.Single(match.Titled("Ostrel case")).ItemId);
    }

    // ---- the printed condition ----

    [Theory]
    [InlineData("46/50", 46, 50)]
    [InlineData("1469/1800", 1469, 1800)]
    [InlineData("166/4ØØ", 166, 400)]
    // A window's two-pixel line read after the number: a trailing "1", or a backslash.
    [InlineData("400/4001", 400, 400)]
    [InlineData("3/31", 3, 3)]
    [InlineData("380/4ØØ\\", 380, 400)]
    [InlineData("-60/60", 60, 60)]
    // A magazine's "50/50" read with an s for the 5 (QA pass of 2026-10-19).
    [InlineData("sØ/SØ", 50, 50)]
    public void A_printed_condition_is_read_as_the_game_prints_it(string text, int current, int max) =>
        Assert.Equal(new DeskPrint(current, max), DeskReader.PrintOf(text));

    [Theory]
    [InlineData("250")]
    [InlineData("gli")]
    [InlineData(null)]
    public void A_count_is_no_condition(string? text) => Assert.Null(DeskReader.PrintOf(text));

    // ---- the frames ----

    private static readonly SKColor Field = new(26, 29, 32), Line = new(96, 100, 104);

    private static void Box(SKBitmap bitmap, int x, int y, int cols, int rows, int pitch)
    {
        using var canvas = new SKCanvas(bitmap);
        using var field = new SKPaint { Color = Field };
        using var line = new SKPaint { Color = Line };
        canvas.DrawRect(x, y, cols * pitch + 1, rows * pitch + 1, field);
        for (var i = 0; i <= cols; i++)
            canvas.DrawRect(x + i * pitch, y, 1, rows * pitch + 1, line);
        for (var j = 0; j <= rows; j++)
            canvas.DrawRect(x, y + j * pitch, cols * pitch + 1, 1, line);
    }

    [Fact]
    public void Compartments_a_few_pixels_apart_are_found_each_and_a_grid_whole()
    {
        const int pitch = 84;
        using var bitmap = new SKBitmap(1200, 900);
        using (var canvas = new SKCanvas(bitmap))
            canvas.Clear(new SKColor(18, 20, 22));
        // A rig's four pockets, one by two, eight pixels apart; a backpack's grid of five by four below.
        for (var k = 0; k < 4; k++)
            Box(bitmap, 100 + k * (pitch + 8), 100, 1, 2, pitch);
        Box(bitmap, 100, 400, 5, 4, pitch);
        var lines = new StashLines(StashGray.Of(bitmap));
        var frames = DeskFrames.Find(lines, pitch, SKRectI.Create(0, 0, 1200, 900), []);
        Assert.Equal(5, frames.Count);
        Assert.Equal(4, frames.Count(f => Math.Abs(f.Width - pitch) <= 3 && Math.Abs(f.Height - 2 * pitch) <= 3));
        Assert.Single(frames, f => Math.Abs(f.Width - 5 * pitch) <= 3 && Math.Abs(f.Height - 4 * pitch) <= 3);
    }

    [Fact]
    public void A_grid_a_window_lies_over_is_found_around_it()
    {
        const int pitch = 84;
        using var bitmap = new SKBitmap(1200, 900);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(18, 20, 22));
        }
        Box(bitmap, 100, 100, 5, 7, pitch);
        // The window: its own field over the grid's lower left, hiding the lines there.
        var window = SKRectI.Create(60, 400, 300, 300);
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { Color = new SKColor(34, 37, 40) })
            canvas.DrawRect(window, paint);
        var lines = new StashLines(StashGray.Of(bitmap));
        var plain = DeskFrames.Find(lines, pitch, SKRectI.Create(0, 0, 1200, 900), []);
        Assert.DoesNotContain(plain, f => Math.Abs(f.Width - 5 * pitch) <= 3 && Math.Abs(f.Height - 7 * pitch) <= 3);
        var under = DeskFrames.Find(lines, pitch, SKRectI.Create(0, 0, 1200, 900), [window], under: true);
        Assert.Single(under, f => Math.Abs(f.Width - 5 * pitch) <= 3 && Math.Abs(f.Height - 7 * pitch) <= 3);
    }

    // ---- a whole GEAR tab, drawn ----

    private static DeskReader Reader()
    {
        var reader = DeskReader.Create("en");
        if (reader is null)
            Assert.Skip("This Windows has no text recognition language installed.");
        return reader;
    }

    private static void Text(StashReaderTests.Page page, string text, int x, int y, float size = 18)
    {
        using var canvas = new SKCanvas(page.Bitmap);
        using var face = SKTypeface.FromFamilyName("Bahnschrift", SKFontStyle.Bold) ?? SKTypeface.Default;
        using var font = new SKFont(face, size);
        using var paint = new SKPaint { Color = new SKColor(214, 218, 220), IsAntialias = true };
        canvas.DrawText(text, x, y + size * 0.8f, SKTextAlign.Left, font, paint);
    }

    private static readonly DeskCandidate[] Catalogue =
    [
        Item(1, "Zorblat", slots: DeskSlotKind.Earpiece),
        // An item of the earpiece's short name that no earpiece slot takes: never the slot's.
        Item(12, "Zorblat", slots: DeskSlotKind.None),
        Item(2, "Quenvik"),
        Item(3, "Pindrake", 5, 7, DeskHolds.Contents),
        Item(4, "Tessaril", 1, 2),
        Item(5, "Hollin"),
        Item(6, "Drevak", 2, 1),
        Item(7, "Ostrel", 1, 1, DeskHolds.Contents),
        Item(8, "Marl 214"),
        Item(9, "Marl 206"),
        // Two items of one short name and size: never told apart.
        Item(10, "Krellnot", 1, 1),
        Item(11, "Krellnot", 1, 1),
        // A name and the end of another's: under a quick slot's plate, either.
        Item(13, "Tessmar", 1, 1),
        Item(14, "Alu Tessmar", 1, 1),
        // A special slot's item, three by one in the item file, in its slot's one cell; and one of its name no special
        // slot takes, one by one.
        Item(15, "Survex", 3, 1, slots: DeskSlotKind.Special),
        Item(16, "Survex", 1, 1, slots: DeskSlotKind.None),
    ];

    // The game's quick slot plate over a cell's top left: light, about a third of a cell, its digit dark.
    private static void QuickSlotPlate(StashReaderTests.Page page, int x, int y, string digit)
    {
        var p = page.Pitch;
        using (var canvas = new SKCanvas(page.Bitmap))
        using (var plate = new SKPaint { Color = new SKColor(216, 220, 220), IsAntialias = true })
            canvas.DrawRoundRect(x + 0.06f * p, y + 0.07f * p, 0.31f * p, 0.29f * p, 2, 2, plate);
        using (var canvas = new SKCanvas(page.Bitmap))
        using (var face = SKTypeface.FromFamilyName("Bahnschrift", SKFontStyle.Normal) ?? SKTypeface.Default)
        using (var font = new SKFont(face, 0.25f * p))
        using (var ink = new SKPaint { Color = new SKColor(40, 44, 46), IsAntialias = true })
            canvas.DrawText(digit, x + 0.13f * p, y + 0.31f * p, SKTextAlign.Left, font, ink);
    }

    // The GEAR tab at 2560×1440 as the game lays it out (the screenshots of 2026-10-19): the loadout's slots under their
    // headings at the left, POCKETS and BACKPACK in the middle column, the stash at the right under SEARCH and SORT TABLE.
    private static StashReaderTests.Page Gear()
    {
        var page = new StashReaderTests.Page(2560, 1440).Start();
        var p = page.Pitch;
        // The loadout: four headings, two slots filled.
        Text(page, "EARPIECE", 92, 199);
        Text(page, "HEADWEAR", 337, 199);
        Text(page, "ARMBAND", 91, 420);
        Text(page, "BODY ARMOR", 338, 420);
        Text(page, "EYEWEAR", 583, 420);
        page.Grid(87, 220, 2, 2, 1);
        page.Item(87, 220, 1, 0, 0, 2, 2, "Zorblat");
        page.Grid(333, 442, 2, 2, 1);
        page.Item(333, 442, 1, 0, 0, 2, 2, "Krellnot", "46/50");
        // The middle column: two pockets eight pixels apart, the backpack's slot and its grid inline.
        Text(page, "POCKETS", 880, 106);
        page.Grid(875, 130, 1, 2, 1);
        page.Grid(875 + p + 8, 130, 1, 2, 1);
        page.Item(875, 130, 1, 0, 0, 1, 1, "Quenvik", "250");
        // The second pocket: an item bound to a quick slot, its plate over its label's start, its print in the game's red.
        page.Item(875 + p + 8, 130, 1, 0, 0, 1, 1, "u Tessmar", "1/3", cornerColour: new SKColor(214, 74, 52));
        QuickSlotPlate(page, 875 + p + 8, 130, "6");
        // The special slots: one filled.
        Text(page, "SPECIAL SLOTS", 1235, 106);
        page.Grid(1262, 130, 1, 1, 1);
        page.Item(1262, 130, 1, 0, 0, 1, 1, "Survex");
        Text(page, "BACKPACK", 880, 336);
        page.Grid(876, 360, 2, 2, 1);
        page.Item(876, 360, 1, 0, 0, 2, 2, "Pindrake");
        page.Grid(1051, 360, 5, 4, 1);
        page.Item(1051, 360, 1, 0, 0, 1, 2, "Tessaril");
        // The stash.
        var (x, y) = page.StashOrigin;
        Text(page, "SEARCH", x + 35, y - 24, 15);
        Text(page, "SORT TABLE", x + 200, y - 24, 15);
        page.Grid(x, y, 10, 12, 1);
        page.Item(x, y, 1, 2, 1, 1, 1, "Hollin");
        page.Item(x, y, 1, 4, 2, 2, 1, "Drevak", "330");
        page.Item(x, y, 1, 0, 5, 1, 1, "Ostrel");
        return page;
    }

    // A container's window over the stash's lower part: its bar (magnifier, title, close) and its own grid, two-pixel lines.
    private static (int X, int Y) Window(StashReaderTests.Page page)
    {
        var p = page.Pitch;
        var (sx, sy) = page.StashOrigin;
        int wx = sx + 2 * p + 46, wy = sy + 7 * p + 30;
        using (var canvas = new SKCanvas(page.Bitmap))
        using (var paint = new SKPaint { Color = new SKColor(30, 33, 36) })
            canvas.DrawRect(wx - 12, wy - 44, 3 * p + 24, 2 * p + 56, paint);
        Text(page, "Q", wx + 4, wy - 34, 18);
        Text(page, "Ostrel", wx + 37, wy - 34, 18);
        Text(page, "x", wx + 3 * p - 20, wy - 34, 18);
        page.Grid(wx, wy, 3, 2, 2);
        page.Item(wx, wy, 2, 1, 0, 1, 1, "Marl 214");
        return (wx, wy);
    }

    private static DeskCell In(DeskPage page, string region, Func<DeskCell, bool> which) =>
        Assert.Single(page.Region(region)!.Cells, c => which(c));

    // SHTURMAP_DESK_DUMP=<folder> keeps the drawn pictures, to look at them with shturmap-cli desk-probe.
    private static void Dump(StashReaderTests.Page page, string name)
    {
        if (Environment.GetEnvironmentVariable("SHTURMAP_DESK_DUMP") is { Length: > 0 } folder)
            page.Save(folder, name);
    }

    [Fact]
    public async Task The_gear_tab_is_read_region_by_region()
    {
        var gear = Gear();
        Dump(gear, "gear.png");
        using var picture = gear.Bitmap.Copy();
        var reading = await Reader().ReadAsync(picture, new DeskCatalogue(Catalogue), new DateTimeOffset(2026, 1, 1, 15, 40, 0, TimeSpan.Zero), Ct);
        Assert.Equal(DeskScreen.Gear, reading.Screen);
        var page = reading.Page!;
        Assert.Equal(["backpack", "loadout", "pockets", "special", "stash"], page.Regions.Select(r => r.Key).Order());

        // The loadout's slots: a strip, no grid (QA pass of 2026-10-19, F2); one cell a slot, no footprint, named among
        // the items its slot takes; a short name two items share stays "one of 2".
        Assert.Equal((0, 0), (page.Region("loadout")!.Cols, page.Region("loadout")!.Rows));
        Assert.Equal([Id(1)], In(page, "loadout", c => c.Slot == "earpiece").Ids);
        var armour = In(page, "loadout", c => c.Slot == "bodyarmor");
        Assert.Equal([Id(10), Id(11)], armour.Ids.Order());
        Assert.Equal(new DeskPrint(46, 50), armour.Print);
        Assert.DoesNotContain(page.Region("loadout")!.Cells, c => c.Slot == "headwear");

        // The pockets: compartments a few pixels apart, side by side in the region's units.
        Assert.Equal(2, page.Region("pockets")!.Parts.Count);
        var quenvik = In(page, "pockets", c => c.Ids.Contains(Id(2)));
        Assert.Equal((0, 0, 250), (quenvik.I, quenvik.J, quenvik.Count));
        // Under a quick slot's plate the label may have lost its start: never named by its end alone (QA pass of
        // 2026-10-19, F4: "splint" named the Splint where the Alu splint lay). Its red print is read.
        var bound = In(page, "pockets", c => c.I == 1);
        Assert.Contains(Id(14), bound.Ids);
        Assert.Equal(new DeskPrint(1, 3), bound.Print);

        // A special slot: its item in one cell whatever its footprint, among the items special slots take.
        Assert.Equal([Id(15)], In(page, "special", c => !c.IsSlot).Ids);

        // The backpack: its own slot (its grid inline, so nothing unseen) and its grid's cells by footprint.
        var backpack = page.Region("backpack")!;
        var slot = In(page, "backpack", c => c.Slot == "backpack");
        Assert.Equal([Id(3)], slot.Ids);
        Assert.False(page.Unseen(backpack, slot));
        Assert.Equal((5, 4), (backpack.Cols, backpack.Rows));
        var tessaril = In(page, "backpack", c => c.Ids.Contains(Id(4)));
        Assert.Equal((0, 0, 1, 2), (tessaril.I, tessaril.J, tessaril.W, tessaril.H));

        // The stash: the container is a cell whose contents are unseen.
        var stash = page.Region("stash")!;
        Assert.Equal((2, 1), (In(page, "stash", c => c.Ids.Contains(Id(5))).I, In(page, "stash", c => c.Ids.Contains(Id(5))).J));
        Assert.Equal((2, 1, 330), (In(page, "stash", c => c.Ids.Contains(Id(6))).W, In(page, "stash", c => c.Ids.Contains(Id(6))).H, In(page, "stash", c => c.Ids.Contains(Id(6))).Count));
        var ostrel = In(page, "stash", c => c.Ids.Contains(Id(7)));
        Assert.Equal(DeskHolds.Contents, ostrel.Holds);
        Assert.True(page.Unseen(stash, ostrel));
    }

    [Fact]
    public async Task A_containers_window_is_read_attached_and_what_it_covers_is_left_as_it_was()
    {
        var gear = Gear();
        Window(gear);
        Dump(gear, "gear-window.png");
        using var picture = gear.Bitmap.Copy();
        var reading = await Reader().ReadAsync(picture, new DeskCatalogue(Catalogue), new DateTimeOffset(2026, 1, 1, 15, 41, 0, TimeSpan.Zero), Ct);
        var page = Core.Desk.DeskLedger.Attach(reading.Page!);
        var window = Assert.Single(page.Regions, r => r.Kind == DeskRegionKind.ContainerWindow);
        Assert.Equal("Ostrel", window.Title);
        Assert.Equal([Id(8)], Assert.Single(window.Cells).Ids);
        // Exactly one Ostrel on the page: the window attaches to it, and its contents are seen.
        Assert.Equal(new DeskAnchor("stash", 0, 5), window.AttachedTo);
        var stash = page.Region("stash")!;
        Assert.False(page.Unseen(stash, In(page, "stash", c => c.Ids.Contains(Id(7)))));
        // The stash's cells under the window are covered: not read, left to the page in hand.
        Assert.NotEmpty(stash.Covered);
    }

    [Fact]
    public async Task A_stash_page_alone_is_read_without_the_gear_tab()
    {
        var page = new StashReaderTests.Page(2560, 1440).Start();
        var (x, y) = page.StashOrigin;
        page.Grid(x, y, 10, 12, 1);
        page.Item(x, y, 1, 2, 1, 1, 1, "Hollin");
        using var picture = page.Bitmap.Copy();
        var reading = await Reader().ReadAsync(picture, new DeskCatalogue(Catalogue), DateTimeOffset.MinValue, Ct);
        Assert.Equal(DeskScreen.Stash, reading.Screen);
        Assert.Equal(["stash"], reading.Page!.Regions.Select(r => r.Key));
        Assert.Equal([Id(5)], reading.Page.Region("stash")!.Cells.Single().Ids);
    }

    [Fact]
    public async Task The_trading_screen_is_told_and_nothing_is_read()
    {
        var page = Gear();
        page.Grid(11, 351, 9, 11, 1);
        page.Item(11, 351, 1, 0, 0, 1, 1, "Ostrel");
        using var picture = page.Bitmap.Copy();
        var reading = await Reader().ReadAsync(picture, new DeskCatalogue(Catalogue), DateTimeOffset.MinValue, Ct);
        Assert.Equal(DeskScreen.Trading, reading.Screen);
        Assert.Null(reading.Page);
    }
}
