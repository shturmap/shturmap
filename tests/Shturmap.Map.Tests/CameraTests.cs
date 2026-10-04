using SkiaSharp;
using Shturmap.Core.Maps;

namespace Shturmap.Map.Tests;

// The view's zoom limit belongs to the map shown, and a fit needs a view to fit into (the review of 2026-10-04).
public class CameraTests
{
    private static readonly MapRect Large = new(0, 0, 1000, 800);
    private static readonly MapRect Small = new(0, 0, 400, 300);
    private static readonly SKPoint Middle = new(768, 520);

    private static Camera Window()
    {
        var camera = new Camera();
        camera.Resize(new SKSize(1536, 1040));
        return camera;
    }

    [Theory]
    [InlineData(1.1)]
    [InlineData(1 / 1.1)]
    public void After_a_look_at_another_map_the_wheel_doesnt_jump(double notch)
    {
        var camera = Window();
        camera.Fit(Large);
        var (center, zoom) = (camera.Center, camera.Zoom);
        // A preview of a smaller map: fitted, it leaves a zoom limit above the first map's whole view.
        camera.Fit(Small);
        Assert.True(camera.MinZoom > zoom);
        camera.Restore(center, zoom);

        camera.ZoomAt(Middle, notch);
        // One notch in is one notch in; one notch out stays where it was rather than jump in to the other map's limit.
        Assert.Equal(notch > 1 ? zoom * notch : zoom, camera.Zoom, 9);
    }

    [Fact]
    public void The_zoom_limit_is_the_drawn_maps()
    {
        // A sheet of 1000 × 1000 m at one pixel a metre in a 1000 px view: its whole view is 0.952, its limit half that.
        var (camera, scene) = TestView.Of([]);
        camera.Fit(new MapRect(0, 0, 100, 100));
        camera.Restore(new MapPoint(0, 0), 0.952);
        Assert.True(camera.MinZoom > 4);

        using var bitmap = new SKBitmap(1000, 1000);
        using var canvas = new SKCanvas(bitmap);
        MapRenderer.Render(canvas, camera, scene);
        Assert.Equal(0.476, camera.MinZoom, 3);
        // The wheel goes out to the map's own limit and no further.
        for (var i = 0; i < 20; i++)
            camera.ZoomAt(new SKPoint(500, 500), 1 / 1.2);
        Assert.Equal(0.476, camera.Zoom, 3);
    }

    [Fact]
    public void A_fit_into_a_view_without_room_waits_for_the_room()
    {
        var camera = new Camera();
        camera.Restore(new MapPoint(5, 5), 3);
        // Minimised, or not laid out yet.
        camera.Resize(new SKSize(0, 0));
        camera.Fit(Large, 24);
        Assert.Equal(3, camera.Zoom);

        camera.Resize(new SKSize(1048, 848));
        Assert.Equal(1, camera.Zoom, 9);
        Assert.Equal(new MapPoint(500, 400), camera.Center);
        Assert.Equal(0.5, camera.MinZoom, 9);

        // Done once: a later change of size keeps the view the player has by then.
        camera.ZoomAt(new SKPoint(524, 424), 2);
        camera.Resize(new SKSize(2000, 1000));
        Assert.Equal(2, camera.Zoom, 9);
    }

    [Fact]
    public void A_view_chosen_meanwhile_isnt_replaced_by_the_waiting_fit()
    {
        var camera = new Camera();
        camera.Resize(new SKSize(10, 10));
        camera.Fit(Large, 24);
        camera.Restore(new MapPoint(120, 80), 2.5);
        camera.Resize(new SKSize(1048, 848));
        Assert.Equal(2.5, camera.Zoom);
        Assert.Equal(new MapPoint(120, 80), camera.Center);
    }
}
