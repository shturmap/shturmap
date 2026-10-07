using SkiaSharp;

namespace Shturmap.Map;

public static partial class MapRenderer
{
    // ---- a quest completed: its places ring out and go (owner, 2026-10-07; docs/DESIGN.md "Map drawing") ----

    /// <summary>
    /// The places of a quest just completed: each a gold disc with the check of a done objective that pops once, with two
    /// gold rings leaving it as a ping's do, then fading; gone after <see cref="MapScene.LeaveLength"/>. The quest's own
    /// colour, so the eye sees which places went. Nothing with animation effects off: the places simply go.
    /// </summary>
    private static void DrawLeaving(SKCanvas canvas, Camera camera, MapScene scene, float ui)
    {
        if (!scene.Pulse || scene.Leaving.Count == 0)
            return;
        var now = DateTime.Now;
        foreach (var (marker, since) in scene.Leaving)
        {
            var t = (now - since) / MapScene.LeaveLength;
            if (t is < 0 or >= 1)
                continue;
            DrawCompletedPlace(canvas, Screen(camera, scene, marker.Position), t, ui);
        }
    }

    /// <summary>One completed place, <paramref name="t"/> of the way through its leaving (0 to 1).</summary>
    internal static void DrawCompletedPlace(SKCanvas canvas, SKPoint at, double t, float ui)
    {
        using var band = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 7 * ui };
        using var ring = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * ui };
        foreach (var delay in new[] { 0.0, 0.22 })
        {
            var p = (t - delay) / 0.7;
            if (p is < 0 or > 1)
                continue;
            var eased = 1 - Math.Pow(1 - p, 3);
            var r = (float)(8 + 30 * eased) * ui;
            var fade = (float)(1 - Math.Pow(p, 2));
            band.Color = Background.WithAlpha((byte)(140 * fade));
            ring.Color = Amber.WithAlpha((byte)(255 * fade));
            canvas.DrawCircle(at, r, band);
            canvas.DrawCircle(at, r, ring);
        }
        // The disc pops once as it turns done, holds, then fades.
        var pop = t < 0.3 ? 1 + 0.45 * Math.Sin(Math.PI * t / 0.3) : 1;
        var alpha = t < 0.55 ? 1 : Math.Max(0, 1 - (t - 0.55) / 0.45);
        var radius = (float)(7 * pop) * ui;
        using var collar = new SKPaint { Color = Background.WithAlpha((byte)(170 * alpha)), IsAntialias = true };
        using var disc = new SKPaint { Color = Amber.WithAlpha((byte)(255 * alpha)), IsAntialias = true };
        canvas.DrawCircle(at, radius + 2.5f * ui, collar);
        canvas.DrawCircle(at, radius, disc);
        using var check = new SKPaint
        {
            Color = Background.WithAlpha((byte)(255 * alpha)), IsAntialias = true, Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.8f * ui * (float)pop, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
        };
        using var builder = new SKPathBuilder();
        var s = radius / 7;
        builder.MoveTo(at.X - 3.4f * s, at.Y + 0.2f * s);
        builder.LineTo(at.X - 1f * s, at.Y + 2.6f * s);
        builder.LineTo(at.X + 3.6f * s, at.Y - 2.4f * s);
        using var path = builder.Detach();
        canvas.DrawPath(path, check);
    }
}
