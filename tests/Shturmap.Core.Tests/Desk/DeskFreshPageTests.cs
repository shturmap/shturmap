using Shturmap.App.Rules;

namespace Shturmap.Core.Tests.Desk;

// A new desk page after a raid in the view (owner, 2026-10-26: "Button + fresh after raid"; docs/DESIGN.md §4, *The stash
// desk*, *A new page after a raid*; Rules.DeskFreshPage): its notice's UNDO stands while no reading came since it was
// offered. CLEAR, its tests here until then, went on 2026-10-27 (D1 B, the newest look in front).
public class DeskFreshPageTests
{
    [Fact]
    public void Undo_stands_until_a_later_reading()
    {
        Assert.True(DeskFreshPage.UndoStands(3, 3));
        Assert.True(DeskFreshPage.UndoStands(3, null));
        Assert.False(DeskFreshPage.UndoStands(3, 4));
        // A new page after a raid is offered at its own reading: the view of that reading leaves it standing.
        Assert.True(DeskFreshPage.UndoStands(4, 4));
    }
}
