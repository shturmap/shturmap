using Shturmap.Core.Desk;

namespace Shturmap.Core.Tests.Desk;

// An item's price history for the desk's card (docs/DESIGN.md §3; §4, *The card*; Core.Desk.DeskHistory): tarkov.dev's
// points read, the 30 days' band, the 7-day change, the high and the low; and the day's range bar with its outlier.
// Made-up points.
public class DeskHistoryTests
{
    private static readonly DateTimeOffset Last = new(2026, 1, 1, 12, 8, 0, TimeSpan.Zero);

    private static DeskPricePoint At(double daysBack, double avg, double? min = null) => new(Last.AddDays(-daysBack), avg, min ?? avg * 0.9);

    [Fact]
    public void The_file_is_read_as_tarkov_dev_writes_it()
    {
        var json = """
                   {"data":[{"priceMin":700000,"price":743718,"timestamp":1767225600000},
                            {"priceMin":850000,"price":938649,"offerCount":52,"timestamp":1767232800000},
                            {"price":1,"priceMin":1},
                            {"priceMin":0,"price":0,"timestamp":1767240000000}],"translations":[]}
                   """;
        var history = DeskHistory.Parse(json)!;
        Assert.Equal(2, history.Points.Count);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1767225600000), history.Points[0].At);
        Assert.Equal(743_718, history.Points[0].Avg);
        Assert.Equal(700_000, history.Points[0].Min);
        Assert.Null(history.Points[0].Offers);
        Assert.Equal(52, history.Points[1].Offers);
        // A bare array too, in any order; not a history: null.
        var bare = DeskHistory.Parse("""[{"price":2,"priceMin":1,"timestamp":2000},{"price":3,"priceMin":2,"timestamp":1000}]""")!;
        Assert.Equal([1000L, 2000L], bare.Points.Select(p => p.At.ToUnixTimeMilliseconds()));
        Assert.Null(DeskHistory.Parse("<html>not here</html>"));
        Assert.Null(DeskHistory.Parse("""{"errors":["no"]}"""));
        Assert.Empty(DeskHistory.Parse("""{"data":[]}""")!.Points);
    }

    // The 7-day change: the last day's averages against the day a week before's, not one point against one.
    [Fact]
    public void The_seven_day_change_compares_a_day_with_the_day_a_week_before()
    {
        var points = new List<DeskPricePoint>();
        for (var h = 0; h < 24 * 10; h += 2)
            points.Add(At(h / 24.0, h < 24 ? 110 : 100));
        var history = new DeskHistory(points);
        Assert.Equal(10, history.Change7d!.Value, 6);
        Assert.Null(new DeskHistory([At(0, 100), At(1, 100)]).Change7d);
        Assert.Null(new DeskHistory([]).Change7d);
    }

    // The band: the last 30 days (a point a day before them is left out); the highest average and the lowest offer, and how
    // many days ago the low was.
    [Fact]
    public void The_band_is_the_last_thirty_days_with_its_high_and_low()
    {
        var history = new DeskHistory([At(45, 5_000, 10), At(29.5, 1_000), At(13.2, 900, 820), At(3, 1_050), At(0, 1_000)]);
        Assert.Equal(4, history.Recent(DeskHistory.Band).Count);
        var range = history.Range()!.Value;
        Assert.Equal(1_050, range.High);
        Assert.Equal(820, range.Low);
        Assert.Equal(13, range.LowDaysAgo);
        Assert.Null(new DeskHistory([]).Range());
    }

    // The day's bar from its low to its high; a high more than twice the average and the last low (the LEDX's one offer of
    // 9,999,999 on 2026-10-19) is past the bar, which ends a tenth past them.
    [Fact]
    public void The_days_bar_leaves_an_outlier_high_to_a_line()
    {
        var ledx = new DeskItemFacts("ledx") { Low24h = 800_000, High24h = 9_999_999, Avg24h = 983_351, LastLow = 1_000_000 };
        var day = DeskDayRange.Of(ledx)!;
        Assert.True(day.Outlier);
        Assert.Equal(1_100_000, day.BarHigh, 3);
        Assert.Equal(0.5, day.At(950_000), 3);
        Assert.Equal(1, day.At(9_999_999));
        var plain = DeskDayRange.Of(ledx with { High24h = 1_200_000 })!;
        Assert.False(plain.Outlier);
        Assert.Equal(1_200_000, plain.BarHigh);
        Assert.Null(DeskDayRange.Of(ledx with { Low24h = null }));
    }
}
