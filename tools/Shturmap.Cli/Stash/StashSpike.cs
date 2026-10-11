using System.Diagnostics;
using System.Text;
using Shturmap.Core.Logs;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Screenshots;
using SkiaSharp;

namespace Shturmap.Cli.Stash;

/// <summary>
/// The measurement tool for "what you have" (docs/NEXT.md): reads stash screenshots headless with the product's
/// reader (<see cref="StashReader"/>, which grew out of the spike of 2026-10-11 that stood here) and says which items
/// of a set it recognises, scored against a hand-written truth (--truth) and drawn over the picture (--dump). Nothing
/// of this runs in the app. The pictures it reads are the owner's own, in the git-ignored tests\fixtures\ocr\stash;
/// it prints item names and scores, never a path's folder.
/// </summary>
internal static class StashSpike
{
    // "--grids": print every grid found, with its place.
    private static bool PrintGrids;

    // "--counts": read the number at the bottom right of every rectangle, named or not, to check the count reading
    // by eye against the picture.
    private static bool ReadCounts;

    // "--art-others N": for every rectangle the truth names, the agreement of its own grid image and the best of N
    // other images of the same size, to see where "clearly another item's art" lies.
    private static int ArtOthers;

    // "--marks": print, for every rectangle, how the bottom right corner correlates with the found-in-raid mark's
    // shape, and write a sheet of the corners to check by eye.
    private static bool PrintMarks;

    // "--names a,b,c": the candidates are these short names (made-up ids, no grid images), not the data's set.
    private static string[]? Names;

    private sealed class Tally
    {
        public int Items, Known, Outside, Unlisted, Right, Wrong, None, Vetoed;
    }

