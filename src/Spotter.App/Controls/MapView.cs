using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Spotter.Map;

namespace Spotter.App.Controls;

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
        PointerWheelChanged += OnPointerWheelChanged;
        DoubleTapped += OnDoubleTapped;
    }

    /// <summary>Keep the player in view when a new position arrives. Turned off by dragging the map.</summary>
    public bool FollowPlayer { get; set; } = true;

    public event Action? FollowChanged;

    public MapScene? Scene => _scene;

    private float PixelScale => (float)(XamlRoot?.RasterizationScale ?? 1.0);

    /// <summary>Shows a new map, fitted to the window.</summary>
    public void SetScene(MapScene? scene)
    {
        _scene = scene;
        _fitPending = true;
        _followedFix = null;
        _panel.Invalidate();
    }

    /// <summary>Redraws after the scene's markers, player or floor changed; follows a new fix if enabled.</summary>
    public void Refresh()
    {
        if (_scene?.Player is { } fix && FollowPlayer && !_fitPending && !ReferenceEquals(fix, _followedFix))
            CenterOnPlayer(zoomIn: _followedFix is null);
        _panel.Invalidate();
    }

    public void CenterOnPlayer(bool zoomIn = false)
    {
        if (_scene?.Player is not { } fix)
            return;
        _followedFix = fix;
        _camera.CenterOn(_scene.Projection.ToMap(fix.Position));
        if (zoomIn)
            _camera.ZoomAt(new SKPoint(_camera.Viewport.Width / 2, _camera.Viewport.Height / 2), 3);
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
            Spotter.Session.AppLog.Info($"Map surface first paint: {e.BackendRenderTarget.Width}x{e.BackendRenderTarget.Height} px, GPU context {(e.Surface.Context is null ? "none" : "ok")}");
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
                CenterOnPlayer(zoomIn: true);
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
        CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragFrom is not { } from)
            return;
        var to = e.GetCurrentPoint(this).Position;
        _camera.Pan((float)(to.X - from.X) * PixelScale, (float)(to.Y - from.Y) * PixelScale);
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
        _dragFrom = null;
        ReleasePointerCapture(e.Pointer);
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        _camera.ZoomAt(Pixels(point.Position), Math.Pow(1.0015, point.Properties.MouseWheelDelta));
        _panel.Invalidate();
        e.Handled = true;
    }

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        _camera.ZoomAt(Pixels(e.GetPosition(this)), 2);
        _panel.Invalidate();
    }
}
