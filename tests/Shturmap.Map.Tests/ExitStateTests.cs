using Shturmap.Core;
using SkiaSharp;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// The extracts the game's own list names this raid, once a screenshot showed it (owner, 2026-10-05; docs/DESIGN.md,
// "Map drawing"): the ones on it stay solid, the others are hollow with a fainter name, and one the game marks
// "??:??:??" carries the "?" a possible location carries.
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
    }

    [Fact]
    public void The_legend_explains_it_only_once_a_list_was_read()
    {
        var (_, scene) = Of([Exit()]);
        Assert.DoesNotContain(LegendSymbol.ExtractNotListed, MapLegend.On(scene));
        scene.ExitsNotListed = new HashSet<string> { "extract:a" };
        Assert.Contains(LegendSymbol.ExtractNotListed, MapLegend.On(scene));
        Assert.Contains(MapLegend.Rows, r => r.Symbol == LegendSymbol.ExtractNotListed);
    }
}
