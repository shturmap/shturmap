using System.Collections.Concurrent;
using SkiaSharp;
using Shturmap.Core.Quests;

namespace Shturmap.Map;

/// <summary>
/// The glyphs for quest types and requirements (docs/DESIGN.md §5). A type's icon is a filled shape from Phosphor
/// Icons (MIT, see THIRD-PARTY-NOTICES.md), chosen by the owner from a panel of sets (2026-10-04): thin outlines from
/// Windows' icon font were faint inside a map marker, where the glyph is about 10 px. Each shape is the family's
/// path as published, in its 256 × 256 box, drawn filled and fitted by its own bounds, here on the map and in the
/// lists (KindGlyph). Keys, things to bring, padlocks and switches still come from Segoe Fluent Icons, which ships
/// with Windows and is used in place.
/// </summary>
public static class Glyphs
{
    public const string FontFamily = "Segoe Fluent Icons";

    // Phosphor Icons, weight "fill" (the magnifier: "bold", whose lens is a ring), Copyright (c) 2023 Phosphor Icons.

    /// <summary>Elimination: a crosshair.</summary>
    private const string Crosshair =
        "M232,120h-8.34A96.14,96.14,0,0,0,136,32.34V24a8,8,0,0,0-16,0v8.34A96.14,96.14,0,0,0,32.34,120H24a8,8,0,0,0,0,16h8.34A96.14,96.14,0,0,0,120,223.66V232a8,8,0,0,0,16,0v-8.34A96.14,96.14,0,0,0,223.66,136H232a8,8,0,0,0,0-16Zm-32,16h7.6A80.15,80.15,0,0,1,136,207.6V200a8,8,0,0,0-16,0v7.6A80.15,80.15,0,0,1,48.4,136H56a8,8,0,0,0,0-16H48.4A80.15,80.15,0,0,1,120,48.4V56a8,8,0,0,0,16,0V48.4A80.15,80.15,0,0,1,207.6,120H200a8,8,0,0,0,0,16Zm-32-8a40,40,0,1,1-40-40A40,40,0,0,1,168,128Z";

    /// <summary>Exploration: a magnifier, as on the game's Tasks screen.</summary>
    private const string MagnifyingGlass =
        "M232.49,215.51,185,168a92.12,92.12,0,1,0-17,17l47.53,47.54a12,12,0,0,0,17-17ZM44,112a68,68,0,1,1,68,68A68.07,68.07,0,0,1,44,112Z";

    /// <summary>Pickup: an open hand.</summary>
    private const string Hand =
        "M216,64v90.93c0,46.2-36.85,84.55-83,85.06A83.71,83.71,0,0,1,72.6,215.4C50.79,192.33,26.15,136,26.15,136a16,16,0,0,1,6.53-22.23c7.66-4,17.1-.84,21.4,6.62l21,36.44a6.09,6.09,0,0,0,6,3.09l.12,0A8.19,8.19,0,0,0,88,151.74V48a16,16,0,0,1,16.77-16c8.61.4,15.23,7.82,15.23,16.43V112a8,8,0,0,0,8.53,8,8.17,8.17,0,0,0,7.47-8.25V32a16,16,0,0,1,16.77-16c8.61.4,15.23,7.82,15.23,16.43V120a8,8,0,0,0,8.53,8,8.17,8.17,0,0,0,7.47-8.25V64.45c0-8.61,6.62-16,15.23-16.43A16,16,0,0,1,216,64Z";

    /// <summary>Place: a push pin.</summary>
    private const string PushPin =
        "M235.33,104l-53.47,53.65c4.56,12.67,6.45,33.89-13.19,60A15.93,15.93,0,0,1,157,224c-.38,0-.75,0-1.13,0a16,16,0,0,1-11.32-4.69L96.29,171,53.66,213.66a8,8,0,0,1-11.32-11.32L85,159.71l-48.3-48.3A16,16,0,0,1,38,87.63c25.42-20.51,49.75-16.48,60.4-13.14L152,20.7a16,16,0,0,1,22.63,0l60.69,60.68A16,16,0,0,1,235.33,104Z";

    /// <summary>Find in raid: a box.</summary>
    private const string Package =
        "M223.68,66.15,135.68,18a15.88,15.88,0,0,0-15.36,0l-88,48.17a16,16,0,0,0-8.32,14v95.64a16,16,0,0,0,8.32,14l88,48.17a15.88,15.88,0,0,0,15.36,0l88-48.17a16,16,0,0,0,8.32-14V80.18A16,16,0,0,0,223.68,66.15ZM128,32l80.35,44L178.57,92.29l-80.35-44Zm0,88L47.65,76,81.56,57.43l80.35,44Zm88,55.85h0l-80,43.79V133.83l32-17.51V152a8,8,0,0,0,16,0V107.56l32-17.51v85.76Z";

