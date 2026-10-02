#:package SkiaSharp
// Builds Shturmap's logo set from code: brand\*.svg and *.png, and src\Shturmap.App\Assets\Shturmap.ico.
// Run from anywhere in the repository: .\eng\dotnet.ps1 run brand\build.cs
// Construction: the three stems stay joined to the base, with no bar across the top; the chevron is cut through
// the centre stem only, pointing up; the wordmark's M stays whole, with stencil bridges only in A, P and R.
// Every shape ends as a plain filled path (Skia path operations), so the SVGs need no masks, text or fonts.
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

var root = new DirectoryInfo(Environment.CurrentDirectory);
while (!File.Exists(Path.Combine(root.FullName, "Shturmap.slnx")))
    root = root.Parent ?? throw new InvalidOperationException("Run this inside the Shturmap repository.");
var brand = Path.Combine(root.FullName, "brand");

var icon = Logo.Icon();
var wordDark = Logo.Wordmark(Logo.Dark);
var wordLight = Logo.Wordmark(Logo.Light);
var logoDark = Logo.Lockup(icon, wordDark);
var logoLight = Logo.Lockup(icon, wordLight);

File.WriteAllText(Path.Combine(brand, "icon.svg"), Output.Svg(icon));
File.WriteAllText(Path.Combine(brand, "icon-small.svg"), Output.Svg(Logo.Small(32)));
File.WriteAllText(Path.Combine(brand, "logo-dark.svg"), Output.Svg(logoDark));
File.WriteAllText(Path.Combine(brand, "logo-light.svg"), Output.Svg(logoLight));
File.WriteAllText(Path.Combine(brand, "wordmark-dark.svg"), Output.Svg(wordDark));
File.WriteAllText(Path.Combine(brand, "wordmark-light.svg"), Output.Svg(wordLight));
File.WriteAllBytes(Path.Combine(brand, "icon-512.png"), Output.Png(icon, 512, 512));
File.WriteAllBytes(Path.Combine(brand, "icon-1024.png"), Output.Png(icon, 1024, 1024));
// The README logo is shown 40 px tall; the PNG fallbacks are twice that.
File.WriteAllBytes(Path.Combine(brand, "logo-dark.png"), Output.Png(logoDark, (int)Math.Round(logoDark.W / logoDark.H * 80), 80));
File.WriteAllBytes(Path.Combine(brand, "logo-light.png"), Output.Png(logoLight, (int)Math.Round(logoLight.W / logoLight.H * 80), 80));
File.WriteAllBytes(Path.Combine(brand, "social-preview.png"), Output.Png(Logo.Social(logoDark), 1280, 640));
// Up to 40 px the pixel-fitted plain letter; from 48 px the detailed icon, whose chevron reads from there on.
Output.Ico(Path.Combine(root.FullName, "src", "Shturmap.App", "Assets", "Shturmap.ico"),
    [16, 20, 24, 32, 40, 48, 64, 96, 128, 256], n => n <= 40 ? Logo.Small(n) : icon);
Console.WriteLine($"Built the logo set in {brand}");

record Layer(string Fill, SKPath Path);

record Drawing(double W, double H, List<Layer> Layers);

record Inks(string Ink, string Amber);

static class Logo
{
    const string Plate = "#151614", Border = "#45463F", Marks = "#4A4A41", Ground = "#0B0C0B", GridLine = "#171815";

    public static readonly Inks Dark = new("#D9D5C4", "#C9AD62");
    public static readonly Inks Light = new("#1E1F1B", "#8C7436");

    // 256 units: the plate and its border, quiet crop marks, and the Ш with a chevron cut through the centre stem.
    public static Drawing Icon()
    {
        var plate = Geo.Rect(12, 12, 232, 232);
        var border = Geo.Op(Geo.Rect(12, 12, 232, 232), Geo.Rect(16, 16, 224, 224), SKPathOp.Difference);
        var marks = Geo.Union(new (double, double)[][]
        {
            [(28, 44), (28, 28), (44, 28)], [(212, 28), (228, 28), (228, 44)],
            [(28, 212), (28, 228), (44, 228)], [(212, 228), (228, 228), (228, 212)],
        }.Select(l => Geo.Stroke(l, 3)));
        var sha = Geo.Poly(50, 49, 84, 49, 84, 180, 111, 180, 111, 49, 145, 49, 145, 180, 172, 180, 172, 49, 206, 49, 206, 208, 50, 208);
        // The chevron runs a unit past the stem's sides so no sliver of amber is left at its edges.
        var chevron = Geo.Poly(128, 84, 146, 102, 146, 113, 128, 95, 110, 113, 110, 102);
        return new(256, 256, [new(Plate, plate), new(Border, border), new(Marks, marks), new(Dark.Amber, Geo.Op(sha, chevron, SKPathOp.Difference))]);
    }