    public static async Task RunAsync(IReadOnlyList<string> args)
    {
        var set = "quest";
        string? dump = null, truthPath = null;
        var inputs = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--set": set = args[++i]; break;
                case "--dump": dump = args[++i]; break;
                case "--truth": truthPath = args[++i]; break;
                case "--grids": PrintGrids = true; break;
                case "--counts": ReadCounts = true; break;
                case "--art-others": ArtOthers = int.Parse(args[++i]); break;
                case "--marks": PrintMarks = true; break;
                case "--names": Names = args[++i].Split(',').Select(n => n.Trim()).Where(n => n.Length > 0).ToArray(); break;
                default: inputs.Add(args[i]); break;
            }
        }
        if (inputs.Count == 0)
        {
            Console.WriteLine("Use: shturmap-cli stash <png or folder>... [--set keys|quest|all | --names a,b,c] [--truth <file>] [--dump <folder>] " +
                              "[--grids] [--counts] [--marks] [--art-others <n>]");
            return;
        }
        var files = inputs.SelectMany(p => Directory.Exists(p) ? Directory.GetFiles(p, "*.png").OrderBy(File.GetLastWriteTime).ToArray() : [p]).ToList();
        // The truth: "A 3,0 Marl 214" per line, for one screenshot: the grid's letter (the biggest first), the cell's
        // column and row, the item's short name.
        var truth = new Dictionary<(char Grid, int I, int J), string>();
        if (truthPath is not null)
        {
            foreach (var line in File.ReadAllLines(truthPath).Where(l => l.Trim().Length > 0))
            {
                var parts = line.Split(' ', 3);
                var cell = parts[1].Split(',');
                truth[(parts[0][0], int.Parse(cell[0]), int.Parse(cell[1]))] = parts[2].Trim();
            }
        }

        List<StashCandidate> candidates;
        if (Names is not null)
        {
            // Given names, without the data: the product's case of a few dozen candidates, and drawn test pages.
            candidates = Names.Select((name, n) => new StashCandidate(n.ToString("D24"), name, null)).ToList();
            Console.WriteLine($"Names: {candidates.Count} candidates");
        }
        else
        {
            var paths = Shturmap.Session.AppPaths.Default;
            var http = CachedHttp.CreateClient();
            var loader = new GameDataLoader(new CachedHttp(http, paths.DataCache));
            var data = await loader.LoadAsync(GameMode.Pve, "en");
            var sources = await loader.LoadSourcesAsync(GameMode.Pve, "en");
            var ids = ItemSet(set, data, sources);
            var sw = Stopwatch.StartNew();
            candidates = await CandidatesAsync(http, paths.GameArtCache, ids, data);
            Console.WriteLine($"Set \"{set}\": {candidates.Count} candidates, {candidates.Count(c => c.GridImage is not null)} with a grid image, in {sw.ElapsedMilliseconds} ms");
        }

        var reader = StashReader.Create("en");
        if (reader is null)
        {
            Console.WriteLine("This Windows has no text recognition language: no reading.");
            return;
        }
        Console.WriteLine($"Recognition: {reader.LanguageTag}");
        if (dump is not null)
            Directory.CreateDirectory(dump);

        var tally = new Tally();
        foreach (var file in files)
            await ReadOneAsync(reader, file, candidates, dump, truth, tally);
        if (truth.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"Against the truth ({tally.Known} of {tally.Items} rectangles in it, {tally.Outside} of them outside the set; " +
                              $"{tally.Unlisted} rectangles the truth doesn't list; {truth.Count} items in the truth):");
            Console.WriteLine("  (an item outside the set counts as right when nothing is said, as wrong when a name is)");
            Console.WriteLine($"  right {tally.Right}, wrong {tally.Wrong}, says nothing {tally.None} ({tally.Vetoed} of them vetoed by the art)");
        }
    }

    // ---- the item set ----

    private const string KeyCategory = "543be5e94bdc2df1348b4568";

    private static HashSet<string> ItemSet(string set, GameData data, ItemSources sources)
    {
        var objectives = data.Tasks.Values.SelectMany(t => t.Objectives ?? []).ToList();
        var keys = sources.Members[KeyCategory].ToHashSet(StringComparer.Ordinal);
        // The things objectives take, want or use up; not the gear worn for a kill (whole classes of armour) and
        // not the weapons.
        var objectiveItems = objectives.SelectMany(o => (o.Items ?? [])
                .Concat(o.UseAny ?? [])
                .Concat(o.MarkerItem is { } marker ? new[] { marker } : Array.Empty<string>()))
            .ToHashSet(StringComparer.Ordinal);
        var questItems = objectives.Select(o => o.QuestItem).OfType<string>().ToHashSet(StringComparer.Ordinal);
        return set switch
        {
            "keys" => keys,
            "all" => sources.Items.Keys.ToHashSet(StringComparer.Ordinal),
            _ => keys.Concat(objectiveItems).Concat(questItems).ToHashSet(StringComparer.Ordinal),
        };
    }

    // ---- candidates: the set's names, with tarkov.dev's grid images fetched once into the game-art cache ----

    private static async Task<List<StashCandidate>> CandidatesAsync(HttpClient http, string folder, IEnumerable<string> ids, GameData data)
    {
        Directory.CreateDirectory(folder);
        var gate = new SemaphoreSlim(4);
        var fetched = 0;
        var loads = ids.Select(async id =>
        {
            var path = Path.Combine(folder, $"grid-{id}.png");
            byte[]? bytes = null;
            if (File.Exists(path))
            {
                bytes = await File.ReadAllBytesAsync(path);
            }
            else if (!CachedHttp.NotedMissing(path, CachedHttp.MissingAge))
            {
                await gate.WaitAsync();
                try
                {
                    var webp = await http.GetByteArrayAsync(new Uri($"https://assets.tarkov.dev/{id}-grid-image.webp"));
                    using var decoded = SKBitmap.Decode(webp);
                    if (decoded is not null)
                    {
                        using var image = SKImage.FromBitmap(decoded);
                        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
                        bytes = png.ToArray();
                        await File.WriteAllBytesAsync(path, bytes);
                        Interlocked.Increment(ref fetched);
                    }
                }
                catch (HttpRequestException e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    CachedHttp.NoteMissing(path);
                }
                finally
                {
                    gate.Release();
                }
            }
            return new StashCandidate(id, data.ItemShortName(id), bytes);
        }).ToList();
        var candidates = (await Task.WhenAll(loads)).ToList();
        if (fetched > 0)
            Console.WriteLine($"  fetched {fetched} grid images from assets.tarkov.dev");
        return candidates;
    }

    // ---- one screenshot ----

    private static async Task ReadOneAsync(StashReader reader, string file, List<StashCandidate> candidates, string? dump,
        Dictionary<(char Grid, int I, int J), string> truth, Tally tally)
    {
        using var bgra = StashPicture.Decode(await File.ReadAllBytesAsync(file));
        if (bgra is null)
        {
            Console.WriteLine($"== {Path.GetFileName(file)}: not a picture");
            return;
        }
        var look = await reader.LookAsync(bgra, candidates, CancellationToken.None);
        var names = candidates.ToDictionary(c => c.ItemId, c => c.ShortName, StringComparer.Ordinal);
        Console.WriteLine($"== {Path.GetFileName(file)}: {look.Width}×{look.Height}, pitch {look.Pitch}, {look.Grids.Count} grids" +
                          (look.IsTradingScreen ? ", the trading screen" : "") + $", grids in {look.GridMs} ms, all in {look.TotalMs} ms");
        if (PrintGrids || look.Grids.Count > 0)
        {
            foreach (var g in look.Grids)
                Console.WriteLine($"   {g.Grid.Place} {g.Grid.Cols}×{g.Grid.Rows} at ({g.Grid.X0},{g.Grid.Y0})");
        }

        SKCanvas? overlay = null;
        SKBitmap? overlayBitmap = null;
        if (dump is not null)
        {
            overlayBitmap = bgra.Copy();
            overlay = new SKCanvas(overlayBitmap);
        }

        int occupied = 0, named = 0, oddShapes = 0;
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var corners = new List<(string Key, SKRectI Rect)>();
        for (var gi = 0; gi < look.Grids.Count; gi++)
        {
            var g = look.Grids[gi];
            var grid = g.Grid;
            var letter = (char)('A' + gi);
            var pitch = look.Pitch;
            // The truth's rows are counted from the first row the spike found; a reading that finds rows above it
            // stands that many rows lower: the offset that matches the most names is the truth's.
            var offset = 0;
            if (truth.Keys.Any(k => k.Grid == letter))
            {
                offset = Enumerable.Range(0, grid.Rows)
                    .Select(d => (d, Matches: g.Cells.Count(c => c.ItemId is not null && truth.TryGetValue((letter, c.Rect.I0, c.Rect.J0 - d), out var t) && names[c.ItemId] == t)))
                    .OrderByDescending(x => x.Matches).ThenBy(x => x.d).First().d;
                if (offset > 0)
                    Console.WriteLine($"   (the truth's row 0 is this reading's row {offset})");
            }
            foreach (var cell in g.Cells)
            {
                occupied++;
                var rect = cell.Pixels;
                var where = $"[{letter} {cell.Rect.I0},{cell.Rect.J0}] {cell.Rect.W}×{cell.Rect.H}";
                truth.TryGetValue((letter, cell.Rect.I0, cell.Rect.J0 - offset), out var expected);
                if (cell.Rect.Odd)
                {
                    oddShapes++;
                    Console.WriteLine($"   {where} not a rectangle: skipped");
                    overlay?.DrawRect(rect, new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Stroke, StrokeWidth = 2 });
                    continue;
                }
                var said = cell.ItemId is null ? null : names[cell.ItemId];
                var line = new StringBuilder($"   {where} {said ?? "?",-12}");
                if (cell.Label is not null)
                    line.Append($" label \"{cell.Label}\" fit {cell.Fit:0.00}");
                else
                    line.Append(" no label");
                if (cell.Agreement is { } agreement)
                    line.Append($"; art {agreement:0.00}" + (cell.Vetoed ? " VETO" : ""));
                if (cell.CountText is not null)
                    line.Append($"; count \"{cell.CountText}\" → {cell.Count}");
                else if (ReadCounts)
                {
                    var text = await reader.ReadCountAsync(bgra, rect, pitch, CancellationToken.None);
                    line.Append($"; count \"{text}\" → {StashLabels.Count(text)}");
                }
                if (PrintMarks)
                {
                    var (plain, over) = StashMarks.Scores(bgra, rect, pitch);
                    var mark = StashMarks.Seen(bgra, rect, pitch);
                    line.Append($"; mark plain {plain:0.00} over {over:0.00}" + (mark is true ? " SEEN" : mark is null ? " UNSURE" : ""));
                }
                if (ArtOthers > 0 && expected is not null && candidates.FirstOrDefault(c => c.ShortName == expected) is { GridImage: not null } own &&
                    StashTemplate.Size(own.GridImage) is { } size && size.W == cell.Rect.W && size.H == cell.Rect.H)
                {
                    var block = StashTemplate.Block(bgra, rect, size.W, size.H);
                    var ownScore = StashTemplate.Load(own.GridImage)!.Agreement(block);
                    var others = candidates.Where(c => c.GridImage is not null && c.ShortName != expected && StashTemplate.Size(c.GridImage) == size)
                        .OrderBy(c => c.ItemId, StringComparer.Ordinal).Take(ArtOthers)
                        .Select(c => StashTemplate.Load(c.GridImage!)!.Agreement(block)).OrderDescending().ToList();
                    line.Append($"; own art {ownScore:0.00}, others best {others.FirstOrDefault():0.00}, " +
                                $"median {(others.Count > 0 ? others[others.Count / 2] : 0):0.00}, over {StashReader.VetoBelow:0.00}: {others.Count(o => o >= StashReader.VetoBelow)} of {others.Count}");
                }
                if (PrintMarks && dump is not null)
                    corners.Add(($"{letter}{cell.Rect.I0},{cell.Rect.J0}", rect));
                if (expected is not null)
                {
                    tally.Known++;
                    if (!candidates.Any(c => c.ShortName == expected))
                    {
                        // An item outside the set: the right answer is nothing; a name is a false one.
                        tally.Outside++;
                        line.Append($"; truth {expected}, not in the set");
                        if (said is null)
                            tally.Right++;
                        else
                        {
                            tally.Wrong++;
                            line.Append(" FALSE");
                        }
                    }
                    else
                    {
                        line.Append($"; truth {expected}");
                        if (said is null)
                        {
                            tally.None++;
                            if (cell.Vetoed)
                                tally.Vetoed++;
                        }
                        else if (said == expected)
                            tally.Right++;
                        else
                        {
                            tally.Wrong++;
                            line.Append(" WRONG");
                        }
                    }
                }
                else if (truth.Count > 0)
                {
                    // A rectangle the truth doesn't know: the reading cut the grid differently from the truth.
                    tally.Unlisted++;
                    line.Append("; not in the truth");
                }
                tally.Items++;
                Console.WriteLine(line);
                if (said is not null)
                {
                    named++;
                    seen[said] = seen.GetValueOrDefault(said) + cell.Count;
                }
                overlay?.DrawRect(rect, new SKPaint { Color = said is not null ? SKColors.Lime : SKColors.Yellow, Style = SKPaintStyle.Stroke, StrokeWidth = 2 });
            }
            if (overlay is not null)
            {
                overlay.DrawRect(SKRectI.Create(grid.X0, grid.Y0, grid.Cols * pitch, grid.Rows * pitch), new SKPaint { Color = SKColors.Cyan, Style = SKPaintStyle.Stroke, StrokeWidth = 3 });
                for (var j = 0; j < grid.Rows; j++)
                {
                    for (var i = 0; i < grid.Cols; i++)
                    {
                        var k = j * grid.Cols + i;
                        if (g.Empty[k] && !g.Hidden[k])
                            overlay.DrawCircle(grid.X0 + i * pitch + pitch / 2, grid.Y0 + j * pitch + pitch / 2, 3, new SKPaint { Color = SKColors.Cyan });
                    }
                }
            }
        }
        Console.WriteLine($"   -- {occupied} items: named {named}, nothing said {occupied - named - oddShapes}, odd shapes {oddShapes}; {look.TotalMs} ms");
        if (seen.Count > 0)
            Console.WriteLine("   -- seen: " + string.Join(", ", seen.OrderBy(s => s.Key).Select(s => s.Value > 1 ? $"{s.Key} ×{s.Value}" : s.Key)));
        if (overlayBitmap is not null)
        {
            overlay!.Dispose();
            using var image = SKImage.FromBitmap(overlayBitmap);
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            await File.WriteAllBytesAsync(Path.Combine(dump!, Path.GetFileNameWithoutExtension(file) + "-grids.png"), png.ToArray());
            overlayBitmap.Dispose();
        }
        if (corners.Count > 0)
            await WriteCornersAsync(bgra, corners, look.Pitch, Path.Combine(dump!, Path.GetFileNameWithoutExtension(file) + "-corners.png"));
    }

    // A sheet of every rectangle's bottom right corner at four times its size, each with its key, to check the
    // found-in-raid marks by eye.
    private static async Task WriteCornersAsync(SKBitmap bitmap, List<(string Key, SKRectI Rect)> corners, int pitch, string path)
    {
        const int zoom = 4, perRow = 8;
        int cw = (int)(pitch * 0.45), ch = (int)(pitch * 0.45);
        int tileW = cw * zoom + 8, tileH = ch * zoom + 28;
        var rows = (corners.Count + perRow - 1) / perRow;
        using var sheet = new SKBitmap(new SKImageInfo(perRow * tileW, rows * tileH, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(SKColors.Black);
        using var font = new SKFont(SKTypeface.Default, 18);
        using var white = new SKPaint { Color = SKColors.White, IsAntialias = true };
        for (var n = 0; n < corners.Count; n++)
        {
            var (key, rect) = corners[n];
            var part = SKRectI.Create(rect.Right - cw, rect.Bottom - ch, cw, ch);
            using var big = StashPicture.Block(bitmap, part, cw * zoom, ch * zoom);
            int x = n % perRow * tileW, y = n / perRow * tileH;
            canvas.DrawBitmap(big, x + 4, y + 24, StashPicture.Sampling);
            canvas.DrawText(key, x + 4, y + 18, SKTextAlign.Left, font, white);
        }
        using var image = SKImage.FromBitmap(sheet);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        await File.WriteAllBytesAsync(path, png.ToArray());
    }
}
