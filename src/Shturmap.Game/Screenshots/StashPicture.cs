using System.Runtime.InteropServices.WindowsRuntime;
using SkiaSharp;
using Windows.Graphics.Imaging;

namespace Shturmap.Game.Screenshots;

/// <summary>A picture as luminance, one value per pixel, for the grid finding and the comparisons.</summary>
internal sealed class StashGray(int w, int h)
{
    public int W { get; } = w;
    public int H { get; } = h;
    public float[] P { get; } = new float[w * h];

    public float this[int x, int y]
    {
        get => P[y * W + x];
        set => P[y * W + x] = value;
    }

    public static StashGray Of(SKBitmap bitmap)
    {
        var gray = new StashGray(bitmap.Width, bitmap.Height);
        var pixels = bitmap.GetPixelSpan();
        for (var i = 0; i < gray.P.Length; i++)
            gray.P[i] = 0.114f * pixels[4 * i] + 0.587f * pixels[4 * i + 1] + 0.299f * pixels[4 * i + 2];
        return gray;
    }

    /// <summary>A three by three average: the fine hatch of the cell's field, two pixels in period, goes.</summary>
    public StashGray Blur3()
    {
        var b = new StashGray(W, H);
        for (var y = 0; y < H; y++)
        {
            for (var x = 0; x < W; x++)
            {
                float sum = 0;
                var n = 0;
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= W || yy >= H)
                            continue;
                        sum += this[xx, yy];
                        n++;
                    }
                }
                b[x, y] = sum / n;
            }
        }
        return b;
    }

    /// <summary>The picture less its local average (a box of the given radius): tints, gradients and the game's hover
    /// highlight go, edges stay.</summary>
    public StashGray HighPass(int radius)
    {
        var stride = W + 1;
        var sum = new double[stride * (H + 1)];
        for (var y = 1; y <= H; y++)
        {
            double row = 0;
            for (var x = 1; x <= W; x++)
            {
                row += this[x - 1, y - 1];
                sum[y * stride + x] = sum[(y - 1) * stride + x] + row;
            }
        }
        var outp = new StashGray(W, H);
        for (var y = 0; y < H; y++)
        {
            int y0 = Math.Max(0, y - radius), y1 = Math.Min(H, y + radius + 1);
            for (var x = 0; x < W; x++)
            {
                int x0 = Math.Max(0, x - radius), x1 = Math.Min(W, x + radius + 1);
                var area = (x1 - x0) * (y1 - y0);
                var mean = (sum[y1 * stride + x1] - sum[y0 * stride + x1] - sum[y1 * stride + x0] + sum[y0 * stride + x0]) / area;
                outp[x, y] = (float)(this[x, y] - mean);
            }
        }
        return outp;
    }

    /// <summary>The picture moved by (dx, dy), the edge pixels repeated.</summary>
    public StashGray Shifted(int dx, int dy)
    {
        var s = new StashGray(W, H);
        for (var y = 0; y < H; y++)
        {
            for (var x = 0; x < W; x++)
                s[x, y] = this[Math.Clamp(x + dx, 0, W - 1), Math.Clamp(y + dy, 0, H - 1)];
        }
        return s;
    }

    /// <summary>The spread (standard deviation) of the values in a box, for telling an empty cell from one with an item.</summary>
    public double Spread(int x0, int y0, int x1, int y1)
    {
        double sum = 0, sq = 0;
        var n = 0;
        for (var y = Math.Max(0, y0); y < Math.Min(H, y1); y++)
        {
            for (var x = Math.Max(0, x0); x < Math.Min(W, x1); x++)
            {
                var v = this[x, y];
                sum += v;
                sq += v * v;
                n++;
            }
        }
        if (n == 0)
            return 0;
        var variance = sq / n - sum / n * (sum / n);
        return Math.Sqrt(Math.Max(0, variance));
    }
}

/// <summary>
/// Where the picture shows the game's thin light lines between cells: for every column, how many of its pixels down
/// to a row are on a vertical line, and for every row the same along it, so a cell's four sides are checked in
/// constant time.
/// </summary>
internal sealed class StashLines
{
    // A line is this much lighter than the field two pixels off on both sides.
    private const float RidgeStep = 7;

    private readonly int _w, _h;
    private readonly int[] _v; // per column, the cumulative count of vertical-line pixels down the column
    private readonly int[] _hh; // per row, the cumulative count of horizontal-line pixels along the row
    private readonly StashGray _g;