    // The plain Ш on an n-pixel grid, every edge on a whole pixel: stems, gaps and base one unit wide, the letter a
    // five-unit square, centred (the left margin takes the odd pixel).
    public static Drawing Small(int n)
    {
        int u = Math.Max(2, (int)Math.Round(n / 8.0, MidpointRounding.AwayFromZero));
        int b = Math.Max(1, (int)Math.Round(n / 16.0, MidpointRounding.AwayFromZero));
        int box = 5 * u, x0 = (n - box) / 2, y0 = (n - box) / 2;
        var border = Geo.Op(Geo.Rect(0, 0, n, n), Geo.Rect(b, b, n - 2 * b, n - 2 * b), SKPathOp.Difference);
        var sha = Geo.Union([
            Geo.Rect(x0, y0, u, box), Geo.Rect(x0 + 2 * u, y0, u, box), Geo.Rect(x0 + 4 * u, y0, u, box),
            Geo.Rect(x0, y0 + 4 * u, box, u),
        ]);
        return new(n, n, [new(Plate, Geo.Rect(0, 0, n, n)), new(Border, border), new(Dark.Amber, sha)]);
    }

    // SHTUR in ink, then MAP cut out of an amber strip a little taller than the letters. Cap height is 100 units.
    public static Drawing Wordmark(Inks k)
    {
        const double wt = 17, track = 12, padV = 8, padH = 12, gap = 14;
        var shtur = Type.Word("SHTUR", wt, track, 0);
        double stripX = shtur.Width + gap;
        var map = Type.Word("MAP", wt, track, stripX + padH);
        double stripW = map.Width + 2 * padH;
        var strip = Geo.Op(Geo.Rect(stripX, -padV, stripW, 100 + 2 * padV), map.Shape, SKPathOp.Difference);
        return new(stripX + stripW, 100 + 2 * padV,
            [new(k.Ink, Geo.Moved(shtur.Shape, 1, 0, padV)), new(k.Amber, Geo.Moved(strip, 1, 0, padV))]);
    }

    // The 256-unit icon drawn 150 units tall, then the wordmark, all centred on one axis. With the plate's own 7-unit
    // margin, the 26-unit gap leaves a third of the cap height between the plate and the letters.
    public static Drawing Lockup(Drawing icon, Drawing word)
    {
        const double M = 150, gap = 26;
        double h = Math.Max(M, word.H + 8);
        var layers = icon.Layers.Select(l => new Layer(l.Fill, Geo.Moved(l.Path, M / 256, 0, (h - M) / 2)))
            .Concat(word.Layers.Select(l => new Layer(l.Fill, Geo.Moved(l.Path, 1, M + gap, (h - word.H) / 2))));
        return new(M + gap + word.W, h, layers.ToList());
    }

    // GitHub's social preview, 1280×640: the logo centred on a map sheet (a faint 64-px grid and the icon's crop
    // marks), inside the 1.91:1 area that sites cropping the card still show.
    public static Drawing Social(Drawing logo)
    {
        const double W = 1280, H = 640, cell = 64, width = 800;
        var grid = Geo.Union(Enumerable.Range(1, (int)(W / cell) - 1).Select(i => Geo.Rect(i * cell, 0, 1, H))
            .Concat(Enumerable.Range(1, (int)(H / cell) - 1).Select(i => Geo.Rect(0, i * cell, W, 1))));
        var marks = Geo.Union(new (double, double)[][]
        {
            [(64, 112), (64, 64), (112, 64)], [(1168, 64), (1216, 64), (1216, 112)],
            [(64, 528), (64, 576), (112, 576)], [(1168, 576), (1216, 576), (1216, 528)],
        }.Select(l => Geo.Stroke(l, 4)));
        double s = width / logo.W;
        var layers = new List<Layer> { new(Ground, Geo.Rect(0, 0, W, H)), new(GridLine, grid), new(Marks, marks) };
        layers.AddRange(logo.Layers.Select(l => new Layer(l.Fill, Geo.Moved(l.Path, s, (W - width) / 2, (H - logo.H * s) / 2))));
        return new(W, H, layers);
    }
}

