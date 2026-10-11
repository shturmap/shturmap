using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Shturmap.Core.Logs;
using Shturmap.Core.Screenshots;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;
using SkiaSharp;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Shturmap.Cli.Stash;

/// <summary>
/// The spike of 2026-10-11 for "what you have" (docs/NEXT.md): reads a stash screenshot headless and says which items
/// of the quest set it recognises, so the owner can decide on evidence whether a reading is worth building. Nothing
/// of this runs in the app: the app never opens a menu screenshot (docs/DESIGN.md §2). The pictures it reads are the
/// owner's own, in the git-ignored tests\fixtures\ocr\stash; it prints item names and scores, never a path's folder.
/// </summary>
internal static class StashSpike
{
    // The game's inventory cell: 63 px on a 1080p screen (its line included), scaled with the screen by the smaller
    // of the two ratios; 84 px at 2560×1440. tarkov.dev's grid images are drawn for 1080p: (w·63+1) × (h·63+1).
    private const int BaseCell = 63;

    // A ridge (the game's one-pixel light line between cells) is this much lighter than both sides.
    private const float RidgeStep = 7;

    // How much of a cell's side must show the line for the cell to count as framed.
    private const double Framed = 0.7;

    // The game draws an item's art somewhat larger in its cell than tarkov.dev's grid image does (seen 2026-10-11:
    // about 15 % at 1440p): every template is tried at these sizes.
    private static readonly double[] Scales = [1.0, 1.08, 1.16, 1.24];

    // Templates are also tried shifted by up to this many pixels (at the template's scale).
    private const int Shift = 2;

    private static readonly SKSamplingOptions Sampling = new(SKCubicResampler.CatmullRom);

    // The radius of the local average taken out before comparing (0: none, only the mean); an experiment's knob.
    private static int HighPassRadius;

    // "--words": print every word the recognition returns, with where it stands in the cell.
    private static bool PrintWords;

    // "--grids": print every lattice found, kept or dropped.
    private static bool PrintGrids;

    // "--probe x,y": print the four sides' line coverage of the cell at that position and its neighbours.
    private static int[]? Probe;

    // A template at one size: compared only where it shows art (the mask), coarsely (every two by two pixels one)
    // and finely.
    private sealed record Variant(double Scale, bool[] CoarseMask, float[] Coarse, bool[] FineMask, float[] Fine);

    private sealed record Template(string Id, string Name, string ShortName, int W, int H, List<Variant> Variants, float[] Band);

    private sealed record Grid(int X0, int Y0, int Cols, int Rows, string Kind)
    {
        public bool Covers(int x, int y, int pitch) => x >= X0 && y >= Y0 && x < X0 + Cols * pitch && y < Y0 + Rows * pitch;
    }

    private sealed class Tally
    {
        public int Items, Known, Outside, Unlisted, PictureRight, PictureWrong, PictureNone, LabelRight, LabelWrong, LabelNone, BothRight, BothWrong, BothNone;
    }

