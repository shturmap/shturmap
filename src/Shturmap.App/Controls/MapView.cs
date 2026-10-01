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
/// The view only moves when the player moves it: a new position pings where it is instead, or at the edge when it is
/// out of view. Drawing is in physical pixels; markers are scaled by the display's scale factor.
/// </summary>
public sealed partial class MapView : Grid
{
    private readonly SKSwapChainPanel _panel = new();
    private readonly Camera _camera = new();
    private MapScene? _scene;
    private bool _fitPending;
    private Windows.Foundation.Point? _dragFrom;
    private PlayerFix? _seenFix;

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

    /// <summary>Shows a new map, fitted to the window.</summary>
    public void SetScene(MapScene? scene)
    {
        _scene = scene;
        if (scene is not null)
            scene.Pulse = new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
        _fitPending = true;
        _seenFix = null;
        _panel.Invalidate();
    }

    /// <summary>
    /// Redraws after the scene's markers, player or floor changed. A new position doesn't move the view (owner,
    /// 2026-10-01): it pings, and says so when it is out of view.
    /// </summary>
    public void Refresh()
    {
        if (_scene is { Player: { } fix } scene && !ReferenceEquals(fix, _seenFix))
        {
            _seenFix = fix;
            if (DateTime.Now - fix.At < PingIfNewerThan)
            {
                scene.PingSince = DateTime.Now;
                // Before the first paint the view isn't fitted yet; the whole map will be in view then.
                PlayerPinged?.Invoke(!_fitPending && MapRenderer.EdgeOf(_camera, scene, PixelScale) is not null);
                Redraw();
            }
        }
        _panel.Invalidate();
    }

    /// <summary>
    /// Redraws without moving, e.g. to let an ageing fix fade. Keeps redrawing while the focus dimming eases in or
    /// out and while focused markers pulse.
    /// </summary>
    public void Redraw()
    {
        _panel.Invalidate();
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
            _animation?.Stop();
            return;
        }
        var now = DateTime.Now;
        var step = (float)((now - _lastStep) / DimEase);
        _lastStep = now;
        var target = DimTarget(scene);
        scene.Dim = scene.Dim < target ? Math.Min(target, scene.Dim + step) : Math.Max(target, scene.Dim - step);
        _panel.Invalidate();
    }

    /// <summary>Moves the view to the player's last position, keeping the zoom (F, the button, the edge arrow).</summary>
    public void CenterOnPlayer()
    {
        if (_scene?.Player is not { } fix)
            return;
        _camera.CenterOn(_scene.Projection.ToMap(fix.Position));
        _panel.Invalidate();
    }

    public void FitMap()
    {
        _fitPending = true;
        _panel.Invalidate();
    }

    public void ZoomBy(double factor)
    {
        _camera.ZoomAt(new SKPoint(_camera.Viewport.Width / 2, _camera.Viewport.Height / 2), factor);
        _panel.Invalidate();
    }

    /// <summary>Draws the current view into a PNG (developer snapshot; the GPU surface itself can't be read back).</summary>
    public void SaveSnapshot(string path)
    {
        var size = _camera.Viewport;
        using var surface = SKSurface.Create(new SKImageInfo(Math.Max(1, (int)size.Width), Math.Max(1, (int)size.Height)));
        if (_scene is null)
            surface.Canvas.Clear(SKColor.Parse("#0e1413"));
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
            Shturmap.Session.AppLog.Info($"Map surface first paint: {e.BackendRenderTarget.Width}x{e.BackendRenderTarget.Height} px, GPU context {(e.Surface.Context is null ? "none" : "ok")}");
        _camera.Resize(new SKSize(e.BackendRenderTarget.Width, e.BackendRenderTarget.Height));
        if (_scene is null)
        {
            canvas.Clear(SKColor.Parse("#0e1413"));
            return;
        }
        if (_fitPending)
        {
            _fitPending = false;
            _camera.Fit(_scene.Projection.WorldRect, 24 * PixelScale);
        }
        MapRenderer.Render(canvas, _camera, _scene, PixelScale);
    }

    private SKPoint Pixels(Windows.Foundation.Point p) => new((float)p.X * PixelScale, (float)p.Y * PixelScale);

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
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

    // The edge arrow of an out-of-view player is a button: it shows them.
    private bool OverEdge(Windows.Foundation.Point at) =>
        _scene is not null && MapRenderer.EdgeOf(_camera, _scene, PixelScale) is { } edge
        && SKPoint.Distance(edge, Pixels(at)) <= MapRenderer.EdgeReach * PixelScale;

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
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
        }
        _camera.Pan((float)(to.X - from.X) * PixelScale, (float)(to.Y - from.Y) * PixelScale);
        _dragDistance += Math.Sqrt((to.X - from.X) * (to.X - from.X) + (to.Y - from.Y) * (to.Y - from.Y));
        _dragFrom = to;
        _panel.Invalidate();
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
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
        _panel.Invalidate();
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
        _panel.Invalidate();
        Study.Ui("map.zoom", ("factor", 2.0), ("how", "double-click"), ("zoom", _camera.Zoom));
    }
}