static class Type
{
    public record Set(SKPath Shape, double Width);

    // Letters set from x0 with tracking and kerning, unioned into one shape.
    public static Set Word(string text, double wt, double track, double x0)
    {
        var shapes = new List<SKPath>();
        double x = x0;
        for (int i = 0; i < text.Length; i++)
        {
            var (shape, w) = Letter(text[i], wt);
            shapes.Add(Geo.Moved(shape, 1, x, 0));
            x += w + track + (i + 1 < text.Length ? Kern(text[i], text[i + 1], wt) : 0);
        }
        return new(Geo.Union(shapes), x - track - x0);
    }

    // T's bar leaves open space under it; pull its neighbours in.
    static double Kern(char a, char b, double wt) => a == 'T' || b == 'T' ? -(4 + wt * 0.2) : 0;

    // Stroked centrelines on a 100-unit cap height, 45-degree chamfers, clipped to the cap height; the closed shapes
    // of A, P and R get one stencil bridge each.
    static (SKPath Shape, double Width) Letter(char c, double wt)
    {
        double W = c == 'M' ? 62 + wt * 0.8 : 46 + wt * 0.8;
        double h = wt / 2, k = 7 + wt * 0.5;
        double x0 = h, x1 = W - h, y0 = h, y1 = 100 - h, ym = 50;
        double g = Math.Max(2.5, wt * 0.22);
        var lines = new List<(double, double)[]>();
        var bridges = new List<SKPath>();
        void L(params double[] xy) => lines.Add(Enumerable.Range(0, xy.Length / 2).Select(i => (xy[2 * i], xy[2 * i + 1])).ToArray());
        switch (c)
        {
            case 'S':
                L(W, y0, x0 + k, y0, x0, y0 + k, x0, ym - k, x0 + k, ym, x1 - k, ym, x1, ym + k, x1, y1 - k, x1 - k, y1, 0, y1);
                break;
            case 'H':
                L(x0, 0, x0, 100); L(x1, 0, x1, 100); L(x0, ym, x1, ym);
                break;
            case 'T':
                L(0, y0, W, y0); L(W / 2, y0, W / 2, 100);
                break;
            case 'U':
                // A smaller chamfer keeps the counter from closing into a V.
                double ku = k * 0.7;
                L(x0, 0, x0, y1 - ku, x0 + ku, y1, x1 - ku, y1, x1, y1 - ku, x1, 0);
                break;
            case 'R':
                double rm = ym + 2, lx = W * 0.5;
                L(x0, 100, x0, y0, x1 - k, y0, x1, y0 + k, x1, rm - k, x1 - k, rm, x0, rm);
                L(lx, rm, x1, rm + (x1 - lx), x1, 100);
                bridges.Add(Geo.Rect(wt, -1, g, wt + 2));
                break;
            case 'M':
                L(x0, 0, x0, 100); L(x1, 0, x1, 100); L(x0, y0, W / 2, 64, x1, y0);
                break;
            case 'A':
                L(x0, 100, x0, y0 + k, x0 + k, y0, x1 - k, y0, x1, y0 + k, x1, 100); L(x0, 60, x1, 60);
                bridges.Add(Geo.Rect(W / 2 - g / 2, -1, g, wt + 2));
                break;
            case 'P':
                double pm = ym + 4;
                L(x0, 100, x0, y0, x1 - k, y0, x1, y0 + k, x1, pm - k, x1 - k, pm, x0, pm);
                bridges.Add(Geo.Rect(wt, -1, g, wt + 2));
                break;
            default:
                throw new ArgumentException($"No letter shape for '{c}'.");
        }
        var shape = Geo.Op(Geo.Union(lines.Select(l => Geo.Stroke(l, wt))), Geo.Rect(-2, 0, W + 4, 100), SKPathOp.Intersect);
        foreach (var bridge in bridges)
            shape = Geo.Op(shape, bridge, SKPathOp.Difference);
        return (shape, W);
    }
}

