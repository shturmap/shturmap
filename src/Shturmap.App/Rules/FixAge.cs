namespace Shturmap.App.Rules;

/// <summary>
/// How old the last position is, in the words the window uses for it: the status bar ("Fix 4 min ago") and the raid
/// card's line on where its distances come from say the same age, and both count with the clock (2026-10-04: the
/// raid card's line was set only when a snapshot arrived, said "1 min ago" from 45 s on and stayed there while the
/// status bar counted on).
/// </summary>
public static class FixAge
{
    /// <summary>
    /// Facing-relative directions are only true briefly after a fix; past this they turn into map directions. The
    /// same duration the map draws the facing cone for (<see cref="Shturmap.Core.Navigation.Facing.Fresh"/>).
    /// </summary>
    public static readonly TimeSpan Fresh = Shturmap.Core.Navigation.Facing.Fresh;

    /// <summary>
    /// How old a position is: the time that passed since its screenshot, not the two clock times apart, which are an
    /// hour off across a clock change (<see cref="Shturmap.Core.Logs.WallClock"/>; the review of 2026-10-04, A41).
    /// </summary>
    /// <param name="zone">The time zone of the two times; the PC's own unless a test gives another.</param>
    public static TimeSpan Of(DateTime fixAt, DateTime now, TimeZoneInfo? zone = null) => Shturmap.Core.Logs.WallClock.Elapsed(fixAt, now, zone);

    /// <summary>"12 s", "4 min", "2 h": whole units, never rounded up.</summary>
    public static string Text(TimeSpan age) =>
        age.TotalSeconds < 60 ? $"{Math.Max(0, (int)age.TotalSeconds)} s"
        : age.TotalMinutes < 60 ? $"{(int)age.TotalMinutes} min"
        : $"{(int)age.TotalHours} h";

    /// <summary>
    /// The raid card's line under the raid line: nothing while the position is fresh (the distances are as good as
    /// they get), then how old the screenshot they are measured from is; without a position, how to get one.
    /// </summary>
    /// <param name="keys">The game's screenshot keys, as help names them ("PrtSc or Home").</param>
    public static string Note(TimeSpan? age, string keys) => age switch
    {
        null => $"No position yet: press {keys} for distances",
        { } a when a < Fresh => "",
        { } a => $"Distances from your screenshot {Text(a)} ago",
    };
}
