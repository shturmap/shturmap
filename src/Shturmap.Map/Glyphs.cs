using SkiaSharp;
using Shturmap.Core.Quests;

namespace Shturmap.Map;

/// <summary>
/// The glyphs for quest types and requirements (docs/DESIGN.md §5). They come from Segoe Fluent Icons, which ships
/// with Windows and is used in place; the crosshair is Shturmap's own path because the font has none. Where the game's
/// Tasks screen has a type icon, the glyph echoes it (magnifier for Exploration, pointing hand for Pickup).
/// </summary>
public static class Glyphs
{
    public const string FontFamily = "Segoe Fluent Icons";

    /// <summary>A crosshair in a 16×16 box: ring, four ticks, centre dot (nonzero fill).</summary>
    public const string CrosshairPath =
        "M8,1.5 A6.5,6.5 0 1 1 8,14.5 A6.5,6.5 0 1 1 8,1.5 Z M8,3 A5,5 0 1 0 8,13 A5,5 0 1 0 8,3 Z " +
        "M7.25,0 H8.75 V5 H7.25 Z M7.25,11 H8.75 V16 H7.25 Z M0,7.25 H5 V8.75 H0 Z M11,7.25 H16 V8.75 H11 Z " +
        "M8,6.9 A1.1,1.1 0 1 1 8,9.1 A1.1,1.1 0 1 1 8,6.9 Z";

    public static readonly string Key = Code(0xE8D7);

    public static readonly string Bring = Code(0xE821);

    /// <summary>A locked door or trunk on the map: a padlock (the key glyph is the key itself).</summary>
    public static readonly string Lock = Code(0xE72E);

    /// <summary>A switch on the map: the power symbol.</summary>
    public static readonly string Switch = Code(0xE7E8);

    /// <summary>The font character for a type, or null for Elimination, which is drawn from <see cref="CrosshairPath"/>.</summary>
    public static string? Character(ObjectiveKind kind) => kind switch
    {
        ObjectiveKind.Elimination => null,
        ObjectiveKind.Exploration => Code(0xE721), // magnifier
        ObjectiveKind.Pickup => Code(0xE7C9),      // pointing hand
        ObjectiveKind.Place => Code(0xE840),       // pin
        ObjectiveKind.FindInRaid => Code(0xE719),  // bag
        ObjectiveKind.Survive => Code(0xE726),     // runner
        _ => Code(0xE716),                         // people (trader)
    };

    private static string Code(int codePoint) => char.ConvertFromUtf32(codePoint);

    private static readonly Lazy<SKTypeface> Typeface = new(() => SKTypeface.FromFamilyName(FontFamily) ?? SKTypeface.Default);
    private static readonly Lazy<SKPath> Crosshair = new(() => SKPath.ParseSvgPathData(CrosshairPath));

    /// <summary>Draws a font glyph centred on a point, about <paramref name="size"/> pixels tall.</summary>
    public static void Draw(SKCanvas canvas, string glyph, SKPoint center, float size, SKColor color)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        using var font = new SKFont(Typeface.Value, size);
        font.MeasureText(glyph, out var bounds, paint);
        canvas.DrawText(glyph, center.X - bounds.MidX, center.Y - bounds.MidY, SKTextAlign.Left, font, paint);
    }

    /// <summary>Draws a type glyph centred on a point, about <paramref name="size"/> pixels tall.</summary>
    public static void Draw(SKCanvas canvas, ObjectiveKind kind, SKPoint center, float size, SKColor color)
    {
        if (Character(kind) is { } glyph)
        {
            Draw(canvas, glyph, center, size, color);
            return;
        }
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        canvas.Save();
        canvas.Translate(center.X - size / 2f, center.Y - size / 2f);
        canvas.Scale(size / 16f);
        canvas.DrawPath(Crosshair.Value, paint);
        canvas.Restore();
    }
}
