using Shturmap.Core;
using SkiaSharp;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// The extracts the game's own list names this raid, once a screenshot showed it (owner, 2026-10-05; docs/DESIGN.md,
// "Map drawing"): the ones on it are lit, a glow in their colour and their names in bold; the others are hollow with a
// fainter name; one the game marks "??:??:??" stays solid without the glow and carries the "?" a possible location
// carries.
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

    [Fact]
    public void An_extract_is_solid_until_a_list_says_it_isnt_the_players()
    {
        var (camera, scene) = Of([Exit()]);
        var solid = Drawn(scene, camera);
        Assert.True(IsGreen(solid.Middle));

        scene.ExitsNotListed = new HashSet<string> { "extract:a" };
        var hollow = Drawn(scene, camera);
        Assert.False(IsGreen(hollow.Middle));
        // Its outline is still there, in its colour.
        Assert.True(hollow.GreenPixels > 10, $"{hollow.GreenPixels} green pixels");

        scene.ExitsNotListed = new HashSet<string>();
        Assert.True(IsGreen(Drawn(scene, camera).Middle));
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
    public void An_exit_the_game_marks_with_question_marks_carries_one()
    {
        var (camera, scene) = Of([Exit()]);
        Assert.False(Assert.Single(MapRenderer.Layout(camera, scene, 1).Markers).Unsure);
        scene.ExitsUnsure = new HashSet<string> { "extract:a" };
        var shown = Assert.Single(MapRenderer.Layout(camera, scene, 1).Markers);
        Assert.True(shown.Unsure);
        Assert.False(shown.NotListed);
        // Still solid: it is on the list.
        Assert.True(IsGreen(Drawn(scene, camera).Middle));
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
