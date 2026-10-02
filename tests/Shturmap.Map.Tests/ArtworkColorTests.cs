using SkiaSharp;

namespace Shturmap.Map.Tests;

// Nothing in the artwork may look like the quest amber (cartography review, 2026-10-02: no pixel within ΔE 15).
public class ArtworkColorTests
{
    private static SKColor Through(SKColorFilter filter, SKColor color)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(2, 2, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Black);
            using var paint = new SKPaint { Color = color, ColorFilter = filter };
            canvas.DrawRect(0, 0, 2, 2, paint);
        }
        return bitmap.GetPixel(1, 1);
    }

    private static readonly SKColorFilter PlainRecede = SKColorFilter.CreateColorMatrix(Matrix(0.38f, 0.85f));

    private static float[] Matrix(float s, float b)
    {
        float lr = 0.2126f, lg = 0.7152f, lb = 0.0722f;
        return
        [
            b * (lr + (1 - lr) * s), b * lg * (1 - s), b * lb * (1 - s), 0, 0,
            b * lr * (1 - s), b * (lg + (1 - lg) * s), b * lb * (1 - s), 0, 0,
            b * lr * (1 - s), b * lg * (1 - s), b * (lb + (1 - lb) * s), 0, 0,
            0, 0, 0, 1, 0,
        ];
    }

    [Theory]
    [InlineData("#c9ad62")] // the quest amber itself
    [InlineData("#f2d04b")] // saturated yellows, as in Factory's blocks and Customs' dashed lines
    [InlineData("#e6c33c")]
    [InlineData("#ffd700")]
    [InlineData("#d9b85c")]
    [InlineData("#f0c060")]
    [InlineData("#e8d27a")]
    public void Amber_like_colours_end_far_from_the_amber(string source)
    {
        var amber = ArtworkColors.Lab(MapRenderer.QuestAmber);
        var before = DeltaE(ArtworkColors.Lab(Through(PlainRecede, SKColor.Parse(source))), amber);
        var after = DeltaE(ArtworkColors.Lab(Through(ArtworkColors.Filter, SKColor.Parse(source))), amber);
        Assert.True(after >= 15, $"{source}: ΔE {before:0.0} before, {after:0.0} after");
    }

    [Theory]
    [InlineData("#c0392b")] // red
    [InlineData("#3a6ea5")] // blue
    [InlineData("#808080")] // grey
    [InlineData("#5a4632")] // a dark brown, much darker than the amber
    [InlineData("#4f7a3a")] // green
    public void Other_colours_only_recede(string source)
    {
        var plain = Through(PlainRecede, SKColor.Parse(source));
        var filtered = Through(ArtworkColors.Filter, SKColor.Parse(source));
        Assert.True(Math.Abs(plain.Red - filtered.Red) <= 1 && Math.Abs(plain.Green - filtered.Green) <= 1 && Math.Abs(plain.Blue - filtered.Blue) <= 1,
            $"{source}: {plain} plain, {filtered} filtered");
    }

    // CIEDE2000, as the review measured it.
    private static double DeltaE((double L, double A, double B) p, (double L, double A, double B) q)
    {
        double c1 = Math.Sqrt(p.A * p.A + p.B * p.B), c2 = Math.Sqrt(q.A * q.A + q.B * q.B), cm = (c1 + c2) / 2;
        double g = 0.5 * (1 - Math.Sqrt(Math.Pow(cm, 7) / (Math.Pow(cm, 7) + Math.Pow(25, 7))));
        double a1 = (1 + g) * p.A, a2 = (1 + g) * q.A;
        double c1p = Math.Sqrt(a1 * a1 + p.B * p.B), c2p = Math.Sqrt(a2 * a2 + q.B * q.B);
        double h1 = Math.Atan2(p.B, a1) * 180 / Math.PI; if (h1 < 0) h1 += 360;
        double h2 = Math.Atan2(q.B, a2) * 180 / Math.PI; if (h2 < 0) h2 += 360;
        double dL = q.L - p.L, dC = c2p - c1p, dh = c1p * c2p == 0 ? 0 : (Math.Abs(h2 - h1) <= 180 ? h2 - h1 : h2 - h1 > 180 ? h2 - h1 - 360 : h2 - h1 + 360);
        double dH = 2 * Math.Sqrt(c1p * c2p) * Math.Sin(dh * Math.PI / 360);
        double lm = (p.L + q.L) / 2, cpm = (c1p + c2p) / 2;
        double hm = c1p * c2p == 0 ? h1 + h2 : Math.Abs(h1 - h2) <= 180 ? (h1 + h2) / 2 : (h1 + h2 < 360 ? (h1 + h2 + 360) / 2 : (h1 + h2 - 360) / 2);
        double t = 1 - 0.17 * Math.Cos((hm - 30) * Math.PI / 180) + 0.24 * Math.Cos(2 * hm * Math.PI / 180) + 0.32 * Math.Cos((3 * hm + 6) * Math.PI / 180) - 0.20 * Math.Cos((4 * hm - 63) * Math.PI / 180);
        double sl = 1 + 0.015 * Math.Pow(lm - 50, 2) / Math.Sqrt(20 + Math.Pow(lm - 50, 2)), sc = 1 + 0.045 * cpm, sh = 1 + 0.015 * cpm * t;
        double rt = -2 * Math.Sqrt(Math.Pow(cpm, 7) / (Math.Pow(cpm, 7) + Math.Pow(25, 7))) * Math.Sin(60 * Math.Exp(-Math.Pow((hm - 275) / 25, 2)) * Math.PI / 180);
        return Math.Sqrt(Math.Pow(dL / sl, 2) + Math.Pow(dC / sc, 2) + Math.Pow(dH / sh, 2) + rt * (dC / sc) * (dH / sh));
    }
}
