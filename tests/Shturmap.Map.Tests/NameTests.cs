using SkiaSharp;
using Shturmap.Core.Maps;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// Map names follow tarkov.dev's label size: landmarks larger, small names only when zoomed in (cartography review, 2026-10-02).
public class NameTests
{
    [Theory]
    [InlineData(100.0, true, 0)]
    [InlineData(90.0, true, 0)]
    [InlineData(80.0, false, 0)]
    [InlineData(70.0, false, 1.5)]
    [InlineData(65.0, false, 1.5)]
    [InlineData(60.0, false, 2.5)]
    [InlineData(null, false, 0)]
    public void Sizes_map_to_tiers(double? size, bool landmark, double fromZoom) =>
        Assert.Equal((landmark, fromZoom), MapRenderer.NameTier(size));

    [Fact]
    public void Smaller_names_appear_as_the_view_zooms_in()
    {
        var (camera, scene) = Of([],
            new MapLabel(0, 30, "Kilmov Shopping Mall", 0, 90), new MapLabel(0, 15, "Kilmov St.", 0, 80), new MapLabel(0, 0, "Pharmacy", 0, 70),
            new MapLabel(0, -15, "Bilbo Coffee", 0, 60), new MapLabel(0, -30, "Lexos", 0, null));
        string[] Names() => MapRenderer.Layout(camera, scene, 1).Names.Select(n => n.Text).Order().ToArray();
        camera.ZoomAt(new SKPoint(500, 500), 3); // 3.2 times the overview
        Assert.Equal(["BILBO COFFEE", "KILMOV SHOPPING MALL", "KILMOV ST.", "LEXOS", "PHARMACY"], Names());
        camera.ZoomAt(new SKPoint(500, 500), 0.6); // 1.9 times
        Assert.Equal(["KILMOV SHOPPING MALL", "KILMOV ST.", "LEXOS", "PHARMACY"], Names());
        camera.ZoomAt(new SKPoint(500, 500), 0.65); // 1.24 times
        Assert.Equal(["KILMOV SHOPPING MALL", "KILMOV ST.", "LEXOS"], Names());
        Assert.True(MapRenderer.Layout(camera, scene, 1).Names.Single(n => n.Text == "KILMOV SHOPPING MALL").Landmark);
    }
}