static class Geo
{
    public static SKPath Rect(double x, double y, double w, double h)
    {
        using var b = new SKPathBuilder();
        b.AddRect(SKRect.Create((float)x, (float)y, (float)w, (float)h), SKPathDirection.Clockwise);
        return b.Detach();
    }

    public static SKPath Poly(params double[] xy)
    {
        using var b = new SKPathBuilder();
        b.MoveTo((float)xy[0], (float)xy[1]);
        for (int i = 2; i < xy.Length; i += 2) b.LineTo((float)xy[i], (float)xy[i + 1]);
        b.Close();
        return b.Detach();
    }

    public static SKPath Op(SKPath a, SKPath b, SKPathOp op) => a.Op(b, op) ?? throw new InvalidOperationException("Path operation failed.");

    public static SKPath Union(IEnumerable<SKPath> paths) => paths.Aggregate(new SKPath(), (acc, p) => Op(acc, p, SKPathOp.Union));

    // The outline of a stroked open polyline: miter joins (limit 4, as in SVG), butt caps.
    public static SKPath Stroke(IEnumerable<(double X, double Y)> points, double width)
    {
        using var b = new SKPathBuilder();
        var (x0, y0) = points.First();
        b.MoveTo((float)x0, (float)y0);
        foreach (var (x, y) in points.Skip(1)) b.LineTo((float)x, (float)y);
        using var line = b.Detach();
        using var paint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = (float)width, StrokeJoin = SKStrokeJoin.Miter, StrokeMiter = 4, StrokeCap = SKStrokeCap.Butt };
        return Op(paint.GetFillPath(line) ?? throw new InvalidOperationException("Stroke failed."), new SKPath(), SKPathOp.Union);
    }

    public static SKPath Moved(SKPath p, double scale, double dx, double dy)
    {
        var c = new SKPath(p);
        c.Transform(SKMatrix.CreateScaleTranslation((float)scale, (float)scale, (float)dx, (float)dy));
        return c;
    }
}

static class Output
{
    static string F(double v) => Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);

    // Plain filled paths over a viewBox, coordinates rounded to two decimals.
    public static string Svg(Drawing d)
    {
        var sb = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {F(d.W)} {F(d.H)}\">");
        foreach (var l in d.Layers)
        {
            var data = Regex.Replace(l.Path.ToSvgPathData(), @"-?\d+\.\d+", m => F(double.Parse(m.Value, CultureInfo.InvariantCulture)));
            var rule = l.Path.FillType is SKPathFillType.EvenOdd or SKPathFillType.InverseEvenOdd ? " fill-rule=\"evenodd\"" : "";
            sb.Append($"<path fill=\"{l.Fill}\"{rule} d=\"{data}\"/>");
        }
        return sb.Append("</svg>").ToString();
    }

    public static byte[] Png(Drawing d, int w, int h)
    {
        using var bmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var c = new SKCanvas(bmp))
        {
            c.Clear(SKColors.Transparent);
            c.Scale((float)(w / d.W), (float)(h / d.H));
            foreach (var l in d.Layers)
            {
                using var paint = new SKPaint { Color = SKColor.Parse(l.Fill), IsAntialias = true, Style = SKPaintStyle.Fill };
                c.DrawPath(l.Path, paint);
            }
        }
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    // A Windows icon with PNG entries: a 6-byte header, a 16-byte directory entry per size, then the images.
    public static void Ico(string path, int[] sizes, Func<int, Drawing> drawingFor)
    {
        var images = sizes.Select(n => (n, Png(drawingFor(n), n, n))).ToList();
        using var w = new BinaryWriter(File.Create(path));
        w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)images.Count);
        int offset = 6 + 16 * images.Count;
        foreach (var (n, png) in images)
        {
            w.Write((byte)(n >= 256 ? 0 : n)); w.Write((byte)(n >= 256 ? 0 : n));
            w.Write((byte)0); w.Write((byte)0);
            w.Write((ushort)1); w.Write((ushort)32);
            w.Write((uint)png.Length); w.Write((uint)offset);
            offset += png.Length;
        }
        foreach (var (_, png) in images) w.Write(png);
    }
}
