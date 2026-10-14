using Shturmap.Core;
using SkiaSharp;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// The extracts the game's own list names this raid, once a screenshot showed it (owner, 2026-10-05; docs/DESIGN.md,
// "Map drawing"): the ones on it are lit, a glow in their colour and their names in bold; the others are struck out and
// faint, with a fainter name; one the game marks "??:??:??" stays solid without the glow, a "?" in its triangle (owner,
// 2026-10-14). A list just read rings the extracts on it out, one after another, and a later one of a run of
// screenshots adds what it lit anew.
public class ExitStateTests
{
    private static readonly SKColor Green = Palette.Sk(Palette.Green);

    private static MapMarker Exit(string id = "extract:a", MarkerKind kind = MarkerKind.ExtractPmc) => new(id, kind, new WorldPoint(0, 0, 0), "Crossroads");

    private static bool IsGreen(SKColor c) => Math.Abs(c.Red - Green.Red) < 40 && Math.Abs(c.Green - Green.Green) < 40 && Math.Abs(c.Blue - Green.Blue) < 40;

    // The marker stands in the middle of a 1000 px view; its symbol's pixels around there.
    private static (SKColor Middle, int GreenPixels) Drawn(MapScene scene, Camera camera)
    {
        using var bitmap = new SKBitmap(1000, 1000);
        using (var canvas = new SKCanvas(bitmap))
            MapRenderer.Render(canvas, camera, scene);
        var green = 0;
        for (var y = 480; y < 520; y++)
        {
            for (var x = 480; x < 520; x++)
            {
                if (IsGreen(bitmap.GetPixel(x, y)))
                    green++;
            }
        }
        return (bitmap.GetPixel(500, 500), green);
    }

    private static SKBitmap Render(MapScene scene, Camera camera)
    {
        var bitmap = new SKBitmap(1000, 1000);
        using var canvas = new SKCanvas(bitmap);
        MapRenderer.Render(canvas, camera, scene);
        return bitmap;
    }

    // Pixels tinted green in a box around a point of the 1000 px view.
    private static int Tinted(SKBitmap bitmap, int x, int y, int r = 2)
    {
        var count = 0;
        for (var i = x - r; i <= x + r; i++)
        {
            for (var j = y - r; j <= y + r; j++)
            {
                var c = bitmap.GetPixel(i, j);
                if (c.Green > c.Red + 8 && c.Green > c.Blue + 20)
                    count++;
            }
        }
        return count;
    }

    // Dark pixels (the ground's) in the middle of the triangle, where a "?" stands.
    private static int Dark(SKBitmap bitmap)
    {
        var count = 0;
        for (var x = 497; x <= 503; x++)
        {
            for (var y = 497; y <= 504; y++)
            {
                var c = bitmap.GetPixel(x, y);
                if (c.Red + c.Green + c.Blue < 120)
                    count++;
            }
        }
        return count;
    }

    [Fact]
    public void An_extract_is_solid_until_a_list_says_it_isnt_the_players()
    {
        var (camera, scene) = Of([Exit()]);
        using (var solid = Render(scene, camera))
        {
            Assert.True(IsGreen(solid.GetPixel(496, 504)));
            // Above its left side, outside the triangle: its collar's dark.
            Assert.Equal(0, Tinted(solid, 494, 493, 1));
        }

        scene.ExitsNotListed = new HashSet<string> { "extract:a" };
        using (var struck = Render(scene, camera))
        {
            // Its outline is still there, faint, in its colour.
            Assert.True(Tinted(struck, 500, 500, 12) > 30, $"{Tinted(struck, 500, 500, 12)} tinted pixels");
            // Not filled: dark inside, below the stroke.
            Assert.False(IsGreen(struck.GetPixel(496, 504)));
            // Struck out (owner, 2026-10-14): the stroke reaches past the triangle at its upper left.
            Assert.True(Tinted(struck, 494, 493, 1) > 2, $"{Tinted(struck, 494, 493, 1)} tinted pixels of the stroke");
        }

        scene.ExitsNotListed = new HashSet<string>();
        using (var again = Render(scene, camera))
            Assert.True(IsGreen(again.GetPixel(496, 504)));
    }

