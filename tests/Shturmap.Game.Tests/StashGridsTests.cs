using Shturmap.Game.Screenshots;
using SkiaSharp;

namespace Shturmap.Game.Tests;

// An item's footprint as the grid finding reads it (the reader round of 2026-10-19: on the owner's 20:38 page 34 "?" cells
// were the pieces of 11 rifles, silencers and scopes). The game draws a light frame round each item; the grid's own line
// shows through a wide item's background, faintly, and a gun's dark receiver reads as an empty cell. Drawn pictures: no
// recognition needed.
public class StashGridsTests
{
    private static readonly SKColor Field = new(26, 29, 32), Line = new(96, 100, 104), Art = new(70, 74, 78);

    // A grid of six by three cells, one-pixel lines, at the picture's (200, 200).
    private const int X0 = 200, Y0 = 200, Cols = 6, Rows = 3;

    private static (SKBitmap Bitmap, int Pitch) Picture(Action<SKCanvas, int> items)
    {
        var page = new StashReaderTests.Page(2560, 1440).Start();
        page.Grid(X0, Y0, Cols, Rows, 1);
        using (var canvas = new SKCanvas(page.Bitmap))
            items(canvas, page.Pitch);
        return (page.Bitmap, page.Pitch);
    }

    // An item over cells (i, j) to (i + w, j + h): its ground and some art, the grid's lines under it covered, its frame the
    // grid's lines round it.
    private static void Item(SKCanvas canvas, int pitch, int i, int j, int w, int h)
    {
        using var ground = new SKPaint { Color = new SKColor(40, 44, 48) };
        using var art = new SKPaint { Color = Art, IsAntialias = true };
        float left = X0 + i * pitch + 1, top = Y0 + j * pitch + 1, right = X0 + (i + w) * pitch, bottom = Y0 + (j + h) * pitch;
        canvas.DrawRect(left, top, right - left, bottom - top, ground);
        canvas.DrawRoundRect(left + 10, top + 18, right - left - 20, bottom - top - 34, 4, 4, art);
        using var bright = new SKPaint { Color = new SKColor(150, 140, 120), IsAntialias = true };
        canvas.DrawCircle(left + (right - left) * 0.3f, top + (bottom - top) * 0.55f, 9, bright);
    }

    // The grid's line between two cells of one item, seen faintly through its ground and art (a dozen levels lighter).
    private static void FaintLine(SKCanvas canvas, int pitch, int i, int j)
    {
        using var faint = new SKPaint { Color = new SKColor(255, 255, 255, 22) };
        canvas.DrawRect(X0 + i * pitch, Y0 + j * pitch + 1, 1, pitch - 1, faint);
    }

    private static List<StashRect> Read(SKBitmap bitmap, int pitch)
    {
        var gray = StashGray.Of(bitmap);
        var lines = new StashLines(gray);
        var grid = new StashGrid(X0, Y0, Cols, Rows, StashGridPlace.Window);
        var high = gray.HighPass(5);
        return StashGrids.Items(grid, lines, new bool[grid.Area], StashGrids.Empty(grid, high, pitch), pitch);
    }

    [Fact]
    public void A_wide_items_cells_are_one_where_the_grids_line_shows_through_it_faintly()
    {
        var (bitmap, pitch) = Picture((canvas, p) =>
        {
            // A silencer two cells wide, the line between its cells showing through; four neighbours of one cell each.
            Item(canvas, p, 0, 0, 2, 1);
            FaintLine(canvas, p, 1, 0);
            for (var i = 2; i < 6; i++)
                Item(canvas, p, i, 0, 1, 1);
            for (var i = 0; i < 6; i++)
                Item(canvas, p, i, 2, 1, 1);
        });
        using (bitmap)
        {
            var rects = Read(bitmap, pitch);
            var silencer = Assert.Single(rects, r => r.J0 == 0 && r.I0 == 0);
            Assert.Equal((2, 1, false), (silencer.W, silencer.H, silencer.Odd));
            // Joined across a faint line: the pieces the lines that show give, for a label that names nothing of its size.
            Assert.Equal(2, silencer.Pieces.Count);
            // The frames between items stand out: each of one cell stays one.
            Assert.Equal(4, rects.Count(r => r.J0 == 0 && r.I0 >= 2 && r.W == 1));
            Assert.Equal(6, rects.Count(r => r.J0 == 2));
        }
    }

