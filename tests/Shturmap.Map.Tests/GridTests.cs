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

    // ---- under the pointer (owner, 2026-10-26: "When you are with the mouse over the map highlight the current square and
    // sub-square and show the currently mouse-overed position somewhere where it makes sense") ----

    // D5-8's middle on the test sheet: 360 px right (world x −140), 415 px down (z 85).
    private static readonly SKPoint OverD5Top = new(350, 415);

    private static SKBitmap Drawn(Camera camera, MapScene scene)
    {
        using var surface = SKSurface.Create(new SKImageInfo(1000, 1000));
        MapRenderer.Render(surface.Canvas, camera, scene, 1, null, Now);
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    private static int Light(SKColor c) => c.Red + c.Green + c.Blue;

    [Fact]
    public void The_pointer_has_the_square_and_ninth_under_it()
    {
        var (camera, scene) = Gridded();
        scene.Pointer = OverD5Top;
        var at = MapRenderer.PointerOf(camera, scene, 1)!;
        Assert.Equal("D5-8", at.Reference.ToString());
        // D5 is 300 to 400 across and 400 to 500 down; its top middle ninth a third of that, at its top.
        Assert.Equal(new SKRect(300, 400, 400, 500), Rounded(at.Square));
        Assert.Equal(new SKRect(333, 400, 367, 433), Rounded(at.Ninth));
        // Each ninth of D5, as on a number pad: 7 at the top left, 3 at the bottom right.
        foreach (var (x, y, digit) in new[] { (310f, 410f, 7), (390f, 410f, 9), (350f, 450f, 5), (310f, 490f, 1), (390f, 490f, 3) })
        {
            scene.Pointer = new SKPoint(x, y);
            Assert.Equal($"D5-{digit}", MapRenderer.PointerOf(camera, scene, 1)!.Reference.ToString());
        }
    }

    private static SKRect Rounded(SKRect r) => new(MathF.Round(r.Left), MathF.Round(r.Top), MathF.Round(r.Right), MathF.Round(r.Bottom));

    [Fact]
    public void At_the_maps_edges_the_pointer_has_the_corner_squares_and_over_a_strip_or_off_the_grid_none()
    {
        var (camera, scene) = Gridded();
        var inner = MapRenderer.Layout(camera, scene, 1, Now).Grid!.Inner;
        // Just inside the strips at the top left: A1's top left ninth; at the bottom right, K10's bottom right.
        scene.Pointer = new SKPoint(inner.Left + 1, inner.Top + 1);
        Assert.Equal("A1-7", MapRenderer.PointerOf(camera, scene, 1)!.Reference.ToString());
        scene.Pointer = new SKPoint(inner.Right - 1, inner.Bottom - 1);
        Assert.Equal("K10-3", MapRenderer.PointerOf(camera, scene, 1)!.Reference.ToString());
        // Over a strip: off the map, nothing lit.
        scene.Pointer = new SKPoint(inner.Left - 5, 500);
        Assert.Null(MapRenderer.PointerOf(camera, scene, 1));
        scene.Pointer = new SKPoint(500, inner.Bottom + 5);
        Assert.Null(MapRenderer.PointerOf(camera, scene, 1));
        // Left of A and above row 1 (the view moved right and down): no reference there, as for a position.
        camera.Pan(200, 200);
        scene.Pointer = new SKPoint(150, 500);
        Assert.Null(MapRenderer.PointerOf(camera, scene, 1));
        scene.Pointer = new SKPoint(500, 150);
        Assert.Null(MapRenderer.PointerOf(camera, scene, 1));
        // A1 now 200 to 300 each way: its middle.
        scene.Pointer = new SKPoint(250, 250);
        Assert.Equal("A1-5", MapRenderer.PointerOf(camera, scene, 1)!.Reference.ToString());
        // Off the map altogether.
        scene.Pointer = null;
        Assert.Null(MapRenderer.PointerOf(camera, scene, 1));
    }

    [Fact]
    public void The_square_and_its_ninth_are_lit_lighter_the_ninth_more_and_nothing_else()
    {
        var (camera, scene) = Gridded();
        using var plain = Drawn(camera, scene);
        scene.Pointer = OverD5Top;
        using var lit = Drawn(camera, scene);
        // In the ninth, in the rest of the square, outside it: lighter, a little lighter, the same. No hue of its own.
        var (ninth, square, outside) = (new SKPoint(350, 420), new SKPoint(320, 470), new SKPoint(250, 470));
        int Step(SKPoint p) => Light(lit.GetPixel((int)p.X, (int)p.Y)) - Light(plain.GetPixel((int)p.X, (int)p.Y));
        Assert.True(Step(square) > 6, $"the square {Step(square)}");
        Assert.True(Step(ninth) > Step(square) + 6, $"the ninth {Step(ninth)}, the square {Step(square)}");
        Assert.Equal(0, Step(outside));
        var c = lit.GetPixel(350, 420);
        var was = plain.GetPixel(350, 420);
        Assert.InRange(Math.Abs((c.Red - was.Red) - (c.Green - was.Green)), 0, 3);
        // The 100 m line at its left keeps its own pixel.
        Assert.Equal(plain.GetPixel(300, 470), lit.GetPixel(300, 470));
    }

    [Fact]
    public void The_strips_light_the_column_and_row_under_the_pointer()
    {
        var (camera, scene) = Gridded();
        using var plain = Drawn(camera, scene);
        scene.Pointer = OverD5Top;
        using var lit = Drawn(camera, scene);
        var grid = scene.GridShown!;
        // Where the letter D stands on the top strip and the number 5 on the left one, more light than before; C and 4 as
        // they were.
        int Ink(SKBitmap b, float x, float y, int half = 7)
        {
            var sum = 0;
            for (var dx = -half; dx <= half; dx++)
            {
                for (var dy = -6; dy <= 6; dy++)
                    sum += Light(b.GetPixel((int)x + dx, (int)y + dy));
            }
            return sum;
        }
        var d = grid.Letters.Single(l => l.Text == "D").At;
        var c = grid.Letters.Single(l => l.Text == "C").At;
        var five = grid.Numbers.Single(n => n.Text == "5").At;
        var four = grid.Numbers.Single(n => n.Text == "4").At;
        var top = grid.Inner.Top / 2;
        var left = grid.Inner.Left / 2;
        Assert.True(Ink(lit, d, top) > Ink(plain, d, top) + 300, $"D {Ink(lit, d, top)} against {Ink(plain, d, top)}");
        Assert.True(Ink(lit, left, five) > Ink(plain, left, five) + 300, $"5 {Ink(lit, left, five)} against {Ink(plain, left, five)}");
        Assert.Equal(Ink(plain, c, top), Ink(lit, c, top));
        Assert.Equal(Ink(plain, left, four), Ink(lit, left, four));
        // The column's span on the bottom strip a step lighter (Raised) away from its letter, the next column's not.
        Assert.Equal(Palette.Sk(Palette.Raised), lit.GetPixel(305, 995));
        Assert.Equal(plain.GetPixel(405, 995), lit.GetPixel(405, 995));
    }

    [Fact]
    public void The_readout_says_the_pointers_square_alone_out_of_a_raid_and_under_YOU_in_one()
    {
        var (camera, scene) = Gridded();
        var inner = MapRenderer.Layout(camera, scene, 1, Now).Grid!.Inner;
        scene.Pointer = OverD5Top;
        // Out of a raid: the only line, in YOU's place 8 inside the strips.
        var alone = MapRenderer.PointerOf(camera, scene, 1)!;
        Assert.True(alone.LineShown);
        Assert.Equal(inner.Left + 8, alone.Line.Left, 1);
        Assert.Equal(inner.Top + 8, alone.Line.Top, 1);
        using (var drawn = Drawn(camera, scene))
            Assert.Equal(Palette.Sk(Palette.LineStrong), drawn.GetPixel((int)alone.Line.Left, (int)alone.Line.MidY));
        // In a raid with a position: under YOU's line, on its plate, YOU where it stood.
        scene.Player = new PlayerFix(new WorldPoint(300, 0, -300), null, Now - TimeSpan.FromMinutes(3));
        var you = MapRenderer.Layout(camera, scene, 1, Now).Grid!.You!;
        var under = MapRenderer.PointerOf(camera, scene, 1)!;
        Assert.Equal("D5-8", under.Reference.ToString());
        Assert.Equal(you.Box.Left, under.Line.Left, 1);
        Assert.InRange(under.Line.Top, you.Box.Top + you.Box.Height / 2, you.Box.Bottom);
        Assert.Equal(inner.Top + 8, you.Box.Top, 1);
        using (var drawn = Drawn(camera, scene))
        {
            // One plate for both: its hairline runs down past YOU's foot to the line's.
            Assert.Equal(Palette.Sk(Palette.LineStrong), drawn.GetPixel((int)you.Box.Left, (int)under.Line.MidY));
            Assert.Equal(Palette.Sk(Palette.LineStrong), drawn.GetPixel((int)you.Box.Left + 4, (int)under.Line.Bottom - 1));
        }
        // The pointer gone: YOU alone again, the plate ending at its foot.
        scene.Pointer = null;
        using (var drawn = Drawn(camera, scene))
            Assert.NotEqual(Palette.Sk(Palette.LineStrong), drawn.GetPixel((int)you.Box.Left + 4, (int)under.Line.Bottom - 1));
    }

    [Fact]
    public void The_readouts_line_gives_way_to_the_pointer_on_it_and_to_a_plate_of_the_window()
    {
        var (camera, scene) = Gridded();
        scene.Pointer = OverD5Top;
        var line = MapRenderer.PointerOf(camera, scene, 1)!.Line;
        // The pointer on the line itself: the square under it still lit, the line not drawn, so it covers nothing pointed at.
        scene.Pointer = new SKPoint(line.MidX, line.MidY);
        var on = MapRenderer.PointerOf(camera, scene, 1)!;
        Assert.False(on.LineShown);
        using (var drawn = Drawn(camera, scene))
            Assert.NotEqual(Palette.Sk(Palette.LineStrong), drawn.GetPixel((int)line.Left, (int)line.MidY));
        // A plate of the window over it (the glance's, a note at the map's top): not drawn, as YOU isn't.
        scene.Pointer = OverD5Top;
        scene.PlateOver = [SKRect.Create(line.MidX, 0, 300, 100)];
        Assert.False(MapRenderer.PointerOf(camera, scene, 1)!.LineShown);
        scene.PlateOver = [];
        Assert.True(MapRenderer.PointerOf(camera, scene, 1)!.LineShown);
    }

    [Fact]
    public void With_the_grid_off_the_pointer_draws_nothing()
    {
        var (camera, scene) = Of([]);
        using var plain = Drawn(camera, scene);
        scene.Pointer = OverD5Top;
        Assert.Null(MapRenderer.PointerOf(camera, scene, 1));
        Assert.False(MapRenderer.PointerMoved(camera, scene, 1));
        using var pointed = Drawn(camera, scene);
        Assert.Null(scene.PointerShown);
        Assert.Equal(plain.Pixels, pointed.Pixels);
    }

    [Fact]
    public void A_move_of_the_pointer_lays_nothing_out_and_draws_only_a_new_ninth()
    {
        var (camera, scene) = Gridded(Quest("a", -100, 100, "A quest"));
        scene.Pointer = OverD5Top;
        using (Drawn(camera, scene)) { }
        var layout = scene.LastLayout!.Value.Layout;
        Assert.Equal(("D5-8", true), (scene.PointerShown!.Value.Reference.ToString(), scene.PointerShown.Value.LineShown));
        // Within the same ninth: no frame needed; into the next ninth, the next square, or off the map: one.
        scene.Pointer = new SKPoint(352, 418);
        Assert.False(MapRenderer.PointerMoved(camera, scene, 1));
        scene.Pointer = new SKPoint(380, 418);
        Assert.True(MapRenderer.PointerMoved(camera, scene, 1));
        scene.Pointer = new SKPoint(450, 418);
        Assert.True(MapRenderer.PointerMoved(camera, scene, 1));
        scene.Pointer = null;
        Assert.True(MapRenderer.PointerMoved(camera, scene, 1));
        // And the frame drawn for it keeps the layout it had: nothing placed anew.
        scene.Pointer = new SKPoint(450, 418);
        using (Drawn(camera, scene)) { }
        Assert.Same(layout, scene.LastLayout!.Value.Layout);
        Assert.Equal("E5-8", scene.PointerShown!.Value.Reference.ToString());
    }
}
