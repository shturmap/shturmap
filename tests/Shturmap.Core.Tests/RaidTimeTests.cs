using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

/// <summary>
/// The raid card's time line (owner, 2026-10-04: "we typically want to see how much time we're in the raid and how
/// much time is left. This is crucial information").
/// </summary>
public class RaidTimeTests
{
    private static readonly DateTime Start = new(2026, 10, 4, 21, 2, 0);

    [Theory]
    [InlineData(0, "0 min in · 40 min left")]
    [InlineData(59, "0 min in · 40 min left")]
    [InlineData(12 * 60 + 30, "12 min in · 28 min left")]
    [InlineData(39 * 60 + 59, "39 min in · 1 min left")]
    public void A_pmc_raid_says_how_long_it_has_run_and_how_long_it_still_runs(int seconds, string expected) =>
        Assert.Equal(expected, RaidTime.Of(true, false, Start, 40, Start.AddSeconds(seconds))!.Text);

    [Fact]
    public void Past_its_length_it_says_so_and_never_a_time_left_below_one()
    {
        Assert.Equal("40 min in · past the raid's 40 min", RaidTime.Of(true, false, Start, 40, Start.AddMinutes(40))!.Text);
        Assert.Equal("47 min in · past the raid's 40 min", RaidTime.Of(true, false, Start, 40, Start.AddMinutes(47))!.Text);
    }

    [Fact]
    public void A_scav_gets_no_time_left() // it joins a raid under way; the log doesn't say how long that still runs
    {
        var reading = RaidTime.Of(true, true, Start, 40, Start.AddMinutes(7))!;
        Assert.Equal("7 min in · time left not known", reading.Text);
        Assert.Equal(RaidTime.ScavTip, reading.Tip);
    }

    [Fact]
    public void Without_a_raid_length_in_the_data_only_the_time_in()
    {
        var reading = RaidTime.Of(true, false, Start, 0, Start.AddMinutes(7))!;
        Assert.Equal("7 min in", reading.Text);
        Assert.Equal(RaidTime.NoLengthTip, reading.Tip);
    }

    [Fact]
    public void Nothing_outside_a_raid_or_without_a_start_in_the_log()
    {
        Assert.Null(RaidTime.Of(false, false, Start, 40, Start.AddMinutes(7)));
        Assert.Null(RaidTime.Of(true, false, null, 40, Start.AddMinutes(7)));
    }

    [Fact]
    public void A_clock_set_back_never_gives_a_time_below_zero()
    {
        Assert.Equal("0 min in · 40 min left", RaidTime.Of(true, false, Start, 40, Start.AddMinutes(-3))!.Text);
    }
}
