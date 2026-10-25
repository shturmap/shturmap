using System.Collections.Concurrent;
using SkiaSharp;

namespace Shturmap.Game.Screenshots;

/// <summary>How a candidate's grid image stands in a cell: over the cell's footprint (a grid's item, turned a quarter where
/// the cell stands the other way), or fitted into a slot's box, centred (a loadout slot, a container's own slot, a special
/// slot, which holds its item in one cell whatever its footprint).</summary>
internal enum DeskIconPlace
{
    Footprint,
    Fitted,
}

/// <summary>One candidate's likeness to a cell's picture, −1 to 1, or null when its grid image isn't at hand; and its
/// likeness where the candidates' images differ (<see cref="DeskIcons.Contrast"/>), null where they don't.</summary>
internal sealed record DeskIconScore(string ItemId, double? Score)
{
    public double? Contrast { get; init; }
}

/// <summary>
/// A "one of N" cell told apart by its picture (owner, 2026-10-25, "go ham"; docs/DESIGN.md §2, "For the stash desk"): when
/// a cell's label fits several items, its art is compared with each candidate's catalogue icon, tarkov.dev's grid image (the
/// art the game draws), from the download cache only. The label match stays the first word: only the candidates it left are
/// compared. The likeness is a correlation of luma (less its mean, so a hover's light changes nothing) and colour at
/// <see cref="PerCell"/> samples a cell, the label's band, the count's and the marks' corners left out; the best of a few
/// sizes and shifts. A cell is taken as its likeliest candidate only when every candidate's image was at hand, the
/// likeliest is like the cell's art at all (<see cref="Least"/>), and where the images differ it is clearly ahead of the
/// next (<see cref="Margin"/>, by <see cref="Contrast"/>); else it stays "one of N", the likeliest first. Items drawn with
/// one art (two keys of one look, a magazine in two lengths of one look) stay "one of N". A candidate whose image isn't in
/// the cache is asked for through the art path the view uses, for the next reading; a reading never waits for it. Nothing
/// of the screenshot is kept: the cell's part is sampled small, compared and let go.
/// </summary>
public sealed class DeskIcons
{
    /// <summary>No images: every cell stays as its label left it.</summary>
    public static readonly DeskIcons None = new(_ => null);

    /// <summary>The comparison's samples along a cell's side (a 1440p cell is 84 px).</summary>
    internal const int PerCell = 32;

    /// <summary>
    /// How far the likeliest candidate's likeness where the images differ must stand ahead of the next's. Measured on the
    /// owner's screenshots (2026-10-25, 325 "one of N" cells of 29 kinds, each item checked by eye; the cells also brightened
    /// by 24 levels and blurred): a wrong candidate first by 0.028 at the most (two Big Stick magazines of one look), the
    /// right ones first by 0.108 at the least (the BAC Bastion's colours, blurred); 0.08 settles the same cells as anything
    /// from 0.03 to 0.10.
    /// </summary>
    internal const double Margin = 0.08;

    /// <summary>The least likeness the likeliest must have over the whole cell: below it the cell's art is none of the
    /// images (a tooltip over it, art tarkov.dev no longer draws so). The right ones measured 0.56 and more (a rig fitted
    /// into its slot's box), most over 0.9.</summary>
    internal const double Least = 0.5;

    /// <summary>How much the colour counts against the luma: armbands, helmets and plate carriers in several colours differ
    /// by colour alone.</summary>
    internal const float ColourWeight = 1.5f;

    /// <summary>How far (in grey levels of luma and weighted colour) a sample must differ among the candidates' images to
    /// tell them apart there.</summary>
    internal const float ContrastStep = 12;

    // The least samples the images must differ in for the contrast to say anything: fewer, and they are one art.
    private const int LeastContrast = 24;

    // The game draws an item's art somewhat larger in its cell than tarkov.dev's image (StashTemplate, 2026-10-11): the
    // cell's part is tried at these sizes, and shifted a sample each way.
    private static readonly double[] Scales = [1.0, 1.08, 1.16];

    private readonly Func<string, byte[]?> _cached;
    private readonly Action<string>? _fetch;

    // The images as compared, per item and place: made once a run, a few kilobytes each.
    private readonly ConcurrentDictionary<(string Id, DeskIconPlace Place, int W, int H), Planes[]> _images = new();
    private const int MostKept = 4000;

    /// <param name="cached">An item's grid image from the download cache (PNG or WebP bytes), or null when it isn't there.</param>
    /// <param name="fetch">Asks for an item's grid image not in the cache, for a later reading; never waited for.</param>
    public DeskIcons(Func<string, byte[]?> cached, Action<string>? fetch = null)
    {
        _cached = cached;
        _fetch = fetch;
    }

