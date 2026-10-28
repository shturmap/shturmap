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

    // The colour of what the shape finds at a place, per channel (blue, green, red): the mean of the pixels on its ring and
    // check less the mean of the field around them.
    private static (float B, float G, float R) Ink(SKBitmap bitmap, int cx, int cy, Drawn shape)
    {
        var pixels = bitmap.GetPixelSpan();
        var side = 2 * shape.Reach + 1;
        double[] on = new double[3], off = new double[3];
        int nOn = 0, nOff = 0;
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var i = y * side + x;
                int px = cx - shape.Reach + x, py = cy - shape.Reach + y;
                if (!shape.Mask[i] || px < 0 || py < 0 || px >= bitmap.Width || py >= bitmap.Height)
                    continue;
                var p = 4 * (py * bitmap.Width + px);
                var sum = shape.Values[i] > 0 ? on : off;
                for (var c = 0; c < 3; c++)
                    sum[c] += pixels[p + c];
                if (shape.Values[i] > 0)
                    nOn++;
                else
                    nOff++;
            }
        }
        if (nOn == 0 || nOff == 0)
            return (0, 0, 0);
        return ((float)(on[0] / nOn - off[0] / nOff), (float)(on[1] / nOn - off[1] / nOff), (float)(on[2] / nOn - off[2] / nOff));
    }

    /// <summary>
    /// Whether a slot's item carries the mark (owner, 2026-10-28): true or false when the correlation says so at the
    /// slot's two places, null between. A slot draws its mark further from its box's corner than a grid's cell does: a
    /// gear slot's 12 px in from the right and 14 up, or 31 up over a printed condition; a weapon's or a knife's, whose
    /// slot has a bar down its right side, 29 in and 14 up, or 59 up over its rounds (2560×1440, scaled by the pitch).
    /// What stands there counts as the mark only where it is grey: the game's insurance shield (orange) and a secure
    /// container's lock (in a magenta glow) stand at the same place and correlate about as well, so their place reads as
    /// no mark.
    /// </summary>
    public static bool? SeenOnSlot(SKBitmap bitmap, SKRectI box, int pitch, bool barred)
    {
        var k = pitch / 84.0;
        var shape = Shape(k);
        var (x, low, high) = barred ? (29, 14, 59) : (12, 14, 31);
        var score = Math.Max(Grey(bitmap, box.Right - x * k, box.Bottom - low * k, shape),
            Grey(bitmap, box.Right - x * k, box.Bottom - high * k, shape));
        return score >= Enough ? true : score <= Absent ? false : null;
    }

    // The best correlation around a place where what it finds is grey; a shape in colour there (the orange shield, the
    // lock's glow) is no mark, nothing at all.
    private static float Grey(SKBitmap bitmap, double cx, double cy, Drawn shape)
    {
        var (score, x, y) = BestAt(bitmap, cx, cy, shape);
        if (score <= Absent)
            return score;
        var (b, g, r) = Ink(bitmap, x, y, shape);
        float most = Math.Max(b, Math.Max(g, r)), least = Math.Min(b, Math.Min(g, r));
        return most > 0 && least >= GreyShare * most ? score : 0;
    }

    // The least channel of a mark's ink against its most. Measured on the owner's screenshots of 2026-10-27 and 2026-10-28
    // (2560×1440 and 1920×1080, the slots' 47 marks and 154 without, each against its answer): the mark 0.92 to 0.98, under
    // the red low-health tint too; the insurance shield 0.10 to 0.13; the secure container's lock 0.47 to 0.68.
    private const float GreyShare = 0.8f;

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
    private static float Best(SKBitmap bitmap, double cx, double cy, Drawn shape) => BestAt(bitmap, cx, cy, shape).Score;

    private static (float Score, int X, int Y) BestAt(SKBitmap bitmap, double cx, double cy, Drawn shape)
    {
        (float Score, int X, int Y) best = (-1, 0, 0);
        for (var dy = -Shift; dy <= Shift; dy++)
        {
            for (var dx = -Shift; dx <= Shift; dx++)
            {
                int x = (int)Math.Round(cx) + dx, y = (int)Math.Round(cy) + dy;
                var score = Correlation(bitmap, x, y, shape);
                if (score > best.Score)
                    best = (score, x, y);
            }
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