    public static async Task RunAsync(IReadOnlyList<string> args)
    {
        var set = "quest";
        var ocr = true;
        string? dump = null, truthPath = null;
        var inputs = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--set": set = args[++i]; break;
                case "--no-ocr": ocr = false; break;
                case "--dump": dump = args[++i]; break;
                case "--hp": HighPassRadius = int.Parse(args[++i]); break;
                case "--truth": truthPath = args[++i]; break;
                case "--words": PrintWords = true; break;
                case "--grids": PrintGrids = true; break;
                case "--probe": Probe = args[++i].Split(',').Select(int.Parse).ToArray(); break;
                default: inputs.Add(args[i]); break;
            }
        }
        if (inputs.Count == 0)
        {
            Console.WriteLine("Use: shturmap-cli stash <png or folder>... [--set keys|quest|all] [--no-ocr] [--dump <folder>] [--hp <radius>] [--truth <file>]");
            return;
        }
        var files = inputs.SelectMany(p => Directory.Exists(p) ? Directory.GetFiles(p, "*.png").OrderBy(File.GetLastWriteTime).ToArray() : [p]).ToList();
        // The truth: "A 3,0 Dorm 214" per line, for one screenshot.
        var truth = new Dictionary<string, string>(StringComparer.Ordinal);
        if (truthPath is not null)
        {
            foreach (var line in File.ReadAllLines(truthPath).Where(l => l.Trim().Length > 0))
            {
                var parts = line.Split(' ', 3);
                truth[parts[0] + " " + parts[1]] = parts[2].Trim();
            }
        }

        var paths = Shturmap.Session.AppPaths.Default;
        var http = CachedHttp.CreateClient();
        var loader = new GameDataLoader(new CachedHttp(http, paths.DataCache));
        var data = await loader.LoadAsync(GameMode.Pve, "en");
        var sources = await loader.LoadSourcesAsync(GameMode.Pve, "en");
        var ids = ItemSet(set, data, sources);
        Console.WriteLine($"Set \"{set}\": {ids.Count} items; high-pass radius {HighPassRadius}; scales {string.Join(", ", Scales)}; shift ±{Shift}");

        var sw = Stopwatch.StartNew();
        var templates = await LoadTemplatesAsync(http, paths.GameArtCache, ids, data);
        Console.WriteLine($"Templates: {templates.Count} loaded in {sw.ElapsedMilliseconds} ms ({templates.Count(t => t.W == 1 && t.H == 1)} of 1×1); " +
                          $"missing {ids.Count - templates.Count}");
        var bySize = templates.ToLookup(t => (t.W, t.H));

        OcrEngine? engine = null;
        if (ocr)
        {
            engine = OcrEngine.TryCreateFromLanguage(new Language("en-US")) ?? OcrEngine.TryCreateFromUserProfileLanguages();
            Console.WriteLine($"OCR: {engine?.RecognizerLanguage.LanguageTag ?? "none"}");
        }
        if (dump is not null)
            Directory.CreateDirectory(dump);

        var tally = new Tally();
        foreach (var file in files)
            await ReadOneAsync(file, bySize, engine, dump, truth, tally);
        if (truth.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"Against the truth ({tally.Known} of {tally.Items} rectangles named, {tally.Outside} of them outside the set; " +
                              $"{tally.Unlisted} rectangles the truth doesn't list; {truth.Count} items in the truth):");
            Console.WriteLine("  (an item outside the set counts as right when nothing is said, as wrong when a name is)");
            Console.WriteLine($"  picture alone: right {tally.PictureRight}, wrong {tally.PictureWrong}, says nothing {tally.PictureNone}");
            Console.WriteLine($"  label alone:   right {tally.LabelRight}, wrong {tally.LabelWrong}, says nothing {tally.LabelNone}");
            Console.WriteLine($"  together:      right {tally.BothRight}, wrong {tally.BothWrong}, says nothing {tally.BothNone}");
        }
    }

    // ---- the item set ----

    private const string KeyCategory = "543be5e94bdc2df1348b4568";

    private static HashSet<string> ItemSet(string set, GameData data, ItemSources sources)
    {
        var objectives = data.Tasks.Values.SelectMany(t => t.Objectives ?? []).ToList();
        var keys = sources.Members[KeyCategory].ToHashSet(StringComparer.Ordinal);
        var questKeys = objectives.SelectMany(o => (o.RequiredKeys ?? []).SelectMany(k => k))
            .Concat(data.Tasks.Values.SelectMany(t => (t.NeededKeys ?? []).SelectMany(k => k.Keys ?? [])))
            .ToHashSet(StringComparer.Ordinal);
        // The things objectives take, want or use up; not the gear worn for a kill (whole classes of armour) and
        // not the weapons.
        var objectiveItems = objectives.SelectMany(o => (o.Items ?? [])
                .Concat(o.UseAny ?? [])
                .Concat(o.MarkerItem is { } marker ? new[] { marker } : Array.Empty<string>()))
            .ToHashSet(StringComparer.Ordinal);
        var worn = objectives.SelectMany(o => (o.Wearing ?? []).SelectMany(w => w.Select(r => r.Id))).ToHashSet(StringComparer.Ordinal);
        var questItems = objectives.Select(o => o.QuestItem).OfType<string>().ToHashSet(StringComparer.Ordinal);
        Console.WriteLine($"Keys {keys.Count} (quest keys {questKeys.Count}); objective items {objectiveItems.Count} (worn gear, left out: {worn.Count}); " +
                          $"quest items {questItems.Count}; all items {sources.Items.Count}");
        return set switch
        {
            "keys" => keys,
            "all" => sources.Items.Keys.ToHashSet(StringComparer.Ordinal),
            _ => keys.Concat(objectiveItems).Concat(questItems).ToHashSet(StringComparer.Ordinal),
        };
    }

    // ---- templates: tarkov.dev's grid images, fetched once into the game-art cache ----

    private static async Task<List<Template>> LoadTemplatesAsync(HttpClient http, string folder, IEnumerable<string> ids, GameData data)
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
            if (bytes is null)
                return null;
            using var bitmap = Decode(bytes);
            if (bitmap is null)
                return null;
            var w = (int)Math.Round((bitmap.Width - 1) / (double)BaseCell);
            var h = (int)Math.Round((bitmap.Height - 1) / (double)BaseCell);
            if (w < 1 || h < 1 || bitmap.Width != w * BaseCell + 1 || bitmap.Height != h * BaseCell + 1)
            {
                Console.WriteLine($"  odd template {bitmap.Width}×{bitmap.Height}: {data.ItemShortName(id)}");
                return null;
            }
            var variants = new List<Variant>();
            foreach (var scale in Scales)
            {
                using var scaled = scale == 1.0 ? null : Scaled(bitmap, scale);
                var raw = ToGray(scaled ?? bitmap);
                var gray = HighPass(Blur3(raw), HighPassRadius);
                var fineMask = Foreground(raw, ArtMask(raw.W, raw.H));
                var coarseMask = CoarseMask(fineMask, raw.W, raw.H);
                variants.Add(new Variant(scale, coarseMask, Normalized(Coarse(gray), coarseMask), fineMask, Normalized(gray, fineMask)));
            }
            var plain = HighPass(ToGray(bitmap), HighPassRadius);
            return new Template(id, data.ItemName(id), data.ItemShortName(id), w, h, variants, Normalized(plain, BandMask(plain.W, plain.H)));
        }).ToList();
        var templates = (await Task.WhenAll(loads)).OfType<Template>().ToList();
        if (fetched > 0)
            Console.WriteLine($"  fetched {fetched} grid images from assets.tarkov.dev");
        return templates;
    }

    // The template drawn larger about its middle, the edges falling off the picture.
    private static SKBitmap Scaled(SKBitmap template, double scale)
    {
        var outp = new SKBitmap(new SKImageInfo(template.Width, template.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(outp);
        canvas.Clear(template.GetPixel(3, template.Height / 2));
        canvas.Translate(template.Width / 2f, template.Height / 2f);
        canvas.Scale((float)scale);
        canvas.Translate(-template.Width / 2f, -template.Height / 2f);
        canvas.DrawBitmap(template, 0, 0, Sampling);
        return outp;
    }

    // ---- pictures as luminance ----

    private sealed class Gray(int w, int h)
    {
        public int W { get; } = w;
        public int H { get; } = h;
        public float[] P { get; } = new float[w * h];
        public float this[int x, int y] { get => P[y * W + x]; set => P[y * W + x] = value; }
    }

    private static SKBitmap? Decode(byte[] bytes)
    {
        var decoded = SKBitmap.Decode(bytes);
        if (decoded is null)
            return null;
        if (decoded.ColorType == SKColorType.Bgra8888)
            return decoded;
        var copy = decoded.Copy(SKColorType.Bgra8888);
        decoded.Dispose();
        return copy;
    }

    private static Gray ToGray(SKBitmap bitmap)
    {
        var gray = new Gray(bitmap.Width, bitmap.Height);
        var pixels = bitmap.GetPixelSpan();
        for (var i = 0; i < gray.P.Length; i++)
            gray.P[i] = 0.114f * pixels[4 * i] + 0.587f * pixels[4 * i + 1] + 0.299f * pixels[4 * i + 2];
        return gray;
    }

    // Where the template shows its item rather than the cell's field: pixels that differ from the field's shade (the
    // middle value of the compared area, most of which is field), and their neighbours. The game and tarkov.dev both
    // hatch the field finely, and that shared texture would otherwise score every template alike.
    private static bool[] Foreground(Gray raw, bool[] art)
    {
        var values = new List<float>();
        for (var i = 0; i < raw.P.Length; i++)
        {
            if (art[i])
                values.Add(raw.P[i]);
        }
        values.Sort();
        var field = values.Count > 0 ? values[values.Count / 2] : 0;
        var differs = new bool[raw.P.Length];
        for (var i = 0; i < raw.P.Length; i++)
            differs[i] = Math.Abs(raw.P[i] - field) > 16;
        var mask = new bool[raw.P.Length];
        for (var y = 0; y < raw.H; y++)
        {
            for (var x = 0; x < raw.W; x++)
            {
                if (!art[y * raw.W + x])
                    continue;
                var near = false;
                for (var dy = -1; dy <= 1 && !near; dy++)
                {
                    for (var dx = -1; dx <= 1 && !near; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        near = xx >= 0 && yy >= 0 && xx < raw.W && yy < raw.H && differs[yy * raw.W + xx];
                    }
                }
                mask[y * raw.W + x] = near;
            }
        }
        return mask;
    }

    // A three by three average: the hatch of the field, two pixels in period, goes.
    private static Gray Blur3(Gray g)
    {
        var b = new Gray(g.W, g.H);
        for (var y = 0; y < g.H; y++)
        {
            for (var x = 0; x < g.W; x++)
            {
                float sum = 0;
                var n = 0;
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= g.W || yy >= g.H)
                            continue;
                        sum += g[xx, yy];
                        n++;
                    }
                }
                b[x, y] = sum / n;
            }
        }
        return b;
    }

    // The normalized cross-correlation of a picture with a template already normalized over the mask: the picture's
    // mean over the mask taken out, scaled by its spread there.
    private static float Ncc(float[] picture, float[] template, bool[] mask)
    {
        double sum = 0, sq = 0, dot = 0;
        var n = 0;
        for (var i = 0; i < picture.Length; i++)
        {
            if (!mask[i])
                continue;
            var p = picture[i];
            sum += p;
            sq += p * p;
            dot += p * template[i];
            n++;
        }
        if (n < 8)
            return 0;
        var spread = sq - sum * sum / n;
        return spread <= 1e-6 ? 0 : (float)(dot / Math.Sqrt(spread));
    }

    // Every two by two pixels averaged: a picture that forgives a pixel's shift.
    private static Gray Coarse(Gray g)
    {
        var c = new Gray(g.W / 2, g.H / 2);
        for (var y = 0; y < c.H; y++)
        {
            for (var x = 0; x < c.W; x++)
                c[x, y] = (g[2 * x, 2 * y] + g[2 * x + 1, 2 * y] + g[2 * x, 2 * y + 1] + g[2 * x + 1, 2 * y + 1]) / 4;
        }
        return c;
    }

    private static Gray Shifted(Gray g, int dx, int dy)
    {
        var s = new Gray(g.W, g.H);
        for (var y = 0; y < g.H; y++)
        {
            for (var x = 0; x < g.W; x++)
                s[x, y] = g[Math.Clamp(x + dx, 0, g.W - 1), Math.Clamp(y + dy, 0, g.H - 1)];
        }
        return s;
    }

    // The picture less its local average (a box of the given radius): tints, gradients and the highlight go, the
    // item's edges stay.
    private static Gray HighPass(Gray g, int radius)
    {
        if (radius <= 0)
            return g;
        var sum = new double[(g.W + 1) * (g.H + 1)];
        var stride = g.W + 1;
        for (var y = 1; y <= g.H; y++)
        {
            double row = 0;
            for (var x = 1; x <= g.W; x++)
            {
                row += g[x - 1, y - 1];
                sum[y * stride + x] = sum[(y - 1) * stride + x] + row;
            }
        }
        var outp = new Gray(g.W, g.H);
        for (var y = 0; y < g.H; y++)
        {
            int y0 = Math.Max(0, y - radius), y1 = Math.Min(g.H, y + radius + 1);
            for (var x = 0; x < g.W; x++)
            {
                int x0 = Math.Max(0, x - radius), x1 = Math.Min(g.W, x + radius + 1);
                var area = (x1 - x0) * (y1 - y0);
                var mean = (sum[y1 * stride + x1] - sum[y0 * stride + x1] - sum[y1 * stride + x0] + sum[y0 * stride + x0]) / area;
                outp[x, y] = (float)(g[x, y] - mean);
            }
        }
        return outp;
    }

    // What of a cell is compared as the item's art: not the label band at the top, not the corners the game paints
    // over (the found-in-raid mark and the count at the bottom right, the icons at the bottom left), not the line.
    private static bool[] ArtMask(int w, int h)
    {
        var mask = new bool[w * h];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var painted = y < 16 || (y >= h - 30 && x >= w - 20) || (y >= h - 17 && x >= w - 40) || (y >= h - 17 && x < 20);
                mask[y * w + x] = !painted && x > 1 && y > 1 && x < w - 2 && y < h - 2;
            }
        }
        return mask;
    }

    private static bool[] CoarseMask(bool[] fine, int w, int h)
    {
        var mask = new bool[w / 2 * (h / 2)];
        for (var y = 0; y < h / 2; y++)
        {
            for (var x = 0; x < w / 2; x++)
                mask[y * (w / 2) + x] = fine[2 * y * w + 2 * x] || fine[2 * y * w + 2 * x + 1] || fine[(2 * y + 1) * w + 2 * x] || fine[(2 * y + 1) * w + 2 * x + 1];
        }
        return mask;
    }

    // The label band: the short name the game writes at the top right.
    private static bool[] BandMask(int w, int h)
    {
        var mask = new bool[w * h];
        for (var y = 1; y < Math.Min(h, 15); y++)
        {
            for (var x = Math.Max(1, w - 50); x < w - 1; x++)
                mask[y * w + x] = true;
        }
        return mask;
    }

    // The masked pixels, mean removed and scaled to unit length, so a dot product is a normalized cross-correlation.
    private static float[] Normalized(Gray g, bool[] mask)
    {
        var v = new float[g.P.Length];
        double mean = 0;
        var n = 0;
        for (var i = 0; i < v.Length; i++)
        {
            if (!mask[i])
                continue;
            mean += g.P[i];
            n++;
        }
        mean /= Math.Max(1, n);
        double norm = 0;
        for (var i = 0; i < v.Length; i++)
        {
            if (!mask[i])
                continue;
            v[i] = (float)(g.P[i] - mean);
            norm += v[i] * v[i];
        }
        var scale = norm > 0 ? (float)(1 / Math.Sqrt(norm)) : 0;
        for (var i = 0; i < v.Length; i++)
            v[i] *= scale;
        return v;
    }

    private static float Dot(float[] a, float[] b)
    {
        float s = 0;
        for (var i = 0; i < a.Length; i++)
            s += a[i] * b[i];
        return s;
    }

    // ---- the lattice ----

    private sealed class Lines
    {
        private readonly int _w, _h;
        private readonly int[] _v; // per column, cumulative count of vertical-ridge pixels down the column
        private readonly int[] _hh; // per row, cumulative count of horizontal-ridge pixels along the row

        public Lines(Gray g)
        {
            _w = g.W;
            _h = g.H;
            _v = new int[g.W * (g.H + 1)];
            _hh = new int[g.H * (g.W + 1)];
            // A line is a light band of one to four pixels with the darker field two pixels off on both sides: the
            // game's one-pixel line, its two-pixel line in a window, and that line between the cyan outlines of two
            // highlighted cells (the game marks items its hideout or quests want).
            for (var x = 0; x < g.W; x++)
            {
                for (var y = 0; y < g.H; y++)
                {
                    var ridge = x >= 2 && x + 3 < g.W && Math.Max(g[x, y], g[x + 1, y]) is var b && b >= g[x - 2, y] + RidgeStep && b >= g[x + 3, y] + RidgeStep;
                    _v[x * (g.H + 1) + y + 1] = _v[x * (g.H + 1) + y] + (ridge ? 1 : 0);
                }
            }
            for (var y = 0; y < g.H; y++)
            {
                for (var x = 0; x < g.W; x++)
                {
                    var ridge = y >= 2 && y + 3 < g.H && Math.Max(g[x, y], g[x, y + 1]) is var b && b >= g[x, y - 2] + RidgeStep && b >= g[x, y + 3] + RidgeStep;
                    _hh[y * (g.W + 1) + x + 1] = _hh[y * (g.W + 1) + x] + (ridge ? 1 : 0);
                }
            }
        }

        /// <summary>How much of the column x between y0 and y1 is a light line (the best of x−1, x, x+1).</summary>
        public double Vertical(int x, int y0, int y1)
        {
            y0 = Math.Clamp(y0, 0, _h);
            y1 = Math.Clamp(y1, y0, _h);
            if (y1 == y0)
                return 0;
            double best = 0;
            for (var dx = -1; dx <= 1; dx++)
            {
                var c = x + dx;
                if (c < 0 || c >= _w)
                    continue;
                best = Math.Max(best, (_v[c * (_h + 1) + y1] - _v[c * (_h + 1) + y0]) / (double)(y1 - y0));
            }
            return best;
        }

        public double Horizontal(int y, int x0, int x1)
        {
            x0 = Math.Clamp(x0, 0, _w);
            x1 = Math.Clamp(x1, x0, _w);
            if (x1 == x0)
                return 0;
            double best = 0;
            for (var dy = -1; dy <= 1; dy++)
            {
                var r = y + dy;
                if (r < 0 || r >= _h)
                    continue;
                best = Math.Max(best, (_hh[r * (_w + 1) + x1] - _hh[r * (_w + 1) + x0]) / (double)(x1 - x0));
            }
            return best;
        }
    }

    private static List<Grid> FindGrids(Lines lines, Gray gray, int w, int h, int pitch)
    {
        // Every position where a cell of the pitch is framed on all four sides, scored by how well.
        var framed = new List<(int X, int Y, double Score)>();
        for (var y = 0; y + pitch < h; y++)
        {
            for (var x = 0; x + pitch < w; x++)
            {
                var l = lines.Vertical(x, y + 2, y + pitch - 2);
                if (l < Framed)
                    continue;
                var r = lines.Vertical(x + pitch, y + 2, y + pitch - 2);
                if (r < Framed)
                    continue;
                var t = lines.Horizontal(y, x + 2, x + pitch - 2);
                if (t < Framed)
                    continue;
                var b = lines.Horizontal(y + pitch, x + 2, x + pitch - 2);
                if (b < Framed)
                    continue;
                framed.Add((x, y, l + r + t + b));
            }
        }
        // One position per cell: the best within two pixels.
        framed.Sort((a, b) => b.Score.CompareTo(a.Score));
        var cells = new List<(int X, int Y)>();
        foreach (var f in framed)
        {
            if (!cells.Any(c => Math.Abs(c.X - f.X) <= 2 && Math.Abs(c.Y - f.Y) <= 2))
                cells.Add((f.X, f.Y));
        }
        // Cells a pitch apart (give or take two pixels) belong to one lattice.
        var parent = Enumerable.Range(0, cells.Count).ToArray();
        int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);
        for (var i = 0; i < cells.Count; i++)
        {
            for (var j = i + 1; j < cells.Count; j++)
            {
                int dx = Math.Abs(cells[i].X - cells[j].X), dy = Math.Abs(cells[i].Y - cells[j].Y);
                var sameColumn = dx <= 2 && Math.Abs(dy - pitch) <= 2;
                var sameRow = dy <= 2 && Math.Abs(dx - pitch) <= 2;
                if (sameColumn || sameRow)
                    parent[Find(i)] = Find(j);
            }
        }
        var groups = cells.Select((c, i) => (c, Root: Find(i))).GroupBy(x => x.Root).Select(g => g.Select(x => x.c).ToList()).ToList();
        // A row of wide items across a grid (weapons in the stash) leaves no framed cell in it and splits the lattice
        // in two: two lattices of the same phase, one above the other over the same columns (or side by side over the
        // same rows), a few cells apart, are one grid.
        var boxes = groups.Select(g => (X0: g.Min(c => c.X), Y0: g.Min(c => c.Y), X1: g.Max(c => c.X) + pitch, Y1: g.Max(c => c.Y) + pitch, Count: g.Count)).ToList();
        var bParent = Enumerable.Range(0, boxes.Count).ToArray();
        int BFind(int i) => bParent[i] == i ? i : bParent[i] = BFind(bParent[i]);
        for (var i = 0; i < boxes.Count; i++)
        {
            for (var j = i + 1; j < boxes.Count; j++)
            {
                var a = boxes[i];
                var b = boxes[j];
                int phaseX = ((a.X0 - b.X0) % pitch + pitch) % pitch, phaseY = ((a.Y0 - b.Y0) % pitch + pitch) % pitch;
                var samePhase = (phaseX <= 2 || phaseX >= pitch - 2) && (phaseY <= 2 || phaseY >= pitch - 2);
                if (!samePhase)
                    continue;
                var sharedColumns = Math.Min(a.X1, b.X1) - Math.Max(a.X0, b.X0) >= pitch - 2;
                var sharedRows = Math.Min(a.Y1, b.Y1) - Math.Max(a.Y0, b.Y0) >= pitch - 2;
                var rowGap = Math.Max(a.Y0, b.Y0) - Math.Min(a.Y1, b.Y1);
                var columnGap = Math.Max(a.X0, b.X0) - Math.Min(a.X1, b.X1);
                if ((sharedColumns && rowGap <= 8 * pitch) || (sharedRows && columnGap <= 3 * pitch))
                    bParent[BFind(i)] = BFind(j);
            }
        }
        var found = new List<(Grid Grid, int Count)>();
        if (PrintGrids)
            Console.WriteLine($"   framed positions {framed.Count}, cells {cells.Count}, lattices {groups.Count}");
        foreach (var merged in boxes.Select((b, i) => (b, Root: BFind(i))).GroupBy(x => x.Root).Select(g => g.Select(x => x.b).ToList()))
        {
            int x0 = merged.Min(b => b.X0), y0 = merged.Min(b => b.Y0), x1 = merged.Max(b => b.X1), y1 = merged.Max(b => b.Y1);
            int cols = (int)Math.Round((x1 - x0) / (double)pitch), rows = (int)Math.Round((y1 - y0) / (double)pitch);
            var count = merged.Sum(b => b.Count);
            if (PrintGrids)
                Console.WriteLine($"   lattice {cols}×{rows} at ({x0},{y0}) from {merged.Count} parts, {count} framed cells" + (cols < 2 || rows < 2 || count < 4 ? " (dropped)" : ""));
            if (cols < 2 || rows < 2 || count < 4)
                continue;
            (x0, y0) = Snap(gray, x0, y0, cols, rows, pitch);
            // The stash is ten wide and stands at the right; the trading screen's left grid is the trader's.
            var kind = cols == 10 && x0 > w / 2 ? "stash" : x0 + cols * pitch < w / 2 ? "left grid" : "window";
            found.Add((new Grid(x0, y0, cols, rows, kind), count));
        }
        // Snapped onto the same lines, two lattices of one grid (one found on the cyan outlines, one on the grey
        // lines, five pixels apart) are one grid.
        for (var i = 0; i < found.Count; i++)
        {
            for (var j = found.Count - 1; j > i; j--)
            {
                var a = found[i].Grid;
                var b = found[j].Grid;
                int phaseX = ((a.X0 - b.X0) % pitch + pitch) % pitch, phaseY = ((a.Y0 - b.Y0) % pitch + pitch) % pitch;
                var samePhase = (phaseX <= 2 || phaseX >= pitch - 2) && (phaseY <= 2 || phaseY >= pitch - 2);
                var overlap = a.X0 < b.X0 + b.Cols * pitch && b.X0 < a.X0 + a.Cols * pitch && a.Y0 < b.Y0 + b.Rows * pitch && b.Y0 < a.Y0 + a.Rows * pitch;
                if (!samePhase || !overlap)
                    continue;
                int x0 = Math.Min(a.X0, b.X0), y0 = Math.Min(a.Y0, b.Y0);
                int x1 = Math.Max(a.X0 + a.Cols * pitch, b.X0 + b.Cols * pitch), y1 = Math.Max(a.Y0 + a.Rows * pitch, b.Y0 + b.Rows * pitch);
                int cols = (int)Math.Round((x1 - x0) / (double)pitch), rows = (int)Math.Round((y1 - y0) / (double)pitch);
                var kind = cols == 10 && x0 > w / 2 ? "stash" : x0 + cols * pitch < w / 2 ? "left grid" : "window";
                found[i] = (new Grid(x0, y0, cols, rows, kind), found[i].Count + found[j].Count);
                found.RemoveAt(j);
            }
        }
        // A sparse lattice inside a bigger one is the bigger one's art seen as lines, not a window over it.
        var grids = found
            .Where(f => f.Count >= f.Grid.Cols * f.Grid.Rows / 4 || !found.Any(o => o.Grid != f.Grid && o.Grid.Cols * o.Grid.Rows > f.Grid.Cols * f.Grid.Rows &&
                                                                                  o.Grid.Covers(f.Grid.X0 + 1, f.Grid.Y0 + 1, pitch)))
            .Select(f => f.Grid);
        return grids.OrderByDescending(g => g.Cols * g.Rows).ToList();
    }

    // The lattice's top left, moved onto the middle of the light line it stands near: the lines are found with a
    // tolerance that can put the lattice a pixel or two off, and a cell cut two pixels early reads worse.
    private static (int X0, int Y0) Snap(Gray gray, int x0, int y0, int cols, int rows, int pitch)
    {
        const int reach = 4, side = 3;
        int y1 = Math.Min(gray.H, y0 + rows * pitch), x1 = Math.Min(gray.W, x0 + cols * pitch);
        var columns = new double[2 * (reach + side) + 1];
        for (var dx = -reach - side; dx <= reach + side; dx++)
        {
            var x = x0 + dx;
            if (x < 0 || x >= gray.W)
                continue;
            double sum = 0;
            for (var y = Math.Max(0, y0 + 4); y < y1 - 4; y++)
                sum += gray[x, y];
            columns[dx + reach + side] = sum / Math.Max(1, y1 - y0 - 8);
        }
        var lines = new double[2 * (reach + side) + 1];
        for (var dy = -reach - side; dy <= reach + side; dy++)
        {
            var y = y0 + dy;
            if (y < 0 || y >= gray.H)
                continue;
            double sum = 0;
            for (var x = Math.Max(0, x0 + 4); x < x1 - 4; x++)
                sum += gray[x, y];
            lines[dy + reach + side] = sum / Math.Max(1, x1 - x0 - 8);
        }
        return (x0 + Thinnest(columns, reach, side) - reach, y0 + Thinnest(lines, reach, side) - reach);
    }

    // The offset within reach where the profile stands out most against both sides a few pixels off: a thin line,
    // not a row of labels (bright over ten pixels) or an item's edge (bright on one side only).
    private static int Thinnest(double[] profile, int reach, int side)
    {
        var best = reach;
        var bestScore = double.MinValue;
        for (var d = 0; d <= 2 * reach; d++)
        {
            var i = d + side;
            var score = profile[i] - Math.Max(profile[i - side], profile[i + side]);
            if (score > bestScore)
            {
                bestScore = score;
                best = d;
            }
        }
        return best;
    }

    // ---- one screenshot ----

    private static async Task ReadOneAsync(string file, ILookup<(int, int), Template> bySize, OcrEngine? engine, string? dump,
        Dictionary<string, string> truth, Tally tally)
    {
        var sw = Stopwatch.StartNew();
        using var bitmap = Decode(await File.ReadAllBytesAsync(file));
        if (bitmap is null)
        {
            Console.WriteLine($"== {Path.GetFileName(file)}: not a picture");
            return;
        }
        var scale = Math.Min(bitmap.Width / 1920.0, bitmap.Height / 1080.0);
        var pitch = (int)Math.Round(BaseCell * scale);
        var gray = ToGray(bitmap);
        var lines = new Lines(gray);
        var grids = FindGrids(lines, gray, gray.W, gray.H, pitch);
        if (Probe is [var px, var py])
        {
            for (var dy = -2; dy <= 2; dy++)
            {
                for (var dx = -2; dx <= 2; dx++)
                {
                    int x = px + dx, y = py + dy;
                    Console.WriteLine($"   probe ({x},{y}): left {lines.Vertical(x, y + 2, y + pitch - 2):0.00} right {lines.Vertical(x + pitch, y + 2, y + pitch - 2):0.00} " +
                                      $"top {lines.Horizontal(y, x + 2, x + pitch - 2):0.00} bottom {lines.Horizontal(y + pitch, x + 2, x + pitch - 2):0.00}");
                }
            }
        }
        var hp = HighPass(gray, (int)Math.Round(4 * scale)); // for the empty-cell test only
        Console.WriteLine($"== {Path.GetFileName(file)}: {bitmap.Width}×{bitmap.Height}, pitch {pitch}, {grids.Count} grids, lattice in {sw.ElapsedMilliseconds} ms");
        foreach (var g in grids)
            Console.WriteLine($"   {g.Kind} {g.Cols}×{g.Rows} at ({g.X0},{g.Y0})");

        SKCanvas? overlay = null;
        SKBitmap? overlayBitmap = null;
        if (dump is not null)
        {
            overlayBitmap = bitmap.Copy();
            overlay = new SKCanvas(overlayBitmap);
        }

        int occupied = 0, matched = 0, agreed = 0, disagreed = 0, unread = 0, byLabel = 0, byPicture = 0, oddShapes = 0;
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var gi = 0; gi < grids.Count; gi++)
        {
            var grid = grids[gi];
            var letter = (char)('A' + gi);
            if (grid.Kind == "left grid")
                continue; // the trader's
            // Which cells are hidden under a smaller grid drawn over this one (a container window over the stash).
            var over = grids.Where((o, oi) => oi != gi && o.Cols * o.Rows < grid.Cols * grid.Rows).ToList();
            var hidden = new bool[grid.Cols * grid.Rows];
            var empty = new bool[grid.Cols * grid.Rows];
            for (var j = 0; j < grid.Rows; j++)
            {
                for (var i = 0; i < grid.Cols; i++)
                {
                    int cx = grid.X0 + i * pitch, cy = grid.Y0 + j * pitch;
                    hidden[j * grid.Cols + i] = over.Any(o => o.Covers(cx + pitch / 2, cy + pitch / 2, pitch));
                    empty[j * grid.Cols + i] = IsEmpty(hp, cx, cy, pitch);
                }
            }
            // Cells joined across a missing line are one item.
            var parent = Enumerable.Range(0, grid.Cols * grid.Rows).ToArray();
            int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);
            for (var j = 0; j < grid.Rows; j++)
            {
                for (var i = 0; i < grid.Cols; i++)
                {
                    var k = j * grid.Cols + i;
                    if (hidden[k] || empty[k])
                        continue;
                    int cx = grid.X0 + i * pitch, cy = grid.Y0 + j * pitch;
                    if (i + 1 < grid.Cols && !hidden[k + 1] && !empty[k + 1] && lines.Vertical(cx + pitch, cy + 4, cy + pitch - 4) < 0.4)
                        parent[Find(k)] = Find(k + 1);
                    if (j + 1 < grid.Rows && !hidden[k + grid.Cols] && !empty[k + grid.Cols] && lines.Horizontal(cy + pitch, cx + 4, cx + pitch - 4) < 0.4)
                        parent[Find(k)] = Find(k + grid.Cols);
                }
            }
            var items = Enumerable.Range(0, grid.Cols * grid.Rows).Where(k => !hidden[k] && !empty[k]).GroupBy(Find)
                .Select(g => (I0: g.Min(k => k % grid.Cols), J0: g.Min(k => k / grid.Cols), I1: g.Max(k => k % grid.Cols), J1: g.Max(k => k / grid.Cols), Cells: g.Count()))
                .OrderBy(r => r.J0).ThenBy(r => r.I0).ToList();

            foreach (var item in items)
            {
                int w = item.I1 - item.I0 + 1, h = item.J1 - item.J0 + 1;
                var rect = SKRectI.Create(grid.X0 + item.I0 * pitch, grid.Y0 + item.J0 * pitch, w * pitch, h * pitch);
                occupied++;
                var key = $"{letter} {item.I0},{item.J0}";
                var where = $"[{key}] {w}×{h}";
                truth.TryGetValue(key, out var expected);
                if (item.Cells != w * h || w > 6 || h > 6)
                {
                    oddShapes++;
                    Console.WriteLine($"   {where} not a rectangle ({item.Cells} cells): skipped");
                    overlay?.DrawRect(rect, new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Stroke, StrokeWidth = 2 });
                    continue;
                }
                var candidates = bySize[(w, h)].ToList();
                if (candidates.Count == 0)
                {
                    Console.WriteLine($"   {where} no template of this size");
                    overlay?.DrawRect(rect, new SKPaint { Color = SKColors.Gray, Style = SKPaintStyle.Stroke, StrokeWidth = 2 });
                    continue;
                }

                // The block, resampled to the template's size; coarsely against every template at every size, then
                // the twelve nearest finely, shifted a little each way.
                using var block = Block(bitmap, rect, w * BaseCell + 1, h * BaseCell + 1);
                var blockRaw = ToGray(block);
                var blockGray = HighPass(Blur3(blockRaw), HighPassRadius);
                var coarse = Coarse(blockGray).P;
                var shifted = new List<float[]>();
                for (var dy = -Shift; dy <= Shift; dy++)
                {
                    for (var dx = -Shift; dx <= Shift; dx++)
                        shifted.Add(Shifted(blockGray, dx, dy).P);
                }
                var band = Normalized(HighPass(blockRaw, HighPassRadius), BandMask(blockRaw.W, blockRaw.H));
                var coarseScored = candidates.Select(t => (T: t, Score: t.Variants.Max(v => Ncc(coarse, v.Coarse, v.CoarseMask)))).OrderByDescending(s => s.Score).ToList();
                var scored = coarseScored.Take(12)
                    .Select(c => (c.T, Fine: c.T.Variants.Select(v => (v.Scale, Score: shifted.Max(s => Ncc(s, v.Fine, v.FineMask)))).MaxBy(x => x.Score)))
                    .Select(x => (x.T, Score: x.Fine.Score, x.Fine.Scale, Band: Dot(band, x.T.Band)))
                    .OrderByDescending(s => s.Score).ToList();
                var best = scored[0];
                var close = scored.Where(s => s.Score >= best.Score - 0.06).ToList();
                var byBand = close.OrderByDescending(s => s.Band).First();

                // The label, read by Windows' recognition, against every candidate's short name.
                string? text = null;
                (Template T, double Sim)? read = null;
                if (engine is not null)
                {
                    // Of the readings, the one that fits a candidate's name best.
                    foreach (var reading in await ReadLabelAsync(engine, bitmap, rect, pitch))
                    {
                        var fit = candidates.Select(t => (T: t, Sim: LabelMatch(reading, t.ShortName))).OrderByDescending(x => x.Sim).First();
                        if (read is null || fit.Sim > read.Value.Sim)
                        {
                            read = fit;
                            text = reading;
                        }
                    }
                }

                // Verdicts: the picture's, the label's, and both together (the label names the item when it reads
                // well; else the picture when it is sure; else nothing).
                var pictureSays = best.Score < 0.55 ? null : close.Count == 1 ? best.T.ShortName : byBand.T.ShortName;
                var labelSays = read is { Sim: >= 0.8 } r0 ? r0.T.ShortName : null;
                // Without a label that names an item: the picture, when it is sure and alone, and no label read
                // speaks against it.
                var labelAgainst = text is not null && LabelMatch(text, best.T.ShortName) < 0.5;
                var togetherSays = labelSays ?? (best.Score >= 0.7 && close.Count == 1 && !labelAgainst ? best.T.ShortName : null);
                if (pictureSays is not null)
                    matched++;
                if (labelSays is not null)
                    byLabel++;
                else if (togetherSays is not null)
                    byPicture++;

                var line = new StringBuilder($"   {where} {togetherSays ?? "?",-12} picture: {best.T.ShortName} {best.Score:0.00} ×{best.Scale:0.00}");
                if (scored.Count > 1)
                    line.Append($" / {scored[1].T.ShortName} {scored[1].Score:0.00}");
                if (close.Count > 1)
                    line.Append($"; band of {close.Count}: {byBand.T.ShortName} {byBand.Band:0.00}" + (byBand.T != best.T ? $" (vs {best.T.ShortName} {best.Band:0.00})" : ""));
                if (text is not null)
                {
                    line.Append($"; label \"{text}\"");
                    if (read is { } r)
                    {
                        line.Append($" → {r.T.ShortName} {r.Sim:0.00}");
                        if (pictureSays is not null && labelSays is not null)
                        {
                            if (labelSays == pictureSays)
                                agreed++;
                            else
                            {
                                disagreed++;
                                line.Append(" DISAGREE");
                            }
                        }
                    }
                }
                else if (engine is not null)
                {
                    unread++;
                }
                if (expected is not null)
                {
                    tally.Known++;
                    if (!candidates.Any(t => t.ShortName == expected))
                    {
                        // An item outside the set: the right answer is nothing; a name is a false one.
                        tally.Outside++;
                        line.Append($"; truth {expected}, not in the set");
                        Count(pictureSays, "", ref tally.PictureRight, ref tally.PictureWrong, ref tally.PictureNone);
                        Count(labelSays, "", ref tally.LabelRight, ref tally.LabelWrong, ref tally.LabelNone);
                        Count(togetherSays, "", ref tally.BothRight, ref tally.BothWrong, ref tally.BothNone);
                        if (togetherSays is not null)
                            line.Append(" FALSE");
                    }
                    else
                    {
                        var rank = scored.FindIndex(s => s.T.ShortName == expected);
                        var coarseRank = coarseScored.FindIndex(s => s.T.ShortName == expected);
                        line.Append(rank >= 0 ? $"; truth {expected} ranks {rank + 1} at {scored[rank].Score:0.00} ×{scored[rank].Scale:0.00}"
                            : $"; truth {expected} not in the twelve (coarse rank {coarseRank + 1})");
                        Count(pictureSays, expected, ref tally.PictureRight, ref tally.PictureWrong, ref tally.PictureNone);
                        Count(labelSays, expected, ref tally.LabelRight, ref tally.LabelWrong, ref tally.LabelNone);
                        Count(togetherSays, expected, ref tally.BothRight, ref tally.BothWrong, ref tally.BothNone);
                        if (togetherSays is not null && togetherSays != expected)
                            line.Append(" WRONG");
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
                if (togetherSays is not null)
                    seen[togetherSays] = seen.GetValueOrDefault(togetherSays) + 1;
                overlay?.DrawRect(rect, new SKPaint { Color = togetherSays is not null ? SKColors.Lime : SKColors.Yellow, Style = SKPaintStyle.Stroke, StrokeWidth = 2 });
            }
            if (overlay is not null)
            {
                overlay.DrawRect(SKRectI.Create(grid.X0, grid.Y0, grid.Cols * pitch, grid.Rows * pitch), new SKPaint { Color = SKColors.Cyan, Style = SKPaintStyle.Stroke, StrokeWidth = 3 });
                for (var j = 0; j < grid.Rows; j++)
                {
                    for (var i = 0; i < grid.Cols; i++)
                    {
                        var k = j * grid.Cols + i;
                        if (empty[k] && !hidden[k])
                            overlay.DrawCircle(grid.X0 + i * pitch + pitch / 2, grid.Y0 + j * pitch + pitch / 2, 3, new SKPaint { Color = SKColors.Cyan });
                    }
                }
            }
        }
        Console.WriteLine($"   -- {occupied} items: named {byLabel + byPicture} (by label {byLabel}, by picture alone {byPicture}), nothing said {occupied - byLabel - byPicture - oddShapes}, " +
                          $"odd shapes {oddShapes}; the picture names {matched}, label agrees {agreed}, disagrees {disagreed}, no label {unread}; {sw.ElapsedMilliseconds} ms");
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
    }

    // Expected "": the item is outside the set, so silence is right and any name is wrong.
    private static void Count(string? said, string expected, ref int right, ref int wrong, ref int none)
    {
        if (said is null && expected.Length == 0)
            right++;
        else if (said is null)
            none++;
        else if (said == expected)
            right++;
        else
            wrong++;
    }

    // An empty cell is the game's dark field: nothing but noise once the gradient is taken out.
    private static bool IsEmpty(Gray hp, int x, int y, int pitch)
    {
        int m = pitch / 8, x0 = x + m, y0 = y + m, x1 = x + pitch - m, y1 = y + pitch - m;
        double sum = 0, sq = 0;
        var n = 0;
        for (var yy = y0; yy < y1; yy++)
        {
            for (var xx = x0; xx < x1; xx++)
            {
                if (xx < 0 || yy < 0 || xx >= hp.W || yy >= hp.H)
                    continue;
                var v = hp[xx, yy];
                sum += v;
                sq += v * v;
                n++;
            }
        }
        if (n == 0)
            return true;
        var variance = sq / n - sum / n * (sum / n);
        return Math.Sqrt(Math.Max(0, variance)) < 3.5;
    }

    private static SKBitmap Block(SKBitmap bitmap, SKRectI rect, int w, int h)
    {
        using var subset = new SKBitmap();
        if (!bitmap.ExtractSubset(subset, rect))
            throw new InvalidOperationException("Block outside the picture");
        return subset.Resize(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul), Sampling)
               ?? throw new InvalidOperationException("Resize failed");
    }

    // The label at the top of the item, read by Windows' recognition from the item's upper part at four times its
    // size; only the words that stand in the label's band count (the art below gives words of its own).
    private static async Task<List<string>> ReadLabelAsync(OcrEngine engine, SKBitmap bitmap, SKRectI rect, int pitch)
    {
        const int zoom = 4;
        var part = SKRectI.Create(rect.Left + 1, rect.Top + 1, rect.Width - 2, (int)Math.Round(pitch * 0.42));
        using var big = Block(bitmap, part, part.Width * zoom, part.Height * zoom);
        var bytes = big.GetPixelSpan().ToArray();
        using var soft = SoftwareBitmap.CreateCopyFromBuffer(bytes.AsBuffer(), BitmapPixelFormat.Bgra8, big.Width, big.Height, BitmapAlphaMode.Premultiplied);
        var result = await engine.RecognizeAsync(soft);
        if (PrintWords)
        {
            foreach (var l in result.Lines)
                Console.WriteLine($"      words: " + string.Join(" | ", l.Words.Select(w => $"\"{w.Text}\" y{w.BoundingRect.Y / zoom:0} h{w.BoundingRect.Height / zoom:0} x{w.BoundingRect.X / zoom:0}")));
        }
        // The label's top stands within the first sixth of the cell; words that start lower are the art's.
        var words = result.Lines.SelectMany(l => l.Words)
            .Where(w => w.BoundingRect.Y <= pitch * 0.14 * zoom)
            .OrderBy(w => w.BoundingRect.X).Select(w => w.Text);
        var text = string.Join(" ", words).Trim();
        if (text.Length > 0 && text.Any(char.IsDigit))
            return [text];
        // The band alone, cut above the art and set in a dark margin: reads the digits that touch a key's bow,
        // and some labels the whole part doesn't. The caller keeps the reading that fits a name better.
        var band = SKRectI.Create(rect.Left + 2, rect.Top + 2, rect.Width - 4, (int)Math.Round(pitch * 0.2));
        using var bigBand = Block(bitmap, band, band.Width * zoom, band.Height * zoom);
        using var framed = new SKBitmap(new SKImageInfo(bigBand.Width + 2 * Margin, bigBand.Height + 2 * Margin, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(framed))
        {
            canvas.Clear(new SKColor(24, 29, 33));
            canvas.DrawBitmap(bigBand, Margin, Margin);
        }
        var bandBytes = framed.GetPixelSpan().ToArray();
        using var softBand = SoftwareBitmap.CreateCopyFromBuffer(bandBytes.AsBuffer(), BitmapPixelFormat.Bgra8, framed.Width, framed.Height, BitmapAlphaMode.Premultiplied);
        var bandResult = await engine.RecognizeAsync(softBand);
        if (PrintWords)
        {
            foreach (var l in bandResult.Lines)
                Console.WriteLine($"      band words: " + string.Join(" | ", l.Words.Select(w => $"\"{w.Text}\"")));
        }
        var bandText = string.Join(" ", bandResult.Lines.Select(l => l.Text)).Trim();
        return new[] { text, bandText }.Where(t => t.Length > 0).Distinct().ToList();
    }

    private const int Margin = 24;

    // How well a read label fits a short name: the same folded letters (ExitList.Fold), or the name's beginning when
    // the game cut the label at the cell's edge; digits must agree, since lookalike keys differ by a digit alone. A
    // cell's edge reads as "!" or "|" now and then: no label has them.
    internal static double LabelMatch(string text, string shortName)
    {
        text = text.Replace("!", "").Replace("|", "").Trim();
        var a = ExitList.Fold(text);
        var b = ExitList.Fold(shortName);
        if (a.Length == 0 || b.Length == 0)
            return 0;
        if (a.Length >= 6 && b.Length > a.Length && b.StartsWith(a, StringComparison.Ordinal))
            return 1;
        var sim = Similarity(a, b);
        // A name with digits in it: the folded digits (an O read for a 0 or an l for a 1 is no difference) must be
        // the same, since lookalike keys differ by one digit.
        if (!shortName.Any(char.IsDigit))
            return sim;
        var digitsA = new string(a.Where(char.IsDigit).ToArray());
        var digitsB = new string(b.Where(char.IsDigit).ToArray());
        return digitsA != digitsB ? Math.Min(sim, 0.5) : sim;
    }

    // 1 for the same text, less by the share of letters to change (Levenshtein), as ExitList does.
    private static double Similarity(string a, string b)
    {
        if (a == b)
            return 1;
        if (a.Length == 0 || b.Length == 0)
            return 0;
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            previous[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1], previous[j]) + 1, previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return 1 - (double)previous[b.Length] / Math.Max(a.Length, b.Length);
    }
}