    /// <summary>
    /// The cell's candidates after its picture: one when it is clearly one of them, else all of them, the likeliest first
    /// (those whose image wasn't at hand last, as they came); with each candidate's likeness. Over a grid's footprint, a
    /// candidate of a larger footprint (the reading's "a larger footprint the label could also have") is compared over the
    /// cells it would take from the label's top right, and must be clearly less like than the likeliest.
    /// </summary>
    /// <param name="w">The cell's footprint in cells (a grid's item); ignored when fitted.</param>
    internal (List<DeskCandidate> Candidates, IReadOnlyList<DeskIconScore> Scores) Settle(SKBitmap picture, SKRectI cell, int pitch,
        List<DeskCandidate> candidates, DeskIconPlace place, int w = 1, int h = 1)
    {
        var byId = new Dictionary<string, DeskCandidate>(StringComparer.Ordinal);
        foreach (var c in candidates)
            byId.TryAdd(c.ItemId, c);
        if (byId.Count < 2 || pitch <= 0 || cell.Width <= 0 || cell.Height <= 0)
            return (candidates, []);
        bool Takes(DeskCandidate c) => place == DeskIconPlace.Fitted || (c.W == w && c.H == h) || (c.W == h && c.H == w);
        var (dw, dh) = place == DeskIconPlace.Footprint
            ? (w * PerCell, h * PerCell)
            : (Math.Max(8, (int)Math.Round(cell.Width * (double)PerCell / pitch)), Math.Max(8, (int)Math.Round(cell.Height * (double)PerCell / pitch)));
        var mask = Mask(dw, dh);
        var views = Views(picture, cell, dw, dh);
        var scores = new List<DeskIconScore>();
        var images = new List<(string Id, Planes[] Images)>();
        foreach (var (id, candidate) in byId)
        {
            if (!Takes(candidate))
                scores.Add(new DeskIconScore(id, Larger(picture, cell, pitch, candidate)));
            else if (ImagesOf(id, place, dw, dh, cell, pitch) is { } own)
                images.Add((id, own));
            else
                scores.Add(new DeskIconScore(id, null));
        }
        if (images.Count > 0)
        {
            // One alignment for the cell, the size and shift the likeliest few agree best at (the game draws the items of
            // one label alike): every candidate is compared there, none finds a shift of its own to look better.
            var view = Aligned(images, views, mask);
            var chosen = new List<(string Id, Planes Image)>();
            foreach (var (id, own) in images)
            {
                var (best, image) = own.Select(i => (Score: Likeness(i, view, mask), Image: i)).MaxBy(x => x.Score);
                scores.Add(new DeskIconScore(id, best));
                chosen.Add((id, image));
            }
            if (chosen.Count >= 2 && scores.Where(s => Takes(byId[s.ItemId])).All(s => s.Score.HasValue))
            {
                var contrast = Contrast(chosen.Select(c => c.Image).ToList(), view, mask);
                scores = scores.Select(s => chosen.FindIndex(c => c.Id == s.ItemId) is var k and >= 0 ? s with { Contrast = contrast[k] } : s).ToList();
            }
        }
        var ordered = scores.OrderByDescending(s => s.Score.HasValue).ThenByDescending(s => s.Score ?? 0).ToList();
        return (Settled(ordered, byId, Takes) is { } one ? [one] : ordered.Select(s => byId[s.ItemId]).ToList(), ordered);
    }

    // The likeliest when the picture settles it: every image at hand, the likeliest of the cell's footprint like its art
    // at all, ahead of the next by the margin where their images differ (first there too), and ahead of any larger one.
    private static DeskCandidate? Settled(List<DeskIconScore> ordered, Dictionary<string, DeskCandidate> byId, Func<DeskCandidate, bool> takes)
    {
        if (ordered.Any(s => s.Score is null))
            return null;
        var main = ordered.Where(s => takes(byId[s.ItemId])).ToList();
        var larger = ordered.Where(s => !takes(byId[s.ItemId])).ToList();
        if (main.Count == 0 || main[0].Score < Least)
            return null;
        if (main.Count >= 2)
        {
            if (main.Any(s => s.Contrast is null))
                return null;
            var byContrast = main.OrderByDescending(s => s.Contrast).ToList();
            if (byContrast[0].ItemId != main[0].ItemId || byContrast[0].Contrast - byContrast[1].Contrast < Margin)
                return null;
        }
        if (larger.Count > 0 && main[0].Score - larger.Max(s => s.Score) < Margin)
            return null;
        return byId[main[0].ItemId];
    }

