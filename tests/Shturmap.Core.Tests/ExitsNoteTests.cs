using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

// What the rail says about the player's extracts (owner, 2026-10-05: "it should also be visible if the exfils [are]
// not verified yet"): unchecked until a screenshot showed the game's own list, and then whether the list names each.
public class ExitsNoteTests
{
    private static readonly DateTime At = new(2026, 10, 5, 21, 4, 30);

    [Fact]
    public void In_a_raid_the_heading_says_how_to_check_until_the_list_was_read()
    {
        Assert.Equal(ExitsNote.HowToCheck, ExitsNote.Of(inRaid: true, readOn: true, noReader: false, readAt: null, listed: 0, extracts: 9));
        Assert.Equal("Your list this raid: 6 of 9 extracts, read from your screenshot at 21:04.",
            ExitsNote.Of(inRaid: true, readOn: true, noReader: false, readAt: At, listed: 6, extracts: 9));
    }

    [Fact]
    public void A_windows_that_cant_read_says_so()
    {
        Assert.Equal(ExitsNote.NoReader, ExitsNote.Of(inRaid: true, readOn: true, noReader: true, readAt: null, listed: 0, extracts: 9));
    }

    [Fact]
    public void Nothing_outside_a_raid_or_with_the_reading_unticked()
    {
        Assert.Equal("", ExitsNote.Of(inRaid: false, readOn: true, noReader: false, readAt: null, listed: 0, extracts: 9));
        Assert.Equal("", ExitsNote.Of(inRaid: true, readOn: false, noReader: false, readAt: null, listed: 0, extracts: 9));
    }

    [Fact]
    public void A_row_says_whether_the_list_names_it_and_nothing_while_unchecked()
    {
        Assert.Equal("", ExitsNote.Row(listed: false, unsure: false, notListed: false));
        Assert.Equal("on your list", ExitsNote.Row(listed: true, unsure: false, notListed: false));
        Assert.Equal("on your list · ??? in game", ExitsNote.Row(listed: false, unsure: true, notListed: false));
        Assert.Equal("not on your list", ExitsNote.Row(listed: false, unsure: false, notListed: true));
    }

    [Fact]
    public void The_glance_says_nearest_only_while_unchecked()
    {
        Assert.Equal("Nearest · not checked against your list", ExitsNote.Glance(listed: false, unsure: false, readable: true));
        Assert.Equal(ExitsNote.GlanceUncheckedTip, ExitsNote.GlanceTip(listed: false, unsure: false, readable: true));
        // Unticked, or a Windows that can't read: the game's own list is the only way to know.
        Assert.Equal("Nearest · check your list in game", ExitsNote.Glance(listed: false, unsure: false, readable: false));
        Assert.Equal(ExitsNote.GlanceNoReadingTip, ExitsNote.GlanceTip(listed: false, unsure: false, readable: false));
        Assert.Equal("On your list this raid", ExitsNote.Glance(listed: true, unsure: false, readable: true));
        Assert.Equal("On your list · ??? in game", ExitsNote.Glance(listed: false, unsure: true, readable: true));
        Assert.Equal(ExitsNote.GlanceUnsureTip, ExitsNote.GlanceTip(listed: false, unsure: true, readable: true));
    }
}
