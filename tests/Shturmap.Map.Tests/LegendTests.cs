namespace Shturmap.Map.Tests;

// The help panel's legend is drawn by the map's renderer, one row per symbol (cartography review, 2026-10-02).
public class LegendTests
{
    [Fact]
    public void Every_symbol_has_one_row()
    {
        Assert.Equal(Enum.GetValues<LegendSymbol>(), MapLegend.Rows.Select(r => r.Symbol).Order());
        Assert.All(MapLegend.Rows, r => Assert.False(string.IsNullOrWhiteSpace(r.Text)));
    }

    [Theory]
    [MemberData(nameof(Symbols))]
    public void Every_swatch_draws_inside_its_box(LegendSymbol symbol)
    {
        using var bitmap = MapLegend.Draw(symbol, 2);
        Assert.Equal((112, 68), (bitmap.Width, bitmap.Height));
        var drawn = bitmap.Pixels.Count(p => p.Alpha > 0);
        Assert.True(drawn > 40, $"{symbol}: {drawn} pixels drawn");
        // Nothing runs into the edges (a clipped badge or label).
        var edge = Enumerable.Range(0, bitmap.Width).SelectMany(x => new[] { bitmap.GetPixel(x, 0), bitmap.GetPixel(x, bitmap.Height - 1) })
            .Concat(Enumerable.Range(0, bitmap.Height).SelectMany(y => new[] { bitmap.GetPixel(0, y), bitmap.GetPixel(bitmap.Width - 1, y) }));
        Assert.True(symbol is LegendSymbol.Guide || edge.All(p => p.Alpha < 40), $"{symbol} touches the edge of its swatch");
    }

    public static TheoryData<LegendSymbol> Symbols() => new(Enum.GetValues<LegendSymbol>());

    // Help lists the symbols of the map on screen first (owner, 2026-10-04): a symbol counts when the scene holds
    // what it stands for.
    [Fact]
    public void A_map_lists_the_symbols_it_holds_and_no_others()
    {
        var at = new Shturmap.Core.WorldPoint(0, 0, 0);
        var (_, scene) = TestView.Of(
        [
            TestView.Quest("a", 0, 0, "Quest"),
            new MapMarker("extract:1", MarkerKind.ExtractPmc, at, "Gate"),
            new MapMarker("boss:1", MarkerKind.BossSpawn, at, "Boss 50%"),
        ]);
        var on = MapLegend.On(scene);
        foreach (var there in new[] { LegendSymbol.Objective, LegendSymbol.Extract, LegendSymbol.Boss, LegendSymbol.OutOfView })
            Assert.Contains(there, on);
        // No position, no pick, no lock, no transit, one place only: none of their symbols.
        foreach (var absent in new[] { LegendSymbol.Player, LegendSymbol.KeptQuest, LegendSymbol.Guide, LegendSymbol.Lock, LegendSymbol.Transit,
                     LegendSymbol.Cluster, LegendSymbol.Optional, LegendSymbol.Done, LegendSymbol.Sniper, LegendSymbol.Hazard, LegendSymbol.Trail })
            Assert.DoesNotContain(absent, on);
    }

    [Fact]
    public void A_position_a_pick_and_a_second_place_bring_their_symbols()
    {
        var at = new Shturmap.Core.WorldPoint(0, 0, 0);
        var (_, scene) = TestView.Of(
        [
            new MapMarker("objective:a:1", MarkerKind.Objective, at, "Quest", "quest-a", Shturmap.Core.Quests.ObjectiveKind.Exploration, Optional: true),
            new MapMarker("objective:a:2", MarkerKind.PossibleLocation, at, "Quest", "quest-a", Shturmap.Core.Quests.ObjectiveKind.Exploration),
        ]);
        scene.Player = new PlayerFix(at, 0, DateTime.UnixEpoch);
        scene.Kept = new HashSet<string> { "quest-a" };
        var on = MapLegend.On(scene);
        foreach (var there in new[] { LegendSymbol.Player, LegendSymbol.PlayerOutOfView, LegendSymbol.KeptQuest, LegendSymbol.Guide,
                     LegendSymbol.PossibleLocation, LegendSymbol.Cluster, LegendSymbol.Optional })
            Assert.Contains(there, on);
    }
}
