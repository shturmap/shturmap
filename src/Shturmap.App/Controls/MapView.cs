using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Shturmap.Map;

namespace Shturmap.App.Controls;

/// <summary>
/// The map surface: draws a <see cref="MapScene"/> on the GPU and handles pan (drag) and zoom (wheel, double-click).
/// The view moves by itself only while "Follow my position" is on (<see cref="Follow"/>): then a new position glides
/// into the middle. Otherwise it stays where the player put it, and a new position pings where it is, or at the edge
/// when it is out of view. Drawing is in physical pixels; markers are scaled by the display's scale factor.
/// </summary>
public sealed partial class MapView : Grid
{
    private readonly SKSwapChainPanel _panel = new();
    private readonly Camera _camera = new();
    private MapScene? _scene;
    private bool _fitPending;
    private Windows.Foundation.Point? _dragFrom;
    private PlayerFix? _seenFix;

    private bool _stopped;

    // Every frame is asked for here, so that none is asked for once the app is closing.
    private void Invalidate()
    {
        if (!_stopped)
            _panel.Invalidate();
    }

    /// <summary>
    /// The app is closing: no more frames. A timer that asks for one while the window's drawing surface is taken down
    /// ends the process with an access violation (found 2026-10-04: exiting with a quest pointed at, its markers
    /// pulsing, ended with 0xC0000005).
    /// </summary>
    public void StopDrawing()
    {
        _stopped = true;
        _animation?.Stop();
        _glideFrames?.Stop();
        _wheelEnd?.Stop();
        _panel.PaintSurface -= OnPaintSurface;
    }

