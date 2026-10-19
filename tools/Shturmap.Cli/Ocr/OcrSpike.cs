using System.Diagnostics;
using System.Globalization;
using System.Text;
using Shturmap.Core.Logs;
using Shturmap.Core.Screenshots;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Screenshots;
using Shturmap.Session;
using SkiaSharp;
using Windows.Media.Ocr;
#if OCR_SPIKE
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
#endif

namespace Shturmap.Cli.Ocr;

/// <summary>
/// <c>shturmap-cli ocr-compare &lt;png|folder&gt;</c>: the spike of 2026-10-19 that asks whether Windows' own text
/// recognition is the best reader of the cells' short names. For every picture with a hand-written truth beside it
/// (<c>truth-&lt;picture&gt;.txt</c>, the stash spike's format), the cells are found as the readers find them
/// (<see cref="StashGrids"/>) and each truth cell's label is read in several ways: Windows as the reader reads it (its
/// own loop of up to nine readings, <see cref="StashReader"/>), Windows on the reader's first two crops alone and on them
/// enlarged otherwise, and, built with <c>-p:OcrSpike=true</c>, PaddleOCR's recognition models through ONNX Runtime on
/// the CPU (the model files are given with <c>--paddle &lt;onnx&gt; &lt;dict&gt;</c>; none is in the repository).
/// Each reading is scored against the truth (exact after folding, the fit at 0.9 and 0.8, wrong, nothing), against the
/// whole catalogue at the desk's fit (named right, wrong, unread, "one of N"), with the time each took and the letters
/// each engine confuses. The pictures are the owner's own, in the git-ignored tests\fixtures\ocr; nothing of them is
/// written anywhere unless --dump names a folder; it prints item names, never a path's folder. Nothing of it runs in
/// the app.
/// </summary>
internal static class OcrSpike
{
    // The reader's crops (StashReader: ReadLabelPartAsync and ReadLabelBandAsync, private there): the label's band starts
    // three pixels below the cell's top, the part is 0.42 of a cell high, the band 0.2, the band set in a dark margin.
    private const int LineGap = 3;
    private const int Margin = 24;
    private static readonly SKColor MarginColour = new(24, 29, 33);
    private static int ZoomFor(int pitch) => Math.Clamp((int)Math.Round(336 / (double)pitch), 2, 8);

    private sealed record TruthCell(char Grid, int I, int J, string Name);

    private sealed record Cell(string Page, TruthCell Truth, StashRect Rect, SKRectI Pixels, int Pitch, SKBitmap Picture);

    private sealed record Reading(string Text, double Ms, int Crops = 1);

    // A way of reading a cell: its name, the engine, and the readings it gave, one per cell.
    private sealed class Variant(string name, string engine, bool tolerant = false)
    {
        public string Name { get; } = name;
        public string Engine { get; } = engine;
        public bool Tolerant { get; } = tolerant;
        public readonly Dictionary<Cell, Reading> Readings = new();
    }

