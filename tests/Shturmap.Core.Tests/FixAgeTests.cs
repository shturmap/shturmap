using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

/// <summary>
/// The raid card's "Distances from your screenshot … ago" says the age the status bar says, at any moment (it is set
/// with the clock): it stood at "1 min ago" from 45 s until the next snapshot (the review of 2026-10-04).
/// </summary>
public class FixAgeTests
{
    [Theory]
    [InlineData(0, "0\u00A0s")]
    [InlineData(44, "44\u00A0s")]
    [InlineData(59, "59\u00A0s")]
    [InlineData(60, "1\u00A0min")]
    [InlineData(119, "1\u00A0min")]
    [InlineData(9 * 60 + 30, "9\u00A0min")]
    [InlineData(59 * 60 + 59, "59\u00A0min")]
    [InlineData(3600, "1\u00A0h")]
    [InlineData(2 * 3600 + 1800, "2\u00A0h")]
    public void The_age_is_in_whole_units(int seconds, string text) =>
        Assert.Equal(text, FixAge.Text(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void A_clock_running_backwards_never_gives_a_negative_age() =>
        Assert.Equal("0\u00A0s", FixAge.Text(TimeSpan.FromSeconds(-3)));

    [Fact]
    public void While_fresh_the_raid_card_says_nothing()
    {
        Assert.Equal("", FixAge.Note(TimeSpan.Zero, "PrtSc"));
        Assert.Equal("", FixAge.Note(TimeSpan.FromSeconds(44.9), "PrtSc"));
    }

    [Theory]
    [InlineData(45, "Distances from your screenshot 45\u00A0s ago")]
    [InlineData(59, "Distances from your screenshot 59\u00A0s ago")]
    [InlineData(61, "Distances from your screenshot 1\u00A0min ago")]
    [InlineData(9 * 60 + 5, "Distances from your screenshot 9\u00A0min ago")]
    [InlineData(3 * 3600, "Distances from your screenshot 3\u00A0h ago")]
    public void Past_that_it_says_how_old_the_screenshot_is(int seconds, string note) =>
        Assert.Equal(note, FixAge.Note(TimeSpan.FromSeconds(seconds), "PrtSc"));

    [Fact]
    public void Without_a_position_it_says_how_to_get_one() =>
        Assert.Equal("No position yet: press PrtSc or Home for distances", FixAge.Note(null, "PrtSc or Home"));
}