    public MapView()
    {
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        Children.Add(_panel);
        _panel.PaintSurface += OnPaintSurface;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) => _dragFrom = null;
        PointerExited += (_, e) => Hover(null, e.GetCurrentPoint(this).Position);
        PointerWheelChanged += OnPointerWheelChanged;
        DoubleTapped += OnDoubleTapped;
        // A tap on a marker is that marker's click (MarkerClicked); only taps on the bare map reach the window, where
        // they let go of held cards.
        Tapped += (_, e) =>
        {
            if (_hovered is not null || _overEdge)
                e.Handled = true;
        };
    }

    private bool _tileRedrawQueued;

    // The artworks whose unreadable floors are logged: an artwork is kept for the session and shown again and again.
    private readonly HashSet<MapArtwork> _floorsWatched = [];

    // Raised on a loader thread, often several tiles at once: one redraw on the UI thread for them all.
    private void OnTilesChanged()
    {
        if (_tileRedrawQueued)
            return;
        _tileRedrawQueued = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _tileRedrawQueued = false;
            Invalidate();
        });
    }

    /// <summary>A new position arrived and pings; true when it is out of view (the edge arrow points to it).</summary>
    public event Action<bool>? PlayerPinged;

    /// <summary>The edge arrow of an out-of-view player was clicked, and the view moved to them.</summary>
    public event Action? EdgeClicked;

    // A position counts as new for a ping only when it is recent: not an old screenshot found when the app starts.
    private static readonly TimeSpan PingIfNewerThan = TimeSpan.FromSeconds(30);

    private bool _overEdge;

    /// <summary>The pointer moved onto a marker (or off all markers: null), with its position in this control.</summary>
    public event Action<MapMarker?, Windows.Foundation.Point>? MarkerHovered;

    private MapMarker? _hovered;

    public MapScene? Scene => _scene;

    private float PixelScale => (float)(XamlRoot?.RasterizationScale ?? 1.0);

    /// <summary>Pings and pulses play even with Windows' animation effects off (the website demo's recording).</summary>
    public bool AlwaysAnimate { get; set; }

    /// <summary>Shows a new map, fitted to the window, or at a view saved earlier with <see cref="View"/>.</summary>
    public void SetScene(MapScene? scene, (Shturmap.Core.Maps.MapPoint Center, double Zoom)? view = null)
    {
        // A tile render redraws as its tiles arrive (they load in the background).
        if (_scene?.Tiles is { } before)
            before.Changed -= OnTilesChanged;
        if (scene?.Tiles is { } after)
            after.Changed += OnTilesChanged;
        // SVG artwork: its floors are read ahead in the background, so the first paint of a floor doesn't wait for
        // its picture (the review of 2026-10-04, A37); a floor that arrives while it is the one shown redraws.
        if (_scene?.Artwork is { } artBefore)
            artBefore.FloorRead -= OnTilesChanged;
        if (scene?.Artwork is { } artwork)
        {
            artwork.FloorRead += OnTilesChanged;
            if (_floorsWatched.Add(artwork))
                artwork.FloorFailed += (id, e) => Shturmap.Session.AppLog.Warn($"Map artwork: the floor '{id}' couldn't be read and isn't drawn", e);
            _ = artwork.ReadFloorsAsync();
        }
        StopGlide();
        _scene = scene;
        if (scene is not null)
            scene.Pulse = AlwaysAnimate || new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
        _fitPending = view is null;
        if (view is { } v)
            _camera.Restore(v.Center, v.Zoom);
        _seenFix = null;
        // Following, a new map opens on the player (after it is fitted, at its first paint).
        _centerAfterFit = _follow.On;
        Invalidate();
    }

    // ---- follow my position (owner, 2026-10-03; docs/DESIGN.md "Follow my position") ----

    private readonly FollowState _follow = new();
    private bool _centerAfterFit;
    private (Shturmap.Core.Maps.MapPoint From, Shturmap.Core.Maps.MapPoint To, DateTime Started)? _glide;

    /// <summary>
    /// How long the view takes to glide to a new position while following: slow enough to see where it comes from and
    /// where it goes (owner, 2026-10-04: "that smooth panning should be way slower"; it was 0.5 s).
    /// </summary>
    public static readonly TimeSpan FollowPan = TimeSpan.FromMilliseconds(2400);

    /// <summary>Whether the view follows the player's position.</summary>
    public bool Follow => _follow.On;

    /// <summary>Turns following on or off; on, the view glides to the player's position at once.</summary>
    public void SetFollow(bool on)
    {
        _follow.Set(on);
        if (!on)
        {
            StopGlide();
            return;
        }
        _centerAfterFit = _fitPending;
        if (!_fitPending)
            GlideToPlayer();
    }

    // Glides to the player at the current zoom, eased in and out, to where the player has room ahead
    // (FollowState.Target); with Windows' animation effects off it jumps.
    private void GlideToPlayer()
    {
        if (_scene is null || FollowState.Target(_scene, _camera, DateTime.Now) is not { } to)
            return;
        _follow.Centred();
        StopGlide();
        if (!_scene.Pulse)
        {
            _camera.CenterOn(to);
            Invalidate();
            return;
        }
        // Each frame works out where the glide is as it is drawn (GlideStep), so none shows a stale centre. The frames
        // are asked for at the system timer's pace (about 64 a second): the map's 16 ms animation timer and the
        // Rendering event each gave only about 30 a second (measured with the dev view, 2026-10-03).
        _glide = (_camera.Center, to, DateTime.Now);
        _scene.Gliding = true;
        if (_glideFrames is null)
        {
            _glideFrames = DispatcherQueue.CreateTimer();
            _glideFrames.Interval = TimeSpan.FromMilliseconds(15);
            _glideFrames.Tick += (_, _) =>
            {
                GlideStep();
                Invalidate();
            };
        }
        _glideFrames.Start();
    }

    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _glideFrames;

    // As a frame is drawn (and on each tick, so it ends unseen too): the view's centre at this moment of the glide; the
    // glide ends on the position.
    private void GlideStep()
    {
        if (_glide is not { } glide)
            return;
        var t = (DateTime.Now - glide.Started) / FollowPan;
        _camera.CenterOn(Camera.PanAt(glide.From, glide.To, t));
        if (t >= 1)
            StopGlide();
    }

    private void StopGlide()
    {
        if (_glide is null)
            return;
        _glide = null;
        _glideFrames?.Stop();
        if (_scene is not null)
            _scene.Gliding = false;
        Invalidate();
    }

    // Zooming while following keeps the player where following puts them.
    private void KeepPlayerCentered()
    {
        if (!_follow.Zoomed() || _scene is null || FollowState.Target(_scene, _camera, DateTime.Now) is not { } to)
            return;
        StopGlide();
        _camera.CenterOn(to);
    }

    /// <summary>Where the view is: to come back to it after a preview of another map.</summary>
    public (Shturmap.Core.Maps.MapPoint Center, double Zoom) View => (_camera.Center, _camera.Zoom);

    /// <summary>
    /// Redraws after the scene's markers, player or floor changed. A new position pings; following, the view glides to
    /// it, otherwise it stays where the player put it and says so when the position is out of view.
    /// </summary>
    public void Refresh()
    {
        if (_scene is { Player: { } fix } scene && !ReferenceEquals(fix, _seenFix))
        {
            _seenFix = fix;
            if (_follow.On)
            {
                // Before the first paint the view has no size: it starts on the position then. After it, it glides.
                _centerAfterFit = _fitPending;
                if (!_fitPending)
                    GlideToPlayer();
            }
            if (Shturmap.Core.Logs.WallClock.Elapsed(fix.At, DateTime.Now) < PingIfNewerThan)
            {
                scene.PingSince = DateTime.Now;
                // Before the first paint the view isn't fitted yet; the whole map will be in view then. Following, the
                // view is on its way to the position, so it is never out of view.
                PlayerPinged?.Invoke(!_follow.On && !_fitPending && MapRenderer.EdgeOf(_camera, scene, PixelScale) is not null);
                Redraw();
            }
        }
        Invalidate();
    }

    /// <summary>
    /// Redraws without moving, e.g. to let an ageing fix fade. Keeps redrawing while the focus dimming eases in or
    /// out and while focused markers pulse.
    /// </summary>
    public void Redraw()
    {
        Invalidate();
        if (_scene is not { } scene)
            return;
        if (!scene.Pulse)
        {
            // Animation effects off: the dimming switches at once and nothing pulses.
            scene.Dim = DimTarget(scene);
            return;
        }
        if (!Animating(scene))
            return;
        if (_animation is null)
        {
            _animation = DispatcherQueue.CreateTimer();
            _animation.Interval = TimeSpan.FromMilliseconds(16);
            _animation.Tick += (_, _) => Step();
        }
        _lastStep = DateTime.Now;
        if (!_animation.IsRunning)
            _animation.Start();
    }

    private static readonly TimeSpan DimEase = TimeSpan.FromMilliseconds(180);
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _animation;
    private DateTime _lastStep;

    private static float DimTarget(MapScene scene) => scene.HasHighlight ? 1f : 0f;

    private static bool Animating(MapScene scene) =>
        scene.Pulsing || scene.Pinging || Math.Abs(scene.Dim - DimTarget(scene)) > 0.001f;

    private void Step()
    {
        if (_scene is not { } scene || !Animating(scene))
        {
            // One more frame without it: a ping's last ring would otherwise stay up until the next redraw.
            _animation?.Stop();
            Invalidate();
            return;
        }
        var now = DateTime.Now;
        var step = (float)((now - _lastStep) / DimEase);
        _lastStep = now;
        var target = DimTarget(scene);
        scene.Dim = scene.Dim < target ? Math.Min(target, scene.Dim + step) : Math.Max(target, scene.Dim - step);
        Invalidate();
    }

    /// <summary>Moves the view to the player's last position, keeping the zoom (F, the button, the edge arrow).</summary>
    public void CenterOnPlayer()
    {
        if (_scene is null || FollowState.Target(_scene, _camera, DateTime.Now) is not { } to)
            return;
        _follow.Centred();
        StopGlide();
        _camera.CenterOn(to);
        Invalidate();
    }

    /// <summary>Shows the whole map. Following stays on: the next position centres the view on the player, at this zoom.</summary>
    public void FitMap()
    {
        _follow.Fitted();
        StopGlide();
        _centerAfterFit = false;
        _fitPending = true;
        Invalidate();
    }

    public void ZoomBy(double factor)
    {
        _camera.ZoomAt(new SKPoint(_camera.Viewport.Width / 2, _camera.Viewport.Height / 2), factor);
        KeepPlayerCentered();
        Invalidate();
    }

    // ---- the website demo's camera moves (players move the view themselves) ----

    private EventHandler<object>? _viewAnimation;

    /// <summary>Moves the view to a centre and zoom over <paramref name="duration"/>, eased in and out.</summary>
    public void AnimateView((Shturmap.Core.Maps.MapPoint Center, double Zoom) to, TimeSpan duration)
    {
        StopViewAnimation();
        StopGlide();
        var from = View;
        var started = DateTime.Now;
        _viewAnimation = (_, _) =>
        {
            var t = Math.Min(1, (DateTime.Now - started) / duration);
            var e = t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
            // The zoom eases on a log scale, so a zoom in and the same zoom out look alike.
            var zoom = Math.Exp(Math.Log(from.Zoom) + (Math.Log(to.Zoom) - Math.Log(from.Zoom)) * e);
            _camera.Restore(new Shturmap.Core.Maps.MapPoint(from.Center.X + (to.Center.X - from.Center.X) * e,
                from.Center.Y + (to.Center.Y - from.Center.Y) * e), zoom);
            Invalidate();
            if (t >= 1)
                StopViewAnimation();
        };
        CompositionTarget.Rendering += _viewAnimation;
    }

    private void StopViewAnimation()
    {
        if (_viewAnimation is null)
            return;
        CompositionTarget.Rendering -= _viewAnimation;
        _viewAnimation = null;
    }

    /// <summary>The view that shows these world positions with <paramref name="padding"/> (DIPs) around them.</summary>
    public (Shturmap.Core.Maps.MapPoint Center, double Zoom) FramingOf(IReadOnlyCollection<Shturmap.Core.WorldPoint> points, double padding)
    {
        var map = points.Select(p => _scene!.Projection.ToMap(p)).ToList();
        var rect = new Shturmap.Core.Maps.MapRect(map.Min(p => p.X), map.Min(p => p.Y), map.Max(p => p.X), map.Max(p => p.Y));
        return _camera.Framing(rect, (float)(padding * PixelScale));
    }

    /// <summary>Where a world position is drawn now, in this control's coordinates (DIPs).</summary>
    public Windows.Foundation.Point PointOf(Shturmap.Core.WorldPoint world)
    {
        var p = _camera.ToScreen(_scene!.Projection.ToMap(world));
        return new(p.X / PixelScale, p.Y / PixelScale);
    }

    /// <summary>Waits until the tiles the current view needs are loaded (a snapshot of a tile render), or an SVG
    /// artwork's floors are read, at most <paramref name="limit"/>.</summary>
    public async Task TilesLoadedAsync(TimeSpan limit)
    {
        if (_scene?.Artwork is { } artwork)
            await Task.WhenAny(artwork.ReadFloorsAsync(), Task.Delay(limit));
        if (_scene is not { Tiles: { } tiles } scene || scene.Definition.TilePath is not { } basePath)
            return;
        var a = _camera.ToMap(new SKPoint(0, 0));
        var b = _camera.ToMap(new SKPoint(_camera.Viewport.Width, _camera.Viewport.Height));
        var view = new Shturmap.Core.Maps.MapRect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        var loads = new List<Task> { tiles.LoadAsync(basePath, view, scene.Projection.WorldRect, _camera.Zoom) };
        if (scene.Floor?.TilePath is { } floorPath && floorPath != basePath)
            loads.Add(tiles.LoadAsync(floorPath, view, scene.Projection.WorldRect, _camera.Zoom));
        await Task.WhenAny(Task.WhenAll(loads), Task.Delay(limit));
    }

    /// <summary>Draws the current view into a PNG (developer snapshot; the GPU surface itself can't be read back),
    /// at <paramref name="scale"/> times the screen's pixels.</summary>
    public void SaveSnapshot(string path, int scale = 1)
    {
        var size = _camera.Viewport;
        using var surface = SKSurface.Create(new SKImageInfo(Math.Max(1, (int)size.Width * scale), Math.Max(1, (int)size.Height * scale)));
        surface.Canvas.Scale(scale);
        // A picture of a floor shows that floor: its artwork is waited for here (a moment at most, and only in this
        // developer aid; the map on screen never waits).
        _scene?.Artwork?.ReadFloorsAsync().Wait(TimeSpan.FromSeconds(10));
        // No map yet: the ground, as everywhere (it had a teal tint of its own until the design system, 2026-10-03).
        if (_scene is null)
            surface.Canvas.Clear(Palette.Sk(Palette.Ground));
        else
            MapRenderer.Render(surface.Canvas, _camera, _scene, PixelScale);
        using var image = surface.Snapshot();
        using var png = image.Encode(SKEncodedImageFormat.Png, 90);
        File.WriteAllBytes(path, png.ToArray());
    }

    private int _paints;

    private void OnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        if (_paints++ == 0)
            Shturmap.Session.AppLog.Debug($"Map surface first paint: {e.BackendRenderTarget.Width}x{e.BackendRenderTarget.Height} px, GPU context {(e.Surface.Context is null ? "none" : "ok")}");
        _camera.Resize(new SKSize(e.BackendRenderTarget.Width, e.BackendRenderTarget.Height));
        if (_scene is null)
        {
            canvas.Clear(Palette.Sk(Palette.Ground));
            return;
        }
        if (_fitPending)
        {
            _fitPending = false;
            _camera.Fit(_scene.Projection.WorldRect, 24 * PixelScale);
        }
        // Following: once the view has its size (and a new map is fitted), it starts on the player.
        if (_centerAfterFit && FollowState.Target(_scene, _camera, DateTime.Now) is { } player)
        {
            _centerAfterFit = false;
            _follow.Centred();
            _camera.CenterOn(player);
        }
        GlideStep();
        MapRenderer.Render(canvas, _camera, _scene, PixelScale);
    }

    private SKPoint Pixels(Windows.Foundation.Point p) => new((float)p.X * PixelScale, (float)p.Y * PixelScale);

