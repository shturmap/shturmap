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
}