    [Fact]
    public void A_dark_cell_inside_an_item_is_the_items_and_an_empty_cell_beside_one_stays_empty()
    {
        var (bitmap, pitch) = Picture((canvas, p) =>
        {
            // A rifle three cells wide whose middle cell is dark and flat (it reads as the empty field), the grid's lines
            // through it faint; an item of one cell beside an empty one.
            Item(canvas, p, 0, 1, 3, 1);
            using (var dark = new SKPaint { Color = Field })
                canvas.DrawRect(X0 + p + 1, Y0 + p + 1, p - 1, p - 1, dark);
            FaintLine(canvas, p, 1, 1);
            FaintLine(canvas, p, 2, 1);
            Item(canvas, p, 4, 1, 1, 1);
            for (var i = 0; i < 6; i++)
                Item(canvas, p, i, 0, 1, 1);
        });
        using (bitmap)
        {
            var rects = Read(bitmap, pitch);
            var rifle = Assert.Single(rects, r => r.J0 == 1 && r.I0 == 0);
            Assert.Equal((3, 1, false), (rifle.W, rifle.H, rifle.Odd));
            Assert.Single(rects, r => r.J0 == 1 && r.I0 == 4 && r.W == 1);
            // The empty cells (3, 1) and (5, 1) and the third row are no item's.
            Assert.DoesNotContain(rects, r => r.J0 >= 1 && (r.I0 == 3 || r.I0 == 5) && r.W == 1 && r.H == 1);
            Assert.DoesNotContain(rects, r => r.J0 == 2);
        }
    }

    [Fact]
    public void Two_items_joined_across_faint_lines_into_no_rectangle_stay_as_the_lines_give_them()
    {
        var (bitmap, pitch) = Picture((canvas, p) =>
        {
            // A case two by two and an item of one cell under its right half, the frame between them faint (bright art at
            // the case's foot): together no rectangle, so two items.
            Item(canvas, p, 0, 0, 2, 2);
            Item(canvas, p, 1, 2, 1, 1);
            using (var faint = new SKPaint { Color = new SKColor(56, 60, 64) })
                canvas.DrawRect(X0 + p + 1, Y0 + 2 * p, p - 1, 1, faint);
            for (var i = 2; i < 6; i++)
            {
                Item(canvas, p, i, 0, 1, 1);
                Item(canvas, p, i, 2, 1, 1);
            }
        });
        using (bitmap)
        {
            var rects = Read(bitmap, pitch);
            Assert.Contains(rects, r => (r.I0, r.J0, r.W, r.H, r.Odd) == (0, 0, 2, 2, false));
            Assert.Contains(rects, r => (r.I0, r.J0, r.W, r.H) == (1, 2, 1, 1));
        }
    }

    // The window's lattice a few pixels off its cells moves onto their lines in the grid finding itself, so the quest marks'
    // reader and the desk read one lattice (the Keys window of 2026-10-19: the quest marks found 23 and 28 of its 72 keys).
    [Fact]
    public void A_windows_lattice_is_found_on_its_cells_lines()
    {
        var page = new StashReaderTests.Page(2560, 1440).Start();
        var p = page.Pitch;
        using (var canvas = new SKCanvas(page.Bitmap))
        using (var outline = new SKPaint { Color = new SKColor(200, 170, 90), Style = SKPaintStyle.Stroke, StrokeWidth = 1 })
            canvas.DrawRect(SKRect.Create(810.5f, 432.5f, 11 * p + 13, 7 * p + 13), outline);
        page.Grid(816, 438, 11, 7, 2);
        for (var j = 0; j < 7; j++)
        {
            for (var i = 0; i < 11; i++)
                page.Item(816, 438, 2, i, j, 1, 1, "Key");
        }
        using var bitmap = page.Bitmap;
        var gray = StashGray.Of(bitmap);
        var lines = new StashLines(gray);
        var window = Assert.Single(StashGrids.Find(lines, gray, bitmap.Width, bitmap.Height, p), g => g.Place == StashGridPlace.Window);
        Assert.InRange(window.X0, 815, 818);
        Assert.InRange(window.Y0, 437, 440);
        var rects = StashGrids.Items(window, lines, new bool[window.Area], StashGrids.Empty(window, gray.HighPass(5), p), p);
        Assert.Equal(77, rects.Count(r => r is { W: 1, H: 1, Odd: false }));
    }

    // The owner's second test of 2026-10-25, picture 16: a window's compartment, found two pixels off its grid's line, was moved
    // onto the window's outline five pixels further out (brighter than the line), and its cells read blank. Moved within
    // the window's inside only, it stands on its grid's line.
    [Fact]
    public void A_windows_compartment_moves_onto_its_grids_line_and_never_onto_its_outline()
    {
        var page = new StashReaderTests.Page(2560, 1440).Start();
        using (var canvas = new SKCanvas(page.Bitmap))
        using (var outline = new SKPaint { Color = new SKColor(231, 201, 97) })
        using (var black = new SKPaint { Color = SKColors.Black })
        {
            canvas.DrawRect(800, 400, 5 * page.Pitch + 14, 3 * page.Pitch + 14, outline);
            canvas.DrawRect(801, 401, 5 * page.Pitch + 12, 3 * page.Pitch + 12, black);
        }
        page.Grid(806, 406, 5, 3, 2);
        var gray = StashGray.Of(page.Bitmap);
        Assert.Equal((800, 400), StashGrids.Snap(gray, 803, 403, 5, 3, page.Pitch));
        var (x, y) = StashGrids.Snap(gray, 803, 403, 5, 3, page.Pitch, minX: 803, minY: 403);
        Assert.InRange(x, 806, 807);
        Assert.InRange(y, 406, 407);
    }

