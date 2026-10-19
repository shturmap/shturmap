using Shturmap.Core.Desk;

namespace Shturmap.Core.Tests.Desk;

// The desk's keep notch (owner, 2026-10-19; docs/DESIGN.md §2, "For the stash desk", and §4, *The cell*): NOW for an
// active quest's need, NEXT for a quest the active ones unlock, LATER further down the tree (marked only when found in raid
// or the item can't be bought), and a hideout module's need, whatever the hideout has built. Made-up quests and items.
public class DeskKeepTests
{
    private const string Item = "000000000000000000000101", Other = "000000000000000000000102";

    private static DeskQuestNeeds Quest(string id, string[] before, params (string, int, bool)[] items) => new(id, before, items);

    private static DeskKeep Keep(IReadOnlySet<string> active, IReadOnlySet<string> done, params DeskQuestNeeds[] quests) =>
        DeskKeep.Of(quests, active, done, []);

    [Fact]
    public void An_active_quests_need_is_now()
    {
        var keep = Keep(new HashSet<string> { "q1" }, new HashSet<string>(), Quest("q1", [], (Item, 2, false)));
        Assert.Equal(DeskKeepLevel.Now, keep.MarkOf(Item, buyable: true));
        Assert.Equal(new DeskNeed(DeskKeepLevel.Now, "q1", 2, false), Assert.Single(keep.NeedsOf(Item)));
    }

    [Fact]
    public void A_quest_the_active_ones_unlock_is_next_and_further_down_is_later()
    {
        var keep = Keep(new HashSet<string> { "q1" }, new HashSet<string> { "q0" },
            Quest("q2", ["q1"], (Item, 1, false)),
            Quest("q3", ["q2"], (Other, 1, true)));
        Assert.Equal(DeskKeepLevel.Next, keep.MarkOf(Item, buyable: true));
        Assert.Equal(DeskKeepLevel.Later, keep.MarkOf(Other, buyable: true));
    }

    // "Later everywhere would mark half the stash and say nothing": a later need marks only when found in raid or the
    // item can't be bought; the card still lists it.
    [Fact]
    public void Later_marks_only_a_found_in_raid_need_or_an_item_that_cant_be_bought()
    {
        var keep = Keep(new HashSet<string>(), new HashSet<string>(), Quest("q1", ["q0"], (Item, 1, false)));
        Assert.Equal(DeskKeepLevel.None, keep.MarkOf(Item, buyable: true));
        Assert.Equal(DeskKeepLevel.Later, keep.MarkOf(Item, buyable: false));
        Assert.Single(keep.NeedsOf(Item));
    }

    [Fact]
    public void A_quest_done_needs_nothing()
    {
        var keep = Keep(new HashSet<string>(), new HashSet<string> { "q1" }, Quest("q1", [], (Item, 1, true)));
        Assert.Equal(DeskKeepLevel.None, keep.MarkOf(Item, buyable: false));
        Assert.Empty(keep.NeedsOf(Item));
    }

    [Fact]
    public void The_strongest_need_sets_the_mark_and_the_hideout_stands_between_next_and_later()
    {
        var hideout = new[] { new DeskHideoutNeed("station-1", 3, Item, 1, true) };
        var later = DeskKeep.Of([Quest("q9", ["q8"], (Item, 1, true))], new HashSet<string>(), new HashSet<string>(), hideout);
        Assert.Equal(DeskKeepLevel.Hideout, later.MarkOf(Item, buyable: true));
        var next = DeskKeep.Of([Quest("q2", [], (Item, 1, false))], new HashSet<string>(), new HashSet<string>(), hideout);
        Assert.Equal(DeskKeepLevel.Next, next.MarkOf(Item, buyable: true));
        // The card's lines in progress order: quests first, the hideout's last.
        Assert.Equal([DeskKeepLevel.Next, DeskKeepLevel.Hideout], next.NeedsOf(Item).Select(n => n.Level));
    }

    [Fact]
    public void A_quest_open_to_start_is_next()
    {
        var keep = Keep(new HashSet<string>(), new HashSet<string> { "q0" }, Quest("q1", ["q0"], (Item, 1, false)));
        Assert.Equal(DeskKeepLevel.Next, keep.MarkOf(Item, buyable: true));
    }
}