    public static async Task RunAsync(IReadOnlyList<string> args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string? truths = null, dump = null;
        var cellsOut = false;
        var gridsOut = false;
        var threads = 1;
        var paddle = new List<(string Model, string Dict)>();
        var inputs = new List<string>();
        var skipZooms = false;
        var deskCheck = false;
        var deskOnly = false;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--truths": truths = args[++i]; break;
                case "--dump": dump = args[++i]; break;
                case "--cells": cellsOut = true; break;
                case "--grids": gridsOut = true; break;
                case "--threads": threads = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--paddle": paddle.Add((args[i + 1], args[i + 2])); i += 2; break;
                case "--no-zooms": skipZooms = true; break;
                case "--desk": deskCheck = true; break;
                case "--desk-only": deskCheck = deskOnly = true; break;
                default: inputs.Add(args[i]); break;
            }
        }
        if (inputs.Count == 0)
        {
            Console.WriteLine("Use: shturmap-cli ocr-compare <png or folder>... [--truths <folder>] [--paddle <model.onnx> <dict.txt>]... [--threads <n>]");
            Console.WriteLine("                  [--cells] [--grids] [--dump <folder>] [--no-zooms] [--desk | --desk-only]");
            Console.WriteLine("  every picture with truth-<picture>.txt beside it (or in --truths) is read: Windows as the reader reads it and on its");
            Console.WriteLine("  crops alone (enlarged 2, 3, 4, 8 and 12 times), and each --paddle model (built with -p:OcrSpike=true), scored");
            Console.WriteLine("  against the truth and the whole catalogue at the desk's fit, with times and the letters each engine confuses.");
            Console.WriteLine("  footprints-<picture>.txt beside a truth (\"A 0,7 2x1\") gives the items' footprints: they are read there, and --desk");
            Console.WriteLine("  tells the desk's \"?\" cells on that page apart: a piece of a larger item (the grid finder's split) or a whole item unread.");
            return;
        }
        var files = inputs.SelectMany(p => Directory.Exists(p) ? Directory.GetFiles(p, "*.png").Order(StringComparer.Ordinal).ToArray() : [p]).ToList();

        // The catalogue from the cache, as the desk reads against it: no request.
        var paths = AppPaths.Default;
        var http = CachedHttp.CreateClient(new NoRequests());
        var loader = new GameDataLoader(new CachedHttp(http, paths.DataCache));
        var data = await loader.LoadAsync(GameMode.Pve, "en");
        var sources = await loader.LoadSourcesAsync(GameMode.Pve, "en");
        var candidates = DeskData.Candidates(data, sources).ToList();
        var catalogue = new DeskMatch(candidates);
        var matcher = new Matcher(candidates);
        var shortNames = candidates.Select(c => c.ShortName).ToHashSet(StringComparer.Ordinal);
        Console.WriteLine($"Catalogue: {catalogue.Count} items, {shortNames.Count} short names (cache only)");

        var reader = StashReader.Create("en");
        if (reader is null)
        {
            Console.WriteLine("This Windows has no text recognition language: no reading.");
            return;
        }
        Console.WriteLine($"Windows recognition: {reader.LanguageTag}, the largest picture it takes {OcrEngine.MaxImageDimension} px");
        if (dump is not null)
            Directory.CreateDirectory(dump);

        // ---- the cells: the truth's, where the grid finder found an item rectangle starting there ----
        var cells = new List<Cell>();
        var pictures = new List<SKBitmap>();
        var pageRects = new Dictionary<string, int>(StringComparer.Ordinal);
        int truthCount = 0, noRect = 0, cutByFinder = 0;
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var truthPath = new[] { Path.GetDirectoryName(file)!, truths }.OfType<string>()
                .Select(d => Path.Combine(d, "truth-" + name + ".txt")).FirstOrDefault(File.Exists);
            if (truthPath is null && !gridsOut)
                continue;
            var picture = StashPicture.Decode(await File.ReadAllBytesAsync(file));
            if (picture is null)
                continue;
            pictures.Add(picture);
            var truth = truthPath is null ? [] : ReadTruth(truthPath);
            // Each item's footprint in cells as the picture shows it ("A 0,7 2x1"; one by one when not listed), where a
            // footprints-<picture>.txt stands beside the truth.
            var footprintPath = truthPath is null ? null : Path.Combine(Path.GetDirectoryName(truthPath)!, "footprints-" + name + ".txt");
            var footprints = footprintPath is not null && File.Exists(footprintPath) ? ReadFootprints(footprintPath) : null;
            var unknown = truth.Where(t => !shortNames.Contains(t.Name)).Select(t => t.Name).Distinct().ToList();
            var scale = Math.Min(picture.Width / 1920.0, picture.Height / 1080.0);
            var pitch = (int)Math.Round(StashTemplate.BaseCell * scale);
            var gray = StashGray.Of(picture);
            var lines = new StashLines(gray);
            var all = StashGrids.Find(lines, gray, picture.Width, picture.Height, pitch);
            var grids = all.Where(g => g.Place != StashGridPlace.Message).ToList();
            var highPassed = gray.HighPass((int)Math.Round(4 * scale));
            var here = 0;
            Console.WriteLine($"== {name}: pitch {pitch}, {grids.Count} grids, {truth.Count} truth cells" + (unknown.Count > 0 ? $"; NOT IN THE CATALOGUE: {string.Join(", ", unknown)}" : ""));
            for (var gi = 0; gi < grids.Count; gi++)
            {
                var grid = grids[gi];
                var letter = (char)('A' + gi);
                var rects = StashGrids.Items(grid, lines, StashGrids.Hidden(grid, all, pitch), StashGrids.Empty(grid, highPassed, pitch), pitch);
                here += rects.Count;
                var mine = truth.Where(t => t.Grid == letter).ToList();
                // The truth's row 0 may be a row lower in the reading: the offset that starts the most rectangles at the truth's cells.
                var offset = Enumerable.Range(0, Math.Max(1, grid.Rows))
                    .Select(d => (d, Hits: mine.Count(t => rects.Any(r => r.I0 == t.I && r.J0 == t.J + d))))
                    .OrderByDescending(x => x.Hits).ThenBy(x => x.d).First().d;
                if (gridsOut)
                {
                    Console.WriteLine($"   {letter} {grid.Place} {grid.Cols}×{grid.Rows} at ({grid.X0},{grid.Y0}), {rects.Count} rectangles" + (mine.Count > 0 ? $", the truth's row 0 is row {offset}" : ""));
                    foreach (var r in rects)
                        Console.WriteLine($"      [{r.I0},{r.J0 - offset}] {r.W}×{r.H}{(r.Odd ? " odd" : "")}");
                }
                var items = new List<(TruthCell Truth, StashRect Rect)>();
                foreach (var t in mine)
                {
                    truthCount++;
                    var rect = rects.FirstOrDefault(r => r.I0 == t.I && r.J0 == t.J + offset);
                    if (footprints is not null)
                    {
                        // The item read where it lies, whatever the grid finder made of it: its label at its footprint's top right.
                        var (fw, fh) = footprints.GetValueOrDefault((t.Grid, t.I, t.J), (1, 1));
                        if (rect is null || rect.W != fw || rect.H != fh)
                        {
                            cutByFinder++;
                            Console.WriteLine($"   the grid finder has {(rect is null ? "no rectangle" : $"{rect.W}×{rect.H}")} at {t.Grid} {t.I},{t.J} ({t.Name}, {fw}×{fh})");
                        }
                        rect = new StashRect(t.I, t.J + offset, fw, fh, false);
                    }
                    else if (rect is null || rect.Odd)
                    {
                        noRect++;
                        Console.WriteLine($"   no rectangle of its own at {t.Grid} {t.I},{t.J} ({t.Name})" + (rect is { Odd: true } ? ": an odd shape" : ""));
                        continue;
                    }
                    var pixels = SKRectI.Create(grid.X0 + rect.I0 * pitch, grid.Y0 + rect.J0 * pitch, rect.W * pitch, rect.H * pitch);
                    cells.Add(new Cell(name, t, rect, pixels, pitch, picture));
                    items.Add((t, rect));
                }
                if (deskCheck && footprints is not null && items.Count > 0)
                    await DeskSplitsAsync(reader, picture, DeskData.Catalogue(data, sources), grid, pitch, items, data);
            }
            pageRects[name] = here;
        }
        Console.WriteLine($"{cells.Count} cells to read ({truthCount} in the truths, {noRect} without a rectangle of their own; " +
                          $"{cutByFinder} read at the footprint given where the grid finder cut the item otherwise) on {pageRects.Count(p => cells.Any(c => c.Page == p.Key))} pages");
        if (cells.Count == 0 || deskOnly)
            return;

        // ---- Windows, one way after the other ----
        var engine = reader.Engine;
        var variants = new List<Variant>();
        var loop = new Variant("Windows, the reader's loop", "Windows");
        var loopTolerant = new Variant("Windows, the reader's loop, tolerant fold", "Windows", tolerant: true);
        foreach (var cell in cells)
        {
            var sw = Stopwatch.StartNew();
            var crops = 0;
            var (label, _, _) = await reader.ReadLabelAsync(cell.Picture, cell.Pixels, cell.Pitch,
                text => { crops++; return catalogue.Match(text, cell.Rect.W); }, DeskMatch.Enough, CancellationToken.None, Confirm);
            loop.Readings[cell] = new Reading(label ?? "", sw.Elapsed.TotalMilliseconds, crops);
            sw.Restart();
            crops = 0;
            (label, _, _) = await reader.ReadLabelAsync(cell.Picture, cell.Pixels, cell.Pitch,
                text => { crops++; return matcher.Match(text, cell.Rect.W, tolerant: true); }, DeskMatch.Enough, CancellationToken.None, Confirm);
            loopTolerant.Readings[cell] = new Reading(label ?? "", sw.Elapsed.TotalMilliseconds, crops);
        }
        variants.Add(loop);
        variants.Add(loopTolerant);

        var zooms = skipZooms ? new double[] { 0 } : [2, 3, 0, 8, 12];
        foreach (var crop in new[] { "part", "band" })
        {
            foreach (var zoom in zooms)
            {
                var label = zoom == 0 ? "the reader's ×4" : $"×{zoom} bicubic";
                var v = new Variant($"Windows, {crop} {label}", "Windows");
                foreach (var cell in cells)
                {
                    var z = zoom == 0 ? ZoomFor(cell.Pitch) : zoom;
                    using var image = crop == "part" ? Part(cell, z) : Band(cell, z, Margin);
                    if (image.Width > OcrEngine.MaxImageDimension || image.Height > OcrEngine.MaxImageDimension)
                    {
                        v.Readings[cell] = new Reading("", 0, 0);
                        continue;
                    }
                    var sw = Stopwatch.StartNew();
                    var text = await WindowsAsync(engine, image, crop == "part" ? cell.Pitch * 0.14 * z : null);
                    v.Readings[cell] = new Reading(text, sw.Elapsed.TotalMilliseconds);
                    if (dump is not null && zoom == 0)
                        Save(image, Path.Combine(dump, $"{Safe(cell)}-{crop}.png"));
                }
                variants.Add(v);
                if (crop == "band" && zoom == 0)
                {
                    var tolerant = new Variant("Windows, band the reader's ×4, tolerant fold", "Windows", tolerant: true);
                    foreach (var (c, r) in v.Readings)
                        tolerant.Readings[c] = r;
                    variants.Add(tolerant);
                }
            }
        }

        // The harness's crops against the reader's own: its first two readings (the part and the band at its zoom) must
        // read as the harness's do.
        var part4 = variants.First(v => v.Name == "Windows, part the reader's ×4");
        var band4 = variants.First(v => v.Name == "Windows, band the reader's ×4");
        int same = 0, compared = 0;
        foreach (var cell in cells)
        {
            var seen = new List<string>();
            await reader.ReadLabelAsync(cell.Picture, cell.Pixels, cell.Pitch, text => { seen.Add(text); return (0.0, 0); }, 2, CancellationToken.None);
            // The reader stops after its first two readings when either read something (nothing fits at 2).
            var mine = new[] { part4.Readings[cell].Text, band4.Readings[cell].Text }.Where(t => t.Length > 0).ToList();
            if (mine.Count == 0)
                continue;
            compared++;
            if (mine.SequenceEqual(seen))
                same++;
        }
        Console.WriteLine($"The harness's part and band crops read as the reader's first two readings for {same} of {compared} cells");

