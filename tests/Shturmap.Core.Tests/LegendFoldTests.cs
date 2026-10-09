using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

// Help's legend lists the shown map's twelve most important symbols; the rest of them, and the symbols the map doesn't
// have, wait behind one link (owner, 2026-10-09: Streets listed 26 rows).
public class LegendFoldTests
{
    [Fact]
    public void A_crowded_map_lists_its_first_twelve_and_folds_the_rest_of_them()
    {
        // 30 rows, most important first; every fifth isn't on the map.
        var rows = Enumerable.Range(0, 30).ToList();
        var (listed, more, elsewhere) = LegendFold.Split(rows, r => r % 5 != 4);
        Assert.Equal([0, 1, 2, 3, 5, 6, 7, 8, 10, 11, 12, 13], listed);
        Assert.Equal([15, 16, 17, 18, 20, 21, 22, 23, 25, 26, 27, 28], more);
        Assert.Equal([4, 9, 14, 19, 24, 29], elsewhere);
    }

    [Fact]
    public void A_quiet_map_lists_all_of_its_own()
    {
        var (listed, more, elsewhere) = LegendFold.Split(Enumerable.Range(0, 30).ToList(), r => r < 8);
        Assert.Equal(8, listed.Count);
        Assert.Empty(more);
        Assert.Equal(22, elsewhere.Count);
    }

    [Fact]
    public void With_no_map_every_row_counts_and_folds_after_twelve()
    {
        var (listed, more, elsewhere) = LegendFold.Split(Enumerable.Range(0, 30).ToList(), _ => true);
        Assert.Equal(LegendFold.Listed, listed.Count);
        Assert.Equal(18, more.Count);
        Assert.Empty(elsewhere);
    }
}
