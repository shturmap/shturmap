using SkiaSharp;
using Shturmap.Core.Maps;

namespace Shturmap.Map;

/// <summary>Maps map units to screen pixels: a centre point and a zoom (pixels per map unit).</summary>
public sealed class Camera
{
    public MapPoint Center { get; private set; }

    public double Zoom { get; private set; } = 1;

    public SKSize Viewport { get; private set; } = new(800, 600);

    public double MinZoom { get; set; } = 0.25;

    public double MaxZoom { get; set; } = 64;

    public void Resize(SKSize viewport) => Viewport = viewport;

    /// <summary>Puts the view back where it was (after looking at another map for a moment).</summary>
    public void Restore(MapPoint center, double zoom)
    {
        Center = center;
        Zoom = zoom;
    }

    public SKPoint ToScreen(MapPoint p) => new(
        (float)((p.X - Center.X) * Zoom + Viewport.Width / 2),
        (float)((p.Y - Center.Y) * Zoom + Viewport.Height / 2));

    public MapPoint ToMap(SKPoint screen) => new(
        (screen.X - Viewport.Width / 2) / Zoom + Center.X,
        (screen.Y - Viewport.Height / 2) / Zoom + Center.Y);

    /// <summary>The canvas transform that draws map units at their screen positions.</summary>
    public SKMatrix Matrix =>
        SKMatrix.CreateTranslation((float)-Center.X, (float)-Center.Y)
            .PostConcat(SKMatrix.CreateScale((float)Zoom, (float)Zoom))
            .PostConcat(SKMatrix.CreateTranslation(Viewport.Width / 2, Viewport.Height / 2));

    public void Fit(MapRect rect, float padding = 24)
    {
        Center = new MapPoint((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
        var zx = (Viewport.Width - 2 * padding) / rect.Width;
        var zy = (Viewport.Height - 2 * padding) / rect.Height;
        Zoom = Math.Clamp(Math.Min(zx, zy), 1e-6, MaxZoom);
        MinZoom = Zoom * 0.5;
    }

    /// <summary>Shows a rectangle with some room around it, without changing the zoom limits.</summary>
    public void Frame(MapRect rect, float padding) => (Center, Zoom) = Framing(rect, padding);

    /// <summary>The centre and zoom that would show a rectangle with some room around it (see <see cref="Frame"/>).</summary>
    public (MapPoint Center, double Zoom) Framing(MapRect rect, float padding)
    {
        var zx = (Viewport.Width - 2 * padding) / Math.Max(rect.Width, 1e-6);
        var zy = (Viewport.Height - 2 * padding) / Math.Max(rect.Height, 1e-6);
        return (new MapPoint((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2), Math.Clamp(Math.Min(zx, zy), MinZoom, MaxZoom));
    }

    public void CenterOn(MapPoint p) => Center = p;

    public void Pan(float dx, float dy) => Center = new MapPoint(Center.X - dx / Zoom, Center.Y - dy / Zoom);

    /// <summary>Zooms by a factor, keeping the map point under the cursor in place.</summary>
    public void ZoomAt(SKPoint screen, double factor)
    {
        var anchor = ToMap(screen);
        Zoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        var moved = ToMap(screen);
        Center = new MapPoint(Center.X + anchor.X - moved.X, Center.Y + anchor.Y - moved.Y);
    }
}