#if OCR_SPIKE
        // ---- PaddleOCR's recognition, one model after the other ----
        foreach (var (model, dict) in paddle)
        {
            var sw = Stopwatch.StartNew();
            using var rec = new PaddleRec(model, dict, threads);
            Console.WriteLine($"Paddle {rec.Name}: {new FileInfo(model).Length / 1e6:0.0} MB, {rec.Classes} classes, input height {rec.Height}, loaded in {sw.ElapsedMilliseconds} ms, {threads} thread(s)");
            var band = new Variant($"{rec.Name}, band native", rec.Name);
            var band4x = new Variant($"{rec.Name}, band the reader's ×4", rec.Name);
            // A first run warms the session (its first inference allocates).
            using (var warm = Band(cells[0], 1, 4))
                rec.Read(warm);
            foreach (var cell in cells)
            {
                using (var image = Band(cell, 1, 4))
                {
                    var t = Stopwatch.StartNew();
                    var text = rec.Read(image);
                    band.Readings[cell] = new Reading(text, t.Elapsed.TotalMilliseconds);
                }
                using (var image = Band(cell, ZoomFor(cell.Pitch), Margin))
                {
                    var t = Stopwatch.StartNew();
                    var text = rec.Read(image);
                    band4x.Readings[cell] = new Reading(text, t.Elapsed.TotalMilliseconds);
                }
            }
            variants.Add(band);
            variants.Add(band4x);
            var tolerant = new Variant($"{rec.Name}, band native, tolerant fold", rec.Name, tolerant: true);
            foreach (var (c, r) in band.Readings)
                tolerant.Readings[c] = r;
            variants.Add(tolerant);
            // Windows first, the model only where Windows' loop names nothing at 0.9: what it adds as a second reader.
            var both = new Variant($"Windows loop, then {rec.Name} band where unread", "Windows+" + rec.Name);
            foreach (var cell in cells)
            {
                var w = loop.Readings[cell];
                var (fit, _) = catalogue.Match(w.Text, cell.Rect.W);
                both.Readings[cell] = fit >= DeskMatch.Enough ? w : new Reading(band.Readings[cell].Text, w.Ms + band.Readings[cell].Ms, w.Crops + 1);
            }
            variants.Add(both);
        }
#else
        if (paddle.Count > 0)
            Console.WriteLine("This build has no ONNX Runtime: build the CLI with -p:OcrSpike=true for the --paddle models.");
