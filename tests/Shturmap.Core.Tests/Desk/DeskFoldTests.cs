using Shturmap.App.Rules;

namespace Shturmap.Core.Tests.Desk;

// The fold (owner, 2026-10-19, the addendum's B1; docs/DESIGN.md §4, *The head line and the fold*): each reading opens the
// regions it changed and folds the rest into one line where they stand, folded neighbours sharing it; a name opens its
// region until the next reading; the first reading opens all, one that changed nothing changes nothing.
public class DeskFoldTests
{
    private static readonly string[] Keys = ["loadout", "rig", "pockets", "special", "backpack", "window:zhuk", "pouch", "stash", "window:wz"];

    private static HashSet<string> Set(params string[] keys) => new(keys, StringComparer.Ordinal);

    [Fact]
    public void A_page_first_shown_and_a_first_reading_open_every_region()
    {
        Assert.Equal(Set(Keys), DeskFold.Open(null, Keys, Set("backpack"), first: false, newReading: false));
        Assert.Equal(Set(Keys), DeskFold.Open(Set("stash"), Keys, Set(Keys), first: true, newReading: true));
        // A page first shown with a reading that changed some of it: the reading's rule, not the whole page.
        Assert.Equal(Set("backpack"), DeskFold.Open(null, Keys, Set("backpack"), first: false, newReading: true));
        Assert.Equal(Set(Keys), DeskFold.Open(null, Keys, Set(), first: false, newReading: true));
    }

    [Fact]
    public void A_reading_opens_what_it_changed_and_folds_the_rest()
    {
        var open = DeskFold.Open(Set(Keys), Keys, Set("backpack", "window:zhuk"), first: false, newReading: true);
        Assert.Equal(Set("backpack", "window:zhuk"), open);
        // A name clicked opens its region, until the next reading.
        open = DeskFold.Unfold(open, "stash");
        Assert.Equal(Set("backpack", "window:zhuk", "stash"), open);
        Assert.Equal(Set("pouch"), DeskFold.Open(open, Keys, Set("pouch"), first: false, newReading: true));
    }

    [Fact]
    public void A_reading_that_changed_nothing_changes_nothing()
    {
        var open = Set("backpack", "stash");
        Assert.Equal(open, DeskFold.Open(open, Keys, Set(), first: false, newReading: true));
        // And a region gone from the page goes from the fold.
        Assert.Equal(Set("backpack"), DeskFold.Open(open, Keys.Where(k => k != "stash").ToList(), Set(), first: false, newReading: false));
    }

    [Fact]
    public void Folded_neighbours_share_one_line_where_they_stand()
    {
        var blocks = DeskFold.Blocks(Keys, Set("backpack", "window:zhuk"));
        Assert.Equal(
            [
                new DeskBlock.Fold(["loadout", "rig", "pockets", "special"]),
                new DeskBlock.Region("backpack"),
                new DeskBlock.Region("window:zhuk"),
                new DeskBlock.Fold(["pouch", "stash", "window:wz"]),
            ],
            blocks, new BlockComparer());
        // Every region open: no fold line.
        Assert.All(DeskFold.Blocks(Keys, Set(Keys)), b => Assert.IsType<DeskBlock.Region>(b));
    }

    // From a desk 980 DIP across the stash stands in a column of its own: a fold line never runs across the break.
    [Fact]
    public void A_fold_line_stays_in_its_column()
    {
        var blocks = DeskFold.Blocks(Keys, Set("backpack"), columnBreak: 7);
        Assert.Equal(
            [
                new DeskBlock.Fold(["loadout", "rig", "pockets", "special"]),
                new DeskBlock.Region("backpack"),
                new DeskBlock.Fold(["window:zhuk", "pouch"]),
                new DeskBlock.Fold(["stash", "window:wz"]),
            ],
            blocks, new BlockComparer());
    }

    // A reading's page comes in cell by cell (*Fun*): the regions it opened, every one on a first reading; nothing for a
    // republish or a name clicked.
    [Fact]
    public void A_readings_opened_regions_come_in_and_nothing_else()
    {
        var all = Set(Keys);
        Assert.Equal(all, DeskFold.Entering(all, all, first: true, newReading: true));
        Assert.Equal(Set("backpack"), DeskFold.Entering(Set("backpack", "stash"), Set("backpack"), first: false, newReading: true));
        Assert.Empty(DeskFold.Entering(all, Set("backpack"), first: false, newReading: false));
    }

    private sealed class BlockComparer : IEqualityComparer<DeskBlock>
    {
        public bool Equals(DeskBlock? x, DeskBlock? y) => (x, y) switch
        {
            (DeskBlock.Region a, DeskBlock.Region b) => a.Key == b.Key,
            (DeskBlock.Fold a, DeskBlock.Fold b) => a.Keys.SequenceEqual(b.Keys),
            _ => false,
        };

        public int GetHashCode(DeskBlock obj) => 0;
    }
}