    // The layout round of 2026-10-28 (the owner's pictures of 2026-10-25 to 2026-10-27: a shotgun, a rifle and a stimulant
    // under the pointer): the game's hover lights an item's whole ground a neutral grey as light as its frame, so no line
    // shows round it, and its cells were joined with their neighbours' (a rifle named over a stimulant and an armour, a
    // shotgun's cells the pieces of two). The lit cells are one item, apart from every other; grey art over part of a row
    // (two magazine cases' lids) is no hover.
    [Theory]
    [InlineData(2560, 1440)]
    [InlineData(1920, 1080)]
    public void The_item_under_the_pointer_is_its_lit_cells_and_apart_from_its_neighbours(int width, int height)
    {
        var page = new StashReaderTests.Page(width, height).Start();
        var p = page.Pitch;
        int x0 = (int)(200 * page.K), y0 = (int)(200 * page.K);
        page.Grid(x0, y0, 6, 4, 1);
        page.Item(x0, y0, 1, 0, 0, 3, 1, "Rifle");
        page.Item(x0, y0, 1, 3, 0, 3, 1, "Rifle");
        page.Item(x0, y0, 1, 4, 1, 2, 1, "Case");
        page.Item(x0, y0, 1, 0, 2, 6, 1, "Rifle");
        page.Item(x0, y0, 1, 0, 3, 1, 1, "Med");
        using (var canvas = new SKCanvas(page.Bitmap))
        {
            // The hovered item, four by one: its ground and frame lit, a dark gun drawn across it.
            using var ground = new SKPaint { Color = new SKColor(94, 96, 99) };
            canvas.DrawRect(x0 - 1, y0 + p - 1, 4 * p + 3, p + 3, ground);
            using var gun = new SKPaint { Color = new SKColor(30, 32, 34) };
            canvas.DrawRect(x0 + 0.3f * p, y0 + 1.35f * p, 3.4f * p, 0.3f * p, gun);
            canvas.DrawRect(x0 + 1.2f * p, y0 + 1.5f * p, 0.25f * p, 0.4f * p, gun);
        }
        var grid = new StashGrid(x0, y0, 6, 4, StashGridPlace.Stash);
        var lit = StashGrids.Lit(grid, page.Bitmap.GetPixelSpan(), width, height, p);
        Assert.Equal(Enumerable.Range(0, 24).Select(k => k is >= 6 and <= 9).ToArray(), lit);
        var gray = StashGray.Of(page.Bitmap);
        var empty = StashGrids.Empty(grid, gray.HighPass(5), p);
        for (var k = 0; k < grid.Area; k++)
            empty[k] &= !lit[k];
        var rects = StashGrids.Items(grid, new StashLines(gray), new bool[grid.Area], empty, p, lit);
        Assert.Contains(rects, r => r is { I0: 0, J0: 1, W: 4, H: 1, Odd: false });
        Assert.Contains(rects, r => r is { I0: 0, J0: 0, W: 3, H: 1 });
        Assert.Contains(rects, r => r is { I0: 0, J0: 2, W: 6, H: 1 });
    }

    // The layout round of 2026-10-28 (the owner's picture of 2026-10-27 at the stash's top): the stash's first whole row
    // under its pane's head held two rifles' lower halves, five by two each, their tops and labels under the head, so no
    // line ran along its top and no cell of it was framed; and a shotgun under the pointer, its ground lit as light as its
    // frame, left one line showing in its two rows. The stash came out a row short, its first row lost, every cell read a
    // row off. It runs from its first whole row down, the lit rows between rows that show its own.
    [Theory]
    [InlineData(2560, 1440)]
    [InlineData(1920, 1080)]
    public void The_stash_runs_from_its_first_whole_row_down_whatever_lies_across_its_rows(int width, int height)
    {
        var page = new StashReaderTests.Page(width, height).Start();
        var (x, y) = page.StashOrigin;
        var p = page.Pitch;
        // The grid from a part-row above the first whole row, twelve whole rows under it.
        page.Grid(x, y - p, 10, 13, 1);
        page.Item(x, y - p, 1, 0, 0, 5, 2, "Rifle");
        page.Item(x, y - p, 1, 5, 0, 5, 2, "Rifle");
        page.Item(x, y - p, 1, 5, 3, 5, 2, "Shotgun");
        using (var canvas = new SKCanvas(page.Bitmap))
        {
            // The pane's head over the part-row's top quarter (the toolbar and the money).
            using var head = new SKPaint { Color = new SKColor(18, 20, 22) };
            canvas.DrawRect(x - p, 0, 11 * p, y - p + p / 4, head);
            // The item under the pointer: its ground lit as light as the lines round it.
            using var lit = new SKPaint { Color = new SKColor(96, 100, 104) };
            canvas.DrawRect(x, y + 2 * p - 2, 5 * p + 3, 2 * p + 5, lit);
        }
        var gray = StashGray.Of(page.Bitmap);
        var stash = Assert.Single(StashGrids.Find(new StashLines(gray), gray, width, height, p), g => g.Place == StashGridPlace.Stash);
        Assert.InRange(stash.Y0, y - 2, y + 2);
        Assert.Equal(12, stash.Rows);
        Assert.Equal(10, stash.Cols);
    }
}