#endif

        Report(variants, cells, matcher, catalogue, pageRects, cellsOut);
        foreach (var p in pictures)
            p.Dispose();
    }

    // A label of three letters or fewer is read once more (the desk's Confirm): a longer reading that fits wins.
    private static bool Confirm(string label) => ExitList.Fold(label).Length <= 3;

    // ---- the crops ----

    private static SKBitmap Part(Cell cell, double zoom)
    {
        var part = SKRectI.Create(cell.Pixels.Left + 1, cell.Pixels.Top + LineGap, cell.Pixels.Width - 2, (int)Math.Round(cell.Pitch * 0.42));
        return StashPicture.Block(cell.Picture, part, (int)Math.Round(part.Width * zoom), (int)Math.Round(part.Height * zoom), StashPicture.Sampling);
    }

    private static SKBitmap Band(Cell cell, double zoom, int margin)
    {
        var band = SKRectI.Create(cell.Pixels.Left + 2, cell.Pixels.Top + LineGap, cell.Pixels.Width - 4, (int)Math.Round(cell.Pitch * 0.2));
        using var block = StashPicture.Block(cell.Picture, band, (int)Math.Round(band.Width * zoom), (int)Math.Round(band.Height * zoom), StashPicture.Sampling);
        return StashPicture.Framed(block, margin, MarginColour);
    }

    // Windows' recognition on a crop: the part's words that start in the label's band (the art below gives words of its
    // own), or every line of a band.
    private static async Task<string> WindowsAsync(OcrEngine engine, SKBitmap image, double? wordTop)
    {
        using var soft = StashPicture.ToSoftwareBitmap(image);
        var result = await engine.RecognizeAsync(soft);
        if (wordTop is { } top)
            return string.Join(" ", result.Lines.SelectMany(l => l.Words).Where(w => w.BoundingRect.Y <= top).OrderBy(w => w.BoundingRect.X).Select(w => w.Text)).Trim();
        return string.Join(" ", result.Lines.Select(l => l.Text)).Trim();
    }

    private static void Save(SKBitmap bitmap, string path)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, png.ToArray());
    }

    private static string Safe(Cell cell) => new string($"{cell.Page}-{cell.Truth.Grid}{cell.Truth.I}-{cell.Truth.J}-{cell.Truth.Name}"
        .Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_').ToArray());

    private static Dictionary<(char Grid, int I, int J), (int W, int H)> ReadFootprints(string path)
    {
        var footprints = new Dictionary<(char, int, int), (int, int)>();
        foreach (var line in File.ReadAllLines(path).Where(l => l.Trim().Length > 0 && !l.StartsWith('#')))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cell = parts[1].Split(',');
            var size = parts[2].Split('x');
            footprints[(parts[0][0], int.Parse(cell[0], CultureInfo.InvariantCulture), int.Parse(cell[1], CultureInfo.InvariantCulture))] =
                (int.Parse(size[0], CultureInfo.InvariantCulture), int.Parse(size[1], CultureInfo.InvariantCulture));
        }
        return footprints;
    }

    /// <summary>
    /// The stash desk's reading of the page (<see cref="DeskReader"/>, as the app reads it) against the items' footprints:
    /// each of its stash cells that says nothing ("?") is either a piece of a larger item (the grid finder cut the item
    /// into cells and the footprint rule didn't join them: the label stood in another piece or the piece has none) or a
    /// whole item whose label wasn't read to a fit.
    /// </summary>
    private static async Task DeskSplitsAsync(StashReader reader, SKBitmap picture, DeskCatalogue catalogue, StashGrid grid, int pitch,
        List<(TruthCell Truth, StashRect Rect)> items, GameData data)
    {
        var look = await new DeskReader(reader).LookAsync(picture, catalogue, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);
        var stash = look.Cells.Where(c => c.Region == "stash").ToList();
        int unknownSplit = 0, unknownWhole = 0, unknownOutside = 0, namedPieces = 0, namedWhole = 0, unknown = 0;
        var whole = new HashSet<TruthCell>();
        Console.WriteLine($"   the desk's stash: {stash.Count} cells");
        foreach (var c in stash)
        {
            int i = (int)Math.Round((c.Pixels.Left - grid.X0) / (double)pitch), j = (int)Math.Round((c.Pixels.Top - grid.Y0) / (double)pitch);
            int w = (int)Math.Round(c.Pixels.Width / (double)pitch), h = (int)Math.Round(c.Pixels.Height / (double)pitch);
            var owner = items.FirstOrDefault(it => i >= it.Rect.I0 && i < it.Rect.I0 + it.Rect.W && j >= it.Rect.J0 && j < it.Rect.J0 + it.Rect.H);
            var isWhole = owner.Truth is not null && owner.Rect.I0 == i && owner.Rect.J0 == j && owner.Rect.W == w && owner.Rect.H == h;
            var said = c.Cell.Ids.Count switch
            {
                0 => "?",
                1 => data.ItemShortName(c.Cell.Ids[0]),
                _ => $"one of {c.Cell.Ids.Count}",
            };
            string kind;
            if (c.Cell.Ids.Count == 0)
            {
                unknown++;
                if (owner.Truth is null)
                    (kind, unknownOutside) = ("OUTSIDE THE TRUTH", unknownOutside + 1);
                else if (isWhole)
                    (kind, unknownWhole) = ("WHOLE ITEM UNREAD", unknownWhole + 1);
                else
                    (kind, unknownSplit) = ("PIECE OF A LARGER ITEM", unknownSplit + 1);
            }
            else if (isWhole)
            {
                namedWhole++;
                kind = "whole";
            }
            else
            {
                namedPieces++;
                kind = owner.Truth is null ? "outside the truth" : "a piece";
            }
            if (isWhole)
                whole.Add(owner.Truth!);
            Console.WriteLine($"      [{i},{j} {w}×{h}] {said,-14} label \"{c.Label}\" fit {c.Fit:0.00}{(c.Contradicted ? " FOOTPRINT" : "")}; " +
                              (owner.Truth is null ? "no item of the truth" : $"{owner.Truth.Name} {owner.Rect.W}×{owner.Rect.H} at {owner.Rect.I0},{owner.Rect.J0}") + $": {kind}");
        }
        var split = items.Where(it => !whole.Contains(it.Truth)).ToList();
        Console.WriteLine($"   the desk on this page: {unknown} \"?\" cells: {unknownSplit} pieces of larger items (the footprint split), {unknownWhole} whole items unread, " +
                          $"{unknownOutside} outside the truth; named or one of N: {namedWhole} whole items, {namedPieces} pieces");
        Console.WriteLine($"   items the desk has no whole cell for: {split.Count} ({string.Join(", ", split.Select(s => $"{s.Truth.Name} {s.Rect.W}×{s.Rect.H}"))})");
    }

    private static List<TruthCell> ReadTruth(string path)
    {
        var truth = new List<TruthCell>();
        foreach (var line in File.ReadAllLines(path).Where(l => l.Trim().Length > 0 && !l.StartsWith('#')))
        {
            var parts = line.Split(' ', 3);
            var cell = parts[1].Split(',');
            truth.Add(new TruthCell(parts[0][0], int.Parse(cell[0], CultureInfo.InvariantCulture), int.Parse(cell[1], CultureInfo.InvariantCulture), parts[2].Trim()));
        }
        return truth;
    }

    // ---- the scores ----

    private enum Outcome { Right, OneOf, Wrong, NotAmong, Unread }

    private static (Outcome Outcome, int Items) Named(Variant v, Cell cell, Matcher matcher, DeskMatch catalogue)
    {
        var text = v.Readings[cell].Text;
        var (fit, best) = v.Tolerant ? matcher.Match(text, cell.Rect.W, tolerant: true) : catalogue.Match(text, cell.Rect.W);
        if (fit < DeskMatch.Enough || best.Count == 0)
            return (Outcome.Unread, 0);
        var ids = best.Select(b => b.ItemId).Distinct(StringComparer.Ordinal).ToList();
        var among = best.Any(b => b.ShortName == cell.Truth.Name);
        if (ids.Count == 1)
            return (among ? Outcome.Right : Outcome.Wrong, 1);
        return (among ? Outcome.OneOf : Outcome.NotAmong, ids.Count);
    }

    private static double TruthFit(Variant v, Cell cell, Matcher matcher)
    {
        var text = v.Readings[cell].Text;
        return v.Tolerant ? matcher.Fit(text, cell.Truth.Name) : StashLabels.Fit(text, cell.Truth.Name);
    }

    private static bool Exact(string text, string name) => ExitList.Fold(text.Replace("!", "").Replace("|", "")) == ExitList.Fold(name);

    private static void Report(List<Variant> variants, List<Cell> cells, Matcher matcher, DeskMatch catalogue, Dictionary<string, int> pageRects, bool cellsOut)
    {
        // Every non-tolerant reading through the harness's matcher must name what the desk's does.
        var mismatches = 0;
        foreach (var v in variants.Where(v => !v.Tolerant))
        {
            foreach (var cell in cells)
            {
                var a = catalogue.Match(v.Readings[cell].Text, cell.Rect.W);
                var b = matcher.Match(v.Readings[cell].Text, cell.Rect.W, tolerant: false);
                if (Math.Abs(a.Fit - b.Fit) > 1e-9 || !a.Best.Select(x => x.ItemId).Order().SequenceEqual(b.Best.Select(x => x.ItemId).Order()))
                    mismatches++;
            }
        }
        Console.WriteLine($"The harness's matcher against the desk's (DeskMatch) on every reading: {mismatches} differences");

        var pages = cells.Select(c => c.Page).Distinct().Count();
        Console.WriteLine();
        Console.WriteLine($"Against the truth, the label alone ({cells.Count} cells on {pages} pages; folded as the readers fold; the fit is StashLabels.Fit, the tolerant fold's own where so named):");
        Console.WriteLine("| Variant | exact | fit ≥ 0.9 | fit ≥ 0.8 | wrong (< 0.8) | nothing | crops a cell | ms a crop (median / mean) | ms a cell |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var v in variants)
        {
            int exact = 0, nine = 0, eight = 0, wrong = 0, nothing = 0;
            foreach (var cell in cells)
            {
                var r = v.Readings[cell];
                var fit = TruthFit(v, cell, matcher);
                if (r.Text.Length == 0)
                    nothing++;
                else if (fit < 0.8)
                    wrong++;
                if (Exact(r.Text, cell.Truth.Name))
                    exact++;
                if (fit >= 0.9)
                    nine++;
                if (fit >= 0.8)
                    eight++;
            }
            var perCrop = cells.Where(c => v.Readings[c].Crops > 0).Select(c => v.Readings[c].Ms / v.Readings[c].Crops).Order().ToList();
            var crops = cells.Average(c => (double)v.Readings[c].Crops);
            var perCell = cells.Average(c => v.Readings[c].Ms);
            Console.WriteLine($"| {v.Name} | {exact} | {nine} | {eight} | {wrong} | {nothing} | {crops:0.00} | {Median(perCrop):0.0} / {(perCrop.Count > 0 ? perCrop.Average() : 0):0.0} | {perCell:0.0} |");
        }

        Console.WriteLine();
        Console.WriteLine($"Against the whole catalogue at the desk's fit ({DeskMatch.Enough}), the label alone (no footprint):");
        Console.WriteLine("| Variant | right | wrong | unread | one of N (truth among) | one of N (not among) | ms a page (its truth cells) | ms a 69-item page |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|");
        foreach (var v in variants)
        {
            var counts = cells.Select(c => Named(v, c, matcher, catalogue).Outcome).GroupBy(o => o).ToDictionary(g => g.Key, g => g.Count());
            var perPage = cells.GroupBy(c => c.Page).Select(g => g.Sum(c => v.Readings[c].Ms)).Average();
            var perCell = cells.Average(c => v.Readings[c].Ms);
            Console.WriteLine($"| {v.Name} | {counts.GetValueOrDefault(Outcome.Right)} | {counts.GetValueOrDefault(Outcome.Wrong)} | {counts.GetValueOrDefault(Outcome.Unread)} | " +
                              $"{counts.GetValueOrDefault(Outcome.OneOf)} | {counts.GetValueOrDefault(Outcome.NotAmong)} | {perPage:0} | {perCell * 69:0} |");
        }

        // Per page, the reader's loop and each engine's best single crop.
        Console.WriteLine();
        Console.WriteLine("Per page (right / wrong / unread / one of N):");
        foreach (var page in cells.Select(c => c.Page).Distinct())
        {
            var mine = cells.Where(c => c.Page == page).ToList();
            var line = new StringBuilder($"   {page} ({mine.Count} cells, {pageRects.GetValueOrDefault(page)} rectangles on the page):");
            foreach (var v in variants.Where(v => v.Name.Contains("loop", StringComparison.Ordinal) || v.Name.EndsWith("band native", StringComparison.Ordinal)))
            {
                var o = mine.Select(c => Named(v, c, matcher, catalogue).Outcome).ToList();
                line.Append($" {v.Name}: {o.Count(x => x == Outcome.Right)}/{o.Count(x => x is Outcome.Wrong or Outcome.NotAmong)}/{o.Count(x => x == Outcome.Unread)}/{o.Count(x => x == Outcome.OneOf)};");
            }
            Console.WriteLine(line);
        }

        // The letters each engine gets wrong, on the readings of this label (folded half alike or better).
        Console.WriteLine();
        Console.WriteLine("Confusions (truth → read, spaces left out, case alone not counted; readings at least half alike the truth when folded):");
        var classes = new[] { "1/l/I", "0/O", "5/S", "8/B", "other digit↔letter", "hyphen", "slash", "x/×", "dot", "other letter", "letter lost", "letter added" };
        Console.WriteLine("| Variant | readings | " + string.Join(" | ", classes) + " |");
        Console.WriteLine("|---|---|" + string.Concat(classes.Select(_ => "---|")));
        var examples = new Dictionary<string, Dictionary<string, int>>();
        foreach (var v in variants.Where(v => !v.Tolerant))
        {
            var tally = classes.ToDictionary(c => c, _ => 0);
            var pairs = new Dictionary<string, int>(StringComparer.Ordinal);
            var counted = 0;
            foreach (var cell in cells)
            {
                var text = v.Readings[cell].Text;
                if (text.Length == 0 || StashLabels.Similarity(ExitList.Fold(text), ExitList.Fold(cell.Truth.Name)) < 0.5)
                    continue;
                counted++;
                foreach (var (t, r) in Align(cell.Truth.Name.Replace(" ", ""), text.Replace(" ", "")))
                {
                    var k = ClassOf(t, r);
                    if (k is null)
                        continue;
                    tally[k]++;
                    var key = $"{(t == '\0' ? "∅" : t.ToString())}→{(r == '\0' ? "∅" : r.ToString())}";
                    pairs[key] = pairs.GetValueOrDefault(key) + 1;
                }
            }
            examples[v.Name] = pairs;
            Console.WriteLine($"| {v.Name} | {counted} | " + string.Join(" | ", classes.Select(c => tally[c])) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("Most frequent pairs:");
        foreach (var (name, pairs) in examples)
            Console.WriteLine($"   {name}: " + string.Join(", ", pairs.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Take(14).Select(p => $"{p.Key} {p.Value}")));

        // What one reads that the other doesn't: the label fits the truth at 0.9 (the desk's fit).
        Console.WriteLine();
        var windowsLoop = variants.First(v => v.Name == "Windows, the reader's loop");
        foreach (var other in variants.Where(v => v.Engine != "Windows" && !v.Engine.StartsWith("Windows+", StringComparison.Ordinal) && !v.Tolerant))
        {
            var onlyOther = cells.Where(c => TruthFit(other, c, matcher) >= 0.9 && TruthFit(windowsLoop, c, matcher) < 0.9).ToList();
            var onlyWindows = cells.Where(c => TruthFit(windowsLoop, c, matcher) >= 0.9 && TruthFit(other, c, matcher) < 0.9).ToList();
            Console.WriteLine($"Only {other.Name} reads ({onlyOther.Count}): " + string.Join("; ", onlyOther.Select(c => $"{c.Truth.Name} (\"{other.Readings[c].Text}\" | Windows \"{windowsLoop.Readings[c].Text}\")")));
            Console.WriteLine($"Only Windows' loop reads ({onlyWindows.Count}): " + string.Join("; ", onlyWindows.Select(c => $"{c.Truth.Name} (\"{windowsLoop.Readings[c].Text}\" | {other.Engine} \"{other.Readings[c].Text}\")")));
        }

        // What the tolerant fold changed: the cells where it reads the truth otherwise, or names otherwise.
        foreach (var tolerant in variants.Where(v => v.Tolerant))
        {
            var changed = cells.Where(c => (TruthFit(tolerant, c, matcher) >= 0.9) != (StashLabels.Fit(tolerant.Readings[c].Text, c.Truth.Name) >= 0.9)
                                           || Named(tolerant, c, matcher, catalogue).Outcome != Named(new Variant("", "") { Readings = { [c] = tolerant.Readings[c] } }, c, matcher, catalogue).Outcome).ToList();
            Console.WriteLine($"The tolerant fold changed ({tolerant.Name}, {changed.Count}): " + string.Join("; ", changed.Select(c =>
                $"{c.Truth.Name} \"{tolerant.Readings[c].Text}\" fit {StashLabels.Fit(tolerant.Readings[c].Text, c.Truth.Name):0.00} → {TruthFit(tolerant, c, matcher):0.00}, " +
                $"named {Named(new Variant("", "") { Readings = { [c] = tolerant.Readings[c] } }, c, matcher, catalogue).Outcome} → {Named(tolerant, c, matcher, catalogue).Outcome}")));
        }

        if (cellsOut)
        {
            Console.WriteLine();
            Console.WriteLine("Every cell (truth: what each variant read):");
            foreach (var cell in cells)
            {
                Console.WriteLine($"   {cell.Page} {cell.Truth.Grid} {cell.Truth.I},{cell.Truth.J} {cell.Rect.W}×{cell.Rect.H} {cell.Truth.Name}:");
                foreach (var v in variants.Where(v => !v.Tolerant || v.Name.Contains("loop", StringComparison.Ordinal)))
                {
                    var (outcome, items) = Named(v, cell, matcher, catalogue);
                    Console.WriteLine($"      {v.Name,-58} \"{v.Readings[cell].Text}\" fit {TruthFit(v, cell, matcher):0.00} {outcome}{(items > 1 ? $" of {items}" : "")}");
                }
            }
        }
    }

    private static double Median(List<double> sorted) => sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];

    // The class of a difference between the truth's character and the one read (\0 for none), or null when they agree.
    private static string? ClassOf(char t, char r)
    {
        if (t != '\0' && r != '\0' && char.ToLowerInvariant(t) == char.ToLowerInvariant(r))
            return null;
        static bool In(char c, string set) => c != '\0' && set.Contains(c);
        if (In(t, "1lIi|!") && In(r, "1lIi|!"))
            return "1/l/I";
        if (In(t, "0OoDQ") && In(r, "0OoDQ"))
            return "0/O";
        if (In(t, "5Ss") && In(r, "5Ss"))
            return "5/S";
        if (In(t, "8Bb") && In(r, "8Bb"))
            return "8/B";
        if (t == '-' || r == '-')
            return "hyphen";
        if (t == '/' || r == '/')
            return "slash";
        if (In(t, "xX×") && In(r, "xX×"))
            return "x/×";
        if (t == '.' || r == '.')
            return "dot";
        if (t != '\0' && r != '\0' && (char.IsDigit(t) != char.IsDigit(r)))
            return "other digit↔letter";
        if (t != '\0' && r != '\0')
            return "other letter";
        return t != '\0' ? "letter lost" : "letter added";
    }

    // The truth and the reading aligned by edit distance (case left aside): each pair of characters, \0 for a gap.
    private static List<(char T, char R)> Align(string truth, string read)
    {
        int n = truth.Length, m = read.Length;
        var d = new int[n + 1, m + 1];
        for (var i = 0; i <= n; i++)
            d[i, 0] = i;
        for (var j = 0; j <= m; j++)
            d[0, j] = j;
        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = char.ToLowerInvariant(truth[i - 1]) == char.ToLowerInvariant(read[j - 1]) ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        var pairs = new List<(char, char)>();
        int a = n, b = m;
        while (a > 0 || b > 0)
        {
            if (a > 0 && b > 0 && d[a, b] == d[a - 1, b - 1] + (char.ToLowerInvariant(truth[a - 1]) == char.ToLowerInvariant(read[b - 1]) ? 0 : 1))
            {
                pairs.Add((truth[a - 1], read[b - 1]));
                a--;
                b--;
            }
            else if (a > 0 && d[a, b] == d[a - 1, b] + 1)
            {
                pairs.Add((truth[a - 1], '\0'));
                a--;
            }
            else
            {
                pairs.Add(('\0', read[b - 1]));
                b--;
            }
        }
        pairs.Reverse();
        return pairs;
    }

    /// <summary>
    /// The desk's match (<see cref="DeskMatch.Match"/>) over the catalogue, written out so that a reading can be folded
    /// against each name in turn: with <c>tolerant</c>, a letter read where the name has a digit is taken for that digit
    /// when it is one the recognition confuses with it (S for 5, B for 8; O, D, Q and I, L are folded to 0 and 1 already),
    /// before the digits must agree. Without it, it names what DeskMatch names (checked on every reading).
    /// </summary>
    private sealed class Matcher(IEnumerable<DeskCandidate> candidates)
    {
        private readonly List<(DeskCandidate C, string Folded, string Digits, bool HasDigits)> _entries = candidates.Where(c => c.ShortName.Length > 0)
            .Select(c => (c, ExitList.Fold(c.ShortName))).Where(x => x.Item2.Length > 0)
            .Select(x => (x.c, x.Item2, new string(x.Item2.Where(char.IsDigit).ToArray()), x.c.ShortName.Any(char.IsDigit))).ToList();

        public (double Fit, List<DeskCandidate> Best) Match(string label, int cells, bool tolerant)
        {
            label = label.Replace("!", "").Replace("|", "").Trim();
            var cut = DeskMatch.QuickSlotCut(label);
            if (cut is not null)
                label = cut;
            var folded = ExitList.Fold(label);
            if (folded.Length == 0)
                return (0, []);
            var mayBeCut = folded.Length >= Math.Max(6, 6 * cells);
            double top = 0;
            var best = new List<DeskCandidate>();
            foreach (var e in _entries)
            {
                var fit = FitFolded(tolerant && e.HasDigits ? Tolerate(folded, e.Folded) : folded, e.Folded, e.Digits, e.HasDigits, mayBeCut, cut is not null);
                if (fit > top + 1e-9)
                {
                    top = fit;
                    best.Clear();
                    best.Add(e.C);
                }
                else if (Math.Abs(fit - top) <= 1e-9 && fit > 0)
                    best.Add(e.C);
            }
            return (top, best);
        }

        // The tolerant fit of a reading to one name, as StashLabels.Fit takes it (any label of six letters may be cut).
        public double Fit(string text, string shortName)
        {
            text = text.Replace("!", "").Replace("|", "").Trim();
            var a = ExitList.Fold(text);
            var b = ExitList.Fold(shortName);
            if (a.Length == 0 || b.Length == 0)
                return 0;
            var hasDigits = shortName.Any(char.IsDigit);
            var tolerated = hasDigits ? Tolerate(a, b) : a;
            // As StashLabels.Fit: a cut label that starts the name fits before the digits are compared.
            if (tolerated.Length >= 6 && b.Length > tolerated.Length && b.StartsWith(tolerated, StringComparison.Ordinal))
                return 1;
            return FitFolded(tolerated, b, new string(b.Where(char.IsDigit).ToArray()), hasDigits, tolerated.Length >= 6, false);
        }

        private static double FitFolded(string a, string b, string digitsB, bool hasDigits, bool mayBeCut, bool cutAtStart)
        {
            double fit;
            if (cutAtStart && a.Length >= 4 && b.Length > a.Length && b.EndsWith(a, StringComparison.Ordinal))
                fit = 1;
            else if (mayBeCut && b.Length > a.Length && b.StartsWith(a, StringComparison.Ordinal))
                fit = 1;
            else
            {
                fit = a == b ? 1 : StashLabels.Similarity(a, b);
                if (mayBeCut && a.Length <= b.Length)
                {
                    var same = 0;
                    while (same < a.Length && a[same] == b[same])
                        same++;
                    if (same >= 6)
                        fit = Math.Max(fit, (double)same / a.Length);
                }
            }
            if (hasDigits && new string(a.Where(char.IsDigit).ToArray()) != digitsB)
                fit = Math.Min(fit, 0.5);
            return fit;
        }

        // The reading with each letter the recognition confuses with a digit taken for the name's digit it stands against.
        private static string Tolerate(string a, string b)
        {
            int n = a.Length, m = b.Length;
            var d = new int[n + 1, m + 1];
            for (var i = 0; i <= n; i++)
                d[i, 0] = i;
            for (var j = 0; j <= m; j++)
                d[0, j] = j;
            for (var i = 1; i <= n; i++)
            {
                for (var j = 1; j <= m; j++)
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (Same(a[i - 1], b[j - 1]) ? 0 : 1));
            }
            var chars = a.ToCharArray();
            int x = n, y = m;
            while (x > 0 && y > 0)
            {
                if (d[x, y] == d[x - 1, y - 1] + (Same(a[x - 1], b[y - 1]) ? 0 : 1))
                {
                    if (a[x - 1] != b[y - 1] && Same(a[x - 1], b[y - 1]))
                        chars[x - 1] = b[y - 1];
                    x--;
                    y--;
                }
                else if (d[x, y] == d[x - 1, y] + 1)
                    x--;
                else
                    y--;
            }
            return new string(chars);
        }

        // A letter read against a digit of the name that the recognition confuses with it.
        private static bool Same(char read, char name) => read == name || (char.IsDigit(name) && (read, name) is ('s', '5') or ('b', '8'));
    }

#if OCR_SPIKE
    /// <summary>
    /// PaddleOCR's text-line recognition (PP-OCR's CTC recogniser, as RapidOCR exports it to ONNX) on the CPU: the crop is
    /// the line, no detection. The line is resized to the model's height (48) keeping its proportion, at least 320 wide
    /// (PaddleOCR's resize_norm_img, the rest zero), in the model's BGR order normalised to −1..1; the output's best
    /// class per column, repeats and blanks dropped, is the text.
    /// </summary>
    private sealed class PaddleRec : IDisposable
    {
        private readonly InferenceSession _session;
        private readonly string _input;
        private readonly string[] _chars;

        public string Name { get; }
        public int Height { get; }
        public int Classes => _chars.Length;

        public PaddleRec(string model, string dict, int threads)
        {
            Name = Path.GetFileNameWithoutExtension(model);
            // ONNX Runtime's Windows build writes telemetry events (ETW) unless told not to: the picture's reading stays on the PC.
            OrtEnv.Instance().DisableTelemetryEvents();
            using var options = new SessionOptions
            {
                IntraOpNumThreads = threads,
                InterOpNumThreads = 1,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            };
            _session = new InferenceSession(model, options);
            var input = _session.InputMetadata.First();
            _input = input.Key;
            var dims = input.Value.Dimensions;
            Height = dims.Length == 4 && dims[2] > 0 ? dims[2] : 48;
            var chars = new List<string> { "" };
            chars.AddRange(File.ReadAllLines(dict, Encoding.UTF8));
            // The class count is the output's last dimension when the model says it; else the dictionary plus a space.
            var classes = _session.OutputMetadata.First().Value.Dimensions[^1];
            if (classes <= 0 || classes > chars.Count)
                chars.Add(" ");
            _chars = chars.ToArray();
        }

        public string Read(SKBitmap line)
        {
            var w = Math.Max(1, (int)Math.Ceiling(Height * line.Width / (double)line.Height));
            var padded = Math.Max(w, Height * 320 / 48);
            using var resized = line.Resize(new SKImageInfo(w, Height, SKColorType.Bgra8888, SKAlphaType.Premul), new SKSamplingOptions(SKFilterMode.Linear))
                                ?? throw new InvalidOperationException("Resize failed");
            var values = new float[3 * Height * padded];
            var pixels = resized.GetPixelSpan();
            var plane = Height * padded;
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var p = y * resized.RowBytes + x * 4;
                    var at = y * padded + x;
                    values[at] = pixels[p] / 127.5f - 1f; // B
                    values[plane + at] = pixels[p + 1] / 127.5f - 1f; // G
                    values[2 * plane + at] = pixels[p + 2] / 127.5f - 1f; // R
                }
            }
            var tensor = new DenseTensor<float>(values, [1, 3, Height, padded]);
            using var results = _session.Run([NamedOnnxValue.CreateFromTensor(_input, tensor)]);
            var output = results.First().AsTensor<float>();
            int steps = output.Dimensions[1], classes = output.Dimensions[2];
            var scores = output is DenseTensor<float> dense ? dense.Buffer.Span : output.ToArray();
            var text = new StringBuilder();
            var previous = -1;
            for (var t = 0; t < steps; t++)
            {
                var best = 0;
                var max = float.MinValue;
                for (var c = 0; c < classes; c++)
                {
                    var v = scores[t * classes + c];
                    if (v > max)
                    {
                        max = v;
                        best = c;
                    }
                }
                if (best != 0 && best != previous && best < _chars.Length)
                    text.Append(_chars[best]);
                previous = best;
            }
            return text.ToString().Trim();
        }

        public void Dispose() => _session.Dispose();
    }
#endif

    // A cache-only run's transport: every request refused, so the data comes from the cache.
    private sealed class NoRequests : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("ocr-compare: cache only"));
    }
}
