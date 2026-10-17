using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

// The trader strip is one row, never two (owner, 2026-10-17: "I do not like that the layout has two rows where Ref falls
// exactly on the last line"): the cells share the rail's inner width, the portrait and its count scale down with them to
// 16 DIP and 10 px, and past that the count stands on the portrait as a badge.
public class TraderStripTests
{
    // The rail's inner width at the smallest window (900 × 560) and at every other: 384 less its border and margins.
    private const double Rail = 351;

    // "88" in the figures' face at 15 px (Bahnschrift SemiBold SemiCondensed), about what the app measures.
    private const double TwoDigits = 16.8;

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(16)]
    public void The_strip_is_one_row_and_every_cell_holds_what_it_shows(int traders)
    {
        var cells = TraderStrip.Layout(traders, Rail, TwoDigits);
        Assert.Equal(Rail, traders * cells.Width + TraderStrip.Gap * (traders - 1), 6);
        Assert.True(TraderStrip.ContentWidth(cells, TwoDigits) <= cells.Width, $"{traders}: {TraderStrip.ContentWidth(cells, TwoDigits)} in {cells.Width}");
        Assert.True(cells.Portrait <= TraderStrip.FullPortrait && cells.Count <= TraderStrip.FullCount);
        // The row is 28 DIP: the portrait, its padding and a badge standing out below it.
        Assert.True(cells.Portrait + 2 * TraderStrip.Pad + (cells.Badge ? TraderStrip.BadgeDown : 0) <= 28);
    }

    [Fact]
    public void Six_traders_at_full_size()
    {
        var cells = TraderStrip.Layout(6, Rail, TwoDigits);
        Assert.Equal((20d, 15d, false), (cells.Portrait, cells.Count, cells.Badge));
    }

    [Fact]
    public void Eight_traders_with_Ref_the_last_stay_in_one_row_a_little_smaller()
    {
        var cells = TraderStrip.Layout(8, Rail, TwoDigits);
        Assert.False(cells.Badge);
        Assert.InRange(cells.Portrait, TraderStrip.MinPortrait, TraderStrip.FullPortrait - 1);
        Assert.InRange(cells.Count, TraderStrip.MinCount, TraderStrip.FullCount - 1);
    }

    [Fact]
    public void Nine_traders_hold_the_portraits_floor_and_the_count_shrinks_on()
    {
        var cells = TraderStrip.Layout(9, Rail, TwoDigits);
        Assert.False(cells.Badge);
        Assert.Equal(TraderStrip.MinPortrait, cells.Portrait);
        Assert.InRange(cells.Count, TraderStrip.MinCount, 12);
    }

    [Fact]
    public void From_ten_traders_the_count_stands_on_the_portrait()
    {
        var cells = TraderStrip.Layout(10, Rail, TwoDigits);
        Assert.True(cells.Badge);
        Assert.Equal(TraderStrip.FullPortrait, cells.Portrait);
        Assert.Equal(TraderStrip.BadgeCount, cells.Count);
    }

    [Fact]
    public void The_layout_follows_the_number_of_traders_alone()
    {
        // Room for two digits is kept, so a count going from 9 to 10 changes nothing; nor does the face measure a little
        // wider or narrower.
        Assert.Equal(TraderStrip.Layout(8, Rail, TwoDigits).Badge, TraderStrip.Layout(8, Rail, TwoDigits + 1).Badge);
        Assert.False(TraderStrip.Layout(9, Rail, TwoDigits + 1).Badge);
        Assert.True(TraderStrip.Layout(10, Rail, TwoDigits - 1).Badge);
    }
}
