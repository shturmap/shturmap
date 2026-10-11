using SkiaSharp;

namespace Shturmap.Game.Screenshots;

/// <summary>
/// The found-in-raid mark: a small light check in a circle the game draws at an item's bottom right, above the count
/// when the item has one. Seen 2026-10-11 at 1440p: 14 px across, its middle 12 px in from the right and 12 px up
/// from the bottom, or 26 px up over a count. It is looked for as the correlation of those two places with a drawn
/// circle-and-check of the same measure (not the game's icon: a shape of Shturmap's own), shifted a little each way.
/// </summary>
internal static class StashMarks
{
    // The mark's measure at 1440p, in pixels: the circle's radius, the ring's half width, and the check's stroke.
    private const double Radius = 6.3;
    private const double RingHalf = 1.0;
    private const double Stroke = 0.9;

    // How far around the middle the comparison reaches: the dark field around the circle is part of the shape.
    private const double Reach = 9.5;

    private const int Shift = 2;

    /// <summary>The two places' correlations with the shape, −1 to 1: without a count under the mark, and over one.</summary>
    public static (float Plain, float OverCount) Scores(SKBitmap bitmap, SKRectI rect, int pitch)
    {
        var k = pitch / 84.0;
        var shape = Shape(k);
        return (Best(bitmap, rect.Right - 12 * k, rect.Bottom - 12 * k, shape),
            Best(bitmap, rect.Right - 11 * k, rect.Bottom - 26 * k, shape));
    }

    /// <summary>Whether a mark stands at either place: true or false when the correlation says so, null between
    /// (never claimed when unsure).</summary>
    public static bool? Seen(SKBitmap bitmap, SKRectI rect, int pitch)
    {
        var (plain, over) = Scores(bitmap, rect, pitch);
        var score = Math.Max(plain, over);
        return score >= Enough ? true : score <= Absent ? false : null;
    }

    // The correlations that say the mark is there and that it isn't. Measured on the owner's three truths
    // (2026-10-11, 150 cells checked by eye): cells with the mark correlate at 0.43 to 0.54, one at 0.39 (the mark
    // over an item's red art); cells without at 0.29 or less (a count's digits at the plain place).
    internal const float Enough = 0.36f;
    internal const float Absent = 0.3f;

    // The shape as a square of values around its middle, normalized, with its mask (the square's corners are out).
    private sealed record Drawn(int Reach, float[] Values, bool[] Mask);

    private static Drawn Shape(double k)
    {
        var reach = (int)Math.Ceiling(Reach * k);
        var side = 2 * reach + 1;
        var values = new float[side * side];
        var mask = new bool[side * side];
        // The check: from the left, down to the bottom, then up and out past the circle's top right.
        (double X, double Y)[] check = [(-3.6, -0.2), (-1.2, 2.6), (5.4, -4.6)];
        for (var y = -reach; y <= reach; y++)
        {
            for (var x = -reach; x <= reach; x++)
            {
                double px = x / k, py = y / k;
                var r = Math.Sqrt(px * px + py * py);
                if (r > Reach)
                    continue;
                var i = (y + reach) * side + (x + reach);
                mask[i] = true;
                var onRing = Math.Abs(r - Radius) <= RingHalf;
                var onCheck = Segment(px, py, check[0], check[1]) <= Stroke || Segment(px, py, check[1], check[2]) <= Stroke;
                values[i] = onRing || onCheck ? 1 : -0.5f;
            }
        }
        // Normalized over the mask, so that a dot product with a likewise normalized window is a correlation.
        double mean = 0;
        var n = 0;
        for (var i = 0; i < values.Length; i++)
        {
            if (!mask[i])
                continue;
            mean += values[i];
            n++;
        }
        mean /= Math.Max(1, n);
        double norm = 0;
        for (var i = 0; i < values.Length; i++)
        {
            if (!mask[i])
                continue;
            values[i] -= (float)mean;
            norm += values[i] * values[i];
        }
        var scale = norm > 0 ? (float)(1 / Math.Sqrt(norm)) : 0;
        for (var i = 0; i < values.Length; i++)
            values[i] *= scale;
        return new Drawn(reach, values, mask);
    }

    // The distance from a point to a line segment.
    private static double Segment(double px, double py, (double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        var t = Math.Clamp(((px - a.X) * dx + (py - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
        double x = a.X + t * dx - px, y = a.Y + t * dy - py;
        return Math.Sqrt(x * x + y * y);
    }

    // The best correlation of the shape with the picture around a place, over small shifts.
    private static float Best(SKBitmap bitmap, double cx, double cy, Drawn shape)
    {
        float best = -1;
        for (var dy = -Shift; dy <= Shift; dy++)
        {
            for (var dx = -Shift; dx <= Shift; dx++)
                best = Math.Max(best, Correlation(bitmap, (int)Math.Round(cx) + dx, (int)Math.Round(cy) + dy, shape));
        }
        return best;
    }

    private static float Correlation(SKBitmap bitmap, int cx, int cy, Drawn shape)
    {
        var pixels = bitmap.GetPixelSpan();
        var side = 2 * shape.Reach + 1;
        var window = new float[side * side];
        double sum = 0, sq = 0;
        var n = 0;
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var i = y * side + x;
                if (!shape.Mask[i])
                    continue;
                int px = cx - shape.Reach + x, py = cy - shape.Reach + y;
                if (px < 0 || py < 0 || px >= bitmap.Width || py >= bitmap.Height)
                    return -1;
                var p = 4 * (py * bitmap.Width + px);
                var lum = 0.114f * pixels[p] + 0.587f * pixels[p + 1] + 0.299f * pixels[p + 2];
                window[i] = lum;
                sum += lum;
                sq += lum * lum;
                n++;
            }
        }
        var spread = sq - sum * sum / n;
        if (spread <= 1e-6)
            return 0;
        var mean = sum / n;
        double dot = 0;
        for (var i = 0; i < window.Length; i++)
        {
            if (shape.Mask[i])
                dot += (window[i] - mean) * shape.Values[i];
        }
        return (float)(dot / Math.Sqrt(spread));
    }
}