#if DEVTOOLS
    /// <summary>Developer view: while set, a left press on the map picks a place instead of panning. It is called with
    /// the world X/Z pressed and, after a drag, the X/Z released (the facing); the middle button still pans.</summary>
    public Action<(double X, double Z), (double X, double Z)?>? DevPick { get; set; }

    private (double X, double Z)? _devPickFrom;
    private Windows.Foundation.Point _devPickAt;

    /// <summary>The world X/Z under a point in this control (DIPs), as drawn now; null without a map.</summary>
    public (double X, double Z)? DevWorldAt(Windows.Foundation.Point at) =>
        _scene is null ? null : _scene.Projection.ToWorld(_camera.ToMap(Pixels(at)));

    /// <summary>The point at these fractions of this control's size (a developer script's "place").</summary>
    public Windows.Foundation.Point DevPointAt(double fx, double fy) => new(ActualWidth * fx, ActualHeight * fy);

    // A press that picks a place: kept until release, which says whether it was a drag (a facing).
    private bool DevPress(PointerRoutedEventArgs e, Microsoft.UI.Input.PointerPoint point)
    {
        if (DevPick is null || !point.Properties.IsLeftButtonPressed || DevWorldAt(point.Position) is not { } world)
            return false;
        _devPickFrom = world;
        _devPickAt = point.Position;
        CapturePointer(e.Pointer);
        e.Handled = true;
        return true;
    }

    private bool DevRelease(PointerRoutedEventArgs e)
    {
        if (_devPickFrom is not { } from)
            return false;
        _devPickFrom = null;
        ReleasePointerCapture(e.Pointer);
        var at = e.GetCurrentPoint(this).Position;
        var dragged = Math.Abs(at.X - _devPickAt.X) + Math.Abs(at.Y - _devPickAt.Y) >= 6;
        DevPick?.Invoke(from, dragged ? DevWorldAt(at) : null);
        return true;
    }