    public StashLines(StashGray g)
    {
        _g = g;
        _w = g.W;
        _h = g.H;
        _v = new int[g.W * (g.H + 1)];
        _hh = new int[g.H * (g.W + 1)];
        // A line is a light band of one to four pixels with the darker field two pixels off on both sides: the
        // game's one-pixel line in the stash, its two-pixel line in a container window, and the line between the cyan
        // outlines of two highlighted cells.
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

    /// <summary>How much of the row y between x0 and x1 is a light line (the best of y−1, y, y+1).</summary>
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

    /// <summary>
    /// How strongly a vertical line stands out between y0 and y1: the median over the rows of the line's brighter pixel
    /// over the brighter field two pixels off on either side, at the best of the columns within two pixels of x (in grey
    /// levels; less than nothing where the field is lighter). An item's frame stands out by 25 to 130 levels on the
    /// owner's screenshots of 2026-10-11 and 2026-10-19, the grid's line seen through an item's background (between the
    /// cells of one wide item) by 18 at most: the share of rows <see cref="Vertical"/> counts sees both as a line.
    /// </summary>
    public double VerticalHeight(int x, int y0, int y1)
    {
        y0 = Math.Clamp(y0, 0, _h);
        y1 = Math.Clamp(y1, y0, _h);
        if (y1 == y0)
            return 0;
        var heights = new float[y1 - y0];
        var best = double.MinValue;
        for (var c = x - 2; c <= x + 2; c++)
        {
            if (c < 2 || c + 3 >= _w)
                continue;
            for (var y = y0; y < y1; y++)
                heights[y - y0] = Math.Max(_g[c, y], _g[c + 1, y]) - Math.Max(_g[c - 2, y], _g[c + 3, y]);
            best = Math.Max(best, Median(heights));
        }
        return best == double.MinValue ? 0 : best;
    }

    /// <summary>How strongly a horizontal line stands out between x0 and x1 (<see cref="VerticalHeight"/>, along a row).</summary>
    public double HorizontalHeight(int y, int x0, int x1)
    {
        x0 = Math.Clamp(x0, 0, _w);
        x1 = Math.Clamp(x1, x0, _w);
        if (x1 == x0)
            return 0;
        var heights = new float[x1 - x0];
        var best = double.MinValue;
        for (var r = y - 2; r <= y + 2; r++)
        {
            if (r < 2 || r + 3 >= _h)
                continue;
            for (var x = x0; x < x1; x++)
                heights[x - x0] = Math.Max(_g[x, r], _g[x, r + 1]) - Math.Max(_g[x, r - 2], _g[x, r + 3]);
            best = Math.Max(best, Median(heights));
        }
        return best == double.MinValue ? 0 : best;
    }

    private static double Median(float[] values)
    {
        var sorted = (float[])values.Clone();
        Array.Sort(sorted);
        return sorted[sorted.Length / 2];
    }
}

/// <summary>Cutting and resizing parts of the picture, and handing them to Windows' recognition.</summary>
internal static class StashPicture
{
    /// <summary>The resampling the parts are enlarged with: sharp (Catmull-Rom); blocky (nearest) and soft
    /// (Mitchell) as further tries, since the recognition reads some labels in one of them only.</summary>
    public static readonly SKSamplingOptions Sampling = new(SKCubicResampler.CatmullRom);
    public static readonly SKSamplingOptions BlockySampling = new(SKFilterMode.Nearest);
    public static readonly SKSamplingOptions SoftSampling = new(SKCubicResampler.Mitchell);

    /// <summary>A part of the picture resampled to the given size.</summary>
    public static SKBitmap Block(SKBitmap bitmap, SKRectI rect, int w, int h) => Block(bitmap, rect, w, h, Sampling);

    public static SKBitmap Block(SKBitmap bitmap, SKRectI rect, int w, int h, SKSamplingOptions sampling)
    {
        using var subset = new SKBitmap();
        if (!bitmap.ExtractSubset(subset, rect))
            throw new InvalidOperationException("Block outside the picture");
        return subset.Resize(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul), sampling)
               ?? throw new InvalidOperationException("Resize failed");
    }

    /// <summary>The bitmap with a margin of the given colour around it: the recognition reads letters at an edge badly.</summary>
    public static SKBitmap Framed(SKBitmap inner, int margin, SKColor colour)
    {
        var framed = new SKBitmap(new SKImageInfo(inner.Width + 2 * margin, inner.Height + 2 * margin, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(framed);
        canvas.Clear(colour);
        canvas.DrawBitmap(inner, margin, margin, Sampling);
        return framed;
    }

    /// <summary>The bitmap in grey by each pixel's brightest channel: the game's red print ("8/9", a used-up med's
    /// count) as bright as its white one, which Windows' recognition, taking the luminance, reads dim.</summary>
    public static SKBitmap Lit(SKBitmap inner)
    {
        var bytes = inner.GetPixelSpan().ToArray();
        for (var i = 0; i + 3 < bytes.Length; i += 4)
        {
            var v = Math.Max(bytes[i], Math.Max(bytes[i + 1], bytes[i + 2]));
            bytes[i] = bytes[i + 1] = bytes[i + 2] = v;
            bytes[i + 3] = 255;
        }
        var lit = new SKBitmap(new SKImageInfo(inner.Width, inner.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        System.Runtime.InteropServices.Marshal.Copy(bytes, 0, lit.GetPixels(), bytes.Length);
        return lit;
    }

    /// <summary>The bitmap as Windows' recognition takes it.</summary>
    public static SoftwareBitmap ToSoftwareBitmap(SKBitmap bitmap)
    {
        var bytes = bitmap.GetPixelSpan().ToArray();
        return SoftwareBitmap.CreateCopyFromBuffer(bytes.AsBuffer(), BitmapPixelFormat.Bgra8, bitmap.Width, bitmap.Height, BitmapAlphaMode.Premultiplied);
    }

    /// <summary>Decoded pixels as BGRA, whatever the file's own format.</summary>
    public static SKBitmap? Decode(Stream stream)
    {
        var decoded = SKBitmap.Decode(stream);
        return ToBgra(decoded);
    }

    public static SKBitmap? Decode(byte[] bytes) => ToBgra(SKBitmap.Decode(bytes));

    private static SKBitmap? ToBgra(SKBitmap? decoded)
    {
        if (decoded is null)
            return null;
        if (decoded.ColorType == SKColorType.Bgra8888)
            return decoded;
        var copy = decoded.Copy(SKColorType.Bgra8888);
        decoded.Dispose();
        return copy;
    }
}