    /// <summary>Survive: a runner.</summary>
    private const string PersonSimpleRun =
        "M120,56a32,32,0,1,1,32,32A32,32,0,0,1,120,56Zm103.28,74.08a8,8,0,0,0-10.6-4c-.25.12-26.71,10.72-72.18-20.19-52.29-35.54-88-7.77-89.51-6.57a8,8,0,1,0,10,12.48c.26-.21,25.12-19.5,64.07,3.27-4.25,13.35-12.76,31.82-25.25,47-18.56,22.48-41.11,32.56-67,30A8,8,0,0,0,31.2,208a92.29,92.29,0,0,0,9.34.47c27.38,0,52-12.38,71.63-36.18.57-.69,1.14-1.4,1.69-2.1C133.31,175.29,168,190.3,168,232a8,8,0,0,0,16,0c0-24.65-10.08-45.35-29.15-59.86a104.29,104.29,0,0,0-31.31-15.81A169.31,169.31,0,0,0,139,124c26.14,16.09,46.84,20,60.69,20,12.18,0,19.06-3,19.67-3.28A8,8,0,0,0,223.28,130.08Z";

    /// <summary>Trader: a handshake.</summary>
    private const string Handshake =
        "M254.3,107.91,228.78,56.85a16,16,0,0,0-21.47-7.15L182.44,62.13,130.05,48.27a8.14,8.14,0,0,0-4.1,0L73.56,62.13,48.69,49.7a16,16,0,0,0-21.47,7.15L1.7,107.9a16,16,0,0,0,7.15,21.47l27,13.51,55.49,39.63a8.06,8.06,0,0,0,2.71,1.25l64,16a8,8,0,0,0,7.6-2.1l40-40,15.08-15.08,26.42-13.21a16,16,0,0,0,7.15-21.46Zm-54.89,33.37L165,113.72a8,8,0,0,0-10.68.61C136.51,132.27,116.66,130,104,122L147.24,80h31.81l27.21,54.41Zm-41.87,41.86L99.42,168.61l-49.2-35.14,28-56L128,64.28l9.8,2.59-45,43.68-.08.09a16,16,0,0,0,2.72,24.81c20.56,13.13,45.37,11,64.91-5L188,152.66Zm-25.72,34.8a8,8,0,0,1-7.75,6.06,8.13,8.13,0,0,1-1.95-.24L80.41,213.33a7.89,7.89,0,0,1-2.71-1.25L51.35,193.26a8,8,0,0,1,9.3-13l25.11,17.94L126,208.24A8,8,0,0,1,131.82,217.94Z";

    public static readonly string Key = Code(0xE8D7);

    public static readonly string Bring = Code(0xE821);

    /// <summary>A locked door or trunk on the map: a padlock (the key glyph is the key itself).</summary>
    public static readonly string Lock = Code(0xE72E);

    /// <summary>A switch on the map: the power symbol.</summary>
    public static readonly string Switch = Code(0xE7E8);

    /// <summary>
    /// A type's icon as SVG path data, filled nonzero, in coordinates of its own (a 256 × 256 box it doesn't fill):
    /// whoever draws it fits it by its bounds.
    /// </summary>
    public static string Path(ObjectiveKind kind) => kind switch
    {
        ObjectiveKind.Elimination => Crosshair,
        ObjectiveKind.Exploration => MagnifyingGlass,
        ObjectiveKind.Pickup => Hand,
        ObjectiveKind.Place => PushPin,
        ObjectiveKind.FindInRaid => Package,
        ObjectiveKind.Survive => PersonSimpleRun,
        _ => Handshake,
    };

    private static string Code(int codePoint) => char.ConvertFromUtf32(codePoint);

    private static readonly Lazy<SKTypeface> Typeface = new(() => SKTypeface.FromFamilyName(FontFamily) ?? SKTypeface.Default);
    private static readonly ConcurrentDictionary<ObjectiveKind, (SKPath Path, SKRect Bounds)> Shapes = new();

    /// <summary>Draws a font glyph centred on a point, about <paramref name="size"/> pixels tall.</summary>
    public static void Draw(SKCanvas canvas, string glyph, SKPoint center, float size, SKColor color)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        using var font = new SKFont(Typeface.Value, size);
        font.MeasureText(glyph, out var bounds, paint);
        canvas.DrawText(glyph, center.X - bounds.MidX, center.Y - bounds.MidY, SKTextAlign.Left, font, paint);
    }

    /// <summary>Draws a type's icon centred on a point, <paramref name="size"/> pixels along its longer side.</summary>
    public static void Draw(SKCanvas canvas, ObjectiveKind kind, SKPoint center, float size, SKColor color)
    {
        var (path, bounds) = Shapes.GetOrAdd(kind, k =>
        {
            var parsed = SKPath.ParseSvgPathData(Path(k));
            return (parsed, parsed.TightBounds);
        });
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        canvas.Save();
        canvas.Translate(center.X, center.Y);
        canvas.Scale(size / Math.Max(bounds.Width, bounds.Height));
        canvas.Translate(-bounds.MidX, -bounds.MidY);
        canvas.DrawPath(path, paint);
        canvas.Restore();
    }
}
