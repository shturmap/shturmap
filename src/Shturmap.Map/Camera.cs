using SkiaSharp;
using Shturmap.Core.Maps;

namespace Shturmap.Map;

/// <summary>Maps map units to screen pixels: a centre point and a zoom (pixels per map unit).</summary>
public sealed class Camera
{
    public MapPoint Center { get; private set; }

    public double Zoom { get; private set; } = 1;

    public SKSize Viewport { get; private set; } = new(800, 600);

    /// <summary>
    /// How far out the view zooms: half the zoom that shows the whole map. It belongs to the map shown, so the renderer
    /// sets it with every frame (<see cref="LimitTo"/>).
    /// </summary>
    public double MinZoom { get; set; } = 0.25;

    public double MaxZoom { get; set; } = 64;

    // A fit asked for while the view had no room for it: done once it has.
    private (MapRect Rect, float Padding)? _fitWhenRoom;

    public void Resize(SKSize viewport)
    {
        Viewport = viewport;
        if (_fitWhenRoom is { } fit && HasRoom(fit.Padding))
            Fit(fit.Rect, fit.Padding);
    }

    /// <summary>Puts the view back where it was (after looking at another map for a moment).</summary>
    public void Restore(MapPoint center, double zoom)
    {
        _fitWhenRoom = null;
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

    /// <summary>
    /// Shows the whole rectangle with some room around it. A view with no room for that yet (a window that is
    /// minimised or not laid out: narrower or lower than the padding takes) is fitted as soon as it has: fitted to
    /// nothing, the zoom fell to almost zero and the map stayed out of sight until "show the whole map".
    /// </summary>
    public void Fit(MapRect rect, float padding = 24)
    {
        Center = new MapPoint((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
        if (!HasRoom(padding))
        {
            _fitWhenRoom = (rect, padding);
            return;
        }
        _fitWhenRoom = null;
        Zoom = Overview(rect, padding);
        MinZoom = Zoom * 0.5;
    }

    /// <summary>
    /// Sets how far out the view zooms for the map shown: half the zoom that shows all of it in this view. The limit a
    /// <see cref="Fit"/> left belongs to the map fitted then: after a look at another map (a preview) and
    /// <see cref="Restore"/> it was that other map's, and the first turn of the wheel jumped to it (the review of
    /// 2026-10-04). The view itself stays as it is.
    /// </summary>
    public void LimitTo(MapRect rect, float padding = 24)
    {
        if (HasRoom(padding) && rect.Width > 0 && rect.Height > 0)
            MinZoom = Overview(rect, padding) * 0.5;
    }

    private bool HasRoom(float padding) => Viewport.Width > 2 * padding && Viewport.Height > 2 * padding;

    // The zoom that shows a rectangle whole, with the padding around it.
    private double Overview(MapRect rect, float padding)
    {
        var zx = (Viewport.Width - 2 * padding) / Math.Max(rect.Width, 1e-6);
        var zy = (Viewport.Height - 2 * padding) / Math.Max(rect.Height, 1e-6);
        return Math.Clamp(Math.Min(zx, zy), 1e-6, MaxZoom);
    }

    /// <summary>Shows a rectangle with some room around it, without changing the zoom limits.</summary>
    public void Frame(MapRect rect, float padding)
    {
        _fitWhenRoom = null;
        (Center, Zoom) = Framing(rect, padding);
    }

    /// <summary>The centre and zoom that would show a rectangle with some room around it (see <see cref="Frame"/>).</summary>
    public (MapPoint Center, double Zoom) Framing(MapRect rect, float padding)
    {
        var zx = (Viewport.Width - 2 * padding) / Math.Max(rect.Width, 1e-6);
        var zy = (Viewport.Height - 2 * padding) / Math.Max(rect.Height, 1e-6);
        return (new MapPoint((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2), Math.Clamp(Math.Min(zx, zy), MinZoom, MaxZoom));
    }

    public void CenterOn(MapPoint p) => Center = p;

    /// <summary>
    /// Where a pan from <paramref name="from"/> to <paramref name="to"/> is at <paramref name="t"/> (0 to 1) of its
    /// time, eased in and out: it sets off gently, travels, and settles (following the player, docs/DESIGN.md "Follow
    /// my position"). Until 2026-10-04 it eased out only, most of the way in the first moment, which read as a jump.
    /// </summary>
    public static MapPoint PanAt(MapPoint from, MapPoint to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        var e = t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        return new MapPoint(from.X + (to.X - from.X) * e, from.Y + (to.Y - from.Y) * e);
    }

    public void Pan(float dx, float dy)
    {
        _fitWhenRoom = null;
        Center = new MapPoint(Center.X - dx / Zoom, Center.Y - dy / Zoom);
    }

    /// <summary>
    /// Zooms by a factor, keeping the map point under the cursor in place. A view outside the limits (put back from
    /// another map, or after the window changed its size) moves toward them or stays; it never jumps to them.
    /// </summary>
    public void ZoomAt(SKPoint screen, double factor)
    {
        _fitWhenRoom = null;
        var anchor = ToMap(screen);
        Zoom = Math.Clamp(Zoom * factor, Math.Min(MinZoom, Zoom), Math.Max(MaxZoom, Zoom));
        var moved = ToMap(screen);
        Center = new MapPoint(Center.X + anchor.X - moved.X, Center.Y + anchor.Y - moved.Y);
    }
}
