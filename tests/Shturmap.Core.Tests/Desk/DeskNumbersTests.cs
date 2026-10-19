using Shturmap.App.Rules;

namespace Shturmap.Core.Tests.Desk;

// The stash desk's numbers (docs/DESIGN.md §4, *The stash desk*; Rules.DeskNumbers): a cell's figure as short as the panel
// drew it, three figures at most ("902k", "1.01 M"), a range in its high end's unit, the head line's totals with the
// rouble sign and their "+", the 48 h change at 56 DIP. English here; German below, with its decimal comma.
public class DeskNumbersTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(850, "850")]
    [InlineData(999.4, "999")]
    [InlineData(1_000, "1k")]
    [InlineData(4_500, "4.5k")]
    [InlineData(4_000, "4k")]
    [InlineData(9_960, "10k")]
    [InlineData(34_000, "34k")]
    [InlineData(902_345, "902k")]
    [InlineData(999_499, "999k")]
    [InlineData(999_700, "1.00 M")]
    [InlineData(1_010_000, "1.01 M")]
    [InlineData(6_840_000, "6.84 M")]
    [InlineData(12_345_678, "12.3 M")]
    [InlineData(123_456_789, "123 M")]
    public void A_value_is_short_with_three_figures_at_most(double value, string text)
    {
        Assert.Equal(text, DeskNumbers.Short(value));
    }

    [Theory]
    [InlineData(63_000, 307_000, "63–307k")]
    [InlineData(300, 294_000, "0.3–294k")]
    [InlineData(4_500, 52_000, "4.5–52k")]
    [InlineData(300, 850, "300–850")]
    [InlineData(450_000, 1_180_000, "0.45–1.18 M")]
    [InlineData(307_000, 63_000, "63–307k")]
    [InlineData(34_200, 34_400, "34k")]
    public void A_range_takes_its_high_ends_unit(double low, double high, string text)
    {
        Assert.Equal(text, DeskNumbers.Range(low, high));
    }

    [Fact]
    public void A_figure_carries_its_marks_and_a_total_its_sign()
    {
        Assert.Equal("~31k", DeskNumbers.Figure(31_000, 31_000, unsure: false, estimate: true, plus: false));
        Assert.Equal("34k+", DeskNumbers.Figure(34_000, 34_000, unsure: false, estimate: false, plus: true));
        Assert.Equal("~83k+", DeskNumbers.Figure(83_000, 83_000, unsure: false, estimate: true, plus: true));
        Assert.Equal("63–307k", DeskNumbers.Figure(63_000, 307_000, unsure: true, estimate: false, plus: false));
        Assert.Equal("1.01 M ₽", DeskNumbers.Total(1_010_000, plus: false));
        Assert.Equal("1.01 M+ ₽", DeskNumbers.Total(1_010_000, plus: true));
        Assert.Equal("0 ₽", DeskNumbers.Total(0, plus: false));
    }

    [Theory]
    [InlineData(-10.3, "↓ −10%")]
    [InlineData(14.2, "↑ +14%")]
    [InlineData(-1.2, "−1%")]
    [InlineData(3, "+3%")]
    [InlineData(0.2, "0%")]
    [InlineData(-9.6, "−10%")]
    public void The_change_has_its_sign_and_an_arrow_from_ten_percent(double percent, string text)
    {
        Assert.Equal(text, DeskNumbers.Change(percent));
    }

    [Fact]
    public void A_count_is_as_the_game_prints_it()
    {
        Assert.Equal("50000", DeskNumbers.Count(50_000));
    }
}

[Collection("UiLanguage")]
public class DeskNumbersInGermanTests
{
    [Fact]
    public void German_writes_the_decimal_comma_and_its_units()
    {
        try
        {
            UiLanguage.Set(UiLanguage.German);
            Assert.Equal("1,01\u00A0M", DeskNumbers.Short(1_010_000));
            Assert.Equal("4,5k", DeskNumbers.Short(4_500));
            Assert.Equal("0,3–294k", DeskNumbers.Range(300, 294_000));
            Assert.Equal("6,84\u00A0M+\u00A0₽", DeskNumbers.Total(6_840_000, plus: true));
            Assert.Equal("↓ −10\u00A0%", DeskNumbers.Change(-10.3));
        }
        finally
        {
            UiLanguage.Set(UiLanguage.English);
        }
    }
}