    [Fact]
    public void Only_the_exits_named_are_hollow()
    {
        var (camera, scene) = Of([Exit(), new MapMarker("extract:b", MarkerKind.ExtractPmc, new WorldPoint(200, 0, 0), "Trailer Park")]);
        scene.ExitsNotListed = new HashSet<string> { "extract:b" };
        var layout = MapRenderer.Layout(camera, scene, 1);
        Assert.False(layout.Markers.Single(m => m.Marker.Id == "extract:a").NotListed);
        Assert.True(layout.Markers.Single(m => m.Marker.Id == "extract:b").NotListed);
        Assert.True(IsGreen(Drawn(scene, camera).Middle));
    }

    [Fact]
    public void An_unlisted_exits_name_is_fainter()
    {
        var (camera, scene) = Of([Exit()]);
        var ink = Assert.Single(MapRenderer.Layout(camera, scene, 1).Labels).Color;
        scene.ExitsNotListed = new HashSet<string> { "extract:a" };
        var faint = Assert.Single(MapRenderer.Layout(camera, scene, 1).Labels).Color;
        Assert.Equal(Palette.Sk(Palette.Ink), ink);
        Assert.Equal(Palette.Sk(Palette.Muted), faint);
    }

    [Fact]
    public void An_exit_the_game_marks_with_question_marks_carries_one_in_its_triangle()
    {
        var (camera, scene) = Of([Exit()]);
        Assert.False(Assert.Single(MapRenderer.Layout(camera, scene, 1).Markers).Unsure);
        int plain;
        using (var bitmap = Render(scene, camera))
            plain = Dark(bitmap);
        scene.ExitsUnsure = new HashSet<string> { "extract:a" };
        var shown = Assert.Single(MapRenderer.Layout(camera, scene, 1).Markers);
        Assert.True(shown.Unsure);
        Assert.False(shown.NotListed);
        // Still solid: it is on the list. The "?" is dark in its middle (owner, 2026-10-14: at its corner it went
        // unseen), and no badge stands out at its corner, so the repel sees its triangle alone.
        Assert.True(Drawn(scene, camera).GreenPixels > 50, $"{Drawn(scene, camera).GreenPixels} green pixels");
        using (var bitmap = Render(scene, camera))
            Assert.True(Dark(bitmap) > plain + 6, $"{Dark(bitmap)} dark pixels in the triangle, {plain} without the ?");
        Assert.Equal(MapRenderer.RestHalf(shown.Marker, 1), MapRenderer.RepelHalf(shown, 1));
    }

    [Fact]
    public void An_unlisted_exits_name_yields_to_one_on_the_list()
    {
        // The one the list leaves out first in the data: the name of the "??:??:??" one is placed first all the same
        // (labels are placed in order, and the first takes its place beside its symbol).
        var (camera, scene) = Of([Exit("extract:b"), new MapMarker("extract:a", MarkerKind.ExtractPmc, new WorldPoint(0, 0, -40), "Trailer Park")]);
        scene.ExitsNotListed = new HashSet<string> { "extract:b" };
        scene.ExitsUnsure = new HashSet<string> { "extract:a" };
        var order = MapRenderer.Layout(camera, scene, 1).Labels.Select(l => l.Of.Marker.Id).ToList();
        Assert.Equal(["extract:a", "extract:b"], order);
    }

    // Pixels tinted green in a ring just outside the triangle and its collar (8 to 18 px from its middle): the glow's.
    private static int Glow(MapScene scene, Camera camera)
    {
        using var bitmap = new SKBitmap(1000, 1000);
        using (var canvas = new SKCanvas(bitmap))
            MapRenderer.Render(canvas, camera, scene);
        var tinted = 0;
        for (var y = 480; y < 520; y++)
        {
            for (var x = 480; x < 520; x++)
            {
                var d = Math.Sqrt((x - 500) * (x - 500) + (y - 500) * (y - 500));
                var c = bitmap.GetPixel(x, y);
                if (d is >= 13 and <= 18 && c.Green > c.Red + 4 && c.Green > c.Blue + 8)
                    tinted++;
            }
        }
        return tinted;
    }

    [Fact]
    public void An_exit_on_the_list_is_lit_and_one_marked_with_question_marks_is_not()
    {
        var (camera, scene) = Of([Exit()]);
        var plain = Glow(scene, camera);
        scene.ExitsListed = new HashSet<string> { "extract:a" };
        Assert.True(Assert.Single(MapRenderer.Layout(camera, scene, 1).Markers).Listed);
        var lit = Glow(scene, camera);
        Assert.True(lit > plain + 100, $"{lit} tinted pixels around a listed exit, {plain} around a plain one");
        // Solid as before: the glow lies under it.
        Assert.True(IsGreen(Drawn(scene, camera).Middle));

        // One marked "??:??:??" isn't lit: only its "?" badge, at the lower left, is in that ring.
        scene.ExitsListed = new HashSet<string>();
        scene.ExitsUnsure = new HashSet<string> { "extract:a" };
        var unsure = Glow(scene, camera);
        Assert.True(unsure < lit / 3, $"{unsure} tinted pixels around an exit marked ??:??:??, {lit} around a listed one");
    }