    // A candidate of a larger footprint over the cells it would take from the cell's top right, either way round: its
    // likeness, or null when its image isn't at hand; −1 when it can't lie there (past the picture's edge).
    private double? Larger(SKBitmap picture, SKRectI cell, int pitch, DeskCandidate candidate)
    {
        double? best = null;
        foreach (var (fw, fh) in new[] { (candidate.W, candidate.H), (candidate.H, candidate.W) }.Distinct())
        {
            var area = SKRectI.Create(cell.Right - fw * pitch, cell.Top, fw * pitch, fh * pitch);
            if (area.Width < cell.Width || area.Height < cell.Height)
                continue;
            if (area.Left < 0 || area.Top < 0 || area.Right > picture.Width || area.Bottom > picture.Height)
            {
                best ??= -1;
                continue;
            }
            int dw = fw * PerCell, dh = fh * PerCell;
            if (ImagesOf(candidate.ItemId, DeskIconPlace.Footprint, dw, dh, area, pitch) is not { } images)
                return null;
            var score = Best(images, Views(picture, area, dw, dh), Mask(dw, dh)).Score;
            best = Math.Max(best ?? -1, score);
        }
        return best ?? -1;
    }

    // The cell's view: each candidate compared at the sizes unshifted, then the three likeliest at every size and shift.
    private static Planes Aligned(List<(string Id, Planes[] Images)> images, List<Planes> views, bool[] mask)
    {
        var unshifted = views.Where((_, k) => k % 9 == 4).ToList();
        var likeliest = images.Select(c => (c.Images, Score: Best(c.Images, unshifted, mask).Score)).OrderByDescending(c => c.Score).Take(3);
        return likeliest.Select(c => Best(c.Images, views, mask)).MaxBy(b => b.Score).View;
    }

    private static (double Score, Planes Image, Planes View) Best(Planes[] images, List<Planes> views, bool[] mask)
    {
        var best = (Score: -2.0, Image: images[0], View: views[0]);
        foreach (var image in images)
        {
            foreach (var view in views)
            {
                var score = Likeness(image, view, mask);
                if (score > best.Score)
                    best = (score, image, view);
            }
        }
        return best;
    }

    // The item's image as compared at the cell's samples: over the footprint, as it lies, or turned a quarter either way
    // where the cell stands the other way round; fitted into a box, centred. Null when the cache hasn't the image (asked
    // for, for the next reading) or it isn't one.
    private Planes[]? ImagesOf(string id, DeskIconPlace place, int dw, int dh, SKRectI cell, int pitch)
    {
        var key = (id, place, dw, dh);
        if (_images.TryGetValue(key, out var known))
            return known;
        var bytes = _cached(id);
        if (bytes is null)
        {
            _fetch?.Invoke(id);
            return null;
        }
        using var image = StashPicture.Decode(bytes);
        if (image is null || image.Width < 8 || image.Height < 8)
            return null;
        var made = new List<Planes>();
        if (place == DeskIconPlace.Footprint)
        {
            var turned = (image.Width > image.Height * 1.2 && dh > dw * 1.2) || (image.Height > image.Width * 1.2 && dw > dh * 1.2);
            if (!turned)
                made.Add(Planes.Sample(image, 0, 0, image.Width, image.Height, dw, dh));
            else
            {
                foreach (var degrees in new[] { 90, 270 })
                {
                    using var t = Turned(image, degrees);
                    made.Add(Planes.Sample(t, 0, 0, t.Width, t.Height, dw, dh));
                }
            }
        }
        else
        {
            // Fitted into the box as the game draws a slot's item: its cells at the screen's pitch, shrunk to fit, centred;
            // the box's rest is no art of it. (Stretched over the box, the armband's and the rig's agreed less.)
            var full = pitch / (double)StashTemplate.BaseCell;
            var scale = Math.Min(1.0, Math.Min(cell.Width / (image.Width * full), cell.Height / (image.Height * full)));
            var fw = image.Width * full * scale * dw / cell.Width;
            var fh = image.Height * full * scale * dh / cell.Height;
            int iw = Math.Clamp((int)Math.Round(fw), 1, dw), ih = Math.Clamp((int)Math.Round(fh), 1, dh);
            int x0 = (dw - iw) / 2, y0 = (dh - ih) / 2;
            var inner = Planes.Sample(image, 0, 0, image.Width, image.Height, iw, ih);
            var planes = new Planes(dw, dh) { Within = new bool[dw * dh] };
            for (var y = 0; y < ih; y++)
            {
                for (var x = 0; x < iw; x++)
                {
                    int to = (y0 + y) * dw + x0 + x, from = y * iw + x;
                    planes.L[to] = inner.L[from];
                    planes.A[to] = inner.A[from];
                    planes.O[to] = inner.O[from];
                    planes.Within[to] = true;
                }
            }
            made.Add(planes);
        }
        var images = made.ToArray();
        if (_images.Count > MostKept)
            _images.Clear();
        _images[key] = images;
        return images;
    }

