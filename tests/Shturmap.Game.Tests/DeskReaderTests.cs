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

    // A weapon preset's name on its cell (owner, 2026-10-26: the holster's "USP .45 Expert", which fit nothing, the catalogue
    // leaving presets out): a label that fits no name but begins with a gun's whole short name, on a word boundary, and goes
    // on names the gun. Only a gun; not where the slot takes none, nor under a quick slot's number (its start may be gone).
    [Fact]
    public void A_label_that_begins_with_a_guns_name_and_goes_on_names_the_gun()
    {
        var match = new DeskMatch([
            Item(1, "Varkel .45", 2, 1, DeskHolds.Mods, slots: DeskSlotKind.Weapon),
            Item(2, "Varkel45 std", 1, 2), // its magazine
            Item(3, "Ripsaw", 1, 1), // an ammo, no gun
        ]);
        Assert.True(match.Match("Varkel .45 Expert").Fit < DeskMatch.Enough);
        Assert.Equal([Id(1)], match.Preset("Varkel .45 Expert").Select(c => c.ItemId));
        Assert.Equal([Id(1)], match.Preset("Varkel.45 Expert", DeskSlotKind.Weapon).Select(c => c.ItemId));
        Assert.Empty(match.Preset("Varkel .45 Expert", DeskSlotKind.Headwear));
        Assert.Empty(match.Preset("Varkel .45 Expert", cutAtStart: true));
        // No word boundary, nothing after the name but a mark, or not a gun's name: nothing.
        Assert.Empty(match.Preset("Varkel .45Expert"));
        Assert.Empty(match.Preset("Varkel .45 ."));
        Assert.Empty(match.Preset("Varkel"));
        Assert.Empty(match.Preset("Ripsaw ammo pack"));
    }

    // Several guns' names begin the label: the longest's; two guns of that one name are "one of 2", never a guess. A label a
    // letter off the name of an item that isn't a gun and begins with the gun's ("Torvik DG" for the Torvik DC muzzle brake)
    // is that item misread as much as a preset's name: nothing.
    [Fact]
    public void A_preset_name_takes_the_longest_guns_name_and_a_near_miss_of_a_part_names_nothing()
    {
        var match = new DeskMatch([
            Item(1, "Torvik", 3, 1, DeskHolds.Mods, slots: DeskSlotKind.Weapon),
            Item(2, "Torvik SD", 3, 1, DeskHolds.Mods, slots: DeskSlotKind.Weapon),
            Item(3, "Brask", 4, 1, DeskHolds.Mods, slots: DeskSlotKind.Weapon),
            Item(4, "Brask", 4, 1, DeskHolds.Mods, slots: DeskSlotKind.Weapon),
            Item(5, "Torvik DC", 2, 1), // a muzzle brake
        ]);
        Assert.Equal([Id(2)], match.Preset("Torvik SD Tactical").Select(c => c.ItemId));
        Assert.Equal([Id(1)], match.Preset("Torvik Default").Select(c => c.ItemId));
        Assert.Equal([Id(3), Id(4)], match.Preset("Brask Default").Select(c => c.ItemId).Order());
        Assert.Empty(match.Preset("Torvik DG"));
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

    // The AVS rig's MPX magazines (the owner's pictures 02 and 03 of 2026-10-26, layout round of 2026-10-28): "0/41" read
    // "10/41", the loaded mark's glyph at the cell's bottom left a "1", and "4/41" taken for "4/4", its "1" for a window's
    // line. The band starts right of the mark; a trailing "1" is a line's only beside a two-pixel line.
    [Theory]
    [InlineData(2560, 1440)]
    [InlineData(1920, 1080)]
    public void A_magazines_count_is_read_right_of_its_mark_and_its_last_one_is_its_own(int width, int height)
    {
        Assert.Equal(new DeskPrint(4, 41), DeskReader.PrintOf("4/41", lineRead: false));
        Assert.Equal(new DeskPrint(3, 3), DeskReader.PrintOf("3/31", lineRead: true));
        var page = new StashReaderTests.Page(width, height).Start();
        var p = page.Pitch;
        int x = (int)(800 * page.K), y = (int)(300 * page.K);
        page.Grid(x, y, 1, 2, 1);
        page.Grid(x + 3 * p, y, 1, 2, 2);
        using (var canvas = new SKCanvas(page.Bitmap))
        using (var green = new SKPaint { Color = new SKColor(101, 144, 85) })
        using (var glyph = new SKPaint { Color = new SKColor(225, 235, 232) })
        {
            canvas.DrawRect(x + 0.08f * p, y + 2 * p - 0.25f * p, 0.18f * p, 0.18f * p, green);
            canvas.DrawRect(x + 0.12f * p, y + 2 * p - 0.22f * p, 0.08f * p, 0.11f * p, glyph);
        }
        var stashCell = SKRectI.Create(x, y, p, 2 * p);
        var windowCell = SKRectI.Create(x + 3 * p, y, p, 2 * p);
        Assert.InRange(DeskReader.MagazineMark(page.Bitmap, stashCell, p)!.Value, x + (int)(0.25 * p), x + (int)(0.3 * p));
        Assert.Null(DeskReader.MagazineMark(page.Bitmap, windowCell, p));
        Assert.False(DeskReader.TwoPixelLine(page.Bitmap, stashCell, p));
        Assert.True(DeskReader.TwoPixelLine(page.Bitmap, windowCell, p));
    }

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

    // The owner's Keys window of 2026-10-19 (20:22, after the game's SORT): its lattice was snapped onto the window's thin
    // bright outline, 6 px left of its first cells; every line between its cells was missed and its keys read as one odd
    // shape. Aligned moves a lattice onto the lines between its cells; one already on them stays put. The grid finding does
    // it for every window, so the quest marks' reader and the desk read one lattice.
    [Fact]
    public void A_windows_lattice_off_its_cells_moves_onto_their_lines()
    {
        const int pitch = 84;
        using var bitmap = new SKBitmap(1400, 900);
        using (var canvas = new SKCanvas(bitmap))
        using (var outline = new SKPaint { Color = new SKColor(200, 170, 90), Style = SKPaintStyle.Stroke, StrokeWidth = 1 })
        using (var field = new SKPaint { Color = Field })
        using (var line = new SKPaint { Color = Line })
        {
            canvas.Clear(new SKColor(18, 20, 22));
            // The window's outline, one bright pixel, 6 px outside its grid of 11 by 7 with two-pixel lines.
            canvas.DrawRect(SKRect.Create(110.5f, 94.5f, 11 * pitch + 13, 7 * pitch + 13), outline);
            canvas.DrawRect(116, 100, 11 * pitch + 2, 7 * pitch + 2, field);
            for (var i = 0; i <= 11; i++)
                canvas.DrawRect(116 + i * pitch, 100, 2, 7 * pitch + 2, line);
            for (var j = 0; j <= 7; j++)
                canvas.DrawRect(116, 100 + j * pitch, 11 * pitch + 2, 2, line);
        }
        var lines = new StashLines(StashGray.Of(bitmap));
        var off = StashGrids.Aligned(lines, new StashGrid(110, 100, 11, 7, StashGridPlace.Window), pitch);
        Assert.InRange(off.X0, 115, 117);
        Assert.Equal(100, off.Y0);
        var on = StashGrids.Aligned(lines, new StashGrid(116, 100, 11, 7, StashGridPlace.Window), pitch);
        Assert.Equal(new StashGrid(116, 100, 11, 7, StashGridPlace.Window), on);
        // The lattice and the frame found on its lines, a few pixels apart, are one compartment: never read twice.
        Assert.True(DeskLayout.Overlapping(SKRectI.Create(110, 100, 11 * pitch, 7 * pitch), SKRectI.Create(116, 100, 11 * pitch, 7 * pitch)));
        Assert.False(DeskLayout.Overlapping(SKRectI.Create(100, 100, pitch, 2 * pitch), SKRectI.Create(100 + pitch + 8, 100, pitch, 2 * pitch)));
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
    // Without the backpack's grid (scrolled out of view), and with its slot empty, for the region read in part.
    private static StashReaderTests.Page Gear(bool backpackGrid = true, string? backpackItem = "Pindrake", bool earpieceMark = false)
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
        page.Item(87, 220, 1, 0, 0, 2, 2, "Zorblat", mark: earpieceMark);
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
        if (backpackItem is not null)
            page.Item(876, 360, 1, 0, 0, 2, 2, backpackItem);
        else
        {
            // An empty slot: its frame, nothing in it.
            using var canvas = new SKCanvas(page.Bitmap);
            using var field = new SKPaint { Color = Field };
            canvas.DrawRect(877, 361, 2 * p - 1, 2 * p - 1, field);
        }
        if (backpackGrid)
        {
            page.Grid(1051, 360, 5, 4, 1);
            page.Item(1051, 360, 1, 0, 0, 1, 2, "Tessaril");
        }
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

    // The owner's picture of 2026-10-27 at the stash's top (04 of the real set): a tomahawk one by three and a helmet two by
    // two in the last rows in view, their feet under the grid's end, said nothing (their footprints taller than what
    // showed). In the stash's last row an item whose bottom shows no line runs on below the view: a footprint as wide and
    // taller is its (layout round, 2026-10-28).
    [Theory]
    [InlineData(2560, 1440)]
    [InlineData(1920, 1080)]
    public async Task An_item_in_the_stashs_last_row_running_on_below_the_view_is_named_by_its_taller_footprint(int width, int height)
    {
        var page = new StashReaderTests.Page(width, height).Start();
        var (x, y) = page.StashOrigin;
        var p = page.Pitch;
        page.Grid(x, y, 10, 13, 1);
        page.Item(x, y, 1, 2, 4, 1, 1, "Hollin");
        page.Item(x, y, 1, 5, 11, 1, 2, "Tessaril");
        // The pane's foot over the thirteenth row: the view ends at the twelfth.
        using (var canvas = new SKCanvas(page.Bitmap))
        using (var foot = new SKPaint { Color = new SKColor(18, 20, 22) })
            canvas.DrawRect(x - 4, y + 12 * p + 2, 10 * p + 8, p + 4, foot);
        using var picture = page.Bitmap.Copy();
        var reading = await Reader().ReadAsync(picture, new DeskCatalogue(Catalogue), DateTimeOffset.MinValue, Ct);
        var stash = reading.Page!.Region("stash")!;
        Assert.Equal(12, stash.Rows);
        var tall = In(reading.Page, "stash", c => c.J == 11);
        Assert.Equal([Id(4)], tall.Ids);
        Assert.Equal(5, tall.I);
    }

    // A gun in the stash labelled with its preset's name (owner, 2026-10-26: the holster's "USP .45 Expert" in the test set's
    // 09-gear.png, read whole and named nothing; "DVL-10 Urbana" six by two in the stash): the gun, as a gun's cell is, its
    // mods unseen ("+"); an item whose label begins with a name that is no gun's stays unread.
    [Fact]
    public async Task A_cell_labelled_with_a_weapon_presets_name_is_the_gun()
    {
        var page = new StashReaderTests.Page(2560, 1440).Start();
        var (x, y) = page.StashOrigin;
        page.Grid(x, y, 10, 12, 1);
        page.Item(x, y, 1, 2, 1, 3, 1, "Varkel .45 Expert");
        page.Item(x, y, 1, 0, 4, 2, 1, "Hollin ammo pack");
        using var picture = page.Bitmap.Copy();
        var catalogue = new DeskCatalogue([.. Catalogue, Item(20, "Varkel .45", 2, 1, DeskHolds.Mods, slots: DeskSlotKind.Weapon), Item(21, "Varkel45 std", 1, 2)]);
        var reading = await Reader().ReadAsync(picture, catalogue, DateTimeOffset.MinValue, Ct);
        var gun = In(reading.Page!, "stash", c => c.J == 1);
        Assert.Equal([Id(20)], gun.Ids);
        Assert.Equal((2, 1, 3, 1), (gun.I, gun.J, gun.W, gun.H));
        Assert.Equal(DeskHolds.Mods, gun.Holds);
        Assert.Empty(In(reading.Page!, "stash", c => c.J == 4).Ids);
    }

    // A gun's cell prints its magazine's rounds where a condition stands (the owner's screenshots: "10/10" on the DVL-10,
    // "0/17" on an M9A3): read as the game prints it, never a condition (docs/DESIGN.md §4, *The cell*, "~"): the figure is
    // the bare gun's, "~" and "+", at "5/10" as at none. An armour's condition beside it still scales its figure.
    [Fact]
    public async Task A_guns_rounds_are_no_condition_and_an_armours_condition_still_counts()
    {
        var page = new StashReaderTests.Page(2560, 1440).Start();
        var (x, y) = page.StashOrigin;
        page.Grid(x, y, 10, 12, 1);
        page.Item(x, y, 1, 2, 1, 3, 1, "Varkel .45", "5/10");
        page.Item(x, y, 1, 0, 4, 2, 2, "Brestag", "23/46");
        using var picture = page.Bitmap.Copy();
        var catalogue = new DeskCatalogue([.. Catalogue, Item(30, "Varkel .45", 3, 1, DeskHolds.Mods, slots: DeskSlotKind.Weapon), Item(31, "Brestag", 2, 2)]);
        var reading = await Reader().ReadAsync(picture, catalogue, DateTimeOffset.MinValue, Ct);
        var gun = In(reading.Page!, "stash", c => c.J == 1);
        Assert.Equal([Id(30)], gun.Ids);
        Assert.Equal((DeskHolds.Mods, new DeskPrint(5, 10)), (gun.Holds, gun.Print));
        var armour = In(reading.Page!, "stash", c => c.J == 4);
        Assert.Equal([Id(31)], armour.Ids);
        Assert.Equal(new DeskPrint(23, 46), armour.Print);

        var facts = new Dictionary<string, DeskItemFacts>
        {
            [Id(30)] = new(Id(30)) { W = 3, Kind = DeskItemKind.Gun, LastLow = 40_000, BasePrice = 20_000, Traders = [("trader-a", 9_000)] },
            [Id(31)] = new(Id(31)) { W = 2, H = 2, Kind = DeskItemKind.Armour, LastLow = 60_000, BasePrice = 30_000, Traders = [("trader-a", 10_000)] },
        };
        var cells = DeskSheet.Of(reading.Page!, id => facts.GetValueOrDefault(id), new DeskFleaRules(0.05, 0.05)).Regions.Single(r => r.Region.Key == "stash").Cells;
        var gunFigure = cells.Single(c => c.Cell.J == 1).Figure!;
        Assert.Equal((40_000d, 40_000d, true, true), (gunFigure.Value, gunFigure.Full, gunFigure.Estimate, gunFigure.Plus));
        var armourFigure = cells.Single(c => c.Cell.J == 4).Figure!;
        Assert.Equal((30_000d, 60_000d, true), (armourFigure.Value, armourFigure.Full, armourFigure.Estimate));
    }

    // ---- PaddleOCR where Windows' recognition reads nothing (owner, 2026-10-26, decision 1) ----

    /// <summary>A second reader that hands back one text, and keeps each line's size it was given.</summary>
    internal sealed class Lines(string? text) : ILineReader
    {
        public List<(int W, int H)> Read { get; } = [];

        public Task<string?> ReadAsync(SKBitmap line, CancellationToken ct)
        {
            lock (Read)
                Read.Add((line.Width, line.Height));
            return Task.FromResult(text);
        }
    }

    // A stash page with two labels Windows' recognition reads and one that fits no name ("Xqvzzt"), a count on one of them.
    private static StashReaderTests.Page Unread()
    {
        var page = new StashReaderTests.Page(2560, 1440).Start();
        var (x, y) = page.StashOrigin;
        page.Grid(x, y, 10, 12, 1);
        page.Item(x, y, 1, 2, 1, 1, 1, "Hollin");
        page.Item(x, y, 1, 4, 2, 2, 1, "Drevak", "330");
        page.Item(x, y, 1, 0, 5, 1, 1, "Xqvzzt");
        return page;
    }

    private static DeskReader Reader(ILineReader paddle) =>
        DeskReader.Create("en", paddle) ?? throw new InvalidOperationException("no recognition (Reader() skips first)");

    // Only the label band Windows' recognition left unread goes to PaddleOCR, once, as the band at its own size in a narrow
    // margin; never a label Windows named, never a count. Its text is matched as Windows' is: "Quenvik" names the cell.
    [Fact]
    public async Task Paddle_reads_only_a_label_band_windows_left_unread_and_its_text_is_matched_as_windows_is()
    {
        Reader();
        var page = Unread();
        using var picture = page.Bitmap.Copy();
        var paddle = new Lines("Quenvik");
        var reading = await Reader(paddle).ReadAsync(picture, new DeskCatalogue(Catalogue), DateTimeOffset.MinValue, Ct);
        Assert.Equal([Id(5)], In(reading.Page!, "stash", c => c.J == 1).Ids);
        var drevak = In(reading.Page!, "stash", c => c.J == 2);
        Assert.Equal([Id(6)], drevak.Ids);
        Assert.Equal(330, drevak.Count);
        Assert.Equal([Id(2)], In(reading.Page!, "stash", c => c.J == 5).Ids);
        var line = Assert.Single(paddle.Read);
        Assert.Equal((int)Math.Round(0.2 * page.Pitch) + 8, line.H);
    }

    // PaddleOCR's text that fits no name, or no text, leaves the cell as Windows' reading left it: nothing said.
    [Theory]
    [InlineData("Zzqqxv")]
    [InlineData("")]
    [InlineData(null)]
    public async Task A_band_paddle_reads_as_nothing_known_stays_unread(string? text)
    {
        Reader();
        using var picture = Unread().Bitmap.Copy();
        var paddle = new Lines(text);
        var reading = await Reader(paddle).ReadAsync(picture, new DeskCatalogue(Catalogue), DateTimeOffset.MinValue, Ct);
        Assert.Empty(In(reading.Page!, "stash", c => c.J == 5).Ids);
        Assert.Equal([Id(5)], In(reading.Page!, "stash", c => c.J == 1).Ids);
        Assert.Single(paddle.Read);
    }

    // A model that can't be loaded (gone from beside the exe, damaged): the reading is Windows' alone, and the failure is
    // said once however many bands and readings ask for it.
    [Fact]
    public async Task A_model_that_cant_be_loaded_leaves_the_reading_as_windows_alone_reads_it()
    {
        var windows = Reader();
        var failed = new List<string>();
        using var paddle = new PaddleReader(Path.Combine(_root, "none.onnx"), Path.Combine(_root, "none.txt"), failed: failed.Add);
        var page = Unread();
        page.Item(page.StashOrigin.X, page.StashOrigin.Y, 1, 6, 7, 1, 1, "Vvqqzz");
        using var picture = page.Bitmap.Copy();
        var catalogue = new DeskCatalogue(Catalogue);
        var alone = await windows.ReadAsync(picture, catalogue, DateTimeOffset.MinValue, Ct);
        var reader = Reader(paddle);
        foreach (var _ in Enumerable.Range(0, 2))
        {
            var reading = await reader.ReadAsync(picture, catalogue, DateTimeOffset.MinValue, Ct);
            Assert.Equal(alone.Page!.Region("stash")!.Cells.Select(c => (c.I, c.J, string.Join(",", c.Ids))),
                reading.Page!.Region("stash")!.Cells.Select(c => (c.I, c.J, string.Join(",", c.Ids))));
        }
        Assert.Single(failed);
    }

    // ---- the layout's faults on the owner's screenshots of 2026-10-19 (owner, 2026-10-25) ----
    // The layout from words given as the recognition reads them (boxes at 1440p, 15 px a heading) over drawn frames: the
    // rules held without the recognition's moods. Each test fails on the reading of before 2026-10-25.

    private static DeskWord Word(string text, int x, int y, int line, int h = 15) => new(text, x, y, (int)Math.Round(0.75 * h * text.Length) + 4, h, line);

    // The GEAR tab without its middle column: four loadout headings over their boxes, the stash at the right, QUICK USE at
    // the foot.
    private static (StashReaderTests.Page Page, List<DeskWord> Words) Tab()
    {
        var page = new StashReaderTests.Page(2560, 1440).Start();
        page.Grid(87, 220, 2, 2, 1);
        page.Grid(333, 220, 2, 2, 1);
        page.Grid(578, 220, 2, 2, 1);
        page.Grid(333, 441, 2, 2, 1);
        var (x, y) = page.StashOrigin;
        page.Grid(x, y, 10, 12, 1);
        List<DeskWord> words =
        [
            Word("EARPIECE", 92, 201, 1), Word("HEADWEAR", 337, 201, 2), Word("ARMBAND", 91, 422, 4), Word("BODY", 338, 422, 5), Word("ARMOR", 390, 422, 5),
            Word("QUICK", 760, 1254, 9), Word("USE", 820, 1254, 9),
        ];
        return (page, words);
    }

    private static DeskLayoutFound Layout(StashReaderTests.Page page, IReadOnlyList<DeskWord> words)
    {
        var gray = StashGray.Of(page.Bitmap);
        return DeskLayout.Find(words, new StashLines(gray), gray, page.Width, page.Height, page.Pitch);
    }

    // A heading word one letter off still counts (owner, 2026-10-25), in capitals; ON BACK's "ON" must be read as it is, and
    // the screen's own BACK button is no BACKPACK.
    [Theory]
    [InlineData("*ACE", "FACE", true)]
    [InlineData("ÄRMBANI", "ARMBAND", true)]
    [InlineData("HOLSTFR", "HOLSTER", true)]
    [InlineData("POCKET", "POCKETS", true)]
    [InlineData("BACKPACKS", "BACKPACK", true)]
    [InlineData("Armband", "ARMBAND", false)]
    [InlineData("BACK", "BACKPACK", false)]
    [InlineData("POUCHES", "POUCH", false)]
    [InlineData("OM", "ON", false)]
    [InlineData("RG", "RIG", false)]
    public void A_heading_word_one_letter_off_still_counts(string read, string heading, bool counts) =>
        Assert.Equal(counts, DeskLayout.HeadingWord(read, Core.Screenshots.ExitList.Fold(heading)));

    // The test set's 12-gear.png (2026-10-19): FACE COVER read as "*ACE COVER", no heading, and the slot box under it,
    // the word as tall as a window's title, became the window "*ACE COVER".
    [Fact]
    public void A_heading_read_a_letter_off_is_the_heading_and_its_slot_box_no_window()
    {
        var (page, words) = Tab();
        words.AddRange([Word("*ACE", 583, 201, 3), Word("COVER", 630, 201, 3)]);
        var layout = Layout(page, words);
        Assert.Equal(DeskScreen.Gear, layout.Screen);
        Assert.DoesNotContain(layout.Regions, r => r.Kind == DeskRegionKind.ContainerWindow);
        Assert.InRange(Assert.Single(layout.Slots, s => s.Key == "facecover").Box.Left, 574, 582);
    }

    // Whatever a word reads where a loadout heading stands, it is no window's title; after the magnifier it is: the Keys
    // window dragged over the loadout, its bar level with ARMBAND (the test set's 00-gear-armband.png).
    [Fact]
    public void A_word_where_a_loadout_heading_stands_is_a_title_only_after_the_magnifier()
    {
        var (page, words) = Tab();
        words.AddRange([Word("Fxqe", 583, 201, 3), Word("Cqvfr", 630, 201, 3)]);
        Assert.DoesNotContain(Layout(page, words).Regions, r => r.Kind == DeskRegionKind.ContainerWindow);
        page.Grid(181, 442, 4, 3, 2);
        words.AddRange([Word("o", 195, 419, 6, 14), Word("Keys", 222, 417, 6, 18)]);
        Assert.Equal("Keys", Assert.Single(Layout(page, words).Regions, r => r.Kind == DeskRegionKind.ContainerWindow).Title);
    }

    // In the middle column a bare word in capitals, its bar's glyphs unread, is no title (a misread heading over a frame); a
    // container's name in small letters still is, by its size (the Zhuk window over the column on the owner's GEAR pictures
    // of 2026-10-19).
    [Fact]
    public void In_the_middle_column_a_title_in_capitals_needs_its_bars_glyphs()
    {
        var (page, words) = Tab();
        words.Add(Word("POCKETS", 880, 192, 7, 14));
        page.Grid(875, 214, 1, 1, 1);
        page.Grid(1100, 640, 4, 2, 1);
        words.Add(Word("XKTRV", 1110, 615, 8));
        var layout = Layout(page, words);
        Assert.DoesNotContain(layout.Regions, r => r.Kind == DeskRegionKind.ContainerWindow);
        words[^1] = Word("Zorblat", 1110, 615, 8);
        Assert.Equal("Zorblat", Assert.Single(Layout(page, words).Regions, r => r.Kind == DeskRegionKind.ContainerWindow).Title);
    }

    // The test set's 02-gear.png and 09-gear.png (2026-10-19): the POUCH's grid, its lines two pixels wide as a window's, was
    // taken for a window of no name (5 cells attached to nothing), and the POUCH left out.
    [Fact]
    public void A_lattice_under_a_middle_heading_is_that_regions_grid_never_a_window()
    {
        var (page, words) = Tab();
        words.Add(Word("POUCH", 880, 700, 7, 14));
        page.Grid(875, 722, 2, 2, 1);
        page.Grid(1051, 722, 3, 3, 2);
        var layout = Layout(page, words);
        Assert.DoesNotContain(layout.Regions, r => r.Kind == DeskRegionKind.ContainerWindow);
        var pouch = Assert.Single(layout.Regions, r => r.Kind == DeskRegionKind.Pouch);
        Assert.InRange(pouch.SlotBox!.Value.Left, 872, 878);
        var grid = Assert.Single(pouch.Parts);
        Assert.Equal((1051, 3, 3), (grid.Left, (int)Math.Round(grid.Width / 84.0), (int)Math.Round(grid.Height / 84.0)));
        Assert.False(pouch.Cut);
    }

    // The test set's 11-gear.png and 12-gear.png (2026-10-19): the backpack's grid ran past the column's foot; its frames,
    // running on, left the region out, and the backpack came back as its slot alone. Read as far as it shows (the whole
    // rows in view), it is said cut: the page keeps the rest (DeskLedger).
    [Fact]
    public void A_grid_the_columns_foot_cuts_is_read_as_far_as_it_shows_and_said_cut()
    {
        var (page, words) = Tab();
        words.Add(Word("BACKPACK", 880, 842, 7));
        page.Grid(875, 864, 2, 2, 1);
        page.Grid(1051, 864, 5, 6, 1);
        // The column's view ends a few pixels above the QUICK USE bar's top: nothing of it below.
        using (var canvas = new SKCanvas(page.Bitmap))
        using (var ground = new SKPaint { Color = new SKColor(18, 20, 22) })
            canvas.DrawRect(840, 1265, 800, 175, ground);
        var backpack = Assert.Single(Layout(page, words).Regions, r => r.Kind == DeskRegionKind.Backpack);
        Assert.True(backpack.Cut);
        Assert.NotNull(backpack.SlotBox);
        var grid = Assert.Single(backpack.Parts);
        Assert.Equal((5, 4), ((int)Math.Round(grid.Width / 84.0), (int)Math.Round(grid.Height / 84.0)));
    }

    // The test set's 13-gear-keys.png (2026-10-19): BACKPACK's slot and grid and POUCH's heading under the Keys window, and a
    // cell of the POUCH's grid below it, taken for the BACKPACK's grid of one cell. A frame whose heading a window may hide
    // is placed under none; the container's slot box from its heading's place lies under the window (covered: the page
    // keeps its grid).
    [Fact]
    public void A_frame_whose_heading_a_window_may_hide_is_placed_under_none()
    {
        var (page, words) = Tab();
        words.Add(Word("BACKPACK", 880, 342, 7));
        page.Grid(818, 439, 6, 4, 2);
        words.AddRange([Word("o", 832, 414, 8, 14), Word("Keys", 859, 414, 8, 17), Word("x", 1300, 414, 8, 14)]);
        page.Grid(1220, 900, 1, 1, 1);
        var layout = Layout(page, words);
        var window = Assert.Single(layout.Regions, r => r.Kind == DeskRegionKind.ContainerWindow);
        var backpack = Assert.Single(layout.Regions, r => r.Kind == DeskRegionKind.Backpack);
        Assert.Empty(backpack.Parts);
        Assert.True(backpack.SlotDerived);
        Assert.True(window.Area!.Value.IntersectsWith(backpack.SlotBox!.Value));
        Assert.Equal(1, layout.Unplaced);
    }

    // ---- the layout round of 2026-10-28 (the real set of 2026-10-27 made a scored check) ----

    // A word at 1440p's place, scaled to the page (the game lays its screens out by their height).
    private static DeskWord Scaled(StashReaderTests.Page page, string text, int x, int y, int line, int h = 15) =>
        Word(text, (int)Math.Round(x * page.K), (int)Math.Round(y * page.K), line, Math.Max(9, (int)Math.Round(h * page.K)));

    private static int At(StashReaderTests.Page page, int at1440) => (int)Math.Round(at1440 * page.K);

    // The owner's body-loot picture of 2026-10-25 (r1 of the real set): in a raid the BACKPACK's and the POUCH's grids, their
    // lines two pixels wide as a window's, were read as two windows of no name, 20 of its 55 items in neither region. A
    // lattice under a middle heading is that region's grid in a raid as in the menus.
    [Theory]
    [InlineData(2560, 1440)]
    [InlineData(1920, 1080)]
    public void In_a_raid_a_lattice_under_a_middle_heading_is_that_regions_grid_never_a_window(int width, int height)
    {
        var page = new StashReaderTests.Page(width, height).Start();
        var p = page.Pitch;
        List<DeskWord> words =
        [
            Scaled(page, "EARPIECE", 92, 201, 1), Scaled(page, "HEADWEAR", 337, 201, 2), Scaled(page, "ARMBAND", 91, 422, 4),
            Scaled(page, "BODY", 338, 422, 5), Scaled(page, "ARMOR", 390, 422, 5), Scaled(page, "BACKPACK", 880, 422, 6),
            Scaled(page, "POUCH", 880, 989, 7, 14), Scaled(page, "QUICK", 760, 1254, 9), Scaled(page, "USE", 820, 1254, 9),
        ];
        page.Grid(At(page, 87), At(page, 220), 2, 2, 1);
        page.Grid(At(page, 875), At(page, 443), 2, 2, 1);
        page.Grid(At(page, 1051), At(page, 445), 5, 6, 2);
        page.Grid(At(page, 875), At(page, 1011), 2, 2, 1);
        page.Grid(At(page, 1051), At(page, 1011), 3, 3, 2);
        var layout = Layout(page, words);
        Assert.Equal(DeskScreen.Raid, layout.Screen);
        Assert.DoesNotContain(layout.Regions, r => r.Kind == DeskRegionKind.ContainerWindow);
        var backpack = Assert.Single(Assert.Single(layout.Regions, r => r.Kind == DeskRegionKind.Backpack).Parts);
        Assert.Equal((5, 6), ((int)Math.Round(backpack.Width / (double)p), (int)Math.Round(backpack.Height / (double)p)));
        var pouch = Assert.Single(Assert.Single(layout.Regions, r => r.Kind == DeskRegionKind.Pouch).Parts);
        Assert.Equal((3, 3), ((int)Math.Round(pouch.Width / (double)p), (int)Math.Round(pouch.Height / (double)p)));
    }

    // The owner's airdrop picture of 2026-10-27 (r2): the container's kind at the LOOT pane's head, "AIRDROP COMMON SUPPLY
    // CRATE", starts left of the screen's right tenth; its first word was taken for a window's title over the crate's grid,
    // and the kind was "SUPPLY CRATE". The kind is its whole phrase, and no title.
    [Theory]
    [InlineData(2560, 1440)]
    [InlineData(1920, 1080)]
    public void The_loot_panes_kind_is_its_whole_phrase_and_no_windows_title(int width, int height)
    {
        var page = new StashReaderTests.Page(width, height).Start();
        List<DeskWord> words =
        [
            Scaled(page, "EARPIECE", 92, 201, 1), Scaled(page, "HEADWEAR", 337, 201, 2), Scaled(page, "ARMBAND", 91, 422, 4),
            Scaled(page, "BODY", 338, 422, 5), Scaled(page, "ARMOR", 390, 422, 5), Scaled(page, "QUICK", 760, 1254, 9), Scaled(page, "USE", 820, 1254, 9),
            Scaled(page, "LOOT", 1690, 80, 10), Scaled(page, "SEARCH", 1880, 80, 10, 13), Scaled(page, "AIRDROP", 2155, 76, 10, 16),
            Scaled(page, "COMMON", 2262, 76, 10, 16), Scaled(page, "SUPPLY", 2359, 76, 10, 16), Scaled(page, "CRATE", 2449, 76, 10, 16),
        ];
        page.Grid(At(page, 87), At(page, 220), 2, 2, 1);
        page.Grid(At(page, 2107), At(page, 106), 4, 6, 2);
        var layout = Layout(page, words);
        Assert.Equal(DeskScreen.Raid, layout.Screen);
        Assert.DoesNotContain(layout.Regions, r => r.Kind == DeskRegionKind.ContainerWindow);
        Assert.Equal("LOOT · AIRDROP COMMON SUPPLY CRATE", layout.Loot!.Title);
        var grid = Assert.Single(Assert.Single(layout.Loot.Parts).Parts);
        Assert.Equal((4, 6), ((int)Math.Round(grid.Width / (double)page.Pitch), (int)Math.Round(grid.Height / (double)page.Pitch)));
    }

    // The same picture: the crate's grid faint, its frames found in two pieces, one with its top on the head's edge 5 px over
    // the grid's line, so the two stood off one lattice and made two compartments read as two grids of other places. A frame
    // whose top stands a few pixels over the grid's first line, which is 10 px (at 1440p) under the head, is on its rows.
    [Fact]
    public void A_containers_frames_a_few_pixels_over_its_grids_first_line_are_on_its_rows()
    {
        var head = Word("LOOT", 1690, 80, 1, 16);
        SKRectI[] frames = [new(2107, 100, 2444, 1111), new(1686, 442, 2190, 1112)];
        var loot = DeskLoot.Find([head], head, frames, [], 2560, 1275, 84);
        var grid = Assert.Single(Assert.Single(loot.Parts).Parts);
        Assert.Equal(new SKRectI(1686, 106, 2444, 1112), grid);
    }

    // The owner's picture of 2026-10-26 of the stash's weapons and mods (03): two medkits side by side, both "Meds"; the
    // first's last letter read on its own stood before the second's label like a window's magnifier, and a frame there made
    // "Meds" a window of one cell over the stash. Over the stash a window shows its panel or its own grid's two-pixel lines
    // under the title (A_containers_window_is_read_attached_and_what_it_covers_is_left_as_it_was: it does).
    [Fact]
    public void In_the_stash_a_label_after_a_letter_read_alone_is_no_windows_title()
    {
        var (page, words) = Tab();
        var (x, y) = page.StashOrigin;
        page.Grid(x + 8 * page.Pitch, y + 4 * page.Pitch + 37, 1, 1, 1);
        words.AddRange([Word("s", x + 7 * page.Pitch + 25, y + 4 * page.Pitch + 4, 12, 12), Word("Meds", x + 7 * page.Pitch + 45, y + 4 * page.Pitch + 4, 12, 12)]);
        Assert.DoesNotContain(Layout(page, words).Regions, r => r.Kind == DeskRegionKind.ContainerWindow);
    }

    // The AVS rig (the owner's pictures 02 and 03 of 2026-10-26): five pockets over five, and two wide ones set between
    // them, centred. Placed by each left edge after the one before it, the wide ones' edges added two columns: seven wide,
    // the second pocket a column off. A compartment set between the columns takes the column it stands in.
    [Theory]
    [InlineData(84, 9)]
    [InlineData(63, 7)]
    public void A_rigs_wide_pockets_set_between_its_columns_add_none(int pitch, int gap)
    {
        var x0 = 1050;
        var spans = Enumerable.Range(0, 5).Select(i => (From: x0 + i * (pitch + gap), To: x0 + i * (pitch + gap) + pitch)).ToList();
        var wide = 2 * pitch + gap;
        spans.Add((x0 + (pitch + gap) / 2, x0 + (pitch + gap) / 2 + wide));
        spans.Add((x0 + 2 * (pitch + gap) + (pitch + gap) / 2, x0 + 2 * (pitch + gap) + (pitch + gap) / 2 + wide));
        var units = DeskReader.Units(spans, pitch);
        Assert.Equal([0, 1, 2, 3, 4], spans.Take(5).Select(s => units[s.From]));
        Assert.Equal((0, 2), (units[spans[5].From], units[spans[6].From]));
        // Pockets with one gone between them still stand where they stand.
        var missing = DeskReader.Units([(x0, x0 + pitch), (x0 + 2 * (pitch + gap), x0 + 2 * (pitch + gap) + pitch)], pitch);
        Assert.Equal(2, missing[x0 + 2 * (pitch + gap)]);
    }

    // The test set's 11-gear.png (2026-10-19): the backpack's grid out of view, its slot alone in it. Read in part: the
    // page keeps its grid (DeskLedger). An empty slot in view says the container is gone, and its grid with it.
    [Fact]
    public async Task A_containers_slot_without_its_grid_in_view_is_read_in_part_and_an_empty_slot_is_not()
    {
        var reader = Reader();
        using (var picture = Gear(backpackGrid: false).Bitmap.Copy())
        {
            var page = (await reader.ReadAsync(picture, new DeskCatalogue(Catalogue), DateTimeOffset.MinValue, Ct)).Page!;
            Assert.True(page.Region("backpack")!.Partial);
            Assert.Equal([Id(3)], In(page, "backpack", c => c.Slot == "backpack").Ids);
            Assert.False(page.Region("pockets")!.Partial);
        }
        using (var picture = Gear(backpackGrid: false, backpackItem: null).Bitmap.Copy())
        {
            var page = (await reader.ReadAsync(picture, new DeskCatalogue(Catalogue), DateTimeOffset.MinValue, Ct)).Page!;
            Assert.False(page.Region("backpack")!.Partial);
            Assert.Empty(page.Region("backpack")!.Cells);
        }
    }

    // ---- the owner's second test of 2026-10-25 (step 1b): windows by their panels, what lies under them, the tooltip ----

    // A container's window as the game draws it (the owner's screenshots of 2026-10-25 at 1440p): a one-pixel grey outline
    // with four pixels of black inside it, the bar (25 grey) at its top, and its grid with two-pixel lines 34 px under the
    // outline's top and 5 px in from its left. The grid's top left.
    private static (int X, int Y) Panel(StashReaderTests.Page page, int x, int y, int cols, int rows)
    {
        var p = page.Pitch;
        int gx = x + 5, gy = y + 34;
        int right = gx + cols * p + 7, bottom = gy + rows * p + 7;
        using (var canvas = new SKCanvas(page.Bitmap))
        using (var outline = new SKPaint { Color = new SKColor(88, 93, 96) })
        using (var black = new SKPaint { Color = SKColors.Black })
        using (var bar = new SKPaint { Color = new SKColor(25, 26, 27) })
        {
            canvas.DrawRect(x, y, right - x + 1, bottom - y + 1, outline);
            canvas.DrawRect(x + 1, y + 1, right - x - 1, bottom - y - 1, black);
            canvas.DrawRect(x + 4, y + 3, right - x - 7, 27, bar);
        }
        page.Grid(gx, gy, cols, rows, 2);
        return (gx, gy);
    }

    // The window's bar as the recognition reads it: the magnifier, and the title right of it.
    private static IEnumerable<DeskWord> Bar(int x, int y, string title, int line) =>
        [Word("P", x + 10, y + 7, line, 19), Word(title, x + 48, y + 10, line, 15)];

    // Picture 16 of the owner's second test (2026-10-25): the POUCH's grid right under the Beta 2 window's lower edge was taken for
    // three more rows of the window (5×8 for its 5×6), its frames touching the window's; the grid's top row under the window.
    // The window's compartments are found inside its panel only: the POUCH's grid is the POUCH's.
    [Fact]
    public void A_windows_compartments_stop_at_its_panel_and_a_grid_under_its_lower_edge_is_the_pages()
    {
        var (page, words) = Tab();
        words.Add(Word("POUCH", 880, 909, 7, 14));
        page.Grid(875, 930, 2, 2, 1);
        page.Grid(1051, 930, 3, 3, 1);
        // The window over the pouch grid's top row: its outline at 993, the grid's second row's line at 1014.
        Panel(page, 1063, 446, 5, 6);
        words.AddRange(Bar(1063, 446, "Beta", 8));
        var layout = Layout(page, words);
        var window = Assert.Single(layout.Regions, r => r.Kind == DeskRegionKind.ContainerWindow);
        var part = Assert.Single(window.Parts);
        Assert.Equal((5, 6), ((int)Math.Round(part.Width / 84.0), (int)Math.Round(part.Height / 84.0)));
        Assert.InRange(window.Area!.Value.Bottom, 990, 997);
        Assert.InRange(window.Area!.Value.Left, 1060, 1064);
        // The POUCH's grid, its top row under the window and its corner beside it, is the POUCH's, three by three.
        var pouch = Assert.Single(layout.Regions, r => r.Kind == DeskRegionKind.Pouch);
        var grid = Assert.Single(pouch.Parts);
        Assert.Equal((1051, 3, 3), (grid.Left, (int)Math.Round(grid.Width / 84.0), (int)Math.Round(grid.Height / 84.0)));
    }

    // Pictures 14 to 16 of the owner's second test (2026-10-25): the backpack's grid under the Beta 2 window, its top row above
    // the window and its left side beside it, was "not in view". Found by its corner and its sides that show, as tall as its
    // left side runs (not on down the POUCH grid's line under it).
    [Fact]
    public void A_grid_a_window_lies_over_is_found_by_its_sides_that_show_as_tall_as_they_run()
    {
        var (page, words) = Tab();
        words.Add(Word("BACKPACK", 880, 342, 7));
        words.Add(Word("POUCH", 880, 909, 9, 14));
        page.Grid(875, 364, 2, 2, 1);
        page.Grid(1051, 364, 5, 6, 1);
        page.Grid(875, 930, 2, 2, 1);
        page.Grid(1051, 930, 3, 3, 1);
        Panel(page, 1063, 446, 5, 6);
        words.AddRange(Bar(1063, 446, "Beta", 8));
        var layout = Layout(page, words);
        var backpack = Assert.Single(layout.Regions, r => r.Kind == DeskRegionKind.Backpack);
        var grid = Assert.Single(backpack.Parts);
        Assert.Equal((5, 6), ((int)Math.Round(grid.Width / 84.0), (int)Math.Round(grid.Height / 84.0)));
        Assert.False(backpack.Cut);
    }

    // Picture 14 of the owner's second test (2026-10-25): the pistol's rounds "0/17" in the stash, its slashed zero read as the
    // letter Ø, made the window "Ø/17". A title needs two letters that are no digit's lookalike.
    [Theory]
    [InlineData("Ø/17", false)]
    [InlineData("0/10", false)]
    [InlineData("SØ/S", false)]
    [InlineData("Beta", true)]
    [InlineData("Keys", true)]
    [InlineData("SICC", true)]
    [InlineData("Zhuk", true)]
    public void A_title_needs_two_letters_that_are_no_digits_lookalike(string text, bool title) =>
        Assert.Equal(title, DeskLayout.TitleLetters(text) >= 2);

    // The game's tooltip in picture 14 of the owner's second test (2026-10-25): the pointer over the CR MK2 (R) in the Beta 2
    // window, its full name in a black box with a grey border over the window and the stash pane. What it covers isn't
    // read, and its words are no title.
    [Fact]
    public void A_tooltips_black_box_is_covered_and_its_words_are_no_title()
    {
        var (page, words) = Tab();
        var (x, y) = page.StashOrigin;
        using (var canvas = new SKCanvas(page.Bitmap))
        using (var border = new SKPaint { Color = new SKColor(82, 89, 90) })
        using (var black = new SKPaint { Color = SKColors.Black })
        {
            canvas.DrawRect(x - 330, y + 480, 400, 74, border);
            canvas.DrawRect(x - 329, y + 481, 398, 72, black);
        }
        words.AddRange([Word("Stich", x - 306, y + 499, 7), Word("Profi", x - 256, y + 499, 7), Word("Chest", x - 206, y + 499, 7),
            Word("TACS", x - 306, y + 521, 8), Word("FG)", x - 256, y + 521, 8)]);
        var layout = Layout(page, words);
        var tooltip = Assert.Single(DeskLayout.Tooltips(words, StashGray.Of(page.Bitmap), new StashLines(StashGray.Of(page.Bitmap)), page.Pitch));
        Assert.Equal(SKRectI.Create(x - 330, y + 480, 400, 74), tooltip);
        Assert.Contains(layout.Over, o => o.Contains(x + 20, y + 500));
        Assert.DoesNotContain(layout.Regions, r => r.Kind == DeskRegionKind.ContainerWindow);
        // A window's panel is no tooltip: its bar is grey.
        var panel = Panel(page, 1100, 700, 3, 2);
        Assert.Empty(DeskLayout.Tooltips([Word("Ostrel", 1148, 710, 9)], StashGray.Of(page.Bitmap), new StashLines(StashGray.Of(page.Bitmap)), page.Pitch));
        Assert.Equal((1105, 734), panel);
    }

    // An item running on under a window, its label showing above it, is read with its footprint (picture 14 of the owner's
    // second test: the Beta 2 backpack in the backpack's grid, its top row over the Beta 2 window); the window attaches to it. Under
    // the game's tooltip, a window's item is read the same (the CR MK2 (R)), and the items the pointer's highlight joined
    // to it by their labels (the Water and the GPU round it). Its count, under the window, is not read.
    [Fact]
    public async Task An_item_running_under_a_window_or_a_tooltip_is_read_by_its_label_and_footprint()
    {
        var gear = Gear(backpackGrid: false);
        var p = gear.Pitch;
        gear.Grid(1051, 360, 5, 5, 1);
        gear.Item(1051, 360, 1, 0, 0, 5, 3, "Quillon", "17");
        // The Quillon's window over its rows 1 and 2 (its count among them), the backpack's grid left of it showing.
        var (wx, wy) = Panel(gear, 1063, 442, 5, 2);
        Text(gear, "Q", 1073, 450, 18);
        Text(gear, "Quillon", 1111, 452, 18);
        gear.Item(wx, wy, 2, 0, 0, 1, 1, "Marl 214");
        // A tooltip under the stash's Drevak (2×1 at its cells 4 and 5 of row 2): its box over the cells' lower part and the
        // row under them, the Drevak's count under it.
        var (sx, sy) = gear.StashOrigin;
        using (var canvas = new SKCanvas(gear.Bitmap))
        using (var border = new SKPaint { Color = new SKColor(82, 89, 90) })
        using (var black = new SKPaint { Color = SKColors.Black })
        {
            canvas.DrawRect(sx + 4 * p + 10, sy + 2 * p + 50, 330, 80, border);
            canvas.DrawRect(sx + 4 * p + 11, sy + 2 * p + 51, 328, 78, black);
        }
        Text(gear, "Drevak", sx + 4 * p + 34, sy + 2 * p + 70, 16);
        Text(gear, "carbine stock", sx + 4 * p + 100, sy + 2 * p + 70, 16);
        Dump(gear, "gear-under.png");
        using var picture = gear.Bitmap.Copy();
        var reading = await Reader().ReadAsync(picture, new DeskCatalogue([.. Catalogue, Item(17, "Quillon", 5, 3, DeskHolds.Contents)]), DateTimeOffset.MinValue, Ct);
        var page = DeskLedger.Attach(reading.Page!);
        var quillon = In(page, "backpack", c => c.Ids.Contains(Id(17)));
        Assert.Equal((0, 0, 5, 3), (quillon.I, quillon.J, quillon.W, quillon.H));
        Assert.Equal(1, quillon.Count);
        var window = Assert.Single(page.Regions, r => r.Kind == DeskRegionKind.ContainerWindow);
        Assert.Equal(new DeskAnchor("backpack", 0, 0), window.AttachedTo);
        var drevak = In(page, "stash", c => c.Ids.Contains(Id(6)));
        Assert.Equal((4, 2, 2, 1, 1), (drevak.I, drevak.J, drevak.W, drevak.H, drevak.Count));
        Assert.DoesNotContain(page.Regions, r => r.Title is { } t && t.Contains("carbine", StringComparison.OrdinalIgnoreCase));
    }

    // A shape of several items whose lines between them didn't show (the pointer's highlight round the CR MK2 (R) in picture
    // 14; a Bloodset over a Bandage in the CR MK2 (R) window in 16, one rectangle) is read by the labels in its cells, each
    // item taking its footprint from its label's cell.
    [Fact]
    public async Task A_rectangle_whose_label_names_smaller_items_is_read_by_the_labels_in_its_cells()
    {
        var page = new StashReaderTests.Page(2560, 1440).Start();
        var (x, y) = page.StashOrigin;
        page.Grid(x, y, 10, 12, 1);
        // Two items one over the other in one frame, no line between them: Tessaril (1×2) over... a Hollin and a Quenvik.
        page.Item(x, y, 1, 3, 1, 1, 2, "Hollin");
        using (var canvas = new SKCanvas(page.Bitmap))
        using (var face = SKTypeface.FromFamilyName("Bahnschrift", SKFontStyle.Bold) ?? SKTypeface.Default)
        using (var font = new SKFont(face, 13.5f * page.K))
        using (var ink = new SKPaint { Color = new SKColor(226, 229, 231), IsAntialias = true })
        using (var outline = new SKPaint { Color = new SKColor(10, 12, 14), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.2f * page.K })
        {
            var left = x + 4 * page.Pitch - 3 * page.K - font.MeasureText("Quenvik");
            var baseline = y + 2 * page.Pitch + 1 + 13 * page.K;
            canvas.DrawText("Quenvik", left, baseline, SKTextAlign.Left, font, outline);
            canvas.DrawText("Quenvik", left, baseline, SKTextAlign.Left, font, ink);
        }
        using var picture = page.Bitmap.Copy();
        var reading = await Reader().ReadAsync(picture, new DeskCatalogue(Catalogue), DateTimeOffset.MinValue, Ct);
        var stash = reading.Page!.Region("stash")!;
        Assert.Equal((3, 1, 1, 1), Assert.Single(stash.Cells, c => c.Ids.Contains(Id(5))) is var h ? (h.I, h.J, h.W, h.H) : default);
        Assert.Equal((3, 2, 1, 1), Assert.Single(stash.Cells, c => c.Ids.Contains(Id(2))) is var q ? (q.I, q.J, q.W, q.H) : default);
    }

    // Picture 17 of the owner's second test (2026-10-25): the recognition may read the game's 6 as a G ("GL31" for the 6L31
    // magazine); a digit and a letter that look alike are swapped once where the label as read fits nothing.
    [Fact]
    public void A_label_with_a_digit_read_as_its_lookalike_letter_names_the_item()
    {
        var match = new DeskMatch([Item(1, "6L31", 1, 2), Item(2, "GL40"), Item(3, "SB-5")]);
        var (fit, best) = match.Match("GL31");
        Assert.True(fit >= DeskMatch.Enough);
        Assert.Equal(Id(1), Assert.Single(best).ItemId);
        Assert.Equal(Id(3), Assert.Single(match.Match("S8-5").Best).ItemId);
        // As read, a label that fits keeps its item: no swap tried.
        Assert.Equal(Id(2), Assert.Single(match.Match("GL40").Best).ItemId);
        // Two swaps are no lookalike.
        Assert.True(match.Match("GLEI").Fit < DeskMatch.Enough);
    }

    // Picture 17 of the owner's second test (2026-10-25): "Marksma", the stash's Marksman 3x30 on a cell one wide, cut at its right
    // edge before the name's digits, its last letter read "e" as often: the name's beginning. A label with digits must still
    // have all of the name's ("Dorm 10" is no Dorm 104).
    [Fact]
    public void A_label_cut_at_the_cells_edge_before_the_names_digits_is_its_beginning()
    {
        var match = new DeskMatch([Item(1, "Marksman 3x30", 2, 1), Item(2, "Dorm 104")]);
        Assert.Equal(Id(1), Assert.Single(match.Match("Marksma").Best).ItemId);
        var (fit, best) = match.Match("Marksme");
        Assert.True(fit >= DeskMatch.Enough);
        Assert.Equal(Id(1), Assert.Single(best).ItemId);
        Assert.True(match.Match("Dorm 10").Fit < DeskMatch.Enough);
        // On a cell two wide the same label isn't cut.
        Assert.True(match.Match("Marksma", cells: 2).Fit < DeskMatch.Enough);
        // A name the label begins exactly outranks one it begins but for its last letter.
        var two = new DeskMatch([Item(1, "Marksman 3x30", 2, 1), Item(3, "Marksmen")]);
        Assert.Equal(Id(1), Assert.Single(two.Match("Marksma").Best).ItemId);
    }

    // Picture 17 of the owner's second test (2026-10-25): the 6L31 magazine's orange highlight right of its label read as a "1"
    // ("6L311"), and the Zhuk rig's slot, its frame found five pixels short, cut its label's "k" ("Zhi-o"). A label is read
    // up to three pixels past its ink, and a slot's a few pixels past its frame.
    [Fact]
    public void A_label_is_read_up_to_its_ink_and_no_line_right_of_it()
    {
        var page = new StashReaderTests.Page(2560, 1440).Start();
        page.Grid(1000, 400, 1, 2, 1);
        page.Item(1000, 400, 1, 0, 0, 1, 2, "6L31");
        using (var canvas = new SKCanvas(page.Bitmap))
        using (var orange = new SKPaint { Color = new SKColor(197, 141, 69) })
            canvas.DrawRect(1000 + page.Pitch - 2, 400, 2, 2 * page.Pitch, orange);
        var cell = SKRectI.Create(1000, 400, page.Pitch, 2 * page.Pitch);
        var read = DeskReader.LabelRect(page.Bitmap, cell, page.Pitch);
        // The label is read from its rectangle's left to a pixel short of its right: the orange line is left out.
        Assert.True(read.Right - 1 <= 1000 + page.Pitch - 2, $"{read}");
        Assert.True(read.Right >= 1000 + page.Pitch - 8, $"{read}");
        // A frame found short of its label: the slot's rectangle widened to it.
        var shortBox = SKRectI.Create(1000, 400, page.Pitch - 6, 2 * page.Pitch);
        Assert.True(DeskReader.LabelRect(page.Bitmap, shortBox, page.Pitch, reach: 6).Right > shortBox.Right);
    }

    // ---- the raid's inventory screen, drawn (the in-raid glance) ----

    // The GEAR tab in a raid (22 screenshots of 2026-10-19): the character screen's tabs at the top, the loadout and the
    // pockets as in the menus, no stash pane and no menus' bar; QUICK USE at the foot; at the right, looting, the LOOT pane:
    // "LOOT" at its head and the container's kind at the screen's right edge. A body: its slots under the loadout's
    // headings, its TACTICAL RIG (a slot, "UNSEARCHED" where its grid stands), its POCKETS (searched) and its BACKPACK (a
    // slot, "SEARCHING" at its heading, what was found so far in its grid). A drawer: one grid.
    // At another size the game lays the screen out by its height (1920×1080: three quarters of each length). With the
    // head as the game draws it (gameHead), "LOOT" in small grey type beside the pane's cube in a dark tab, about 8 px high
    // at 1080p.
    private static StashReaderTests.Page Raid(string? loot, int width = 2560, int height = 1440, bool gameHead = false)
    {
        var page = new StashReaderTests.Page(width, height).Start();
        var p = page.Pitch;
        var k = page.K;
        int S(int at1440) => (int)Math.Round(at1440 * k);
        Text(page, "OVERALL", S(80), S(20), 16 * k);
        Text(page, "GEAR", S(330), S(20), 16 * k);
        Text(page, "HEALTH", S(530), S(20), 16 * k);
        Text(page, "SKILLS", S(760), S(20), 16 * k);
        Text(page, "EARPIECE", S(92), S(199), 18 * k);
        Text(page, "HEADWEAR", S(337), S(199), 18 * k);
        Text(page, "ARMBAND", S(91), S(420), 18 * k);
        Text(page, "BODY ARMOR", S(338), S(420), 18 * k);
        Text(page, "EYEWEAR", S(583), S(420), 18 * k);
        page.Grid(S(87), S(220), 2, 2, 1);
        page.Item(S(87), S(220), 1, 0, 0, 2, 2, "Zorblat");
        Text(page, "POCKETS", S(880), S(106), 18 * k);
        page.Grid(S(875), S(130), 1, 2, 1);
        page.Item(S(875), S(130), 1, 0, 0, 1, 1, "Quenvik", "250");
        Text(page, "QUICK USE", S(760), S(1262), 15 * k);
        if (loot is null)
            return page;
        if (gameHead)
            GameLootHead(page, S(1660), S(74));
        else
            Text(page, "LOOT", S(1690), S(80), 15 * k);
        Text(page, loot, S(2440), S(76), 18 * k);
        if (loot == "DRAWER")
        {
            page.Grid(S(1686), S(106), 4, 3, 2);
            page.Item(S(1686), S(106), 2, 0, 0, 1, 1, "Hollin");
            page.Item(S(1686), S(106), 2, 1, 0, 2, 1, "Drevak", "330");
            return page;
        }
        Text(page, "ON SLING", S(1767), S(150), 18 * k);
        page.Grid(S(1762), S(171), 5, 2, 1);
        page.Item(S(1762), S(171), 1, 0, 0, 5, 2, "Hollin");
        Text(page, "TACTICAL RIG", S(1767), S(420), 18 * k);
        page.Grid(S(1762), S(441), 2, 2, 1);
        page.Item(S(1762), S(441), 1, 0, 0, 2, 2, "Ostrel");
        Text(page, "UNSEARCHED", S(2010), S(480), 30 * k);
        Text(page, "POCKETS", S(1767), S(640), 18 * k);
        page.Grid(S(1762), S(664), 1, 2, 1);
        page.Grid(S(1762) + p + S(8), S(664), 1, 2, 1);
        page.Item(S(1762) + p + S(8), S(664), 1, 0, 0, 1, 1, "Marl 214");
        Text(page, "BACKPACK", S(1767), S(880), 18 * k);
        Text(page, "SEARCHING", S(1995), S(880), 15 * k);
        page.Grid(S(1762), S(901), 2, 2, 1);
        page.Item(S(1762), S(901), 1, 0, 0, 2, 2, "Pindrake");
        page.Grid(S(1951), S(901), 4, 3, 1);
        page.Item(S(1951), S(901), 1, 0, 0, 1, 2, "Tessaril");
        return page;
    }

    // The LOOT pane's head as the game draws it (the owner's play session of 2026-10-28): a dark tab, the pane's cube, and
    // "LOOT" in grey, 15 px type at 1440p.
    private static void GameLootHead(StashReaderTests.Page page, int x, int y)
    {
        var k = page.K;
        using var canvas = new SKCanvas(page.Bitmap);
        using var tab = new SKPaint { Color = new SKColor(30, 31, 33), IsAntialias = true };
        canvas.DrawRoundRect(x, y, 150 * k, 26 * k, 3 * k, 3 * k, tab);
        // The cube: a light hexagon, its three faces parted by dark lines, a few pixels before the word.
        float cx = x + 17 * k, cy = y + 13 * k, r = 8 * k;
        using (var hexagon = new SKPath())
        {
            for (var i = 0; i < 6; i++)
            {
                var angle = Math.PI / 3 * i - Math.PI / 2;
                var (px, py) = (cx + r * (float)Math.Cos(angle), cy + r * (float)Math.Sin(angle));
                if (i == 0)
                    hexagon.MoveTo(px, py);
                else
                    hexagon.LineTo(px, py);
            }
            hexagon.Close();
            using var cube = new SKPaint { Color = new SKColor(200, 200, 186), IsAntialias = true };
            canvas.DrawPath(hexagon, cube);
        }
        using (var edge = new SKPaint { Color = new SKColor(30, 31, 33), IsAntialias = true, StrokeWidth = 1.5f * k })
        {
            canvas.DrawLine(cx, cy, cx, cy + r, edge);
            canvas.DrawLine(cx, cy, cx - 0.87f * r, cy - r / 2, edge);
            canvas.DrawLine(cx, cy, cx + 0.87f * r, cy - r / 2, edge);
        }
        using var face = SKTypeface.FromFamilyName("Bahnschrift", SKFontStyleWeight.Light, SKFontStyleWidth.SemiCondensed, SKFontStyleSlant.Upright) ?? SKTypeface.Default;
        using var font = new SKFont(face, 15 * k);
        using var ink = new SKPaint { Color = new SKColor(140, 140, 130), IsAntialias = true };
        canvas.DrawText("LOOT", x + 30 * k, y + 19 * k, SKTextAlign.Left, font, ink);
    }

    [Fact]
    public async Task A_raids_inventory_screen_is_read_with_a_bodys_loot_and_each_parts_search()
    {
        var raid = Raid("BODY");
        Dump(raid, "raid-body.png");
        using var picture = raid.Bitmap.Copy();
        var reader = Reader();
        var reading = await reader.ReadRaidAsync(picture, new DeskCatalogue(Catalogue), new DateTimeOffset(2026, 1, 1, 15, 42, 0, TimeSpan.Zero), Ct);
        Assert.Equal(DeskScreen.Raid, reading.Screen);
        var page = reading.Page!;
        // ON YOU: the player's own regions, as in the menus.
        Assert.Equal([Id(1)], In(page, "loadout", c => c.Slot == "earpiece").Ids);
        Assert.Contains(page.Region("pockets")!.Cells, c => c.Ids.Contains(Id(2)));
        Assert.All(page.Regions.Where(r => !r.IsLoot), r => Assert.True(r.OnYou));
        // LOOT: the body's slots as a strip, its parts each with its search state; the game's words for their title.
        Assert.All(page.Regions.Where(r => r.IsLoot), r => Assert.Equal("LOOT · BODY", r.Title));
        Assert.Equal([Id(5)], In(page, "loot:slots", c => c.Slot == "onsling").Ids);
        var rig = page.Region("loot:rig")!;
        Assert.Equal(Core.Desk.DeskSearch.Unsearched, rig.Search);
        Assert.Equal([Id(7)], In(page, "loot:rig", c => c.Slot == "rig").Ids);
        Assert.DoesNotContain(rig.Cells, c => !c.IsSlot);
        Assert.Equal(Core.Desk.DeskSearch.Searched, page.Region("loot:pockets")!.Search);
        Assert.Equal([Id(8)], In(page, "loot:pockets", c => !c.IsSlot).Ids);
        var backpack = page.Region("loot:backpack")!;
        Assert.Equal(Core.Desk.DeskSearch.Searching, backpack.Search);
        Assert.True(backpack.ContentsUnseen);
        Assert.Contains(backpack.Cells, c => c.Ids.Contains(Id(4)));
        // Never the desk's: the menus' reading of the same picture says no screen of its own.
        var desk = await reader.ReadAsync(picture, new DeskCatalogue(Catalogue), DateTimeOffset.MinValue, Ct);
        Assert.Null(desk.Page);
    }

    [Fact]
    public async Task A_drawers_loot_is_one_grid_and_the_world_and_the_inventory_alone_are_told()
    {
        var reader = Reader();
        var drawer = Raid("DRAWER");
        using (var picture = drawer.Bitmap.Copy())
        {
            var page = (await reader.ReadRaidAsync(picture, new DeskCatalogue(Catalogue), DateTimeOffset.MinValue, Ct)).Page!;
            var loot = Assert.Single(page.Regions, r => r.IsLoot);
            Assert.Equal((DeskRegionKind.Loot, "LOOT · DRAWER"), (loot.Kind, loot.Title));
            Assert.Equal([Id(5), Id(6)], loot.Cells.SelectMany(c => c.Ids).Order());
        }
        // The player's inventory alone: ON YOU, no LOOT.
        using (var picture = Raid(null).Bitmap.Copy())
        {
            var reading = await reader.ReadRaidAsync(picture, new DeskCatalogue(Catalogue), DateTimeOffset.MinValue, Ct);
            Assert.Equal(DeskScreen.Raid, reading.Screen);
            Assert.DoesNotContain(reading.Page!.Regions, r => r.IsLoot);
        }
        // The world: no tabs at the top, nothing read past them.
        var world = new StashReaderTests.Page(2560, 1440).Start();
        using (var picture = world.Bitmap.Copy())
        {
            var reading = await reader.ReadRaidAsync(picture, new DeskCatalogue(Catalogue), DateTimeOffset.MinValue, Ct);
            Assert.Equal((DeskScreen.None, null), (reading.Screen, reading.Page));
        }
    }

    // The owner's weapon box of 2026-10-26 (build 1.2.5.0.48146): looting, the LOOT pane stands where the menus' stash pane
    // would, and the container's grid there, six by six, showed lines enough to be taken for the stash, ten wide from its
    // lines (seven of its eleven): the screen was told as the GEAR tab and the glance said nothing. Its rifles and rails
    // hide the grid's lines, so its empty cells frame a lattice four wide only.
    private static StashReaderTests.Page WeaponBox()
    {
        var page = Raid(null);
        Text(page, "LOOT", 1690, 80, 15);
        Text(page, "WEAPON BOX", 2420, 76);
        page.Grid(1686, 106, 6, 6, 1);
        page.Item(1686, 106, 1, 0, 0, 4, 2, "Vostrak");
        page.Item(1686, 106, 1, 0, 2, 4, 2, "Brindel");
        page.Item(1686, 106, 1, 4, 0, 1, 2, "Tessaril");
        page.Item(1686, 106, 1, 5, 0, 1, 2, "Calvory");
        page.Item(1686, 106, 1, 4, 2, 1, 2, "Calvory");
        page.Item(1686, 106, 1, 5, 2, 1, 2, "Feldspan");
        page.Item(1686, 106, 1, 4, 4, 1, 2, "Feldspan");
        page.Item(1686, 106, 1, 5, 4, 1, 2, "Tessaril");
        return page;
    }

    private static readonly DeskCandidate[] WeaponBoxItems =
        [Item(20, "Vostrak", 4, 2), Item(21, "Brindel", 4, 2), Item(22, "Calvory", 1, 2), Item(23, "Feldspan", 1, 2)];

    [Fact]
    public async Task A_looted_containers_grid_where_the_stash_would_stand_is_the_raids_loot()
    {
        var box = WeaponBox();
        Dump(box, "raid-weapon-box.png");
        using var picture = box.Bitmap.Copy();
        var catalogue = new DeskCatalogue([.. Catalogue, .. WeaponBoxItems]);
        var reader = Reader();
        var reading = await reader.ReadRaidAsync(picture, catalogue, DateTimeOffset.MinValue, Ct);
        // No menus' bar and the LOOT pane's head: the raid's inventory, the grid the container's, never the stash.
        Assert.Equal(DeskScreen.Raid, reading.Screen);
        var page = reading.Page!;
        Assert.DoesNotContain(page.Regions, r => r.Kind == DeskRegionKind.Stash);
        var loot = Assert.Single(page.Regions, r => r.IsLoot);
        Assert.Equal((DeskRegionKind.Loot, "LOOT · WEAPON BOX", 6, 6), (loot.Kind, loot.Title, loot.Cols, loot.Rows));
        // Every item named in its place, the rifles' rows beside the lattice of the empty cells too.
        Assert.Equal([(0, 0, Id(20)), (4, 0, Id(4)), (5, 0, Id(22)), (0, 2, Id(21)), (4, 2, Id(22)), (5, 2, Id(23)), (4, 4, Id(23)), (5, 4, Id(4))],
            loot.Cells.Select(c => (c.I, c.J, Assert.Single(c.Ids))).OrderBy(c => c.J).ThenBy(c => c.I));
        // The player's own regions as before.
        Assert.Contains(page.Region("pockets")!.Cells, c => c.Ids.Contains(Id(2)));
        // Never the desk's.
        Assert.Null((await reader.ReadAsync(picture, catalogue, DateTimeOffset.MinValue, Ct)).Page);
    }

    // The menus' GEAR tab: the stash at the right and the menus' bar at the foot (BUILDS, HANDBOOK, MESSENGER). It stays
    // the GEAR tab, for the glance as for the desk, whatever stands at the stash pane's head.
    [Fact]
    public async Task The_menus_gear_tab_with_its_stash_and_bar_is_never_the_raids()
    {
        var gear = Gear();
        Text(gear, "OVERALL", 80, 20, 16);
        Text(gear, "GEAR", 330, 20, 16);
        Text(gear, "HEALTH", 530, 20, 16);
        Text(gear, "SKILLS", 760, 20, 16);
        Text(gear, "QUICK USE", 760, 1262, 15);
        Text(gear, "BUILDS", 1640, 1400, 16);
        Text(gear, "HANDBOOK", 1830, 1400, 16);
        Text(gear, "MESSENGER", 2030, 1400, 16);
        // A word "LOOT" at the head's place changes nothing: the menus' bar tells the menus.
        Text(gear, "LOOT", 2050, 78, 15);
        Dump(gear, "gear-menus-bar.png");
        using var picture = gear.Bitmap.Copy();
        var reader = Reader();
        var raid = await reader.ReadRaidAsync(picture, new DeskCatalogue(Catalogue), DateTimeOffset.MinValue, Ct);
        Assert.Equal((DeskScreen.Gear, null), (raid.Screen, raid.Page));
        var desk = await reader.ReadAsync(picture, new DeskCatalogue(Catalogue), DateTimeOffset.MinValue, Ct);
        Assert.Equal(DeskScreen.Gear, desk.Screen);
        Assert.Contains(desk.Page!.Regions, r => r.Kind == DeskRegionKind.Stash);
        Assert.DoesNotContain(desk.Page.Regions, r => r.IsLoot);
    }

    // The owner's weapon box of 2026-10-26: the AEM-5 suppressor's label read "AEM-?", whose "?" the fold dropped, named the
    // AEM textbook whole. A "?" is a glyph the recognition saw: any letter or digit in its place first; where that fits
    // nothing, nothing, as before (a speck read as "?" after a whole name).
    [Fact]
    public void A_glyph_read_as_a_question_mark_stands_for_one_in_its_place()
    {
        var match = new DeskMatch([Item(1, "AEM", 1, 2), Item(2, "AEM-5", 1, 2), Item(3, "Salewa", 1, 2)]);
        Assert.Equal(Id(2), Assert.Single(match.Match("AEM-?").Best).ItemId);
        Assert.Equal(Id(1), Assert.Single(match.Match("AEM").Best).ItemId);
        Assert.Equal(Id(3), Assert.Single(match.Match("Salewa?").Best).ItemId);
        // Two names a glyph there fits: one of 2, never a guess.
        var two = new DeskMatch([Item(1, "AEM", 1, 2), Item(2, "AEM-5", 1, 2), Item(4, "AEM-7", 1, 2)]);
        Assert.Equal([Id(2), Id(4)], two.Match("AEM-?").Best.Select(c => c.ItemId).Order());
    }

    // ---- the found-in-raid mark on a slot (owner, 2026-10-28) ----

    // A slot as the game draws it (the owner's screenshots of 2026-10-27 and 2026-10-28, at 2560×1440 and 1920×1080): its box
    // under its heading, 172 px high at 1440p, a frame one pixel wide round its ground, a print at its bottom right; a
    // weapon's slot has a bar down its right side. Returns the box the layout gives.
    private static SKRectI SlotBox(StashReaderTests.Page page, int x, int y, int cells, string? print = null, bool barred = false, SKColor? ground = null)
    {
        var k = page.K;
        var box = SKRectI.Create(x, y, cells * page.Pitch, (int)Math.Round(172 * k));
        using var canvas = new SKCanvas(page.Bitmap);
        using var line = new SKPaint { Color = Line };
        using var field = new SKPaint { Color = ground ?? Field };
        canvas.DrawRect(box.Left - 1, box.Top + 1, box.Width, box.Height - 2, line);
        canvas.DrawRect(box.Left, box.Top + 2, box.Width - 2, box.Height - 4, field);
        using var art = new SKPaint { Color = new SKColor(70, 64, 54), IsAntialias = true };
        canvas.DrawRoundRect(box.Left + 20 * k, box.Top + 30 * k, box.Width - 70 * k, box.Height - 80 * k, 6 * k, 6 * k, art);
        if (barred)
        {
            using var bar = new SKPaint { Color = new SKColor(120, 124, 126) };
            canvas.DrawRect(box.Right - 14 * k, box.Top + 40 * k, 3 * k, box.Height - 52 * k, bar);
        }
        if (print is not null)
        {
            using var face = SKTypeface.FromFamilyName("Bahnschrift", SKFontStyle.Bold) ?? SKTypeface.Default;
            using var font = new SKFont(face, 15 * k);
            using var ink = new SKPaint { Color = new SKColor(226, 229, 231), IsAntialias = true };
            if (!barred)
                canvas.DrawText(print, box.Right - 5 * k, box.Bottom - 8 * k, SKTextAlign.Right, font, ink);
            else
            {
                // A weapon's rounds: the loaded count large, its maximum small after it, left of the bar.
                var slash = print.IndexOf('/');
                using var large = new SKFont(face, 34 * k);
                canvas.DrawText(print[slash..], box.Right - 18 * k, box.Bottom - 10 * k, SKTextAlign.Right, font, ink);
                canvas.DrawText(print[..slash], box.Right - 18 * k - font.MeasureText(print[slash..]), box.Bottom - 10 * k, SKTextAlign.Right, large, ink);
            }
        }
        return box;
    }

    // The game's found-in-raid mark (a light circle with a check), or the same shape in another colour, its middle (dx, dy)
    // in from the box's bottom right at 1440p.
    private static void MarkAt(StashReaderTests.Page page, SKRectI box, double dx, double dy, SKColor? colour = null)
    {
        var k = page.K;
        float cx = (float)(box.Right - dx * k), cy = (float)(box.Bottom - dy * k);
        using var canvas = new SKCanvas(page.Bitmap);
        using var ring = new SKPaint { Color = colour ?? new SKColor(196, 200, 204), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.6f * k };
        canvas.DrawCircle(cx, cy, 6.3f * k, ring);
        canvas.DrawLine(cx - 3.6f * k, cy - 0.2f * k, cx - 1.2f * k, cy + 2.6f * k, ring);
        canvas.DrawLine(cx - 1.2f * k, cy + 2.6f * k, cx + 5.4f * k, cy - 4.6f * k, ring);
    }

    // A secure container's lock at a slot's corner, as the owner's screenshots show it: a light grey padlock with a red
    // keyhole in a dark magenta glow.
    private static void LockAt(StashReaderTests.Page page, SKRectI box)
    {
        var k = page.K;
        float cx = box.Right - 13 * k, cy = box.Bottom - 14 * k;
        using var canvas = new SKCanvas(page.Bitmap);
        using var glow = new SKPaint { Color = new SKColor(58, 11, 30), IsAntialias = true };
        using var body = new SKPaint { Color = new SKColor(161, 158, 161), IsAntialias = true };
        using var shackle = new SKPaint { Color = new SKColor(161, 158, 161), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f * k };
        using var hole = new SKPaint { Color = new SKColor(124, 37, 68), IsAntialias = true };
        canvas.DrawRoundRect(cx - 11 * k, cy - 13 * k, 22 * k, 24 * k, 5 * k, 5 * k, glow);
        canvas.DrawCircle(cx, cy - 4 * k, 4.5f * k, shackle);
        canvas.DrawRoundRect(cx - 7.5f * k, cy - 3 * k, 15 * k, 11 * k, 1.5f * k, 1.5f * k, body);
        canvas.DrawRect(cx - 1 * k, cy, 2 * k, 4 * k, hole);
    }

    // Measured on the owner's screenshots (2026-10-27, 2026-10-28): a gear slot's mark 12 px in from its box's right and 14
    // up, or 31 up over a printed condition; a weapon's or a knife's, beside the bar down its slot's right side, 29 in and 14
    // up, or 59 up over its rounds. Read at those places, scaled by the pitch, at both sizes. (The game's print, in its own
    // narrow font, correlates with the mark at 0.3 at the most on those screenshots; the test's font more, so an unmarked
    // slot with a print is held only to "never yes".)
    [Theory]
    [InlineData(2560, 1440)]
    [InlineData(1920, 1080)]
    public void A_slots_found_in_raid_mark_is_read_at_its_corner_and_over_its_print(int width, int height)
    {
        var page = new StashReaderTests.Page(width, height).Start();
        var p = page.Pitch;
        var k = page.K;
        int Y(int row) => (int)Math.Round((80 + row * 230) * k);
        var plain = SlotBox(page, (int)(80 * k), Y(0), 2);
        MarkAt(page, plain, 12, 14);
        var printed = SlotBox(page, (int)(80 * k) + 3 * p, Y(0), 2, "28/36");
        MarkAt(page, printed, 12, 31);
        var none = SlotBox(page, (int)(80 * k) + 6 * p, Y(0), 2);
        var nonePrinted = SlotBox(page, (int)(80 * k) + 9 * p, Y(0), 2, "100/100");
        var gun = SlotBox(page, (int)(80 * k), Y(1), 5, "?/75", barred: true);
        MarkAt(page, gun, 29, 59);
        var bare = SlotBox(page, (int)(80 * k) + 6 * p, Y(1), 5, barred: true);
        MarkAt(page, bare, 29, 14);
        var unmarked = SlotBox(page, (int)(80 * k), Y(2), 5, barred: true);
        var unmarkedPrinted = SlotBox(page, (int)(80 * k) + 6 * p, Y(2), 5, "30/30", barred: true);

        Assert.True(StashMarks.SeenOnSlot(page.Bitmap, plain, p, barred: false));
        Assert.True(StashMarks.SeenOnSlot(page.Bitmap, printed, p, barred: false));
        Assert.False(StashMarks.SeenOnSlot(page.Bitmap, none, p, barred: false));
        Assert.NotEqual(true, StashMarks.SeenOnSlot(page.Bitmap, nonePrinted, p, barred: false));
        Assert.True(StashMarks.SeenOnSlot(page.Bitmap, gun, p, barred: true));
        Assert.True(StashMarks.SeenOnSlot(page.Bitmap, bare, p, barred: true));
        Assert.False(StashMarks.SeenOnSlot(page.Bitmap, unmarked, p, barred: true));
        Assert.NotEqual(true, StashMarks.SeenOnSlot(page.Bitmap, unmarkedPrinted, p, barred: true));
        // A weapon's mark is beside the bar, not in the corner a gear slot's takes.
        Assert.NotEqual(true, StashMarks.SeenOnSlot(page.Bitmap, gun, p, barred: false));
    }

    // The game's insurance shield (orange) stands where the mark would on the player's insured gear and correlates with the
    // mark's shape about as well (0.47 to 0.52 on the owner's screenshots, the mark 0.50 to 0.55), and so does a secure
    // container's lock: what stands there counts only where it is grey. Drawn here as the mark's own shape in the shield's
    // orange, the hardest case.
    [Theory]
    [InlineData(2560, 1440)]
    [InlineData(1920, 1080)]
    public void The_insurance_shield_and_a_secure_containers_lock_are_no_mark(int width, int height)
    {
        var page = new StashReaderTests.Page(width, height).Start();
        var p = page.Pitch;
        var k = page.K;
        var orange = new SKColor(206, 138, 40);
        var insured = SlotBox(page, (int)(80 * k), (int)(80 * k), 2);
        MarkAt(page, insured, 12, 14, orange);
        var insuredPrint = SlotBox(page, (int)(80 * k) + 3 * p, (int)(80 * k), 2, "32/32");
        MarkAt(page, insuredPrint, 12, 31, orange);
        var insuredGun = SlotBox(page, (int)(80 * k), (int)(310 * k), 5, barred: true);
        MarkAt(page, insuredGun, 29, 14, orange);
        var pouch = SlotBox(page, (int)(80 * k) + 6 * p, (int)(310 * k), 2, ground: new SKColor(24, 13, 20));
        LockAt(page, pouch);
        var marked = SlotBox(page, (int)(80 * k) + 6 * p, (int)(80 * k), 2);
        MarkAt(page, marked, 12, 14);

        Assert.False(StashMarks.SeenOnSlot(page.Bitmap, insured, p, barred: false));
        Assert.NotEqual(true, StashMarks.SeenOnSlot(page.Bitmap, insuredPrint, p, barred: false));
        Assert.False(StashMarks.SeenOnSlot(page.Bitmap, insuredGun, p, barred: true));
        Assert.False(StashMarks.SeenOnSlot(page.Bitmap, pouch, p, barred: false));
        Assert.True(StashMarks.SeenOnSlot(page.Bitmap, marked, p, barred: false));
    }

    // A slot's corner is judged only where nothing lies over it and the slot's frame shows along its bottom: the pane's or
    // the column's foot cutting the slot, the hover's light (the ground as light as the frame) and the red low-health tint
    // at its strongest hide that line, and the mark is left unread.
    [Theory]
    [InlineData(2560, 1440)]
    [InlineData(1920, 1080)]
    public void A_slots_corner_is_judged_only_where_its_frame_shows_and_nothing_covers_it(int width, int height)
    {
        var page = new StashReaderTests.Page(width, height).Start();
        var p = page.Pitch;
        var k = page.K;
        var shown = SlotBox(page, (int)(80 * k), (int)(80 * k), 2);
        MarkAt(page, shown, 12, 14);
        var lit = SlotBox(page, (int)(80 * k) + 3 * p, (int)(80 * k), 2, ground: new SKColor(95, 96, 97));
        MarkAt(page, lit, 12, 14);
        var cut = SlotBox(page, (int)(80 * k) + 6 * p, (int)(80 * k), 2);
        MarkAt(page, cut, 12, 14);
        // The view's foot: the bar under the column, over the slot's last pixels.
        using (var canvas = new SKCanvas(page.Bitmap))
        using (var foot = new SKPaint { Color = new SKColor(12, 13, 14) })
            canvas.DrawRect(cut.Left - 4, cut.Bottom - 6 * k, cut.Width + 8, 40 * k, foot);
        var lines = new StashLines(StashGray.Of(page.Bitmap));

        Assert.True(DeskReader.MarkCornerShows(shown, p, lines, []));
        Assert.False(DeskReader.MarkCornerShows(lit, p, lines, []));
        Assert.False(DeskReader.MarkCornerShows(cut, p, lines, []));
        // A window's edge over the corner, less than a quarter of the slot (which leaves the slot itself read).
        var window = SKRectI.Create(shown.Right - p / 3, shown.Bottom - p / 2, 6 * p, 4 * p);
        Assert.False(DeskReader.MarkCornerShows(shown, p, lines, [window]));
        Assert.True(DeskReader.MarkCornerShows(shown, p, lines, [SKRectI.Create(shown.Right + p, shown.Top, 4 * p, 4 * p)]));
    }

    // Through the GEAR tab's reading: the earpiece's mark read on its slot, the armour's slot (its print, no mark) read as
    // none; the slot's cell carries it as a grid's cell does.
    [Fact]
    public async Task The_gear_tabs_slots_carry_their_found_in_raid_mark()
    {
        var gear = Gear(earpieceMark: true);
        Dump(gear, "gear-slot-mark.png");
        using var picture = gear.Bitmap.Copy();
        var reading = await Reader().ReadAsync(picture, new DeskCatalogue(Catalogue), new DateTimeOffset(2026, 1, 1, 15, 40, 0, TimeSpan.Zero), Ct);
        var page = reading.Page!;
        Assert.True(In(page, "loadout", c => c.Slot == "earpiece").FoundInRaid);
        Assert.False(In(page, "loadout", c => c.Slot == "bodyarmor").FoundInRaid);
        Assert.False(In(page, "backpack", c => c.Slot == "backpack").FoundInRaid);
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

    // ---- the second layout round of 2026-10-28: 1920×1080, and a raid's grids the column cuts (the owner's play session) ----

    // b01, b02, b07 and b09 of the play session (1920×1080): the LOOT pane's head, "LOOT" in small grey type beside the
    // pane's cube, about 8 px high at 1080p, was never read by the whole picture's reading, and the pane never found. Where
    // that reading has no "LOOT", the head's band is read again as a cell's label is, enlarged to the labels' size in a
    // dark margin.
    [Theory]
    [InlineData(2560, 1440)]
    [InlineData(1920, 1080)]
    public async Task The_loot_panes_head_in_its_small_type_is_read_at_either_size(int width, int height)
    {
        var raid = Raid("BODY", width, height, gameHead: true);
        Dump(raid, $"raid-body-{height}.png");
        using var picture = raid.Bitmap.Copy();
        var reading = await Reader().ReadRaidAsync(picture, new DeskCatalogue(Catalogue), DateTimeOffset.MinValue, Ct);
        Assert.Equal(DeskScreen.Raid, reading.Screen);
        var page = reading.Page!;
        Assert.All(page.Regions.Where(r => r.IsLoot), r => Assert.Equal("LOOT · BODY", r.Title));
        Assert.Equal([Id(5)], In(page, "loot:slots", c => c.Slot == "onsling").Ids);
        Assert.Equal([Id(8)], In(page, "loot:pockets", c => !c.IsSlot).Ids);
        Assert.Contains(page.Region("loot:backpack")!.Cells, c => c.Ids.Contains(Id(4)));
    }

    // a11 of the play session: the LOOT pane's head "LOOT", the container being searched beside it ("SEARCHING ✕ 0:04"), its
    // cross read as a window's close glyph, made a window "LOOT" over the pane's grid, which then read as the window's and
    // no LOOT region. The head's word is the pane's, never a title.
    [Theory]
    [InlineData(2560, 1440)]
    [InlineData(1920, 1080)]
    public void The_loot_panes_head_word_beside_a_close_glyph_is_no_windows_title(int width, int height)
    {
        var page = new StashReaderTests.Page(width, height).Start();
        List<DeskWord> words =
        [
            Scaled(page, "EARPIECE", 92, 201, 1), Scaled(page, "HEADWEAR", 337, 201, 2), Scaled(page, "ARMBAND", 91, 422, 4),
            Scaled(page, "BODY", 338, 422, 5), Scaled(page, "ARMOR", 390, 422, 5), Scaled(page, "QUICK", 760, 1254, 9), Scaled(page, "USE", 820, 1254, 9),
            Scaled(page, "LOOT", 1722, 83, 10, 13), Scaled(page, "SEARCHING", 1880, 83, 10, 13), Scaled(page, "x", 1985, 83, 10, 13),
            Scaled(page, "0:04", 2020, 83, 10, 13), Scaled(page, "GRENADE", 2355, 76, 11, 16), Scaled(page, "BOX", 2470, 76, 11, 16),
        ];
        page.Grid(At(page, 87), At(page, 220), 2, 2, 1);
        page.Grid(At(page, 1686), At(page, 106), 3, 2, 2);
        var layout = Layout(page, words);
        Assert.Equal(DeskScreen.Raid, layout.Screen);
        Assert.DoesNotContain(layout.Regions, r => r.Kind == DeskRegionKind.ContainerWindow);
        var grid = Assert.Single(Assert.Single(layout.Loot!.Parts).Parts);
        Assert.Equal((3, 2), ((int)Math.Round(grid.Width / (double)page.Pitch), (int)Math.Round(grid.Height / (double)page.Pitch)));
    }

    // a04 of the play session: the middle column scrolled, the AVS rig's last rows at its top, its TACTICAL RIG heading out
    // of view above; its two-by-two pocket, its cells' lines two pixels wide, was taken for a window of no name. In the
    // middle column a lattice under none of its headings, with no window's panel round it, is no window.
    [Theory]
    [InlineData(2560, 1440)]
    [InlineData(1920, 1080)]
    public void In_the_middle_column_a_lattice_under_no_heading_and_no_panel_is_no_window(int width, int height)
    {
        var page = new StashReaderTests.Page(width, height).Start();
        List<DeskWord> words =
        [
            Scaled(page, "EARPIECE", 92, 201, 1), Scaled(page, "HEADWEAR", 337, 201, 2), Scaled(page, "ARMBAND", 91, 422, 4),
            Scaled(page, "BODY", 338, 422, 5), Scaled(page, "ARMOR", 390, 422, 5), Scaled(page, "QUICK", 760, 1254, 9), Scaled(page, "USE", 820, 1254, 9),
            Scaled(page, "POCKETS", 880, 370, 6),
        ];
        page.Grid(At(page, 87), At(page, 220), 2, 2, 1);
        page.Grid(At(page, 1108), At(page, 158), 2, 2, 2);
        page.Grid(At(page, 875), At(page, 391), 1, 1, 1);
        var layout = Layout(page, words);
        Assert.Equal(DeskScreen.Raid, layout.Screen);
        Assert.DoesNotContain(layout.Regions, r => r.Kind == DeskRegionKind.ContainerWindow);
        Assert.Contains(layout.Notes, n => n.Contains("under no heading and no panel: no window", StringComparison.Ordinal));
        Assert.Single(Assert.Single(layout.Regions, r => r.Kind == DeskRegionKind.Pockets).Parts);
    }

    // b01 of the play session (1920×1080): the AVS rig's two-by-two pocket, a twelfth of a cell (5 px) under the pockets
    // above it, found two pixels above its own line and moved onto theirs, its sides then running on past its corners: the
    // rig was left out as cut. A frame is moved onto its own line, never onto the next compartment's: the reach stops
    // short of it (4 px at 1440p, as before; 2 at 1080p).
    [Theory]
    [InlineData(2560, 1440)]
    [InlineData(1920, 1080)]
    public void A_frame_snaps_onto_its_own_line_and_never_onto_the_next_compartments(int width, int height)
    {
        var page = new StashReaderTests.Page(width, height).Start();
        var p = page.Pitch;
        var gap = (int)Math.Round(p / 12.0);
        int x0 = At(page, 1000), y0 = At(page, 400);
        // A row of pockets, and a twelfth of a cell under them a compartment two by two, an item's lighter ground under its
        // top line (the BP rounds in b01).
        page.Grid(x0, y0, 1, 1, 1);
        page.Grid(x0 + p + gap, y0, 1, 1, 1);
        var top = y0 + p + gap;
        page.Grid(x0, top, 2, 2, 1);
        using (var canvas = new SKCanvas(page.Bitmap))
        using (var ground = new SKPaint { Color = new SKColor(62, 60, 50) })
            canvas.DrawRect(x0 + 1, top + 1, 2 * p - 1, p / 3, ground);
        var gray = StashGray.Of(page.Bitmap);
        Assert.Equal(top, StashGrids.Snap(gray, x0, top - 2, 2, 2, p).Y0);
        Assert.Equal(top, StashGrids.Snap(gray, x0, top + 1, 2, 2, p).Y0);
        Assert.Equal((4, 2), (StashGrids.SnapReach(84), StashGrids.SnapReach(63)));
    }

    // b04 of the play session (1920×1080): two of the AVS rig's pockets side by side, 5 px apart line to line, an item's
    // light border 3 px inside the right one's right line; a frame from the left one's right line to that border was framed
    // first, a compartment 4 px off its own, and the rig was left out as cut. One compartment framed twice a few pixels
    // apart is the frame whose corner turns, the other's top running on left of its corner.
    [Theory]
    [InlineData(2560, 1440)]
    [InlineData(1920, 1080)]
    public void A_compartment_framed_twice_a_few_pixels_apart_is_the_frame_whose_corner_turns(int width, int height)
    {
        var page = new StashReaderTests.Page(width, height).Start();
        var p = page.Pitch;
        var gap = (int)Math.Round(p / 12.0);
        int x0 = At(page, 1000), y0 = At(page, 400);
        var right = x0 + p + gap;
        page.Grid(x0, y0, 1, 2, 1);
        page.Grid(right, y0, 1, 2, 1);
        using (var canvas = new SKCanvas(page.Bitmap))
        using (var border = new SKPaint { Color = new SKColor(150, 150, 120) })
            canvas.DrawRect(right + p - 3, y0 + 2, 1, 2 * p - 3, border);
        var lines = new StashLines(StashGray.Of(page.Bitmap));
        var frames = DeskFrames.Find(lines, p, new SKRectI(At(page, 900), At(page, 300), At(page, 1400), At(page, 700)), []);
        Assert.Contains(frames, f => Math.Abs(f.Left - right) <= 2 && Math.Abs(f.Top - y0) <= 2);
        Assert.DoesNotContain(frames, f => f.Left > x0 + p - 3 && f.Left < right - 2);
    }

    // The owner's pick of 2026-10-28 (2): in a raid the player's own grid the middle column's foot cuts is read as far as it
    // shows, as the desk reads it since 2026-10-25 and the glance the LOOT pane's grid its scrolling cuts: its rows in view,
    // its contents past the foot unseen ("+" on ON YOU). Until then it was left out with its region (b01, b02, b04 and b12
    // of the play session: the Trooper 35's grid, eleven items whole in view).
    [Theory]
    [InlineData(2560, 1440)]
    [InlineData(1920, 1080)]
    public async Task In_a_raid_a_grid_the_columns_foot_cuts_is_read_as_far_as_it_shows_and_its_rest_unseen(int width, int height)
    {
        var raid = Raid(null, width, height);
        var k = raid.K;
        int S(int at1440) => (int)Math.Round(at1440 * k);
        Text(raid, "BACKPACK", S(880), S(842), 18 * k);
        raid.Grid(S(875), S(864), 2, 2, 1);
        raid.Item(S(875), S(864), 1, 0, 0, 2, 2, "Pindrake");
        raid.Grid(S(1051), S(864), 5, 6, 1);
        raid.Item(S(1051), S(864), 1, 0, 0, 1, 2, "Tessaril");
        raid.Item(S(1051), S(864), 1, 2, 1, 1, 1, "Quenvik", "250");
        // The column's view ends a few pixels above the QUICK USE bar's top: nothing of the grid below.
        using (var canvas = new SKCanvas(raid.Bitmap))
        using (var ground = new SKPaint { Color = new SKColor(18, 20, 22) })
            canvas.DrawRect(S(1040), S(1265), S(500), raid.Height - S(1265), ground);
        Dump(raid, $"raid-cut-{height}.png");
        using var picture = raid.Bitmap.Copy();
        var reading = await Reader().ReadRaidAsync(picture, new DeskCatalogue(Catalogue), DateTimeOffset.MinValue, Ct);
        var page = reading.Page!;
        var backpack = page.Region("backpack")!;
        Assert.True(backpack.Cut);
        Assert.True(backpack.ContentsUnseen);
        Assert.Equal((5, 4), (backpack.Cols, backpack.Rows));
        Assert.Equal([Id(3)], In(page, "backpack", c => c.IsSlot).Ids);
        Assert.Equal([Id(4)], In(page, "backpack", c => !c.IsSlot && c.I == 0 && c.J == 0).Ids);
        Assert.Equal([Id(2)], In(page, "backpack", c => !c.IsSlot && c.I == 2 && c.J == 1).Ids);
        // The pockets in view whole: no contents unseen.
        Assert.False(page.Region("pockets")!.ContentsUnseen);
    }
}