    [Fact]
    public void An_exit_on_the_list_says_its_name_in_bold_ink()
    {
        var (camera, scene) = Of([Exit()]);
        Assert.False(Assert.Single(MapRenderer.Layout(camera, scene, 1).Labels).Bold);
        scene.ExitsListed = new HashSet<string> { "extract:a" };
        var label = Assert.Single(MapRenderer.Layout(camera, scene, 1).Labels);
        Assert.True(label.Bold);
        Assert.Equal(Palette.Sk(Palette.Ink), label.Color);
    }

    [Fact]
    public void A_change_of_the_exits_states_makes_a_new_layout()
    {
        var (_, scene) = Of([Exit()]);
        var before = scene.LayoutVersion;
        scene.ExitsNotListed = new HashSet<string> { "extract:a" };
        Assert.True(scene.LayoutVersion > before);
        before = scene.LayoutVersion;
        scene.ExitsUnsure = new HashSet<string> { "extract:a" };
        Assert.True(scene.LayoutVersion > before);
        before = scene.LayoutVersion;
        scene.ExitsListed = new HashSet<string> { "extract:a" };
        Assert.True(scene.LayoutVersion > before);
    }

    // ---- a list just read rings its extracts out (owner, 2026-10-14) ----

    private static MapMarker ExitAt(string id, double x) => new(id, MarkerKind.ExtractPmc, new WorldPoint(x, 0, 0), id);

    [Fact]
    public void A_list_read_at_once_rings_the_lit_ones_nearest_first_then_the_unsure_ones()
    {
        var (_, scene) = Of([ExitAt("extract:far", 300), ExitAt("extract:near", 50), ExitAt("extract:unsure", 10), ExitAt("extract:closed", 20)]);
        scene.Player = new PlayerFix(new WorldPoint(0, 0, 0), null, DateTime.Now);
        scene.ExitsListed = new HashSet<string> { "extract:far", "extract:near" };
        scene.ExitsUnsure = new HashSet<string> { "extract:unsure" };
        scene.ExitsNotListed = new HashSet<string> { "extract:closed" };
        var now = DateTime.Now;
        scene.RingExits(now);
        var turn = MapRenderer.ExitTurn;
        Assert.Equal(new ExitRing(now, true), scene.ExitsRinging["extract:near"]);
        Assert.Equal(new ExitRing(now + turn, true), scene.ExitsRinging["extract:far"]);
        // Nearer than both, but "??:??:??": after the lit ones.
        Assert.Equal(new ExitRing(now + 2 * turn, false), scene.ExitsRinging["extract:unsure"]);
        // One the list leaves out doesn't ring: it is simply struck out.
        Assert.False(scene.ExitsRinging.ContainsKey("extract:closed"));
        Assert.True(scene.ExitsArriving);
    }

    [Fact]
    public void A_long_list_keeps_its_turns_short()
    {
        var exits = Enumerable.Range(0, 10).Select(i => ExitAt($"extract:{i}", i * 10)).ToList();
        var (_, scene) = Of(exits);
        scene.ExitsListed = exits.Select(e => e.Id).ToHashSet();
        var now = DateTime.Now;
        scene.RingExits(now);
        Assert.Equal(now + MapRenderer.ExitTurn * (MapRenderer.ExitTurns - 1), scene.ExitsRinging.Values.Max(r => r.At));
    }

