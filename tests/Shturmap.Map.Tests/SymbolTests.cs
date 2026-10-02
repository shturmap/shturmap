using Shturmap.Core;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// Symbol sizes follow the four levels (docs/DESIGN.md, "Map drawing").
public class SymbolTests
{
    private static MapRenderer.ShownMarker Shown(MapMarker marker)
    {
        var (camera, scene) = Of([marker]);
        return Assert.Single(MapRenderer.Layout(camera, scene, 1).Markers);
    }

    [Fact]
    public void Extracts_and_transits_are_at_least_as_large_as_the_boss_diamond()
    {
        var boss = Shown(new MapMarker("boss:b:z", MarkerKind.BossSpawn, new WorldPoint(0, 0, 0), "Kaban 75%", "boss:b"));
        foreach (var kind in new[] { MarkerKind.ExtractPmc, MarkerKind.ExtractScav, MarkerKind.ExtractShared, MarkerKind.Transit })
            Assert.True(Shown(new MapMarker("x", kind, new WorldPoint(0, 0, 0), "Exit")).Reach >= boss.Reach, kind.ToString());
    }

    [Fact]
    public void Quest_markers_stay_the_largest_at_rest()
    {
        var quest = Shown(Quest("q", 0, 0, "Dandies"));
        var extract = Shown(new MapMarker("x", MarkerKind.ExtractPmc, new WorldPoint(0, 0, 0), "Exit"));
        var scav = Shown(new MapMarker("scav:z", MarkerKind.ScavSpawn, new WorldPoint(0, 0, 0), ""));
        Assert.True(quest.R > extract.R);
        Assert.True(extract.Reach > scav.Reach);
    }
}