#endif

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
#if DEVTOOLS
        if (DevPress(e, point))
            return;
#endif
        if (!point.Properties.IsLeftButtonPressed && !point.Properties.IsMiddleButtonPressed)
            return;
        _dragFrom = point.Position;
        _pressedAt = point.Position;
        _pressedOn = _hovered;
        _pressedEdge = _overEdge;
        _dragging = false;
        CapturePointer(e.Pointer);
        e.Handled = true;
    }

    /// <summary>A marker was clicked (pressed and released without dragging the map).</summary>
    public event Action<MapMarker>? MarkerClicked;

    private Windows.Foundation.Point _pressedAt;
    private MapMarker? _pressedOn;
    private bool _pressedEdge;
    private bool _dragging;

    private void Hover(MapMarker? marker, Windows.Foundation.Point at)
    {
        if (marker?.Id == _hovered?.Id)
            return;
        _hovered = marker;
        ProtectedCursor = marker is null && !_overEdge ? null : InputSystemCursor.Create(InputSystemCursorShape.Hand);
        MarkerHovered?.Invoke(marker, at);
    }

    /// <summary>Lets go of the marker under the pointer, as if the pointer had left the map.</summary>
    public void ClearHover() => Hover(null, default);

    // The edge arrow of an out-of-view player is a button: it shows them.
    private bool OverEdge(Windows.Foundation.Point at) =>
        _scene is not null && MapRenderer.EdgeOf(_camera, _scene, PixelScale) is { } edge
        && SKPoint.Distance(edge, Pixels(at)) <= MapRenderer.EdgeReach * PixelScale;

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
#if DEVTOOLS
        if (_devPickFrom is not null)
            return;
