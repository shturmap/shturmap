using Shturmap.App.Rules;
using Shturmap.Core.Desk;
using Shturmap.Core.Logs;

namespace Shturmap.Core.Tests.Desk;

// Clearing the desk's page in the view (owner, 2026-10-26: "Button + fresh after raid"; docs/DESIGN.md §4, *The stash
// desk*, *Clearing the page*; Rules.DeskClearing): CLEAR stands where there is something to clear, the notice's UNDO while no
// reading came since it was offered, and the made-up desk empties to a page of no region, every region drawn as never read.
public class DeskClearingTests
{
    private static readonly DeskView Made = DeskExample.View(1, GameMode.Pve, 3, null, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Clear_stands_where_the_page_has_a_region()
    {
        Assert.True(DeskClearing.Offered(Made));
        Assert.False(DeskClearing.Offered(null));
        Assert.False(DeskClearing.Offered(DeskClearing.Emptied(Made, DateTimeOffset.UnixEpoch)));
    }

    [Fact]
    public void Undo_stands_until_a_later_reading()
    {
        Assert.True(DeskClearing.UndoStands(3, 3));
        Assert.True(DeskClearing.UndoStands(3, null));
        Assert.False(DeskClearing.UndoStands(3, 4));
        // A new page after a raid is offered at its own reading: the view of that reading leaves it standing.
        Assert.True(DeskClearing.UndoStands(4, 4));
    }

    // The made-up desk emptied: the same reading (nothing comes in), no region, every fixed region a placeholder, the head
    // line's totals nothing and its parts left out (DeskPageRules.HeadLine: KEEP alone).
    [Fact]
    public void The_made_up_desk_empties_to_a_page_of_no_region()
    {
        var emptied = DeskClearing.Emptied(Made, DateTimeOffset.UnixEpoch.AddMinutes(5));
        Assert.Equal(Made.Reading, emptied.Reading);
        Assert.Empty(emptied.Sheet.Regions);
        Assert.Empty(emptied.Changed);
        Assert.False(emptied.First);
        Assert.All(DeskPageRules.Blocks(emptied.Sheet.Page.Regions), b => Assert.True(b.Placeholder));
        Assert.Equal(DeskPageRules.Fixed.Count, DeskPageRules.Blocks(emptied.Sheet.Page.Regions).Count);
        var totals = emptied.Sheet.Totals;
        Assert.Equal((0d, 0d), (totals.OnYou, totals.Stash));
        Assert.Equal(RuleTexts.DeskHeadLineKeep(now: 0, next: 0, later: 0), DeskPageRules.HeadLine(totals, 0, 0, emptied.Sheet.Page.Regions));
    }
}
