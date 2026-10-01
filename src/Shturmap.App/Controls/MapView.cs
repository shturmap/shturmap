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
/// The map surface: draws a <see cref="MapScene"/> on the GPU and handles pan (drag), zoom (wheel, double-click)
/// and following the player. Drawing is in physical pixels; markers are scaled by the display's scale factor.
/// </summary>
public sealed partial class MapView : Grid
{
    private readonly SKSwapChainPanel _panel = new();
    private readonly Camera _camera = new();
    private MapScene? _scene;
    private bool _fitPending;
    private Windows.Foundation.Point? _dragFrom;
    private PlayerFix? _followedFix;

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
            if (_hovered is not null)
                e.Handled = true;
        };
    }

    /// <summary>Keep the player in view when a new position arrives. Turned off by dragging the map.</summary>
    public bool FollowPlayer { get; set; } = true;

    public event Action? FollowChanged;

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
        _followedFix = null;
        _panel.Invalidate();
    }

    /// <summary>Redraws after the scene's markers, player or floor changed; frames a new fix if following.</summary>
    public void Refresh()
    {
        if (_scene?.Player is { } fix && FollowPlayer && !_fitPending && !ReferenceEquals(fix, _followedFix))
            CenterOnPlayer(frame: true);
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

    private static float DimTarget(MapScene scene) => scene.Focus.Count > 0 ? 1f : 0f;

    private static bool Animating(MapScene scene) =>
        scene.Focus.Count > 0 || Math.Abs(scene.Dim - DimTarget(scene)) > 0.001f;

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

    /// <param name="frame">Fit the player and the nearest open objective (or about 150 m around) into view.</param>
    public void CenterOnPlayer(bool frame = false)
    {
        if (_scene?.Player is not { } fix)
            return;
        _followedFix = fix;
        var at = _scene.Projection.ToMap(fix.Position);
        if (!frame)
        {
            _camera.CenterOn(at);
            _panel.Invalidate();
            return;
        }
        // A fix comes now and then, so each one should answer "where am I and where to next" at a glance.
        var step = _scene.Projection.ToMap(fix.Position.X + 1, fix.Position.Z);
        var unitsPerMeter = Math.Sqrt((step.X - at.X) * (step.X - at.X) + (step.Y - at.Y) * (step.Y - at.Y));
        var target = _scene.Markers
            .Where(m => m.Objective is not null && m.Kind is MarkerKind.Objective or MarkerKind.PossibleLocation)
            .Where(m => fix.Position.HorizontalDistanceTo(m.Position) < 600)
            .MinBy(m => fix.Position.HorizontalDistanceTo(m.Position));
        var reach = 150 * unitsPerMeter;
        var points = new List<Shturmap.Core.Maps.MapPoint> { at };
        if (target is not null)
            points.Add(_scene.Projection.ToMap(target.Position));
        var rect = new Shturmap.Core.Maps.MapRect(
            Math.Min(points.Min(p => p.X), at.X - reach / 2), Math.Min(points.Min(p => p.Y), at.Y - reach / 2),
            Math.Max(points.Max(p => p.X), at.X + reach / 2), Math.Max(points.Max(p => p.Y), at.Y + reach / 2));
        _camera.Frame(rect, 90 * PixelScale);
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
            if (FollowPlayer && _scene.Player is not null)
                CenterOnPlayer(frame: true);
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
        _dragging = false;
        CapturePointer(e.Pointer);
        e.Handled = true;
    }

    /// <summary>A marker was clicked (pressed and released without dragging the map).</summary>
    public event Action<MapMarker>? MarkerClicked;

    private Windows.Foundation.Point _pressedAt;
    private MapMarker? _pressedOn;
    private bool _dragging;

    private void Hover(MapMarker? marker, Windows.Foundation.Point at)
    {
        if (marker?.Id == _hovered?.Id)
            return;
        _hovered = marker;
        ProtectedCursor = marker is null ? null : InputSystemCursor.Create(InputSystemCursorShape.Hand);
        MarkerHovered?.Invoke(marker, at);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragFrom is not { } from)
        {
            var at = e.GetCurrentPoint(this).Position;
            Hover(_scene is null ? null : MapRenderer.HitTest(_camera, _scene, Pixels(at), PixelScale), at);
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
        if (FollowPlayer)
        {
            FollowPlayer = false;
            FollowChanged?.Invoke();
        }
        _panel.Invalidate();
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var clicked = !_dragging && _dragFrom is not null ? _pressedOn : null;
        if (_dragging)
            Study.Ui("map.pan", ("px", _dragDistance), ("s", DateTime.Now - _dragStarted));
        _dragFrom = null;
        _dragging = false;
        ReleasePointerCapture(e.Pointer);
        if (clicked is not null)
            MarkerClicked?.Invoke(clicked);
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
