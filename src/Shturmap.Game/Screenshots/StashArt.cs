using SkiaSharp;

namespace Shturmap.Game.Screenshots;

/// <summary>
/// tarkov.dev's grid image of an item, as the game's cell is compared against it: a check on the label's verdict,
/// never a source of names (with a candidate set of a few dozen items, an item outside the set would match a
/// needed one's art). The game draws the art somewhat larger than the image and hatches the cell's field as the
/// image does, so the art alone is compared, at several sizes and small shifts, as a normalized correlation.
/// </summary>
internal sealed class StashTemplate
{
    /// <summary>The game's cell at 1080p, its line included: the images are drawn for it, (w·63+1) × (h·63+1).</summary>
    public const int BaseCell = 63;

    // The game draws an item's art about 15 % larger in its cell than the image does (seen 2026-10-11 at 1440p):
    // every image is tried at these sizes.
    private static readonly double[] Scales = [1.0, 1.08, 1.16, 1.24];

    // The art is also tried shifted by up to this many pixels each way.
    private const int Shift = 2;

    // The image at one size, normalized over its art's pixels (the mask) so that a dot product is a correlation.
    private sealed record Variant(bool[] Mask, float[] Art);

    public int W { get; }
    public int H { get; }
    private readonly List<Variant> _variants;

    private StashTemplate(int w, int h, List<Variant> variants)
    {
        W = w;
        H = h;
        _variants = variants;
    }

    /// <summary>The item's size in cells from the image's header alone, or null when the bytes aren't a grid image of
    /// whole cells. Cheap: the pixels aren't decoded.</summary>
    public static (int W, int H)? Size(byte[] bytes)
    {
        try
        {
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);
            return codec is null ? null : Cells(codec.Info.Width, codec.Info.Height);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return null;
        }
    }

    private static (int W, int H)? Cells(int width, int height)
    {
        var w = (int)Math.Round((width - 1) / (double)BaseCell);
        var h = (int)Math.Round((height - 1) / (double)BaseCell);
        return w < 1 || h < 1 || width != w * BaseCell + 1 || height != h * BaseCell + 1 ? null : (w, h);
    }

    /// <summary>The template from the image's bytes (PNG or WebP), or null when they aren't a grid image of whole cells.</summary>
    public static StashTemplate? Load(byte[] bytes)
    {
        using var bitmap = StashPicture.Decode(bytes);
        if (bitmap is null || Cells(bitmap.Width, bitmap.Height) is not var (w, h))
            return null;
        var variants = new List<Variant>();
        foreach (var scale in Scales)
        {
            using var scaled = scale == 1.0 ? null : Scaled(bitmap, scale);
            var raw = StashGray.Of(scaled ?? bitmap);
            var mask = Foreground(raw, ArtMask(raw.W, raw.H));
            variants.Add(new Variant(mask, Normalized(raw.Blur3(), mask)));
        }
        return new StashTemplate(w, h, variants);
    }

    /// <summary>
    /// How well the item's cells in the picture agree with the image, −1 to 1: the best correlation over the sizes
    /// and shifts. The rectangle must be the template's size in cells.
    /// </summary>
    public float Agreement(SKBitmap picture, SKRectI rect) => Agreement(Block(picture, rect, W, H));

    /// <summary>The item's cells in the picture, resampled to the images' size and shifted each way, ready to be
    /// compared with every template of that size.</summary>
    public static List<float[]> Block(SKBitmap picture, SKRectI rect, int w, int h)
    {
        using var block = StashPicture.Block(picture, rect, w * BaseCell + 1, h * BaseCell + 1);
        var gray = StashGray.Of(block).Blur3();
        var shifted = new List<float[]>();
        for (var dy = -Shift; dy <= Shift; dy++)
        {
            for (var dx = -Shift; dx <= Shift; dx++)
                shifted.Add(gray.Shifted(dx, dy).P);
        }
        return shifted;
    }

    public float Agreement(List<float[]> block) => _variants.Max(v => block.Max(s => Correlation(s, v.Art, v.Mask)));

    // The image drawn larger about its middle, the edges falling off the picture.
    private static SKBitmap Scaled(SKBitmap template, double scale)
    {
        var outp = new SKBitmap(new SKImageInfo(template.Width, template.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(outp);
        canvas.Clear(template.GetPixel(3, template.Height / 2));
        canvas.Translate(template.Width / 2f, template.Height / 2f);
        canvas.Scale((float)scale);
        canvas.Translate(-template.Width / 2f, -template.Height / 2f);
        canvas.DrawBitmap(template, 0, 0, StashPicture.Sampling);
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

    // Where the image shows its item rather than the cell's field: pixels that differ from the field's shade (the
    // middle value of the compared area, most of which is field), and their neighbours. The game and tarkov.dev both
    // hatch the field finely, and that shared texture would otherwise score every image alike.
    private static bool[] Foreground(StashGray raw, bool[] art)
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

    // The masked pixels, mean removed and scaled to unit length.
    private static float[] Normalized(StashGray g, bool[] mask)
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

    // The normalized cross-correlation of a picture with an image already normalized over the mask: the picture's
    // mean over the mask taken out, scaled by its spread there.
    private static float Correlation(float[] picture, float[] template, bool[] mask)
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
}