    private static SKBitmap Turned(SKBitmap image, int degrees)
    {
        var turned = new SKBitmap(new SKImageInfo(image.Height, image.Width, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(turned);
        canvas.Translate(turned.Width / 2f, turned.Height / 2f);
        canvas.RotateDegrees(degrees);
        canvas.Translate(-image.Width / 2f, -image.Height / 2f);
        using var art = SKImage.FromBitmap(image);
        canvas.DrawImage(art, 0, 0, StashPicture.BlockySampling);
        return turned;
    }

    // The cell's part at the comparison's samples, at the sizes the game may draw the art at, each shifted a sample each way.
    private static List<Planes> Views(SKBitmap picture, SKRectI cell, int dw, int dh)
    {
        var views = new List<Planes>(Scales.Length * 9);
        double sx = cell.Width / (double)dw, sy = cell.Height / (double)dh;
        foreach (var scale in Scales)
        {
            double w = cell.Width / scale, h = cell.Height / scale;
            double x0 = cell.Left + (cell.Width - w) / 2, y0 = cell.Top + (cell.Height - h) / 2;
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                    views.Add(Planes.Sample(picture, x0 + dx * sx, y0 + dy * sy, w, h, dw, dh));
            }
        }
        return views;
    }

    /// <summary>What of a cell is its art: not the label band at the top, not the count's (or condition's) band at the bottom,
    /// not the found-in-raid mark at the bottom right above the count, nor a quick slot's plate at the top left: the game
    /// draws those over the art.</summary>
    internal static bool[] Mask(int dw, int dh)
    {
        var mask = new bool[dw * dh];
        int top = (int)Math.Round(0.22 * PerCell), bottom = dh - (int)Math.Round(0.24 * PerCell);
        int markX = dw - (int)Math.Round(0.35 * PerCell), markY = dh - (int)Math.Round(0.5 * PerCell);
        int plateX = (int)Math.Round(0.4 * PerCell), plateY = (int)Math.Round(0.4 * PerCell);
        for (var y = 0; y < dh; y++)
        {
            for (var x = 0; x < dw; x++)
            {
                var drawn = y < top || y >= bottom || (x >= markX && y >= markY) || (x < plateX && y < plateY);
                mask[y * dw + x] = !drawn && x > 0 && y > 0 && x < dw - 1 && y < dh - 1;
            }
        }
        return mask;
    }

    /// <summary>The likeness of an image and a view over the samples counted (the mask, and the image's own area when
    /// fitted): luma less its mean and colour as it is (grey is none), one vector each, their cosine.</summary>
    internal static double Likeness(Planes image, Planes view, bool[] mask)
    {
        // One pass of sums; the lumas' means taken out after.
        double st = 0, sv = 0, stt = 0, svv = 0, stv = 0, ctv = 0, ctt = 0, cvv = 0;
        var n = 0;
        var within = image.Within;
        float[] lt = image.L, lv = view.L, at = image.A, av = view.A, ot = image.O, ov = view.O;
        for (var i = 0; i < mask.Length; i++)
        {
            if (!mask[i] || (within is not null && !within[i]))
                continue;
            double t = lt[i], v = lv[i];
            st += t;
            sv += v;
            stt += t * t;
            svv += v * v;
            stv += t * v;
            ctv += at[i] * av[i] + ot[i] * ov[i];
            ctt += at[i] * at[i] + ot[i] * ot[i];
            cvv += av[i] * av[i] + ov[i] * ov[i];
            n++;
        }
        if (n < 16)
            return 0;
        const double c2 = ColourWeight * ColourWeight;
        var dot = stv - st * sv / n + c2 * ctv;
        var tt = stt - st * st / n + c2 * ctt;
        var vv = svv - sv * sv / n + c2 * cvv;
        return tt <= 1e-6 || vv <= 1e-6 ? 0 : dot / Math.Sqrt(tt * vv);
    }

    /// <summary>
    /// Each candidate's likeness to the cell's view only where the candidates' images differ by
    /// <see cref="ContrastStep"/> or more: what they share (the field, the outline of one model in several colours) says
    /// nothing of which it is, and over the whole cell it made a plate carrier's two colours 0.07 apart, here 0.13. Null for
    /// all where they differ in too few samples: one art for several items.
    /// </summary>
    internal static double?[] Contrast(List<Planes> chosen, Planes view, bool[] mask)
    {
        var k = chosen.Count;
        var result = new double?[k];
        var where = new bool[mask.Length];
        var count = 0;
        const double c2 = ColourWeight * ColourWeight;
        // Where every fitted image lies (a grid's images cover the whole cell).
        var all = (bool[])mask.Clone();
        foreach (var w in chosen.Select(c => c.Within).OfType<bool[]>())
        {
            for (var i = 0; i < all.Length; i++)
                all[i] &= w[i];
        }
        for (var i = 0; i < mask.Length; i++)
        {
            if (!all[i])
                continue;
            double ml = 0, ma = 0, mo = 0;
            foreach (var image in chosen)
            {
                ml += image.L[i];
                ma += image.A[i];
                mo += image.O[i];
            }
            ml /= k;
            ma /= k;
            mo /= k;
            double spread = 0;
            foreach (var image in chosen)
            {
                double dl = image.L[i] - ml, da = image.A[i] - ma, dout = image.O[i] - mo;
                spread = Math.Max(spread, dl * dl + c2 * (da * da + dout * dout));
            }
            if (spread >= ContrastStep * ContrastStep)
            {
                where[i] = true;
                count++;
            }
        }
        if (count < LeastContrast)
            return result;
        for (var j = 0; j < k; j++)
            result[j] = Likeness(chosen[j], view, where);
        return result;
    }

    /// <summary>A picture's part as samples of luma and two colour axes (red against green, yellow against blue), each the
    /// average of the pixels it covers; for a fitted image, where in the box it lies.</summary>
    internal sealed class Planes(int w, int h)
    {
        public int W { get; } = w;
        public int H { get; } = h;
        public float[] L { get; } = new float[w * h];
        public float[] A { get; } = new float[w * h];
        public float[] O { get; } = new float[w * h];
        public bool[]? Within { get; init; }

        /// <summary>The bitmap's area from (x0, y0), w by h pixels (fractions allowed; the edge repeated outside it),
        /// averaged into dw by dh samples.</summary>
        public static Planes Sample(SKBitmap bitmap, double x0, double y0, double w, double h, int dw, int dh)
        {
            var planes = new Planes(dw, dh);
            var pixels = bitmap.GetPixelSpan();
            int bw = bitmap.Width, bh = bitmap.Height;
            var (cols, colWeights, colStarts) = Spans(x0, w, dw, bw);
            var (rows, rowWeights, rowStarts) = Spans(y0, h, dh, bh);
            for (var v = 0; v < dh; v++)
            {
                for (var u = 0; u < dw; u++)
                {
                    double r = 0, g = 0, b = 0, weight = 0;
                    for (var ry = rowStarts[v]; ry < rowStarts[v + 1]; ry++)
                    {
                        var rowAt = rows[ry] * bw;
                        var wy = rowWeights[ry];
                        for (var cx = colStarts[u]; cx < colStarts[u + 1]; cx++)
                        {
                            var i = 4 * (rowAt + cols[cx]);
                            var k = colWeights[cx] * wy;
                            b += k * pixels[i];
                            g += k * pixels[i + 1];
                            r += k * pixels[i + 2];
                            weight += k;
                        }
                    }
                    if (weight <= 0)
                        continue;
                    r /= weight;
                    g /= weight;
                    b /= weight;
                    var at = v * dw + u;
                    planes.L[at] = (float)(0.299 * r + 0.587 * g + 0.114 * b);
                    planes.A[at] = (float)(r - g);
                    planes.O[at] = (float)((r + g) / 2 - b);
                }
            }
            return planes;
        }

        // For each sample along one side, the pixels it covers with their share of it, flattened: sample s covers the
        // entries from starts[s] to starts[s + 1].
        private static (int[] At, double[] Weight, int[] Starts) Spans(double start, double length, int samples, int size)
        {
            var at = new List<int>(samples * 4);
            var weight = new List<double>(samples * 4);
            var starts = new int[samples + 1];
            var step = length / samples;
            for (var s = 0; s < samples; s++)
            {
                starts[s] = at.Count;
                double a = start + s * step, b = a + step;
                for (var p = (int)Math.Floor(a); p < b; p++)
                {
                    var share = Math.Min(b, p + 1) - Math.Max(a, p);
                    if (share <= 1e-6)
                        continue;
                    at.Add(Math.Clamp(p, 0, size - 1));
                    weight.Add(share);
                }
            }
            starts[samples] = at.Count;
            return (at.ToArray(), weight.ToArray(), starts);
        }
    }
}
