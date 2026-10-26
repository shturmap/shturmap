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
        _zoomFrames?.Stop();
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

    // A screenshot counts as just taken when it is recent: not an old one found when the app starts. Only then does its
    // position ping and its extract list ring the extracts out (MainWindow).
    internal static readonly TimeSpan JustTaken = TimeSpan.FromSeconds(30);

    private bool _overEdge;

    /// <summary>The pointer moved onto a marker (or off all markers: null), with its position in this control.</summary>
    public event Action<MapMarker?, Windows.Foundation.Point>? MarkerHovered;

    private MapMarker? _hovered;

    public MapScene? Scene => _scene;

    private float PixelScale => (float)(XamlRoot?.RasterizationScale ?? 1.0);

    // The size of the map's labels and symbols (MapRenderer's uiScale): the screen's pixels per DIP, times the text size
    // (Windows' text size and settings' step, TextScale.Effective; docs/DESIGN.md §4, "Map drawing"), which WinUI's own
    // text scaling doesn't reach in a swap chain.
    private float UiScale => PixelScale * (float)TextScale.Effective;

    // The camera told the text size before it works out a view: the label room's share of the view grows with it
    // (Camera.TextScale; QA pass of 2026-10-19, F2).
    // And the grid's strips: what a view frames lies inside them (Camera.Inset; UI QA pass of the squad grid, F2).
    private Camera Sized()
    {
        _camera.TextScale = (float)TextScale.Effective;
        _camera.Inset = _scene is { } scene ? MapRenderer.GridInset(scene, UiScale) : SKSize.Empty;
        return _camera;
    }

    /// <summary>Pings and pulses play even with Windows' animation effects off (the website demo's recording).</summary>
    public bool AlwaysAnimate { get; set; }

    /// <summary>
    /// The linked highlight's breath (owner, 2026-10-17, "B"; docs/DESIGN.md §4, *Motion*): one clock for every scene this
    /// view shows and for the rail's rows (Linked.Breath), so the halos on the map and the glyphs in the rows breathe in
    /// phase. It breathes only with Windows' animation effects on (or <see cref="AlwaysAnimate"/>) and never in a
    /// snapshot run (<see cref="StaticHighlight"/>).
    /// </summary>
    public LinkedBreath Breath { get; } = new();

    /// <summary>A snapshot run ("--snapshot"): a linked set stands in its static look, as with the animation effects off.</summary>
    public bool StaticHighlight
    {
        get => _staticHighlight;
        set
        {
            _staticHighlight = value;
            Breath.Effects = BreathEffects();
        }
    }

    private bool _staticHighlight;

    private bool BreathEffects() => !_staticHighlight && (AlwaysAnimate || new Windows.UI.ViewManagement.UISettings().AnimationsEnabled);

    /// <summary>
    /// The squad grid on every scene this view shows (owner, 2026-10-26; settings' "Grid on the map", on unless unticked;
    /// docs/DESIGN.md §4, "Map drawing", *The grid*); a change redraws the scene shown at once.
    /// </summary>
    public bool ShowGrid
    {
        get => _showGrid;
        set
        {
            _showGrid = value;
            if (_scene is not null)
            {
                _scene.ShowGrid = value;
                Redraw();
            }
        }
    }

    private bool _showGrid = true;

    /// <summary>
    /// Where a plate of the window lies over the map for a while (DIPs in this view; the in-raid glance's), null while none
    /// does: the player's reference at the map's top left isn't drawn under it (<see cref="MapScene.PlateOver"/>), on every
    /// scene this view shows; a change redraws at once.
    /// </summary>
    public Windows.Foundation.Rect? PlateOver
    {
        get => _plateOver;
        set
        {
            _plateOver = value;
            if (_scene is not null)
            {
                _scene.PlateOver = PlatePixels();
                Invalidate();
            }
        }
    }

    private Windows.Foundation.Rect? _plateOver;

    private SKRect? PlatePixels() => _plateOver is { } r ? Pixels(r) : null;

    private SKRect Pixels(Windows.Foundation.Rect r) =>
        SKRect.Create((float)r.X * PixelScale, (float)r.Y * PixelScale, (float)r.Width * PixelScale, (float)r.Height * PixelScale);

    /// <summary>
    /// Where the window's own controls stand over the map (DIPs in this view: the WIKI MAP link with the credit, the buttons
    /// with the floor list): no label or name is placed under them, and the squares' references are left out there
    /// (<see cref="MapScene.ControlsOver"/>), on every scene this view shows; a change lays the view out anew.
    /// </summary>
    public IReadOnlyList<Windows.Foundation.Rect> ControlsOver
    {
        get => _controlsOver;
        set
        {
            _controlsOver = value;
            if (_scene is not null)
            {
                _scene.ControlsOver = ControlPixels();
                Invalidate();
            }
        }
    }

    private IReadOnlyList<Windows.Foundation.Rect> _controlsOver = [];

    private IReadOnlyList<SKRect> ControlPixels() => _controlsOver.Select(Pixels).ToList();

    // The maps whose grid starts at tarkov.dev's bounds for want of a fixed A1 (a map added since; GridOrigins), said once
    // each in the app log.
    private readonly HashSet<string> _gridUnfixedSaid = [];

    /// <summary>Shows a new map, fitted to the window, or at a view saved earlier with <see cref="View"/>.</summary>
    public void SetScene(MapScene? scene, (Shturmap.Core.Maps.MapPoint Center, double Zoom)? view = null)
    {
        if (scene is not null)
        {
            scene.ShowGrid = _showGrid;
            scene.PlateOver = PlatePixels();
            scene.ControlsOver = ControlPixels();
            if (!scene.Grid.Fixed && _gridUnfixedSaid.Add(scene.Definition.Key))
                Shturmap.Session.AppLog.Warn($"Map grid: no fixed A1 for the map '{scene.Definition.Key}'; its grid starts at tarkov.dev's bounds until it gets one (GridOrigins)");
        }
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
        // A move of the view still running belongs to the scene before (the replay's or the tour's glide): left running,
        // it would carry on moving the new one (review of 2026-10-09).
        StopViewAnimation();
        StopGlide();
        _scene = scene;
        if (scene is not null)
        {
            scene.Pulse = AlwaysAnimate || new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
            Breath.Effects = BreathEffects();
            scene.Breath = Breath;
        }
        _fitPending = view is null;
        if (view is { } v)
            _camera.Restore(v.Center, v.Zoom);
        _seenFix = null;
        // Following, a new map opens on the player (after it is fitted, at its first paint).
        _centerAfterFit = Following;
        Invalidate();
        // A scene that comes with its extracts already ringing or flying draws them on.
        if (scene is { ExitsArriving: true })
            Redraw();
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

    /// <summary>
    /// While set, following stands still: a new position pings where it is, and neither it nor a new scene moves the
    /// view. The tour sets it while it is up (owner, 2026-10-15, on What's New's tour:5 line: "the highlight does weird
    /// movements and highlights the wrong things sometimes"): it frames each chapter's example itself, above its band,
    /// and YOUR SCREENSHOT KEY's example position, set right after the chapter framed it, glided off that framing
    /// over 2.4 s, under a cut-out placed where the position was before the glide. An example is nobody's to follow.
    /// </summary>
    public bool FollowHeld { get; set; }

    private bool Following => _follow.On && !FollowHeld;

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
    /// Whether <see cref="View"/> is a view of a map: one is drawn, and fitted. Before that the camera holds its default
    /// (centre 0, zoom 1), which is nobody's view: a preview that kept it would bring the map back there instead of
    /// fitting it (review of 2026-10-09: the tour at a first start opened before the map was drawn).
    /// </summary>
    public bool HasView => _scene is not null && !_fitPending;

    /// <summary>
    /// Redraws after the scene's markers, player or floor changed. A new position pings; following, the view glides to
    /// it, otherwise it stays where the player put it and says so when the position is out of view.
    /// </summary>
    public void Refresh()
    {
        if (_scene is { Player: { } fix } scene && !ReferenceEquals(fix, _seenFix))
        {
            _seenFix = fix;
            // A move of the view under way (the roll call's zoom out to the extracts just read, owner, 2026-10-15) is let
            // finish: a glide started now would pull its centre one way while the move takes it another. The position
            // pings all the same, and the next one glides.
            if (Following && _viewAnimation is null)
            {
                // Before the first paint the view has no size: it starts on the position then. After it, it glides.
                _centerAfterFit = _fitPending;
                if (!_fitPending)
                    GlideToPlayer();
            }
            if (Shturmap.Core.Logs.WallClock.Elapsed(fix.At, DateTime.Now) < JustTaken)
            {
                scene.PingSince = DateTime.Now;
                // Before the first paint the view isn't fitted yet; the whole map will be in view then. Following, the
                // view is on its way to the position, so it is never out of view.
                PlayerPinged?.Invoke(!_follow.On && !_fitPending && MapRenderer.EdgeOf(_camera, scene, UiScale) is not null);
                Redraw();
            }
        }
        Invalidate();
    }

    /// <summary>
    /// Redraws without moving, e.g. to let an ageing fix fade. Keeps redrawing while the focus dimming eases in or
    /// out, while focused markers pulse and while a linked set breathes or settles.
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
        scene.Pulsing || scene.BreathingAt(DateTime.Now) || scene.Pinging || scene.LeavingNow || scene.ExitsArriving || Math.Abs(scene.Dim - DimTarget(scene)) > 0.001f;

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
        StopZoom();
        _centerAfterFit = false;
        _fitPending = true;
        Invalidate();
    }

    /// <summary>+ and −, the zoom buttons: zooms by <paramref name="factor"/> about the view's middle, over 0.2 s
    /// (<see cref="ZoomAt"/>).</summary>
    public void ZoomBy(double factor) =>
        ZoomAt(new SKPoint(_camera.Viewport.Width / 2, _camera.Viewport.Height / 2), factor, WheelZoom.Step);

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

    /// <summary>Stops a move of the view under way (<see cref="AnimateView"/>, or a zoom of the wheel, the keys, the
    /// buttons or a double-click) where it is.</summary>
    public void StopViewAnimation()
    {
        StopZoom();
        if (_viewAnimation is null)
            return;
        CompositionTarget.Rendering -= _viewAnimation;
        _viewAnimation = null;
    }

    /// <summary>The view that shows these world positions with <paramref name="padding"/> (DIPs) around them, and the
    /// label room on their right (<see cref="Camera.FramingAbove"/>).</summary>
    public (Shturmap.Core.Maps.MapPoint Center, double Zoom) FramingOf(IReadOnlyCollection<Shturmap.Core.WorldPoint> points, double padding)
    {
        var map = points.Select(p => _scene!.Projection.ToMap(p)).ToList();
        var rect = new Shturmap.Core.Maps.MapRect(map.Min(p => p.X), map.Min(p => p.Y), map.Max(p => p.X), map.Max(p => p.Y));
        return Sized().Framing(rect, (float)(padding * PixelScale), Camera.LabelRoom * UiScale);
    }

    /// <summary>
    /// The view of <paramref name="scene"/> that shows these world positions, with <paramref name="padding"/> DIPs
    /// around them and the label room on their right (<see cref="Camera.FramingAbove"/>), at least
    /// <paramref name="minMetres"/> across, in the part of this view above <paramref name="foot"/> DIPs at its foot (the
    /// raid replay's band), and under <paramref name="head"/> DIPs at its top (the preview label, in the tour).
    /// </summary>
    public (Shturmap.Core.Maps.MapPoint Center, double Zoom) FramingAbove(MapScene scene, IReadOnlyCollection<Shturmap.Core.WorldPoint> points,
        double padding, double foot, double minMetres, double head = 0)
    {
        var x = points.Average(p => p.X);
        var z = points.Average(p => p.Z);
        var room = points.Append(new(x - minMetres / 2, 0, z - minMetres * 0.3)).Append(new(x + minMetres / 2, 0, z + minMetres * 0.3));
        var map = room.Select(p => scene.Projection.ToMap(p)).ToList();
        var rect = new Shturmap.Core.Maps.MapRect(map.Min(p => p.X), map.Min(p => p.Y), map.Max(p => p.X), map.Max(p => p.Y));
        return Sized().FramingAbove(rect, (float)(padding * PixelScale), (float)(foot * PixelScale), Camera.LabelRoom * UiScale, (float)(head * PixelScale));
    }

    /// <summary>
    /// Where the roll call takes the view (owner, 2026-10-15, from the design panel: option A, "Roll call, then fly-out";
    /// <see cref="Camera.CallOut"/>): every one of <paramref name="extracts"/> and the <paramref name="player"/> in view,
    /// <paramref name="padding"/> DIPs around them and the label room on their right, in the part of this view between
    /// <paramref name="head"/> DIPs at its top and <paramref name="foot"/> DIPs at its foot (the tour's label and band). A
    /// zoom out only, and no move at all where they are all in view already (Moves false; the view as it is, or where a
    /// Follow glide under way ends it, which then goes on): a fit that zoomed out nothing still took them into the view's
    /// middle (coordinator, 2026-10-15: on Customs it cut off the map's west and left empty map at the top). Null without a
    /// scene, before the view is laid out and fitted, or with no room for the padding. The window moves the view there with
    /// <see cref="AnimateView"/>, or <see cref="Jump"/> with animation effects off.
    /// </summary>
    public ((Shturmap.Core.Maps.MapPoint Center, double Zoom) View, bool Moves)? CallOutView(IReadOnlyCollection<Shturmap.Core.WorldPoint> extracts,
        Shturmap.Core.WorldPoint? player, double foot = 0, double padding = Camera.CallOutPadding, double head = 0)
    {
        if (_scene is not { } scene || _fitPending)
            return null;
        var scale = PixelScale;
        // Inside the grid's strips too, where the head and the foot don't reach further (Camera.Inset).
        var inset = Sized().Inset;
        var footPixels = Math.Max((float)(foot * scale), inset.Height);
        var headPixels = Math.Max((float)(head * scale), inset.Height);
        // The part between the head and the foot, as a view of its own: its middle lies (foot - head) / 2 above the view's.
        var between = new SKSize(Math.Max(0, _camera.Viewport.Width - 2 * inset.Width), Math.Max(0, _camera.Viewport.Height - footPixels - headPixels));
        double Shift(double zoom) => (footPixels - headPixels) / 2 / zoom;
        var settled = _glide is { } glide ? (glide.To, _camera.Zoom) : View;
        var inPart = (new Shturmap.Core.Maps.MapPoint(settled.Item1.X, settled.Item1.Y - Shift(settled.Item2)), settled.Item2);
        var points = extracts.Select(e => scene.Projection.ToMap(e)).ToList();
        Shturmap.Core.Maps.MapPoint? you = player is { } at ? scene.Projection.ToMap(at) : null;
        if (Camera.CallOut(points, you, between, inPart, Camera.LabelRoom * UiScale, (float)(padding * scale), _camera.MinZoom, (float)TextScale.Effective) is not { } fit)
            return null;
        if (!fit.Moves)
            return (settled, false);
        return ((new Shturmap.Core.Maps.MapPoint(fit.View.Center.X, fit.View.Center.Y + Shift(fit.View.Zoom)), fit.View.Zoom), true);
    }

    /// <summary>Whether a view (a centre and zoom, as <see cref="View"/> gives one) shows a world position, in the part of
    /// this view above <paramref name="foot"/> DIPs at its foot.</summary>
    public bool Shows((Shturmap.Core.Maps.MapPoint Center, double Zoom) view, Shturmap.Core.WorldPoint world, double foot = 0)
    {
        if (_scene is null)
            return false;
        var p = _scene.Projection.ToMap(world);
        var x = (p.X - view.Center.X) * view.Zoom + _camera.Viewport.Width / 2;
        var y = (p.Y - view.Center.Y) * view.Zoom + _camera.Viewport.Height / 2;
        return x >= 0 && x <= _camera.Viewport.Width && y >= 0 && y <= _camera.Viewport.Height - foot * PixelScale;
    }

    /// <summary>Puts the view at a centre and zoom at once (the replay with animation effects off).</summary>
    public void Jump((Shturmap.Core.Maps.MapPoint Center, double Zoom) view)
    {
        StopViewAnimation();
        StopGlide();
        _fitPending = false;
        _centerAfterFit = false;
        _camera.Restore(view.Center, view.Zoom);
        Invalidate();
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
            MapRenderer.Render(surface.Canvas, Sized(), _scene, UiScale);
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
        Sized().Resize(new SKSize(e.BackendRenderTarget.Width, e.BackendRenderTarget.Height));
        if (_scene is null)
        {
            canvas.Clear(Palette.Sk(Palette.Ground));
            return;
        }
        if (_fitPending)
        {
            _fitPending = false;
            _camera.Fit(_scene.Projection.WorldRect, 24 * PixelScale, Camera.LabelRoom * UiScale);
        }
        // Following: once the view has its size (and a new map is fitted), it starts on the player.
        if (_centerAfterFit && FollowState.Target(_scene, _camera, DateTime.Now) is { } player)
        {
            _centerAfterFit = false;
            _follow.Centred();
            _camera.CenterOn(player);
        }
        GlideStep();
        ZoomStep();
        MapRenderer.Render(canvas, _camera, _scene, UiScale);
        // The scale bar keeps its place while the view only moves, and settles in the first frame drawn with the view still
        // (MapRenderer.Scale). The end of a move draws no frame after its last by itself (AnimateView, the roll call's and
        // the tour's; a zoom of the wheel, the keys, the buttons or a double-click; a Follow glide; a drag), so a frame
        // whose bar held asks for one more, after it.
        if (_scene.ScaleBarShown is { Held: true })
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, Invalidate);
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
        _scene is not null && MapRenderer.EdgeOf(_camera, _scene, UiScale) is { } edge
        && SKPoint.Distance(edge, Pixels(at)) <= MapRenderer.EdgeReach * UiScale;

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
                ToolTipService.SetToolTip(this, overEdge ? AppTexts.MapEdgeTip : null);
            }
            var hit = _scene is null || overEdge ? null : MapRenderer.HitTest(_camera, _scene, Pixels(at), UiScale);
            Hover(hit, at);
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

    // ---- the zoom, animated: the wheel's (owner, 2026-10-15), the keys', the buttons' and a double-click's (owner,
    // 2026-10-15, evening; WheelZoom) ----

    private readonly WheelZoom _zoom = new();
    private SKPoint _zoomAt;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _zoomFrames;

    /// <summary>
    /// Zooms by <paramref name="factor"/> over <paramref name="over"/>, eased, keeping <paramref name="at"/> (pixels in
    /// this view: the point under the pointer, or the view's middle) in place; following with the view on the player,
    /// about the player. One during another goes on from it. So its frames come under the rule that holds the symbols
    /// steady while the view moves (docs/DESIGN.md "Map drawing"); at once, a zoom laid them out afresh. With Windows'
    /// animation effects off it zooms at once. While the view moves by itself (the replay's glide, the roll call's), each
    /// of that move's frames puts the view where the move has it, and a zoom lasted a frame: it is left out then (false).
    /// </summary>
    private bool ZoomAt(SKPoint at, double factor, TimeSpan over)
    {
        if (_viewAnimation is not null)
            return false;
        _zoomAt = at;
        _zoom.Turn(_camera.Zoom, factor, _camera.MinZoom, _camera.MaxZoom, DateTime.Now, _scene?.Pulse == true, over);
        ZoomStep();
        if (_zoom.Running)
        {
            // Frames at the system timer's pace, as a glide's (GlideToPlayer).
            if (_zoomFrames is null)
            {
                _zoomFrames = DispatcherQueue.CreateTimer();
                _zoomFrames.Interval = TimeSpan.FromMilliseconds(15);
                _zoomFrames.Tick += (_, _) =>
                {
                    ZoomStep();
                    Invalidate();
                };
            }
            _zoomFrames.Start();
        }
        Invalidate();
        return true;
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true;
        var point = e.GetCurrentPoint(this);
        var factor = WheelZoom.FactorOf(point.Properties.MouseWheelDelta);
        // A notch zooms over 0.15 s, at the pointer.
        if (!ZoomAt(Pixels(point.Position), factor, WheelZoom.Notch))
            return;

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

    // As a frame is drawn (and on each tick, so it lands unseen too): the zoom at this moment of the zoom under way, its
    // point in place; following with the view on the player, about the player.
    private void ZoomStep()
    {
        if (!_zoom.Running)
            return;
        var now = DateTime.Now;
        _camera.ZoomAt(_zoomAt, _zoom.At(now) / _camera.Zoom);
        KeepPlayerCentered();
        if (_zoom.Over(now))
            StopZoom();
    }

    private void StopZoom()
    {
        _zoom.Stop();
        _zoomFrames?.Stop();
    }

    // A double-click zooms in twice as far over 0.2 s, at the pointer.
    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ZoomAt(Pixels(e.GetPosition(this)), 2, WheelZoom.Step))
            Study.Ui("map.zoom", ("factor", 2.0), ("how", "double-click"), ("zoom", _zoom.Target));
    }
}
