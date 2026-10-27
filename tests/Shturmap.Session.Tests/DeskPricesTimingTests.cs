namespace Shturmap.Session.Tests;

// When the stash desk asks for the item file (owner, 2026-10-27, from the decision page: D3 E, "Ask when the file is due";
// docs/DESIGN.md §3, "The stash desk's prices come per reading"; DeskPricesTiming): at the file's Last-Modified plus its
// rhythm plus 45 s; each minute while the answer is unchanged; never more often than once a minute; every five minutes with
// no rhythm known, after a failure, and once a file is a whole beat late. The times are the decision page's: files of
// 19:10:57 and 19:21:04, a scan every ten minutes.
public class DeskPricesTimingTests
{
    private static readonly DateTimeOffset At = new(2026, 1, 1, 19, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset T(int minutes, int seconds = 0) => At + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);

    [Fact]
    public void The_rhythm_is_the_shortest_recent_beat_between_files_seen_one_after_the_other()
    {
        Assert.Null(DeskPricesTiming.Rhythm([]));
        Assert.Null(DeskPricesTiming.Rhythm([T(10, 57)]));
        Assert.Equal(TimeSpan.FromSeconds(607), DeskPricesTiming.Rhythm([T(10, 57), T(21, 4)]));
        // A file missed between two seen (the desk off screen): a gap of two beats never sets the rhythm while one of one beat
        // is there; an hour away is no beat at all.
        Assert.Equal(TimeSpan.FromSeconds(607), DeskPricesTiming.Rhythm([T(10, 57), T(21, 4), T(41, 10)]));
        Assert.Equal(TimeSpan.FromMinutes(20), DeskPricesTiming.Rhythm([T(1), T(21)]));
        Assert.Null(DeskPricesTiming.Rhythm([T(-60), T(21, 4)]));
        Assert.Null(DeskPricesTiming.Rhythm([T(21, 4), T(21, 4)]));
        // Only the newest six beats count: the five-minute beats of an hour before give way to ten.
        var times = new List<DateTimeOffset>();
        for (var m = 0; m <= 30; m += 5)
            times.Add(T(m));
        for (var m = 40; m <= 100; m += 10)
            times.Add(T(m));
        Assert.Equal(TimeSpan.FromMinutes(10), DeskPricesTiming.Rhythm(times));
    }

    [Fact]
    public void With_a_rhythm_the_file_is_asked_for_when_it_is_due_then_each_minute_while_unchanged()
    {
        var beat = TimeSpan.FromSeconds(607);
        var file = T(21, 4);
        // The file came in at 19:22: the next is due at 19:31:56 (19:21:04 + 10:07 + 0:45).
        Assert.Equal(T(31, 56), DeskPricesTiming.Due(file, beat, T(22), T(22), failed: false));
        // Asked then and still the same file: each minute.
        Assert.Equal(T(32, 56), DeskPricesTiming.Due(file, beat, T(31, 56), T(31, 56), failed: false));
        Assert.Equal(T(33, 56), DeskPricesTiming.Due(file, beat, T(32, 56), T(32, 56), failed: false));
        // The desk back on screen at 19:40 with the file of 19:01, last asked for at 19:05: due since 19:11:52, so at once.
        Assert.Equal(T(11, 52), DeskPricesTiming.Due(T(1), beat, T(5), T(5), failed: false));
        // Never two asks within a minute, however due the file is.
        Assert.Equal(T(21, 30), DeskPricesTiming.Due(T(10), beat, T(20, 30), T(20, 30), failed: false));
    }

    [Fact]
    public void A_file_a_whole_beat_late_a_failure_or_no_rhythm_falls_back_to_five_minutes()
    {
        var beat = TimeSpan.FromMinutes(10);
        var file = T(21);
        // Asked each minute from 19:31:45; at 19:41:45 a whole beat late: every five minutes until a new file sets the rhythm.
        Assert.Equal(T(41, 45), DeskPricesTiming.Due(file, beat, T(40, 45), T(40, 45), failed: false));
        Assert.Equal(T(46, 45), DeskPricesTiming.Due(file, beat, T(41, 45), T(41, 45), failed: false));
        // A failure, or tarkov.dev not reached and the saved copy given back: five minutes on, never a tight loop.
        Assert.Equal(T(36, 56), DeskPricesTiming.Due(file, beat, T(22), T(31, 56), failed: true));
        // No rhythm known (a start; tarkov.dev sent no Last-Modified): five minutes after the file was confirmed or asked
        // for, whichever was later, as before 2026-10-27.
        Assert.Equal(T(27), DeskPricesTiming.Due(file, null, T(22), null, failed: false));
        Assert.Equal(T(30), DeskPricesTiming.Due(null, beat, T(22), T(25), failed: false));
        Assert.Equal(TimeSpan.FromMinutes(5), DeskPricesTiming.Fallback);
        Assert.Equal(TimeSpan.FromMinutes(1), DeskPricesTiming.Least);
    }
}