#endif
        if (_dragFrom is not { } from)
        {
            var at = e.GetCurrentPoint(this).Position;
            var overEdge = OverEdge(at);
            if (overEdge != _overEdge)
            {
                _overEdge = overEdge;
                ProtectedCursor = overEdge || _hovered is not null ? InputSystemCursor.Create(InputSystemCursorShape.Hand) : null;
                ToolTipService.SetToolTip(this, overEdge ? "Show my position (F)" : null);
            }
            Hover(_scene is null || overEdge ? null : MapRenderer.HitTest(_camera, _scene, Pixels(at), PixelScale), at);
            return;
        }
        var to = e.GetCurrentPoint(this).Position;
        // A few pixels of hand jitter during a click is not a drag (and must not stop following the player).
        if (!_dragging && Math.Abs(to.X - _pressedAt.X) + Math.Abs(to.Y - _pressedAt.Y) < 4)
            return;
        if (!_dragging)
        {
            _dragging = true;
            _dragStarted = DateTime.Now;
            _dragDistance = 0;
            Hover(null, to);
            // Dragging takes the view for now: a glide under way ends, and the next position brings the view back
            // (following stays on; owner, 2026-10-04).
            _follow.Dragged();
            StopGlide();
            _centerAfterFit = false;
        }
        _camera.Pan((float)(to.X - from.X) * PixelScale, (float)(to.Y - from.Y) * PixelScale);
        _dragDistance += Math.Sqrt((to.X - from.X) * (to.X - from.X) + (to.Y - from.Y) * (to.Y - from.Y));
        _dragFrom = to;
        Invalidate();
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
#if DEVTOOLS
        if (DevRelease(e))
            return;
