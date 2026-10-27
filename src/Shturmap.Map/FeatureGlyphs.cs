using SkiaSharp;

namespace Shturmap.Map;

/// <summary>A feature's picture on What's New's card, where no map symbol says it (owner, 2026-10-27, D4 B).</summary>
public enum FeatureGlyph
{
    /// <summary>The stash desk: a few of its cells, the keep notch in one corner, their figures.</summary>
    Desk,

    /// <summary>Dailies and weeklies: a stopwatch in a frame, as the operational glyphs are framed.</summary>
    Tasks,

    /// <summary>Text size: a small and a large "A".</summary>
    TextSize,

    /// <summary>The in-raid glance: its plate, a band between two amber hairlines, its total and its cells.</summary>
    Glance,

    /// <summary>The squad grid: its lines and the player's dot in a square.</summary>
    Grid,
}

/// <summary>
/// What's New's pictures for 0.7.0's features (owner, 2026-10-27, D4 B: "each feature should have a icon and a appropriate
/// picture or even animation"; docs/DESIGN.md §4, *What's New*): drawn by the app in the design system's colours, at the
/// card's 56×34 picture place, never a bundled image (§3). And the text size preview's plate: the same lines drawn at each
/// step's size, side by side, so the window's own text size never changes under the pointer.
/// </summary>
public static class FeatureGlyphs
{
    /// <summary>The card's picture place, in DIP.</summary>
    public const float Width = 56, Height = 34;

