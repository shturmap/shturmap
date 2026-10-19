using Shturmap.Game.Screenshots;
using SkiaSharp;

namespace Shturmap.Game.Tests;

// The items read from a screenshot of the game's gear screen (docs/NEXT.md, "what you have"; the spike of
// 2026-10-11). The first tests draw a page themselves, laid out as the game's (the stash ten cells wide at the right,
// a thin light line between cells, the short name at a cell's top right, a count at its bottom right), with made-up
// item names and ids, so they run on any PC whose Windows has text recognition for English. The last ones read the
// owner's real screenshots against hand-written truths; those stay on the PC that took them (tests\fixtures\ocr\stash,
// ignored by git) and are skipped elsewhere.
public sealed class StashReaderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("shturmap-stash-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static StashReader Reader()
    {
        var reader = StashReader.Create("en");
        if (reader is null)
            Assert.Skip("This Windows has no text recognition language installed.");
        return reader;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Made-up items: ids of the data's shape, names that are no item's.
    private static string Id(int n) => n.ToString("D24");

    private static readonly StashCandidate Zorblat = new(Id(1), "Zorblat", null);
    private static readonly StashCandidate Quenvik = new(Id(2), "Quenvik", null);
    private static readonly StashCandidate Pindrake = new(Id(3), "Pindrake", null);
    private static readonly StashCandidate Tessaril = new(Id(4), "Tessaril", null);
    private static readonly StashCandidate Vantressimo = new(Id(5), "Vantressimo Alpha", null);
    // Lookalikes, as the game's keys to numbered rooms: the name differs in one digit. (The recognition misreads a
    // lone digit in the test's own font, 5 as o and 7 as 1; the game's keys carry three.)
    private static readonly StashCandidate Marloc214 = new(Id(6), "Marl 214", null);
    private static readonly StashCandidate Marloc206 = new(Id(7), "Marl 206", null);
    private static readonly StashCandidate Hollin = new(Id(8), "Hollin", null);
    private static readonly StashCandidate Drevak = new(Id(9), "Drevak", null);
    private static readonly StashCandidate Ostrel = new(Id(10), "Ostrel", null);

    private static readonly StashCandidate[] Everything = [Zorblat, Quenvik, Pindrake, Tessaril, Vantressimo, Marloc214, Marloc206, Hollin, Drevak, Ostrel];

    // ---- drawing pages as the game lays them out ----

    /// <summary>A page of the given size, drawn as the game draws its gear screen at that size.</summary>
    internal sealed class Page(int width, int height)
    {
        public int Width { get; } = width;
        public int Height { get; } = height;

        /// <summary>The scale of lengths laid out by the screen's height (the game's at 1440p).</summary>
        public float K { get; } = height / 1440f;

        /// <summary>The cell's size: 63 px at 1080p, scaled by the smaller of the two ratios.</summary>
        public int Pitch { get; } = (int)Math.Round(63 * Math.Min(width / 1920.0, height / 1080.0));

        /// <summary>The stash's top left: ten cells wide, its right edge 33 px (at 1440p) from the screen's, under the toolbar.</summary>
        public (int X, int Y) StashOrigin => (Width - (int)Math.Round(33 * K) - 10 * Pitch, (int)Math.Round(105 * K));

        public SKBitmap Bitmap { get; } = new(width, height);

        private static readonly SKColor Field = new(26, 29, 32);
        private static readonly SKColor Line = new(96, 100, 104);
        private static readonly SKColor WindowField = new(34, 37, 40);
        private static readonly SKColor WindowLine = new(118, 122, 126);

        public Page Start()
        {
            using var canvas = new SKCanvas(Bitmap);
            canvas.Clear(new SKColor(18, 20, 22)); // the screen behind the grids
            return this;
        }

        /// <summary>A grid of cells with lines of the given width between them (one pixel in the stash, two in a window).</summary>
        public void Grid(int x0, int y0, int cols, int rows, int lineWidth)
        {
            using var canvas = new SKCanvas(Bitmap);
            using var field = new SKPaint { Color = lineWidth == 1 ? Field : WindowField };
            using var line = new SKPaint { Color = lineWidth == 1 ? Line : WindowLine };
            canvas.DrawRect(x0, y0, cols * Pitch + lineWidth, rows * Pitch + lineWidth, field);
            for (var i = 0; i <= cols; i++)
                canvas.DrawRect(x0 + i * Pitch, y0, lineWidth, rows * Pitch + lineWidth, line);
            for (var j = 0; j <= rows; j++)
                canvas.DrawRect(x0, y0 + j * Pitch, cols * Pitch + lineWidth, lineWidth, line);
        }

        /// <summary>
        /// An item in a grid: its art over its cells (the lines inside it covered, as the game does), its short name
        /// at the top right (left-aligned and cut at the edge when it doesn't fit), a count or load at the bottom
        /// right, and the found-in-raid mark when asked for.
        /// </summary>
        public void Item(int x0, int y0, int lineWidth, int i, int j, int w, int h, string label, string? corner = null, bool mark = false, SKColor? art = null,
            SKColor? cornerColour = null)
        {
            using var canvas = new SKCanvas(Bitmap);
            float left = x0 + i * Pitch + lineWidth, top = y0 + j * Pitch + lineWidth;
            float right = x0 + (i + w) * Pitch, bottom = y0 + (j + h) * Pitch;
            using var field = new SKPaint { Color = lineWidth == 1 ? Field : WindowField };
            canvas.DrawRect(left, top, right - left, bottom - top, field);
            using var paint = new SKPaint { Color = art ?? new SKColor(118, 104, 80), IsAntialias = true };
            using var shade = new SKPaint { Color = new SKColor(70, 60, 44), IsAntialias = true };
            canvas.DrawRoundRect(left + 8 * K, top + 16 * K, right - left - 16 * K, bottom - top - 30 * K, 4 * K, 4 * K, paint);
            canvas.DrawRoundRect(left + 16 * K, top + 26 * K, (right - left) * 0.4f, (bottom - top) * 0.3f, 3 * K, 3 * K, shade);
            using var face = SKTypeface.FromFamilyName("Bahnschrift", SKFontStyle.Bold) ?? SKTypeface.Default;
            using var font = new SKFont(face, 13.5f * K);
            using var text = new SKPaint { Color = new SKColor(226, 229, 231), IsAntialias = true };
            using var outline = new SKPaint { Color = new SKColor(10, 12, 14), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.2f * K };
            var labelWidth = font.MeasureText(label);
            var fits = labelWidth <= right - left - 5 * K;
            canvas.Save();
            canvas.ClipRect(new SKRect(left, top, right, bottom));
            var x = fits ? right - 3 * K - labelWidth : left + 2 * K;
            canvas.DrawText(label, x, top + 13 * K, SKTextAlign.Left, font, outline);
            canvas.DrawText(label, x, top + 13 * K, SKTextAlign.Left, font, text);
            if (corner is not null)
            {
                using var cornerText = new SKPaint { Color = cornerColour ?? text.Color, IsAntialias = true };
                canvas.DrawText(corner, right - 3 * K, bottom - 4 * K, SKTextAlign.Right, font, outline);
                canvas.DrawText(corner, right - 3 * K, bottom - 4 * K, SKTextAlign.Right, font, cornerText);
            }
            canvas.Restore();
            if (mark)
            {
                // The game's mark: a light circle with a check, at the bottom right, over a count when there is one.
                float cx = right - 12 * K, cy = bottom - (corner is null ? 12 : 26) * K;
                using var ring = new SKPaint { Color = new SKColor(196, 200, 204), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.6f * K };
                canvas.DrawCircle(cx, cy, 6.3f * K, ring);
                canvas.DrawLine(cx - 3.6f * K, cy - 0.2f * K, cx - 1.2f * K, cy + 2.6f * K, ring);
                canvas.DrawLine(cx - 1.2f * K, cy + 2.6f * K, cx + 5.4f * K, cy - 4.6f * K, ring);
            }
        }

        public string Save(string root, string name)
        {
            var path = Path.Combine(root, name);
            using var data = Bitmap.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(path, data.ToArray());
            return path;
        }
    }

    /// <summary>The gear screen's stash with the items every stash test looks for.</summary>
    private static Page StashPage(int width, int height)
    {
        var page = new Page(width, height).Start();
        var (x, y) = page.StashOrigin;
        page.Grid(x, y, 10, 14, 1);
        page.Item(x, y, 1, 2, 1, 1, 1, "Zorblat", mark: true);
        page.Item(x, y, 1, 4, 1, 1, 1, "Quenvik", "250");
        page.Item(x, y, 1, 6, 2, 2, 1, "Pindrake", "30/30");
        page.Item(x, y, 1, 0, 3, 1, 2, "Tessaril");
        page.Item(x, y, 1, 8, 5, 1, 1, "Vantressimo Alpha");
        page.Item(x, y, 1, 1, 6, 1, 1, "Marl 214");
        page.Item(x, y, 1, 3, 6, 1, 1, "Marl 206");
        page.Item(x, y, 1, 7, 8, 1, 1, "Krellnot"); // an item no candidate names
        return page;
    }

    // The counts drawn are ones the recognition reads in the test's own font (it drops some two-digit numbers
    // drawn with Bahnschrift, "12" among them, while it reads the game's "12" and "70"; see the real screenshots).

    private static Dictionary<string, StashItemSeen> ById(StashGridReading grid) => grid.Items.ToDictionary(i => i.ItemId, StringComparer.Ordinal);

    [Theory]
    [InlineData(2560, 1440)]
    [InlineData(1920, 1080)]
    public async Task The_stash_is_read_by_its_labels_with_counts(int width, int height)
    {
        var path = StashPage(width, height).Save(_root, "stash.png");
        var reading = await Reader().ReadAsync(path, Everything, tries: 1, ct: Ct);
        Assert.NotNull(reading);
        Assert.False(reading.IsTradingScreen);
        var grid = Assert.Single(reading.Grids);
        Assert.Equal(StashGridKind.Stash, grid.Kind);
        var items = ById(grid);
        Assert.Equal([Id(1), Id(2), Id(3), Id(4), Id(5), Id(6), Id(7)], items.Keys.Order());
        Assert.Equal(1, items[Id(1)].Count);
        Assert.Equal(250, items[Id(2)].Count); // a stack's count
        Assert.Equal(1, items[Id(3)].Count); // a magazine's load is no count
        Assert.Equal(1, items[Id(5)].Count); // the long name, cut at the cell's edge
        // The lookalikes differ by a digit alone: each is read as itself.
        Assert.Equal(1, items[Id(6)].Count);
        Assert.Equal(1, items[Id(7)].Count);
    }

    [Fact]
    public async Task The_found_in_raid_mark_is_seen_where_it_is_and_not_where_it_isnt()
    {
        var path = StashPage(2560, 1440).Save(_root, "marks.png");
        var reading = await Reader().ReadAsync(path, Everything, tries: 1, ct: Ct);
        Assert.NotNull(reading);
        var items = ById(Assert.Single(reading.Grids));
        Assert.True(items[Id(1)].FoundInRaid);
        Assert.NotEqual(true, items[Id(2)].FoundInRaid);
        Assert.NotEqual(true, items[Id(4)].FoundInRaid);
    }

    [Fact]
    public async Task A_container_window_over_the_stash_is_read_and_hides_the_cells_under_it()
    {
        var page = StashPage(2560, 1440);
        var (x, y) = page.StashOrigin;
        // A five by five window over the stash's first two columns, rows 2 to 6, with the game's two-pixel lines.
        int wx = x - 3 * page.Pitch + 10, wy = y + 2 * page.Pitch + 7;
        page.Grid(wx, wy, 5, 5, 2);
        page.Item(wx, wy, 2, 1, 1, 1, 1, "Hollin");
        page.Item(wx, wy, 2, 3, 2, 1, 1, "Drevak", "330");
        var path = page.Save(_root, "window.png");
        var reading = await Reader().ReadAsync(path, Everything, tries: 1, ct: Ct);
        Assert.NotNull(reading);
        Assert.Equal(2, reading.Grids.Count);
        var stash = Assert.Single(reading.Grids, g => g.Kind == StashGridKind.Stash);
        var window = Assert.Single(reading.Grids, g => g.Kind == StashGridKind.Window);
        Assert.Equal([Id(8), Id(9)], ById(window).Keys.Order());
        Assert.Equal(330, ById(window)[Id(9)].Count);
        // Tessaril and Marl 214 stood in the stash's first two columns under the window; the rest of the stash is
        // read as before.
        Assert.Equal([Id(1), Id(2), Id(3), Id(5), Id(7)], ById(stash).Keys.Order());
    }

    [Fact]
    public async Task The_trading_screen_is_told_by_the_traders_grid_at_the_left_and_nothing_is_read()
    {
        var page = StashPage(2560, 1440);
        // The trader's grid hangs on the screen's left edge.
        page.Grid(11, 351, 9, 11, 1);
        page.Item(11, 351, 1, 0, 0, 1, 1, "Ostrel");
        page.Item(11, 351, 1, 4, 2, 1, 1, "Hollin");
        var path = page.Save(_root, "trading.png");
        var reading = await Reader().ReadAsync(path, Everything, tries: 1, ct: Ct);
        Assert.NotNull(reading);
        Assert.True(reading.IsTradingScreen);
        Assert.Empty(reading.Grids);
    }

    // The messages screen (owner, 2026-10-11: delivered items "will be on the left hand side of the screen"): a
    // message's attachments stand in a grid of the stash's width at the left, set in from the edge by most of a cell
    // where the trader's grid hangs on the edge, level with the stash. They aren't the player's until taken.
    [Fact]
    public async Task The_messages_screens_attachments_at_the_left_are_never_read_and_the_stash_beside_them_is()
    {
        var page = StashPage(2560, 1440);
        var (_, y) = page.StashOrigin;
        page.Grid(64, y, 10, 14, 1);
        page.Item(64, y, 1, 0, 0, 1, 1, "Ostrel");
        page.Item(64, y, 1, 3, 0, 1, 1, "Hollin");
        var path = page.Save(_root, "messages.png");
        var reading = await Reader().ReadAsync(path, Everything, tries: 1, ct: Ct);
        Assert.NotNull(reading);
        Assert.False(reading.IsTradingScreen);
        var stash = Assert.Single(reading.Grids);
        Assert.Equal(StashGridKind.Stash, stash.Kind);
        Assert.Equal([Id(1), Id(2), Id(3), Id(4), Id(5), Id(6), Id(7)], ById(stash).Keys.Order());
    }

    [Fact]
    public async Task A_window_dragged_left_of_the_middle_is_still_a_window()
    {
        var page = StashPage(2560, 1440);
        page.Grid(400, 300, 5, 5, 2);
        page.Item(400, 300, 2, 2, 2, 1, 1, "Hollin");
        var path = page.Save(_root, "left-window.png");
        var reading = await Reader().ReadAsync(path, Everything, tries: 1, ct: Ct);
        Assert.NotNull(reading);
        Assert.False(reading.IsTradingScreen);
        Assert.Equal([Id(8)], ById(Assert.Single(reading.Grids, g => g.Kind == StashGridKind.Window)).Keys);
    }

    [Fact]
    public async Task A_screen_without_grids_gives_no_grids()
    {
        var page = new Page(2560, 1440).Start();
        var path = page.Save(_root, "plain.png");
        var reading = await Reader().ReadAsync(path, Everything, tries: 1, ct: Ct);
        Assert.NotNull(reading);
        Assert.False(reading.IsTradingScreen);
        Assert.Empty(reading.Grids);
    }

    // The art is a veto, never a source: a label that names an item whose grid image shows clearly other art says
    // nothing. Here the cell's art is a filled white square and the image a thin diagonal line.
    [Fact]
    public async Task A_label_whose_cell_shows_clearly_other_art_than_the_items_image_names_nothing()
    {
        var page = new Page(2560, 1440).Start();
        var (x, y) = page.StashOrigin;
        page.Grid(x, y, 10, 14, 1);
        page.Item(x, y, 1, 2, 1, 1, 1, "Zorblat", art: SKColors.White);
        page.Item(x, y, 1, 5, 1, 1, 1, "Quenvik");
        var path = page.Save(_root, "veto.png");
        using var template = new SKBitmap(64, 64);
        using (var canvas = new SKCanvas(template))
        {
            canvas.Clear(new SKColor(26, 29, 32));
            using var pen = new SKPaint { Color = new SKColor(200, 200, 200), StrokeWidth = 2, IsAntialias = true, Style = SKPaintStyle.Stroke };
            canvas.DrawLine(6, 58, 58, 6, pen);
        }
        using var png = template.Encode(SKEncodedImageFormat.Png, 100);
        var candidates = new[] { Zorblat with { GridImage = png.ToArray() }, Quenvik };
        var reading = await Reader().ReadAsync(path, candidates, tries: 1, ct: Ct);
        Assert.NotNull(reading);
        Assert.Equal([Id(2)], ById(Assert.Single(reading.Grids)).Keys);
        // Without the image the label alone decides.
        var alone = await Reader().ReadAsync(path, [Zorblat, Quenvik], tries: 1, ct: Ct);
        Assert.NotNull(alone);
        Assert.Equal([Id(1), Id(2)], ById(Assert.Single(alone.Grids)).Keys.Order());
    }

    [Fact]
    public async Task What_is_no_picture_is_let_be()
    {
        var reader = Reader();
        var empty = Path.Combine(_root, "empty.png");
        File.WriteAllText(empty, "");
        var text = Path.Combine(_root, "text.png");
        File.WriteAllText(text, "picture");
        var wait = TimeSpan.FromMilliseconds(5);
        Assert.Null(await reader.ReadAsync(empty, Everything, tries: 2, wait, Ct));
        Assert.Null(await reader.ReadAsync(text, Everything, tries: 2, wait, Ct));
        Assert.Null(await reader.ReadAsync(Path.Combine(_root, "gone.png"), Everything, tries: 2, wait, Ct));
    }

    [Theory]
    [InlineData("Zorblat", "Zorblat", 1.0)]
    [InlineData("Zorb1at", "Zorblat", 1.0)] // an l read as 1 folds the same
    [InlineData("Vantressim", "Vantressimo Alpha", 1.0)] // cut at the cell's edge
    [InlineData("Pindrak4", "Pindrake", 0.875)] // the cut letter read as anything
    [InlineData("Marl 214", "Marl 206", 0.5)] // the digits must agree
    [InlineData("Mar1 214", "Marl 214", 1.0)]
    [InlineData("Zorblat!", "Zorblat", 1.0)] // the cell's edge read as a stroke
    [InlineData("Tessa", "Tessaril", 0.625)]
    public void A_label_fits_a_name_by_its_folded_letters(string label, string name, double fit) =>
        Assert.Equal(fit, StashLabels.Fit(label, name), 3);

    [Theory]
    [InlineData("250", 250)]
    [InlineData("12", 12)]
    [InlineData("30/30", 1)]
    [InlineData("2/3", 1)]
    [InlineData("", 1)]
    [InlineData(null, 1)]
    [InlineData("366156", 9999)]
    [InlineData("0", 1)]
    [InlineData("til 30", 1)] // letters among the digits: an icon read as words
    public void A_count_is_digits_alone(string? text, int count) => Assert.Equal(count, StashLabels.Count(text));

    // ---- tarkov.dev's grid images, where the PC's cache has them ----

    [Fact]
    public void Cached_grid_images_load_as_templates_of_whole_cells()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shturmap", "cache", "game-art");
        var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "grid-*.png").Order(StringComparer.Ordinal).Take(12).ToList() : [];
        if (files.Count == 0)
            Assert.Skip("No grid images in the local cache: they are never fetched by a test.");
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file);
            var size = StashTemplate.Size(bytes);
            var template = StashTemplate.Load(bytes);
            Assert.Equal(size is not null, template is not null);
            if (size is { } s && template is not null)
                Assert.Equal((s.W, s.H), (template.W, template.H));
        }
    }

    // ---- real screenshots, where a PC has them ----

    private static string Fixtures => Path.Combine(AppContext.BaseDirectory, "fixtures", "ocr", "stash");

    /// <summary>One line of a truth: the grid (A is the biggest), the cell, the item's short name.</summary>
    private sealed record TruthCell(char Grid, int I, int J, string Name);

    /// <summary>The truths on this PC: the screenshot each is of, and the cells it lists ("A 3,0 Short name" per
    /// line).</summary>
    private static List<(string Screenshot, List<TruthCell> Cells)> Truths()
    {
        if (!Directory.Exists(Fixtures))
            return [];
        return Directory.GetFiles(Fixtures, "truth-*.txt").Order(StringComparer.Ordinal)
            .Select(truth => (
                Path.Combine(Fixtures, Path.GetFileNameWithoutExtension(truth)["truth-".Length..] + ".png"),
                File.ReadAllLines(truth).Where(l => l.Trim().Length > 0).Select(l =>
                {
                    var parts = l.Split(' ', 3);
                    var cell = parts[1].Split(',');
                    return new TruthCell(parts[0][0], int.Parse(cell[0]), int.Parse(cell[1]), parts[2].Trim());
                }).ToList()))
            .Where(t => File.Exists(t.Item1))
            .ToList();
    }

    private static readonly string[] Decoys =
    [
        "Zorblat", "Quenvik", "Pindrake", "Tessaril", "Vantressimo", "Marl 214", "Hollin", "Drevak", "Ostrel", "Krellnot",
        "Brindlecap", "Sorvane", "Tulgrim", "Pellowick", "Hastrine", "Dornacle", "Vexmoor", "Quillbert", "Ashfold 3", "Rimsley",
    ];

    // The truths were written against the grids the spike found; the reader finds the stash's rows above those too,
    // so a truth's rows may start some rows down in the reading: the offset that matches the most names. Each truth
    // cell's rectangle must then name the truth's item or nothing; a decoy named anywhere is wrong; nine in ten of
    // the truth's items must be named.
    [Fact]
    public async Task Real_screenshots_are_read_with_no_wrong_name_and_most_of_the_truths_items()
    {
        var truths = Truths();
        if (truths.Count == 0)
            Assert.Skip(@"Real screenshots stay out of the repository: none with a truth on this PC (tests\fixtures\ocr\stash).");
        var reader = Reader();
        foreach (var (screenshot, cells) in truths)
        {
            var file = Path.GetFileName(screenshot);
            // The candidates: the truth's names once each, and twenty names no item has.
            var names = cells.Select(c => c.Name).Distinct(StringComparer.Ordinal).ToList();
            var candidates = names.Concat(Decoys.Where(d => !names.Contains(d)))
                .Select((name, n) => new StashCandidate(Id(100 + n), name, null)).ToList();
            var byId = candidates.ToDictionary(c => c.ItemId, c => c.ShortName, StringComparer.Ordinal);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var look = await reader.LookAsync(screenshot, candidates, tries: 1, wait: null, Ct);
            watch.Stop();
            Assert.NotNull(look);
            Assert.False(look.IsTradingScreen);
            Assert.NotEmpty(look.Grids);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"{file}: {watch.ElapsedMilliseconds} ms");
            var said = look.Grids.SelectMany(g => g.Cells).Where(c => c.ItemId is not null).Select(c => byId[c.ItemId!]).ToList();
            Assert.DoesNotContain(said, Decoys.Contains);
            int right = 0;
            var wrong = new List<string>();
            foreach (var group in cells.GroupBy(c => c.Grid))
            {
                var grid = look.Grids[group.Key - 'A'];
                string? Said(int i, int j) => grid.Cells.FirstOrDefault(c => c.Rect.I0 == i && c.Rect.J0 == j)?.ItemId is { } id ? byId[id] : null;
                var offset = Enumerable.Range(0, grid.Grid.Rows).Select(d => (d, Matches: group.Count(c => Said(c.I, c.J + d) == c.Name)))
                    .OrderByDescending(x => x.Matches).ThenBy(x => x.d).First().d;
                foreach (var cell in group)
                {
                    var name = Said(cell.I, cell.J + offset);
                    if (name == cell.Name)
                        right++;
                    else if (name is not null)
                        wrong.Add($"{cell.Grid} {cell.I},{cell.J}: {name} for {cell.Name}");
                }
            }
            Assert.True(wrong.Count == 0, $"{file}: wrong: {string.Join("; ", wrong)}");
            Assert.True(right >= 0.9 * cells.Count, $"{file}: {right} of {cells.Count} named");
        }
    }

    [Fact]
    public async Task Real_trading_screens_are_told()
    {
        var list = Path.Combine(Fixtures, "trading.txt");
        var screens = File.Exists(list) ? File.ReadAllLines(list).Where(l => l.Trim().Length > 0).Select(l => Path.Combine(Fixtures, l.Trim())).Where(File.Exists).ToList() : [];
        if (screens.Count == 0)
            Assert.Skip(@"Real screenshots stay out of the repository: no trading screens listed on this PC (tests\fixtures\ocr\stash\trading.txt).");
        var reader = Reader();
        foreach (var screen in screens)
        {
            var reading = await reader.ReadAsync(screen, Everything, tries: 1, ct: Ct);
            Assert.NotNull(reading);
            Assert.True(reading.IsTradingScreen, Path.GetFileName(screen));
            Assert.Empty(reading.Grids);
        }
    }
}
