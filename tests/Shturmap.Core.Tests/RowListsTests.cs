using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

/// <summary>
/// The rail's lists stay on screen while a new snapshot says the same (review of 2026-10-09: every snapshot rebuilt the
/// rows under a resting pointer), and give way as soon as one row says something else.
/// </summary>
public class RowListsTests
{
    // A row as the window's are made: a record with a list in it, which a record compares by reference.
    private sealed record Row(string Name, int Distance, IReadOnlyList<string> Items)
    {
        public static bool Same(Row a, Row b) => a == b with { Items = a.Items } && RowLists.Same(a.Items, b.Items);
    }

    private static List<Row> Made(int distance = 86) => [new("Mark Stryker", distance, ["ms2000"]), new("Find the bronze watch", 140, [])];

    [Fact]
    public void A_list_that_says_the_same_keeps_the_one_on_screen()
    {
        var shown = Made();
        var made = Made();
        // Made anew, the rows are other objects, and a record alone takes their item lists for different ones.
        Assert.NotEqual(shown[0], made[0]);
        Assert.Same(shown, RowLists.Keep<Row>(shown, made, Row.Same));
    }

    [Fact]
    public void A_row_that_changed_brings_the_new_list()
    {
        var shown = Made();
        var nearer = Made(distance: 40);
        Assert.Same(nearer, RowLists.Keep<Row>(shown, nearer, Row.Same));
        List<Row> otherItems = [new("Mark Stryker", 86, ["ms2000", "flare"]), new("Find the bronze watch", 140, [])];
        Assert.Same(otherItems, RowLists.Keep<Row>(shown, otherItems, Row.Same));
        var fewer = Made().Take(1).ToList();
        Assert.Same(fewer, RowLists.Keep<Row>(shown, fewer, Row.Same));
        var reordered = Made().AsEnumerable().Reverse().ToList();
        Assert.Same(reordered, RowLists.Keep<Row>(shown, reordered, Row.Same));
    }

    [Fact]
    public void Lists_of_values_and_missing_lists()
    {
        List<string> shown = ["a", "b"];
        Assert.Same(shown, RowLists.Keep<string>(shown, ["a", "b"]));
        Assert.True(RowLists.Same<string>(null, null));
        Assert.False(RowLists.Same<string>(null, []));
        Assert.False(RowLists.Same<string>(["a"], ["b"]));
    }
}