    /// <summary>Draws one picture into a new bitmap, <paramref name="scale"/> device pixels per DIP, on a transparent ground.</summary>
    public static SKBitmap Draw(FeatureGlyph glyph, float scale)
    {
        var bitmap = new SKBitmap(new SKImageInfo((int)Math.Ceiling(Width * scale), (int)Math.Ceiling(Height * scale), SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(scale);
        MapRenderer.DrawFeatureGlyph(canvas, glyph);
        return bitmap;
    }

    /// <summary>One step of the text size preview: its caption ("NORMAL") and the sizes its lines are drawn at, in DIP.</summary>
    public sealed record TextStepSample(string Caption, float Name, float Note, float Label);

    /// <summary>
    /// The text size preview's picture: for each step a panel with a quest's name, a muted line and a map label, each at the
    /// step's size, its caption under it, the panels side by side and top-aligned, so the larger text reads as larger.
    /// Words come from the caller (the data's names, the texts files).
    /// </summary>
    public static SKBitmap TextSizes(IReadOnlyList<TextStepSample> steps, string name, string note, string label, float scale) =>
        MapRenderer.DrawTextSizes(steps, name, note, label, scale);
}

public static partial class MapRenderer
{
    // A paint for a hairline or a stroke, and one for a fill, in a design system colour.
    private static SKPaint StrokeOf(SKColor color, float width, SKStrokeCap cap = SKStrokeCap.Butt) =>
        new() { Color = color, Style = SKPaintStyle.Stroke, StrokeWidth = width, StrokeCap = cap, IsAntialias = true };

    private static SKPaint FillOf(SKColor color) => new() { Color = color, Style = SKPaintStyle.Fill, IsAntialias = true };

    /// <summary>Draws a feature's picture in DIP on a canvas scaled to device pixels (<see cref="FeatureGlyphs.Draw"/>).</summary>
    internal static void DrawFeatureGlyph(SKCanvas canvas, FeatureGlyph glyph)
    {
        var lineStrong = Palette.Sk(Palette.LineStrong);
        var cell = Palette.Sk(Palette.Cell);
        switch (glyph)
        {
            case FeatureGlyph.Desk:
            {
                // Five cells of the desk's page, one of them two wide; the keep notch in the first's corner, the figures'
                // bands in amber, as the desk draws its prices.
                using var fill = FillOf(cell);
                using var edge = StrokeOf(lineStrong, 1);
                SKRect[] cells = [SKRect.Create(11.5f, 5.5f, 11, 11), SKRect.Create(22.5f, 5.5f, 11, 11), SKRect.Create(33.5f, 5.5f, 11, 11),
                    SKRect.Create(11.5f, 16.5f, 11, 11), SKRect.Create(22.5f, 16.5f, 22, 11)];
                foreach (var r in cells)
                {
                    canvas.DrawRect(r, fill);
                    canvas.DrawRect(r, edge);
                }
                using var notch = new SKPath();
                notch.MoveTo(12, 6);
                notch.LineTo(17, 6);
                notch.LineTo(12, 11);
                notch.Close();
                using var amber = FillOf(Amber);
                canvas.DrawPath(notch, amber);
                using var figure = StrokeOf(Amber, 1.6f);
                canvas.DrawLine(27, 8.5f, 32, 8.5f, figure);
                canvas.DrawLine(38, 8.5f, 43, 8.5f, figure);
                canvas.DrawLine(36, 19.5f, 43, 19.5f, figure);
                break;
            }
            case FeatureGlyph.Tasks:
            {
                // A stopwatch in a frame, as the operational tasks' glyphs stand framed on NEXT RAID's rows.
                using var frame = StrokeOf(lineStrong, 1);
                canvas.DrawRect(SKRect.Create(15.5f, 3.5f, 27, 27), frame);
                using var watch = StrokeOf(Amber, 1.6f, SKStrokeCap.Square);
                canvas.DrawCircle(29, 18.5f, 7.5f, watch);
                canvas.DrawLine(29, 18.5f, 29, 14, watch);
                canvas.DrawLine(26.5f, 7.5f, 31.5f, 7.5f, watch);
                canvas.DrawLine(29, 7.5f, 29, 11, watch);
                break;
            }
            case FeatureGlyph.TextSize:
            {
                // A small "A" and a large one in the app's type, a caret between them pointing up.
                using var ink = FillOf(Ink);
                using var small = new SKFont(TypefaceBold, 11);
                using var large = new SKFont(TypefaceBold, 27);
                canvas.DrawText("A", 12, 27, SKTextAlign.Left, small, ink);
                canvas.DrawText("A", 23, 29, SKTextAlign.Left, large, ink);
                using var caret = StrokeOf(Muted, 1.2f);
                using var path = new SKPath();
                path.MoveTo(19.5f, 9);
                path.LineTo(22.5f, 6);
                path.LineTo(25.5f, 9);
                canvas.DrawPath(path, caret);
                break;
            }
            case FeatureGlyph.Glance:
            {
                // The glance's plate: the ground at 90 % between two amber hairlines, its title (the word in ink, the total
                // in amber) and three of its cells with their figures.
                using var band = FillOf(Palette.Sk(Palette.Ground).WithAlpha(230));
                canvas.DrawRect(SKRect.Create(4, 5, 48, 24), band);
                using var hairline = StrokeOf(Amber, 1);
                canvas.DrawLine(4, 5.5f, 52, 5.5f, hairline);
                canvas.DrawLine(4, 28.5f, 52, 28.5f, hairline);
                using var word = StrokeOf(Ink, 2);
                canvas.DrawLine(8, 10.5f, 15, 10.5f, word);
                using var total = StrokeOf(Amber, 2);
                canvas.DrawLine(17, 10.5f, 27, 10.5f, total);
                using var fill = FillOf(cell);
                using var edge = StrokeOf(lineStrong, 1);
                for (var i = 0; i < 4; i++)
                {
                    var r = SKRect.Create(8.5f + i * 10, 14.5f, 10, 10);
                    canvas.DrawRect(r, fill);
                    canvas.DrawRect(r, edge);
                }
                using var figure = StrokeOf(Amber, 1.4f);
                canvas.DrawLine(11, 16.5f, 16, 16.5f, figure);
                canvas.DrawLine(31, 16.5f, 36, 16.5f, figure);
                break;
            }
            case FeatureGlyph.Grid:
            {
                // The grid's lines and the player's dot in a square, sand on a ground rim as the map draws the player.
                using var line = StrokeOf(lineStrong, 1);
                foreach (var x in new[] { 8.5f, 18.5f, 28.5f, 38.5f, 48.5f })
                    canvas.DrawLine(x, 2, x, 32, line);
                foreach (var y in new[] { 7.5f, 17.5f, 27.5f })
                    canvas.DrawLine(6, y, 51, y, line);
                using var rim = FillOf(Palette.Sk(Palette.Ground));
                using var body = FillOf(Player);
                canvas.DrawCircle(33.5f, 22.5f, 3.6f, rim);
                canvas.DrawCircle(33.5f, 22.5f, 2.4f, body);
                break;
            }
        }
    }

    /// <summary>The text size preview's picture (<see cref="FeatureGlyphs.TextSizes"/>).</summary>
    internal static SKBitmap DrawTextSizes(IReadOnlyList<FeatureGlyphs.TextStepSample> steps, string name, string note, string label, float scale)
    {
        const float pad = 12, gap = 16, captionGap = 8, caption = 10;
        var fonts = steps.Select(s => (Name: new SKFont(TypefaceBold, s.Name), Note: new SKFont(Typeface, s.Note), Label: new SKFont(TypefaceBold, s.Label))).ToList();
        using var captionFont = new SKFont(TypefaceBold, caption);
        try
        {
            // Each panel as wide as its widest line, as high as its lines; the panels top-aligned, their captions under them.
            var panels = fonts.Select(f =>
            {
                var width = new[] { f.Name.MeasureText(name), f.Note.MeasureText(note), f.Label.MeasureText(label) }.Max() + 2 * pad;
                var height = pad + f.Name.Size * 1.25f + 4 + f.Note.Size * 1.3f + 10 + f.Label.Size * 1.25f + pad;
                return new SKSize(width, height);
            }).ToList();
            var tallest = panels.Max(p => p.Height);
            var widthAll = panels.Sum(p => p.Width) + gap * Math.Max(0, panels.Count - 1);
            var heightAll = tallest + captionGap + caption * 1.3f;
            var bitmap = new SKBitmap(new SKImageInfo((int)Math.Ceiling(widthAll * scale), (int)Math.Ceiling(heightAll * scale), SKColorType.Rgba8888, SKAlphaType.Premul));
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(scale);
            using var ground = FillOf(Palette.Sk(Palette.Panel));
            using var edge = StrokeOf(Palette.Sk(Palette.LineStrong), 1);
            using var ink = FillOf(Ink);
            using var muted = FillOf(Muted);
            using var green = FillOf(Green);
            using var halo = new SKPaint { Color = Palette.Sk(Palette.Ground), Style = SKPaintStyle.Stroke, StrokeJoin = SKStrokeJoin.Round, IsAntialias = true };
            var x = 0f;
            for (var i = 0; i < panels.Count; i++)
            {
                var (size, font) = (panels[i], fonts[i]);
                var box = SKRect.Create(x + 0.5f, 0.5f, size.Width - 1, size.Height - 1);
                canvas.DrawRect(box, ground);
                canvas.DrawRect(box, edge);
                var y = pad + font.Name.Size;
                canvas.DrawText(name, x + pad, y, SKTextAlign.Left, font.Name, ink);
                y += font.Name.Size * 0.25f + 4 + font.Note.Size;
                canvas.DrawText(note, x + pad, y, SKTextAlign.Left, font.Note, muted);
                y += font.Note.Size * 0.3f + 10 + font.Label.Size;
                // A map label as the map writes it: on a halo of the ground, so it reads over the artwork.
                halo.StrokeWidth = Math.Max(2, font.Label.Size * 0.28f);
                canvas.DrawText(label, x + pad, y, SKTextAlign.Left, font.Label, halo);
                canvas.DrawText(label, x + pad, y, SKTextAlign.Left, font.Label, green);
                canvas.DrawText(steps[i].Caption, x, tallest + captionGap + caption, SKTextAlign.Left, captionFont, muted);
                x += size.Width + gap;
            }
            return bitmap;
        }
        finally
        {
            foreach (var f in fonts)
            {
                f.Name.Dispose();
                f.Note.Dispose();
                f.Label.Dispose();
            }
        }
    }
}
