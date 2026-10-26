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
    /// DIP cut its picked quest's). The other sides keep their padding. Callers give it in pixels: times the UI scale. A
    /// picked or pointed-at name that needs more finds it in the layout (owner, 2026-10-15;
    /// <see cref="MapRenderer.LitNameMost"/>).
    /// </summary>
    public const float LabelRoom = 150;

    /// <summary>
    /// The share of the view's width the label room takes at most, at 100 % text: at the smallest window the map is about
    /// 500 DIP wide, and 150 of them would shrink a wide map by more than a quarter (the 100 it keeps: a sixth). It grows
    /// with the text size (<see cref="TextScale"/>), as the names do.
    /// </summary>
    public const float LabelRoomShare = 0.2f;

    /// <summary>
    /// The text size beyond the screen's scale: Windows' text size times settings' step (docs/DESIGN.md §4, "Screen
    /// anatomy", *Text size*), 1 in the CLI and the tests. The label room's share of the view grows with it
    /// (<see cref="RoomRight"/>): at 150 % the names are half as wide again, and with the room of 100 % a name at the right
    /// edge of the smallest window lost whole words, "Beschädigte", "Abwasserk" (QA pass of 2026-10-19, F2). The window
    /// sets it (MapView).
    /// </summary>
    public float TextScale { get; set; } = 1;

    /// <summary>
    /// Pixels at each side of the view that every framing (the whole map's view, <see cref="Fit"/>, with
    /// <see cref="Overview"/> and <see cref="LimitTo"/>; <see cref="Framing"/>, <see cref="FramingAbove"/>, the roll call's
    /// <see cref="FittingAll(IReadOnlyCollection{MapPoint}, MapPoint?, float, float)"/>) keeps clear before its padding:
    /// <c>Width</c> at the left and at the right, <c>Height</c> at the top and at the foot, where a framing's own foot or
    /// head, given from the view's edge, doesn't reach further. The squad grid's strips while it shows
    /// (<see cref="MapRenderer.GridInset"/>; UI QA pass of the squad grid, 2026-10-26, F2): the map's own controls stepped
    /// inside them, so what a view frames steps in with them and keeps from them what it kept from the window's edges before
    /// (at 900×560 the credit, raised 18 DIP, ran over "Transit to Interchange" at the whole map's foot). Its owner sets it
    /// (MapView, the CLI); none by default.
    /// </summary>
    public SKSize Inset { get; set; }

    // The view inside the inset: centred in the whole view, so a centre worked out in it is the whole view's too.
    private SKSize Inner => new(Math.Max(0, Viewport.Width - 2 * Inset.Width), Math.Max(0, Viewport.Height - 2 * Inset.Height));

    // A framing inside the inset: the sides' taken from the width (the part left is centred, so its centre is the view's),
    // the top's and the foot's as a head and a foot of at least their size.
    private (MapPoint Center, double Zoom) Inside(MapRect rect, float padding, float labelRoom, float foot, float head, double minZoom) =>
        Place(new SKSize(Math.Max(0, Viewport.Width - 2 * Inset.Width), Viewport.Height), rect, padding, labelRoom, Math.Max(foot, Inset.Height), minZoom,
            Math.Max(minZoom, MaxZoom), TextScale, Math.Max(head, Inset.Height));

    /// <summary>
    /// The pixels a view of <paramref name="width"/> keeps right of what it fits or frames: the label room
    /// (<paramref name="labelRoom"/>, pixels), at most <see cref="LabelRoomShare"/> of the width times the text size
    /// (<paramref name="text"/>, <see cref="TextScale"/>), never less than the padding.
    /// </summary>
    public static float RoomRight(float width, float padding, float labelRoom, float text = 1) =>
        Math.Max(padding, Math.Min(labelRoom, width * LabelRoomShare * Math.Max(1, text)));

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
        (Center, Zoom) = Whole(rect, padding, labelRoom);
        MinZoom = Zoom * 0.5;
    }

    // The whole map's view: as FramingAbove frames, inside the inset.
    private (MapPoint Center, double Zoom) Whole(MapRect rect, float padding, float labelRoom) =>
        Inside(rect, padding, labelRoom, 0, 0, 1e-6);

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

    private bool HasRoom(float padding) => Inner.Width > 2 * padding && Inner.Height > 2 * padding;

    /// <summary>The zoom that shows a rectangle whole, with the padding around it (inside the <see cref="Inset"/>) and the
    /// label room on its right.</summary>
    public double Overview(MapRect rect, float padding, float labelRoom = 0) => Whole(rect, padding, labelRoom).Zoom;

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
        Inside(rect, padding, labelRoom, 0, 0, MinZoom);

    /// <summary>
    /// The centre and zoom that show a rectangle whole in the part of the view above <paramref name="foot"/> pixels at
    /// its foot (the raid replay's band, the tour's), <paramref name="padding"/> pixels around it and
    /// <paramref name="labelRoom"/> pixels on its right, at most <see cref="LabelRoomShare"/> of the view's width and
    /// never less than the padding (owner, 2026-10-09). The rectangle stays in the middle where that leaves the room on
    /// the right anyway (a tall map in a wide view), and moves left only as far as the room needs. Not held to the zoom
    /// limits: they may still be another map's (a preview's, about to be shown).
    /// </summary>
    /// <param name="head">Pixels at the view's top that what is framed stays under (the preview label the map's top holds
    /// in the tour; QA pass, 2026-10-15).</param>
    public (MapPoint Center, double Zoom) FramingAbove(MapRect rect, float padding, float foot, float labelRoom = 0, float head = 0) =>
        Inside(rect, padding, labelRoom, foot, head, 1e-6);

    /// <summary>
    /// The padding (DIPs) the roll call's framing keeps around the listed extracts and the player
    /// (<see cref="FittingAll(IReadOnlyCollection{MapPoint}, MapPoint?, SKSize, float, float, double, double)"/>): room for
    /// a triangle with its glow and its echoes' first reach. Callers give it in pixels: times the UI scale.
    /// </summary>
    public const float CallOutPadding = 48;

    /// <summary>
    /// The view the roll call zooms out to (owner, 2026-10-15, from the design panel: option A, "Roll call, then
    /// fly-out"): every one of <paramref name="points"/> (the listed extracts' places, in map units) and the player's
    /// <paramref name="player"/> (map units; null without a position) inside <paramref name="viewport"/> (pixels), with
    /// <paramref name="padding"/> pixels on the left, top and bottom and <paramref name="labelRoom"/> pixels on the right
    /// for their names, as every framing keeps them (<see cref="FramingAbove"/>). It is a fit, not a zoom out only: points
    /// already in view are framed all the same, but never past the zoom limits (<paramref name="minZoom"/>,
    /// <paramref name="maxZoom"/>: the map's <see cref="MinZoom"/> and <see cref="MaxZoom"/>), so points close together
    /// stop at the closest zoom the wheel reaches; a caller that wants the view only to zoom out gives its zoom as
    /// <paramref name="maxZoom"/>. Null with nothing to frame, or in a view with no room for the padding (minimised, not
    /// laid out). Pure: the window moves the view to it with MapView.AnimateView.
    /// </summary>
    public static (MapPoint Center, double Zoom)? FittingAll(IReadOnlyCollection<MapPoint> points, MapPoint? player, SKSize viewport, float labelRoom,
        float padding, double minZoom, double maxZoom, float text = 1)
    {
        var all = player is { } you ? points.Append(you).ToList() : points.ToList();
        if (all.Count == 0 || viewport.Width <= 2 * padding || viewport.Height <= 2 * padding)
            return null;
        var rect = new MapRect(all.Min(p => p.X), all.Min(p => p.Y), all.Max(p => p.X), all.Max(p => p.Y));
        return Place(viewport, rect, padding, labelRoom, 0, minZoom, Math.Max(minZoom, maxZoom), text);
    }

    /// <summary>The roll call's view (<see cref="FittingAll(IReadOnlyCollection{MapPoint}, MapPoint?, SKSize, float, float, double, double)"/>)
    /// in this camera's viewport inside its <see cref="Inset"/>, within the zoom limits of the map shown.</summary>
    public (MapPoint Center, double Zoom)? FittingAll(IReadOnlyCollection<MapPoint> points, MapPoint? player, float labelRoom, float padding) =>
        FittingAll(points, player, Inner, labelRoom, padding, MinZoom, MaxZoom, TextScale);

    /// <summary>
    /// The roll call's move (owner, 2026-10-15, from the design panel: option A, "Roll call, then fly-out"; docs/DESIGN.md,
    /// "Map drawing", *The view leaves the player*): none where every one of <paramref name="points"/> and the
    /// <paramref name="player"/> is in <paramref name="view"/> already (<see cref="Holds"/>), the view then as it is; else
    /// <see cref="FittingAll(IReadOnlyCollection{MapPoint}, MapPoint?, SKSize, float, float, double, double)"/> with the
    /// view's zoom as its closest, a zoom out only. Until 2026-10-15 a fit that zoomed out nothing still took them into the
    /// view's middle (coordinator, from the roll call's build: on Customs it cut off the map's west and left empty map at
    /// the top). Null with nothing to frame or no room for the padding.
    /// </summary>
    public static ((MapPoint Center, double Zoom) View, bool Moves)? CallOut(IReadOnlyCollection<MapPoint> points, MapPoint? player, SKSize viewport,
        (MapPoint Center, double Zoom) view, float labelRoom, float padding, double minZoom, float text = 1)
    {
        if (points.Count == 0 && player is null)
            return null;
        if (Holds(points, player, viewport, view, labelRoom, padding, text))
            return (view, false);
        return FittingAll(points, player, viewport, labelRoom, padding, minZoom, view.Zoom, text) is { } fit ? (fit, true) : null;
    }

    /// <summary>
    /// Whether <paramref name="view"/> of <paramref name="viewport"/> shows every one of <paramref name="points"/> and the
    /// <paramref name="player"/> where a fit of them would put them: <paramref name="padding"/> pixels inside the view's
    /// left, top and bottom, and the label room inside its right (as <see cref="FramingAbove"/> keeps it), so each name
    /// reads too.
    /// </summary>
    public static bool Holds(IReadOnlyCollection<MapPoint> points, MapPoint? player, SKSize viewport, (MapPoint Center, double Zoom) view,
        float labelRoom, float padding, float text = 1)
    {
        var right = RoomRight(viewport.Width, padding, labelRoom, text);
        foreach (var p in player is { } you ? points.Append(you) : points)
        {
            var x = (p.X - view.Center.X) * view.Zoom + viewport.Width / 2;
            var y = (p.Y - view.Center.Y) * view.Zoom + viewport.Height / 2;
            if (x < padding || x > viewport.Width - right || y < padding || y > viewport.Height - padding)
                return false;
        }
        return true;
    }

    private static (MapPoint Center, double Zoom) Place(SKSize viewport, MapRect rect, float padding, float labelRoom, float foot, double minZoom, double maxZoom,
        float text, float head = 0)
    {
        var width = viewport.Width;
        var right = RoomRight(width, padding, labelRoom, text);
        var zx = (width - padding - right) / Math.Max(rect.Width, 1e-6);
        var zy = (viewport.Height - foot - head - 2 * padding) / Math.Max(rect.Height, 1e-6);
        var zoom = Math.Clamp(Math.Min(zx, zy), minZoom, maxZoom);
        // Where the rectangle's left edge goes on screen: the middle, or left of it as far as the room on the right
        // needs but not into the padding on the left; one too wide even for the padding (held at the zoom limit) stays
        // in the middle.
        var shown = rect.Width * zoom;
        var left = Math.Min((width - shown) / 2, Math.Max(padding, width - right - shown));
        // The middle of what is framed lies in the middle of the part between the head and the foot.
        return (new MapPoint(rect.Left + (width / 2 - left) / zoom, (rect.Top + rect.Bottom) / 2 + (foot - head) / 2 / zoom), zoom);
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