    [Fact]
    public void A_later_screenshot_of_a_run_adds_only_what_it_lit_anew_after_the_last_turn()
    {
        var (_, scene) = Of([ExitAt("extract:a", 10), ExitAt("extract:b", 20), ExitAt("extract:c", 30)]);
        var first = DateTime.Now;
        scene.ExitsListed = new HashSet<string> { "extract:a" };
        scene.ExitsUnsure = new HashSet<string> { "extract:b" };
        scene.RingExits(first);
        var b = scene.ExitsRinging["extract:b"];

        // Half a second later, while they still ring: the next list names one more, and the game no longer marks b.
        var later = first + TimeSpan.FromSeconds(0.5);
        scene.ExitsListed = new HashSet<string> { "extract:a", "extract:b", "extract:c" };
        scene.ExitsUnsure = new HashSet<string>();
        scene.RingExits(later);
        // What already rings carries on, from where it was.
        Assert.Equal(new ExitRing(first, true), scene.ExitsRinging["extract:a"]);
        // What is new, or lit anew, comes after: never before the last turn, never in the past.
        Assert.Equal(new ExitRing(later, true), scene.ExitsRinging["extract:b"]);
        Assert.Equal(new ExitRing(later + MapRenderer.ExitTurn, true), scene.ExitsRinging["extract:c"]);
        Assert.True(scene.ExitsRinging["extract:b"].At > b.At);

        // The same list again at once: nothing changes.
        var rings = scene.ExitsRinging.ToDictionary();
        scene.RingExits(later + TimeSpan.FromSeconds(0.2));
        Assert.Equal(rings, scene.ExitsRinging.ToDictionary());
    }

    [Fact]
    public void A_list_read_after_the_rings_are_over_rings_them_all_again()
    {
        var (_, scene) = Of([ExitAt("extract:a", 10), ExitAt("extract:b", 20)]);
        scene.ExitsListed = new HashSet<string> { "extract:a", "extract:b" };
        scene.RingExits(DateTime.Now - TimeSpan.FromSeconds(10));
        Assert.False(scene.ExitsArriving);
        var now = DateTime.Now;
        scene.RingExits(now);
        Assert.Equal(now, scene.ExitsRinging["extract:a"].At);
        Assert.Equal(now + MapRenderer.ExitTurn, scene.ExitsRinging["extract:b"].At);
        Assert.True(scene.ExitsArriving);
    }

    [Fact]
    public void Nothing_rings_with_animation_effects_off()
    {
        var (camera, scene) = Of([Exit()]);
        scene.Pulse = false;
        scene.ExitsListed = new HashSet<string> { "extract:a" };
        scene.RingExits(DateTime.Now);
        Assert.False(scene.ExitsArriving);
        // Simply lit.
        Assert.True(Glow(scene, camera) > 100);
    }

    [Fact]
    public void A_lit_extract_echoes_its_triangle_out_and_is_lit_once_its_turn_comes()
    {
        var (camera, scene) = Of([Exit()]);
        scene.ExitsListed = new HashSet<string> { "extract:a" };
        // At rest nothing reaches 34 px above it, past the glow.
        using (var rest = Render(scene, camera))
            Assert.Equal(0, Tinted(rest, 500, 466));

        // A moment into its ring: the echo of its triangle, its apex up there.
        scene.RingExits(DateTime.Now - TimeSpan.FromSeconds(0.4));
        Assert.True(scene.ExitsArriving);
        using (var ringing = Render(scene, camera))
            Assert.True(Tinted(ringing, 500, 466) > 3, $"{Tinted(ringing, 500, 466)} tinted pixels of the echo");

        // Before its turn it isn't lit yet: the extracts light up one after another.
        var (camera2, waiting) = Of([Exit()]);
        waiting.ExitsListed = new HashSet<string> { "extract:a" };
        waiting.RingExits(DateTime.Now + TimeSpan.FromSeconds(1));
        Assert.True(Glow(waiting, camera2) < 40, $"{Glow(waiting, camera2)} tinted pixels before its turn");

        // Over: lit as ever, nothing moving.
        var (camera3, done) = Of([Exit()]);
        done.ExitsListed = new HashSet<string> { "extract:a" };
        done.RingExits(DateTime.Now - TimeSpan.FromSeconds(10));
        Assert.False(done.ExitsArriving);
        using (var over = Render(done, camera3))
            Assert.Equal(0, Tinted(over, 500, 466));
    }

    [Fact]
    public void The_legend_explains_it_only_once_a_list_was_read()
    {
        var (_, scene) = Of([Exit()]);
        Assert.DoesNotContain(LegendSymbol.ExtractNotListed, MapLegend.On(scene));
        Assert.DoesNotContain(LegendSymbol.ExtractListed, MapLegend.On(scene));
        scene.ExitsNotListed = new HashSet<string> { "extract:a" };
        Assert.Contains(LegendSymbol.ExtractNotListed, MapLegend.On(scene));
        Assert.Contains(MapLegend.Rows, r => r.Symbol == LegendSymbol.ExtractNotListed);
        scene.ExitsListed = new HashSet<string> { "extract:b" };
        Assert.Contains(LegendSymbol.ExtractListed, MapLegend.On(scene));
    }
}