#endif
        var click = !_dragging && _dragFrom is not null;
        var clicked = click ? _pressedOn : null;
        if (_dragging)
            Study.Ui("map.pan", ("px", _dragDistance), ("s", DateTime.Now - _dragStarted));
        _dragFrom = null;
        _dragging = false;
        ReleasePointerCapture(e.Pointer);
        if (click && _pressedEdge)
        {
            CenterOnPlayer();
            _overEdge = false;
            ToolTipService.SetToolTip(this, null);
            ProtectedCursor = null;
            EdgeClicked?.Invoke();
        }
        else if (clicked is not null)
        {
            MarkerClicked?.Invoke(clicked);
        }
    }

    private DateTime _dragStarted;
    private double _dragDistance;
    private double _wheelFactor = 1;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _wheelEnd;

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var factor = Math.Pow(1.0015, point.Properties.MouseWheelDelta);
        _camera.ZoomAt(Pixels(point.Position), factor);
        KeepPlayerCentered();
        Invalidate();
        e.Handled = true;

        // One study event per burst of wheel turns.
        _wheelFactor *= factor;
        if (_wheelEnd is null)
        {
            _wheelEnd = DispatcherQueue.CreateTimer();
            _wheelEnd.Interval = TimeSpan.FromMilliseconds(600);
            _wheelEnd.IsRepeating = false;
            _wheelEnd.Tick += (_, _) =>
            {
                Study.Ui("map.zoom", ("factor", _wheelFactor), ("how", "wheel"), ("zoom", _camera.Zoom));
                _wheelFactor = 1;
            };
        }
        _wheelEnd.Stop();
        _wheelEnd.Start();
    }

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        _camera.ZoomAt(Pixels(e.GetPosition(this)), 2);
        KeepPlayerCentered();
        Invalidate();
        Study.Ui("map.zoom", ("factor", 2.0), ("how", "double-click"), ("zoom", _camera.Zoom));
    }
}
