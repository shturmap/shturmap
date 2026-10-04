namespace Shturmap.Core.Logs;

/// <summary>
/// From wall-clock times to time passed. The game's logs give local times without an offset, a screenshot's time and
/// the PC's clock are local too, and taking one from another is an hour off across a clock change: on the night the
/// clocks go forward a raid begun at 01:50 was 95 minutes old at 03:25 (it is 35), and was closed as one that can't
/// still be running; on the night they go back a raid begun at 02:50 and ended at 02:10 ran for minus 40 minutes
/// (review of 2026-10-04, A41). Every span of time computed from such times goes through here, by Windows' rules
/// for the PC's time zone; what the player reads ("started 21:02") stays the local time as it was written.
/// <para>
/// The hour a change repeats (02:00 to 03:00 on the autumn night) can stand for two instants, and a log's time
/// doesn't say which. No reading is fixed: it is settled by what is known of the order.
/// <see cref="Elapsed"/> is for two times whose order is known, and takes the reading in which time doesn't run
/// backwards, and of two such the one with the least time between: a raid of 20 minutes rather than 80, and an
/// open raid that began in the repeated hour counts from its later reading, since closing a raid that still runs is
/// the worse mistake. <see cref="Apart"/> is for two times that lie close together in either order, and takes the
/// nearest reading. <see cref="Sequence"/> is for the lines of one log, whose times never go backwards: it starts
/// with the first pass through the hour, because only then is the step back to 02:00 seen for what it is.
/// </para>
/// </summary>
public static class WallClock
{
    /// <summary>
    /// The instants (UTC) a wall-clock time can stand for: one, or two in the hour a clock change repeats, the earlier
    /// first. A time that says of itself what it is counts as that: UTC, and a local time from the PC's own clock or
    /// file system (<see cref="DateTime.Now"/>, a file's creation time), which .NET marks with the side of the repeated
    /// hour it is on. A time in the hour a spring change skips, which no clock shows, is read as if the clocks hadn't
    /// moved yet.
    /// </summary>
    /// <param name="zone">The time zone the wall clock is in; the PC's own unless a test gives another.</param>
    public static IReadOnlyList<DateTime> Instants(DateTime time, TimeZoneInfo? zone = null)
    {
        zone ??= TimeZoneInfo.Local;
        if (time.Kind == DateTimeKind.Utc)
            return [time];
        if (time.Kind == DateTimeKind.Local && (ReferenceEquals(zone, TimeZoneInfo.Local) || zone.Equals(TimeZoneInfo.Local)))
            return [time.ToUniversalTime()];
        var wall = DateTime.SpecifyKind(time, DateTimeKind.Unspecified);
        if (zone.IsAmbiguousTime(wall))
            return zone.GetAmbiguousTimeOffsets(wall).Select(offset => Utc(wall - offset)).Order().ToArray();
        if (zone.IsInvalidTime(wall))
        {
            var before = wall;
            while (zone.IsInvalidTime(before))
                before = before.AddMinutes(-15);
            return [Utc(wall - zone.GetUtcOffset(before))];
        }
        return [TimeZoneInfo.ConvertTimeToUtc(wall, zone)];
    }

    private static DateTime Utc(DateTime time) => DateTime.SpecifyKind(time, DateTimeKind.Utc);

    /// <summary>
    /// The time passed from one wall-clock time to a later one. Where one of them lies in the repeated hour: the
    /// reading in which time doesn't run backwards, and of two such the shorter. Negative only when every reading is
    /// (the clock was set back by hand): then the one nearest to nothing.
    /// </summary>
    public static TimeSpan Elapsed(DateTime from, DateTime to, TimeZoneInfo? zone = null)
    {
        TimeSpan? best = null;
        foreach (var span in Spans(from, to, zone))
        {
            if (best is not { } known || (span >= TimeSpan.Zero ? known < TimeSpan.Zero || span < known : known < TimeSpan.Zero && span > known))
                best = span;
        }
        return best!.Value;
    }

    /// <summary>
    /// How far <paramref name="to"/> lies after <paramref name="from"/> (negative: before it), for two times known to
    /// be close together but not in which order (a note of the insurer around a raid's end, a screenshot around it):
    /// of the readings, the one that puts them nearest to each other.
    /// </summary>
    public static TimeSpan Apart(DateTime from, DateTime to, TimeZoneInfo? zone = null) =>
        Spans(from, to, zone).MinBy(span => span.Duration());

    private static IEnumerable<TimeSpan> Spans(DateTime from, DateTime to, TimeZoneInfo? zone)
    {
        var ends = Instants(to, zone);
        foreach (var start in Instants(from, zone))
        {
            foreach (var end in ends)
                yield return end - start;
        }
    }

    /// <summary>
    /// The instants of one log's lines, in the order they were written: a log's times never go backwards, so in the
    /// repeated hour each line takes the earliest reading that isn't before the line above it. Events of several
    /// logs are put in order by these, not by their wall-clock times, which run through that hour twice.
    /// </summary>
    public sealed class Sequence(TimeZoneInfo? zone = null)
    {
        private DateTime? _last;

        /// <summary>The time zone the log's clock is in; null is the PC's own.</summary>
        public TimeZoneInfo? Zone => zone;

        /// <summary>The instant of the next line's time.</summary>
        public DateTime Next(DateTime time)
        {
            var readings = Instants(time, zone);
            // When no reading is at or after the line above, the clock was set back by hand: taken as written, in its
            // later reading.
            var instant = _last is { } last ? readings.Where(r => r >= last).DefaultIfEmpty(readings[^1]).First() : readings[0];
            _last = instant;
            return instant;
        }
    }
}
