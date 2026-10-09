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

    /// <summary>
    /// DIPs a view keeps right of what it fits or frames, for the label the map writes right of a symbol (owner,
    /// 2026-10-09: on a wide map the rightmost names ran off the window, "Transit to I…", "Crossr…"; the tour's 90
    /// DIP cut its picked quest's). The other sides keep their padding. Callers give it in pixels: times the UI scale.
    /// </summary>
    public const float LabelRoom = 150;

    /// <summary>
    /// The share of the view's width the label room takes at most: at the smallest window the map is about 500 DIP
    /// wide, and 150 of them would shrink a wide map by more than a quarter (the 100 it keeps: a sixth).
    /// </summary>
    public const float LabelRoomShare = 0.2f;

    // A fit asked for while the view had no room for it: done once it has.
    private (MapRect Rect, float Padding, float LabelRoom)? _fitWhenRoom;

    public void Resize(SKSize viewport)
    {
        Viewport = viewport;
        if (_fitWhenRoom is { } fit && HasRoom(fit.Padding))
            Fit(fit.Rect, fit.Padding, fit.LabelRoom);
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
    /// Shows the whole rectangle with some room around it (<see cref="FramingAbove"/>; <paramref name="labelRoom"/> in
    /// pixels, see <see cref="LabelRoom"/>). A view with no room for that yet (a window that is minimised or not laid
    /// out: narrower or lower than the padding takes) is fitted as soon as it has: fitted to nothing, the zoom fell to
    /// almost zero and the map stayed out of sight until "show the whole map".
    /// </summary>
    public void Fit(MapRect rect, float padding = 24, float labelRoom = 0)
    {
        if (!HasRoom(padding))
        {
            Center = new MapPoint((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
            _fitWhenRoom = (rect, padding, labelRoom);
            return;
        }
        _fitWhenRoom = null;
        (Center, Zoom) = FramingAbove(rect, padding, 0, labelRoom);
        MinZoom = Zoom * 0.5;
    }

    /// <summary>
    /// Sets how far out the view zooms for the map shown: half the zoom that shows all of it in this view. The limit a
    /// <see cref="Fit"/> left belongs to the map fitted then: after a look at another map (a preview) and
    /// <see cref="Restore"/> it was that other map's, and the first turn of the wheel jumped to it (the review of
    /// 2026-10-04). The view itself stays as it is.
    /// </summary>
    public void LimitTo(MapRect rect, float padding = 24, float labelRoom = 0)
    {
        if (HasRoom(padding) && rect.Width > 0 && rect.Height > 0)
            MinZoom = Overview(rect, padding, labelRoom) * 0.5;
    }

    private bool HasRoom(float padding) => Viewport.Width > 2 * padding && Viewport.Height > 2 * padding;

    /// <summary>The zoom that shows a rectangle whole, with the padding around it and the label room on its
    /// right.</summary>
    public double Overview(MapRect rect, float padding, float labelRoom = 0) => FramingAbove(rect, padding, 0, labelRoom).Zoom;

    /// <summary>Shows a rectangle with some room around it, without changing the zoom limits.</summary>
    public void Frame(MapRect rect, float padding)
    {
        _fitWhenRoom = null;
        (Center, Zoom) = Framing(rect, padding);
    }

    /// <summary>
    /// The centre and zoom that would show a rectangle with some room around it (see <see cref="Frame"/>), within the
    /// zoom limits of the map shown.
    /// </summary>
    public (MapPoint Center, double Zoom) Framing(MapRect rect, float padding, float labelRoom = 0) =>
        Place(rect, padding, labelRoom, 0, MinZoom);

    /// <summary>
    /// The centre and zoom that show a rectangle whole in the part of the view above <paramref name="foot"/> pixels at
    /// its foot (the raid replay's band, the tour's), <paramref name="padding"/> pixels around it and
    /// <paramref name="labelRoom"/> pixels on its right, at most <see cref="LabelRoomShare"/> of the view's width and
    /// never less than the padding (owner, 2026-10-09). The rectangle stays in the middle where that leaves the room on
    /// the right anyway (a tall map in a wide view), and moves left only as far as the room needs. Not held to the zoom
    /// limits: they may still be another map's (a preview's, about to be shown).
    /// </summary>
    public (MapPoint Center, double Zoom) FramingAbove(MapRect rect, float padding, float foot, float labelRoom = 0) =>
        Place(rect, padding, labelRoom, foot, 1e-6);

    private (MapPoint Center, double Zoom) Place(MapRect rect, float padding, float labelRoom, float foot, double minZoom)
    {
        var width = Viewport.Width;
        var right = Math.Max(padding, Math.Min(labelRoom, width * LabelRoomShare));
        var zx = (width - padding - right) / Math.Max(rect.Width, 1e-6);
        var zy = (Viewport.Height - foot - 2 * padding) / Math.Max(rect.Height, 1e-6);
        var zoom = Math.Clamp(Math.Min(zx, zy), minZoom, MaxZoom);
        // Where the rectangle's left edge goes on screen: the middle, or left of it as far as the room on the right
        // needs but not into the padding on the left; one too wide even for the padding (held at the zoom limit) stays
        // in the middle.
        var shown = rect.Width * zoom;
        var left = Math.Min((width - shown) / 2, Math.Max(padding, width - right - shown));
        // The middle of what is framed lies in the middle of the part above the foot.
        return (new MapPoint(rect.Left + (width / 2 - left) / zoom, (rect.Top + rect.Bottom) / 2 + foot / 2 / zoom), zoom);
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
