using SkiaSharp;
using Shturmap.Core;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// The squad grid on the map (owner, 2026-10-26: E, the squad grid; docs/DESIGN.md §4, "Map drawing", *The grid*). The test
// sheet has one metre to the pixel, world x to the right and z upward, ±500 m: its grid starts at its bounds (no fixed A1
// for a made-up map), ten squares of 100 px each way, A1 at the view's top left.
public class GridTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);

    // x −140, z 80: 360 m right of A1's edge (D, its middle third), 420 m down (row 5, its top third): D5-8.
    private static readonly WorldPoint InD5Top = new(-140, 0, 80);

    private static (Camera Camera, MapScene Scene) Gridded(params MapMarker[] markers)
    {
        var (camera, scene) = Of(markers);
        scene.ShowGrid = true;
        return (camera, scene);
    }

    [Fact]
    public void Without_the_setting_nothing_of_the_grid_is_laid_out()
    {
        var (camera, scene) = Of([]);
        scene.Player = new PlayerFix(InD5Top, null, Now);
        Assert.Null(MapRenderer.Layout(camera, scene, 1, Now).Grid);
        // The tag says nothing for a fresh position, as before the grid.
        Assert.Null(MapRenderer.PlayerAgeTag(camera, scene, 1, Now));
    }

    [Fact]
    public void The_lines_lie_100_metres_apart_and_the_strips_say_the_letters_and_numbers()
    {
        var (camera, scene) = Gridded();
        var grid = MapRenderer.Layout(camera, scene, 1, Now).Grid!;
        Assert.Equal(11, grid.Verticals.Length);
        Assert.All(grid.Verticals.Zip(grid.Verticals.Skip(1)), p => Assert.Equal(100, p.Second - p.First, 1));
        Assert.Equal(new SKRect(MapRenderer.GridStripWide, MapRenderer.GridStripHigh, 1000 - MapRenderer.GridStripWide, 1000 - MapRenderer.GridStripHigh), grid.Inner);
        Assert.Equal(["A", "B", "C", "D", "E", "F", "G", "H", "J", "K"], grid.Letters.Select(l => l.Text));
        Assert.Equal(Enumerable.Range(1, 10).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)), grid.Numbers.Select(n => n.Text));
    }

    [Fact]
    public void Each_square_says_its_reference_at_its_top_left_corner_unless_a_symbol_lies_there()
    {
        // A quest's symbol at D5's top-left corner (x −200, z 100), none anywhere else.
        var (camera, scene) = Gridded(Quest("a", -197, 97, ""));
        var grid = MapRenderer.Layout(camera, scene, 1, Now).Grid!;
        var said = grid.References.Select(r => r.Text).ToHashSet();
        // A1's corner holds the sheet's caption (a sheet's top left), so the references under it are left out too.
        Assert.DoesNotContain("A1", said);
        Assert.Contains("E1", said);
        Assert.Contains("K10", said);
        Assert.Contains("D4", said);
        Assert.DoesNotContain("D5", said);
        Assert.True(grid.LeftOut is >= 2 and <= 6, string.Join(", ", Enumerable.Range(0, 100).Select(i => Shturmap.Core.Maps.SquadGrid.Letter(i % 10) + (i / 10 + 1)).Where(r => !said.Contains(r))));
        // Each at its square's corner: E3's at x 400 + 4, its baseline under y 200.
        var e3 = grid.References.Single(r => r.Text == "E3").At;
        Assert.Equal(404, e3.X, 1);
        Assert.InRange(e3.Y, 200, 220);
    }

    [Fact]
    public void The_keypad_is_ticks_at_the_overview_and_lines_once_a_third_is_about_60_px()
    {
        var (camera, scene) = Gridded();
        // 100 px a square: a third is 33 px, below the fade's start.
        Assert.Equal(0, MapRenderer.Layout(camera, scene, 1, Now).Grid!.Keypad);
        // 180 px a square: a third is 60 px, the keypad's lines in whole.
        camera.Restore(new Shturmap.Core.Maps.MapPoint(0, 0), 1.8);
        Assert.Equal(1, MapRenderer.Layout(camera, scene, 1, Now).Grid!.Keypad, 3);
        // Between, fading in.
        camera.Restore(new Shturmap.Core.Maps.MapPoint(0, 0), 1.6);
        Assert.InRange(MapRenderer.Layout(camera, scene, 1, Now).Grid!.Keypad, 0.1, 0.9);
    }

    [Fact]
    public void The_players_tag_says_the_reference_and_its_age_once_it_has_one()
    {
        var (camera, scene) = Gridded();
        scene.Player = new PlayerFix(InD5Top, null, Now);
        Assert.Equal("D5-8", MapRenderer.PlayerAgeTag(camera, scene, 1, Now)!.Value.Text);
        Assert.Equal("D5-8 · 2 MIN OLD", MapRenderer.PlayerAgeTag(camera, scene, 1, Now + TimeSpan.FromMinutes(2.5))!.Value.Text);
        Assert.Equal("D5-8 · 1 H OLD", MapRenderer.PlayerAgeTag(camera, scene, 1, Now + TimeSpan.FromMinutes(75))!.Value.Text);
    }

    [Fact]
    public void The_readout_stands_at_the_top_left_inside_the_strips_and_no_label_covers_it()
    {
        // Quests crowding the map's top left, whose names would run under the readout.
        var markers = Enumerable.Range(0, 12).Select(i => Quest($"q{i}", -470 + (i % 4) * 40, 470 - (i / 4) * 16, $"A quest with a long name {i}")).ToArray();
        var (camera, scene) = Gridded(markers);
        scene.Player = new PlayerFix(InD5Top, null, Now - TimeSpan.FromMinutes(3));
        var layout = MapRenderer.Layout(camera, scene, 1, Now);
        var you = layout.Grid!.You!;
        Assert.Equal("D5-8", you.Reference);
        Assert.Equal("3 MIN OLD", you.Age);
        Assert.True(you.Stale);
        Assert.Equal(MapRenderer.GridStripWide + 8, you.Box.Left, 1);
        Assert.Equal(MapRenderer.GridStripHigh + 8, you.Box.Top, 1);
        Assert.NotEmpty(layout.Labels);
        Assert.All(layout.Labels, l => Assert.False(l.Box.IntersectsWith(you.Box), $"{l.Text} under the readout"));
        Assert.All(layout.Grid.References, r => Assert.False(you.Box.Contains(r.At), $"{r.Text} under the readout"));
    }

    [Fact]
    public void Out_of_a_raid_there_is_no_readout()
    {
        // No position shown (the menus, the replay): no "you", no readout, no reference on a tag.
        var (camera, scene) = Gridded();
        Assert.Null(MapRenderer.Layout(camera, scene, 1, Now).Grid!.You);
    }

    [Fact]
    public void No_label_stands_under_a_strip()
    {
        // Names at the very edges, which would otherwise run under the strips.
        var markers = new[] { Quest("l", -495, 0, "Left edge"), Quest("r", 470, 100, "Right edge long name"), Quest("t", 0, 495, "Top edge"), Quest("b", 100, -495, "Bottom edge") };
        var (camera, scene) = Gridded(markers);
        var layout = MapRenderer.Layout(camera, scene, 1, Now);
        var inner = layout.Grid!.Inner;
        Assert.All(layout.Labels, l => Assert.True(inner.Contains(l.Box), $"{l.Text} at {l.Box} under a strip"));
    }

    [Fact]
    public void The_scale_bar_and_the_edge_badge_step_inside_the_strips()
    {
        var (camera, scene) = Of([]);
        scene.Player = new PlayerFix(new WorldPoint(2000, 0, 0), null, Now);
        var plainBar = MapRenderer.Layout(camera, scene, 1, Now).Scale.Origin;
        var plainEdge = MapRenderer.EdgeOf(camera, scene, 1)!.Value;
        scene.ShowGrid = true;
        var bar = MapRenderer.Layout(camera, scene, 1, Now).Scale.Origin;
        var edge = MapRenderer.EdgeOf(camera, scene, 1)!.Value;
        Assert.Equal(plainBar.X + MapRenderer.GridStripWide, bar.X, 1);
        Assert.Equal(plainBar.Y - MapRenderer.GridStripHigh, bar.Y, 1);
        // Far to the right: the badge keeps its inset from the right strip.
        Assert.Equal(plainEdge.X - MapRenderer.GridStripWide, edge.X, 1);
        // A position under a strip is out of view: the badge points to it.
        scene.Player = new PlayerFix(new WorldPoint(490, 0, 0), null, Now);
        Assert.NotNull(MapRenderer.EdgeOf(camera, scene, 1));
    }

    [Fact]
    public void Turning_the_grid_on_lays_the_map_out_anew()
    {
        var (camera, scene) = Of([]);
        Assert.Null(MapRenderer.LayoutOf(camera, scene, 1, Now).Grid);
        scene.ShowGrid = true;
        Assert.NotNull(MapRenderer.LayoutOf(camera, scene, 1, Now).Grid);
        scene.ShowGrid = false;
        Assert.Null(MapRenderer.LayoutOf(camera, scene, 1, Now).Grid);
    }

    [Fact]
    public void The_sheets_caption_stands_whole_under_the_readout_where_this_lies_over_its_corner()
    {
        // UI QA pass of the squad grid, F3: on a sheet with a position the readout covered "NO ARTWO" of the caption.
        var (camera, scene) = Gridded();
        scene.Player = new PlayerFix(InD5Top, null, Now);
        // The sheet's corner under the readout: the view moved 40 px right and down.
        camera.Pan(40, 40);
        var you = MapRenderer.Layout(camera, scene, 1, Now).Grid!.You!;
        var caption = MapRenderer.SheetCaption(camera, scene, 1, you);
        Assert.False(caption.Box.IntersectsWith(you.Box), $"the caption at {caption.Box} under the readout at {you.Box}");
        Assert.Equal(40 + 10, caption.At.X, 1);
        Assert.True(caption.At.Y > you.Box.Bottom);
        // Without the readout, in its corner as before.
        Assert.Equal(40 + 18, MapRenderer.SheetCaption(camera, scene, 1, null).At.Y, 1);
    }

    [Fact]
    public void Under_a_plate_of_the_window_the_readout_isnt_drawn_and_keeps_its_room()
    {
        // UI QA pass of the squad grid, F5: the in-raid glance's plate left "YOU C" of the readout showing.
        var (camera, scene) = Gridded();
        scene.Player = new PlayerFix(InD5Top, null, Now);
        SKColor At(SKPoint p)
        {
            using var surface = SKSurface.Create(new SKImageInfo(1000, 1000));
            MapRenderer.Render(surface.Canvas, camera, scene, 1, null, Now);
            using var image = surface.Snapshot();
            using var bitmap = SKBitmap.FromImage(image);
            return bitmap.GetPixel((int)p.X, (int)p.Y);
        }
        var you = MapRenderer.Layout(camera, scene, 1, Now).Grid!.You!;
        // The readout's hairline at its left edge, then the sheet's panel there once a plate lies over part of it.
        var edge = new SKPoint(you.Box.Left, you.Box.MidY);
        var drawn = At(edge);
        scene.PlateOver = [SKRect.Create(you.Box.MidX, 0, 400, 300)];
        var covered = At(edge);
        Assert.True(drawn.Red > covered.Red + 10, $"{drawn} against {covered}");
        // Its room is still kept: no label is placed there while the plate stands.
        Assert.NotNull(MapRenderer.LayoutOf(camera, scene, 1, Now).Grid!.You);
        scene.PlateOver = [];
        Assert.Equal(drawn, At(edge));
    }

    [Fact]
    public void A_reference_keeps_a_labels_gap_from_what_stands_beside_it_and_keeps_off_the_windows_controls()
    {
        // UI QA pass of the squad grid, F7: "A8" stood 3 px before "Crash Site" on its line and read as part of it. E3's
        // reference is written from x 404, its box 2 px around it.
        var (camera, scene) = Gridded();
        Assert.Contains("E3", MapRenderer.LayoutOf(camera, scene, 1, Now).Grid!.References.Select(r => r.Text));
        using var font = new SKFont(SKTypeface.FromFamilyName("Bahnschrift", new SKFontStyle(SKFontStyleWeight.SemiBold, SKFontStyleWidth.SemiCondensed, SKFontStyleSlant.Upright)), 10.5f);
        var end = 404 + font.MeasureText("E3");
        // Something 3 px after it on its line (here a control of the window, which the references keep off too): left out.
        scene.ControlsOver = [SKRect.Create(end + 3, 203, 60, 12)];
        Assert.DoesNotContain("E3", MapRenderer.LayoutOf(camera, scene, 1, Now).Grid!.References.Select(r => r.Text));
        // 12 px after it, beyond a label's gap from its box: it stays.
        scene.ControlsOver = [SKRect.Create(end + 12, 203, 40, 12)];
        Assert.Contains("E3", MapRenderer.LayoutOf(camera, scene, 1, Now).Grid!.References.Select(r => r.Text));
    }

    [Fact]
    public void No_label_stands_under_the_windows_controls()
    {
        // UI QA pass of the squad grid, F2: inside the strips the credit ran over "Transit to Interchange". A quest whose name
        // goes right of it, where a control of the window then stands: the name takes another side.
        var (camera, scene) = Gridded(Quest("c", 0, -400, "Under the credit"));
        var free = MapRenderer.LayoutOf(camera, scene, 1, Now).Labels.Single();
        var control = SKRect.Inflate(free.Box, 4, 4);
        scene.ControlsOver = [control];
        var moved = MapRenderer.LayoutOf(camera, scene, 1, Now).Labels.Single();
        Assert.False(moved.Box.IntersectsWith(control), $"{moved.Text} at {moved.Box} under the control at {control}");
    }

    [Fact]
    public void A_view_frames_the_map_inside_the_strips()
    {
        // UI QA pass of the squad grid, F2: the map's controls stepped inside the strips, and the whole map with them, so at
        // 900x560 the raised credit no longer runs over the names at its foot.
        var (camera, scene) = Gridded();
        camera.Inset = MapRenderer.GridInset(scene, 1);
        camera.Fit(scene.Projection.WorldRect, 24, Camera.LabelRoom);
        SKRect Shown(Shturmap.Core.Maps.MapRect rect)
        {
            var a = camera.ToScreen(new Shturmap.Core.Maps.MapPoint(rect.Left, rect.Top));
            var b = camera.ToScreen(new Shturmap.Core.Maps.MapPoint(rect.Right, rect.Bottom));
            return new SKRect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }
        var whole = Shown(scene.Projection.WorldRect);
        Assert.True(whole.Left >= MapRenderer.GridStripWide + 24 - 0.5, $"{whole}");
        Assert.True(whole.Top >= MapRenderer.GridStripHigh + 24 - 0.5 && whole.Bottom <= 1000 - MapRenderer.GridStripHigh - 24 + 0.5, $"{whole}");
        Assert.True(whole.Right <= 1000 - MapRenderer.GridStripWide - Camera.LabelRoom + 0.5, $"{whole}");
        // A framing with a foot shorter than the strip keeps the strip; with no grid, the view's edge as before.
        var tall = new Shturmap.Core.Maps.MapRect(-100, -400, 100, 400);
        var (center, zoom) = camera.FramingAbove(tall, 40, 10);
        camera.Restore(center, zoom);
        Assert.True(Shown(tall).Bottom <= 1000 - MapRenderer.GridStripHigh - 40 + 0.5, $"{Shown(tall)}");
        camera.Inset = SKSize.Empty;
        (center, zoom) = camera.FramingAbove(tall, 40, 10);
        camera.Restore(center, zoom);
        Assert.Equal(1000 - 10 - 40, Shown(tall).Bottom, 1);
    }

    [Fact]
    public void A_frame_with_the_grid_draws_its_lines_lighter_than_the_ground_and_its_strips()
    {
        var (camera, scene) = Gridded();
        using var surface = SKSurface.Create(new SKImageInfo(1000, 1000));
        MapRenderer.Render(surface.Canvas, camera, scene, 1, null, Now);
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        var grid = scene.GridShown!;
        // On the sheet, a 100 m line's pixel is lighter than the panel beside it, with no hue of its own.
        var x = (int)grid.Verticals[5];
        var on = bitmap.GetPixel(x, 650);
        var beside = bitmap.GetPixel(x + 15, 650);
        Assert.True(on.Red > beside.Red + 20 && on.Green > beside.Green + 20, $"{on} against {beside}");
        Assert.InRange(Math.Abs((on.Red - beside.Red) - (on.Green - beside.Green)), 0, 6);
        // The strips are the ground, nearly opaque, over the map's edge.
        var strip = bitmap.GetPixel(500, 4);
        Assert.True(strip.Red < 20 && strip.Green < 20, $"{strip}");
    }
}
