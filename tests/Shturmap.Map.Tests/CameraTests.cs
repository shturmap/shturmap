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

    // ---- the roll call's view: zoomed out to every extract on the list and the player (owner, 2026-10-15, from the
    // design panel: option A, "Roll call, then fly-out") ----

    private static readonly SKSize RollCallView = new(1200, 820);

    // Where a view puts a map point on screen.
    private static SKPoint On((MapPoint Center, double Zoom) view, MapPoint p, SKSize size)
    {
        var camera = new Camera();
        camera.Resize(size);
        camera.Restore(view.Center, view.Zoom);
        return camera.ToScreen(p);
    }

    // Inside the view, with the padding on the left, top and bottom and the label room on the right (half a pixel of
    // rounding either way).
    private static void Inside(SKPoint p, SKSize size, float padding, float right)
    {
        Assert.InRange(p.X, padding - 0.5, size.Width - right + 0.5);
        Assert.InRange(p.Y, padding - 0.5, size.Height - padding + 0.5);
    }

    [Fact]
    public void The_roll_calls_view_holds_every_extract_and_the_player_with_the_padding_and_the_label_room()
    {
        MapPoint[] exits = [new(100, 200), new(700, 260), new(380, 440), new(520, 90)];
        var player = new MapPoint(40, 300);
        var view = Camera.FittingAll(exits, player, RollCallView, Camera.LabelRoom, Camera.CallOutPadding, 0.01, 64)!.Value;
        foreach (var p in exits.Append(player))
            Inside(On(view, p, RollCallView), RollCallView, Camera.CallOutPadding, Camera.LabelRoom);
        // A fit, not just any view that holds them: the rightmost touches the label room, the leftmost the padding (a
        // wide spread: its width decides).
        Assert.Equal(RollCallView.Width - Camera.LabelRoom, On(view, new MapPoint(700, 260), RollCallView).X, 2);
        Assert.Equal(Camera.CallOutPadding, On(view, player, RollCallView).X, 2);

        // Without a position, the extracts alone.
        var alone = Camera.FittingAll(exits, null, RollCallView, Camera.LabelRoom, Camera.CallOutPadding, 0.01, 64)!.Value;
        Assert.Equal(Camera.CallOutPadding, On(alone, new MapPoint(100, 200), RollCallView).X, 2);
        foreach (var p in exits)
            Inside(On(alone, p, RollCallView), RollCallView, Camera.CallOutPadding, Camera.LabelRoom);
    }

    [Fact]
    public void A_single_far_extract_is_framed_with_the_player()
    {
        var player = new MapPoint(500, 400);
        var far = new MapPoint(-2500, 3100);
        var view = Camera.FittingAll([far], player, RollCallView, Camera.LabelRoom, Camera.CallOutPadding, 0.01, 64)!.Value;
        Inside(On(view, far, RollCallView), RollCallView, Camera.CallOutPadding, Camera.LabelRoom);
        Inside(On(view, player, RollCallView), RollCallView, Camera.CallOutPadding, Camera.LabelRoom);
        // Tall: the two lie on the top and bottom padding.
        Assert.Equal(Camera.CallOutPadding, On(view, player, RollCallView).Y, 2);
        Assert.Equal(RollCallView.Height - Camera.CallOutPadding, On(view, far, RollCallView).Y, 2);
    }

    [Fact]
    public void Points_already_in_view_are_framed_too_but_never_past_the_zoom_limits()
    {
        // The map's whole view at 0.9 px per unit, its limits as every frame sets them: out to half of that, in to 64.
        var camera = new Camera();
        camera.Resize(RollCallView);
        camera.Fit(new MapRect(0, 0, 1200, 800), 24, Camera.LabelRoom);
        var whole = camera.Zoom;
        Assert.Equal(whole * 0.5, camera.MinZoom, 9);

        // Close together and all in view: framed, which here zooms in, as a fit does.
        MapPoint[] near = [new(500, 400), new(620, 430)];
        var view = camera.FittingAll(near, new MapPoint(530, 390), Camera.LabelRoom, Camera.CallOutPadding)!.Value;
        Assert.True(view.Zoom > whole, $"{view.Zoom} against the whole map's {whole}");
        Assert.True(view.Zoom <= camera.MaxZoom);
        Assert.Equal(RollCallView.Width - Camera.LabelRoom, On(view, new MapPoint(620, 430), RollCallView).X, 2);

        // One extract right by the player: not filling the window, held at the limit, the two in the middle.
        var by = camera.FittingAll([new MapPoint(500, 400)], new MapPoint(500.5, 400.2), Camera.LabelRoom, Camera.CallOutPadding)!.Value;
        Assert.Equal(camera.MaxZoom, by.Zoom, 9);
        Inside(On(by, new MapPoint(500, 400), RollCallView), RollCallView, Camera.CallOutPadding, Camera.LabelRoom);

        // Wider than the limit out allows (the whole map would need less than half its zoom): held there, never further.
        var wide = camera.FittingAll([new MapPoint(-3000, 0), new MapPoint(4000, 800)], null, Camera.LabelRoom, Camera.CallOutPadding)!.Value;
        Assert.Equal(camera.MinZoom, wide.Zoom, 9);
    }

    [Fact]
    public void Nothing_to_frame_or_no_room_gives_no_view()
    {
        Assert.Null(Camera.FittingAll([], null, RollCallView, Camera.LabelRoom, Camera.CallOutPadding, 0.01, 64));
        // A minimised window, or one not laid out yet: the view stays as it is.
        Assert.Null(Camera.FittingAll([new MapPoint(1, 1)], null, new SKSize(0, 0), Camera.LabelRoom, Camera.CallOutPadding, 0.01, 64));
        // The player alone is something to frame.
        Assert.NotNull(Camera.FittingAll([], new MapPoint(1, 1), RollCallView, Camera.LabelRoom, Camera.CallOutPadding, 0.01, 64));
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
