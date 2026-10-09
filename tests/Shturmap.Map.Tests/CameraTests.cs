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
        // A sheet of 1000 × 1000 m at one pixel a metre in a 1000 px view: its whole view is 0.826 (24 px on the left,
        // the label room's 150 on the right), its limit half that.
        var (camera, scene) = TestView.Of([]);
        camera.Fit(new MapRect(0, 0, 100, 100));
        camera.Restore(new MapPoint(0, 0), 0.826);
        Assert.True(camera.MinZoom > 4);

        using var bitmap = new SKBitmap(1000, 1000);
        using var canvas = new SKCanvas(bitmap);
        MapRenderer.Render(canvas, camera, scene);
        Assert.Equal(0.413, camera.MinZoom, 3);
        // The wheel goes out to the map's own limit and no further.
        for (var i = 0; i < 20; i++)
            camera.ZoomAt(new SKPoint(500, 500), 1 / 1.2);
        Assert.Equal(0.413, camera.Zoom, 3);
    }

    // The map writes a symbol's label to its right: the whole map and every framing keep room for it there, the other
    // sides their padding (owner, 2026-10-09: on Customs "Transit to I…" and "Crossr…" ran off the window).
    [Fact]
    public void A_wide_map_leaves_the_label_room_on_its_right()
    {
        var camera = new Camera();
        camera.Resize(new SKSize(1200, 820));
        camera.Fit(new MapRect(0, 0, 2000, 800), 24, Camera.LabelRoom);
        // A place at the map's right edge has the label room between it and the view's edge.
        Assert.Equal(1200 - Camera.LabelRoom, camera.ToScreen(new MapPoint(2000, 400)).X, 2);
        Assert.Equal(24, camera.ToScreen(new MapPoint(0, 400)).X, 2);
        // The zoom limit is half this whole view, as before.
        Assert.Equal(camera.Zoom * 0.5, camera.MinZoom, 9);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.5)]
    public void The_label_room_is_capped_in_a_small_view(float scale)
    {
        // The smallest window leaves the map about 500 DIP wide: 150 of them would shrink a wide map by more than a
        // quarter, so the room is a fifth of the width there (at any display scale).
        var camera = new Camera();
        camera.Resize(new SKSize(500 * scale, 480 * scale));
        camera.Fit(new MapRect(0, 0, 2000, 800), 24 * scale, Camera.LabelRoom * scale);
        Assert.Equal(400 * scale, camera.ToScreen(new MapPoint(2000, 400)).X, 2);
        Assert.Equal(24 * scale, camera.ToScreen(new MapPoint(0, 400)).X, 2);
    }

    [Fact]
    public void A_map_with_room_beside_it_moves_only_as_far_as_the_labels_need()
    {
        var camera = new Camera();
        camera.Resize(new SKSize(1200, 820));
        // A tall map (Streets): 386 px wide in the view, with more than the label room on either side; it stays in the
        // middle.
        camera.Fit(new MapRect(0, 0, 500, 1000), 24, Camera.LabelRoom);
        Assert.Equal(250, camera.Center.X, 6);
        Assert.Equal(600, camera.ToScreen(new MapPoint(250, 500)).X, 2);
        // Nearly as wide as the view: centred, its right edge would be 137 px from the view's; it moves left by 13.
        camera.Fit(new MapRect(0, 0, 1200, 1000), 24, Camera.LabelRoom);
        Assert.Equal(1200 - Camera.LabelRoom, camera.ToScreen(new MapPoint(1200, 500)).X, 2);
        Assert.True(camera.ToScreen(new MapPoint(0, 500)).X > 24);
    }

    [Fact]
    public void A_framing_above_a_band_keeps_the_label_room()
    {
        // The tour: places framed with 90 px around them, above a band of 200 px at the view's foot.
        var camera = new Camera();
        camera.Resize(new SKSize(1200, 820));
        camera.Restore(new MapPoint(0, 0), 1);
        var (center, zoom) = camera.FramingAbove(new MapRect(100, 100, 700, 300), 90, 200, Camera.LabelRoom);
        camera.Restore(center, zoom);
        Assert.Equal(1200 - Camera.LabelRoom, camera.ToScreen(new MapPoint(700, 200)).X, 2);
        Assert.Equal(90, camera.ToScreen(new MapPoint(100, 200)).X, 2);
        // The middle of what is framed is in the middle of the part above the band.
        Assert.Equal((820 - 200) / 2, camera.ToScreen(new MapPoint(400, 200)).Y, 2);
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
