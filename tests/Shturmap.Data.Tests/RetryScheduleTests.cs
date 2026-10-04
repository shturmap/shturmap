using Shturmap.Data.TarkovDev;

namespace Shturmap.Data.Tests;

// How long a failed load waits before tarkov.dev is asked again (owner, 2026-10-04: no unnecessary load on
// tarkov.dev; it was every 2 minutes for as long as it failed).
public class RetryScheduleTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    [InlineData(5, 30)]
    [InlineData(6, 30)]
    [InlineData(500, 30)]
    // Asked before anything failed: the shortest wait.
    [InlineData(0, 2)]
    [InlineData(-3, 2)]
    public void The_wait_doubles_from_two_minutes_and_stays_at_thirty(int failures, int minutes) =>
        Assert.Equal(TimeSpan.FromMinutes(minutes), RetrySchedule.Wait(failures));

    [Fact]
    public void A_day_of_failing_asks_about_fifty_times_not_seven_hundred()
    {
        var asked = 0;
        for (var waited = TimeSpan.Zero; waited < TimeSpan.FromHours(24); asked++)
            waited += RetrySchedule.Wait(asked + 1);
        Assert.InRange(asked, 45, 55);
    }

    [Theory]
    [InlineData(120, "2 minutes")]
    [InlineData(60, "1 minute")]
    [InlineData(1800, "30 minutes")]
    [InlineData(1, "1 second")]
    [InlineData(20, "20 seconds")]
    public void A_wait_is_said_in_words(int seconds, string words) =>
        Assert.Equal(words, RetrySchedule.InWords(TimeSpan.FromSeconds(seconds)));
}
