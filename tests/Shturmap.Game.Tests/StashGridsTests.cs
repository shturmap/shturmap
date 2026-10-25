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
}
