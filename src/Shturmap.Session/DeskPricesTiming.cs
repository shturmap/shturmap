using Shturmap.Data.TarkovDev;

namespace Shturmap.Session;

/// <summary>
/// When the stash desk asks for tarkov.dev's item file again (owner, 2026-10-27, from the decision page: D3 E, "Ask when
/// the file is due"; docs/DESIGN.md §3, "The stash desk's prices come per reading"). The file changes on a rhythm of its own
/// (about every 10 minutes, about 3 minutes after each scan, measured 2026-10-27): the next is due at the file in hand's
/// Last-Modified plus that rhythm plus 45 s; while the answer is unchanged, again each minute; never more often than once a
/// minute; every five minutes while no rhythm is known, after a failure, and once a file is a whole beat late (its rhythm
/// lost). The session asks one request at a time (GameSession.RefreshDeskPrices).
/// </summary>
public static class DeskPricesTiming
{
    /// <summary>Never two asks closer than this.</summary>
    public static readonly TimeSpan Least = TimeSpan.FromMinutes(1);

    /// <summary>After the time a file is due by its rhythm, the room tarkov.dev's build of it may take.</summary>
    public static readonly TimeSpan Margin = TimeSpan.FromSeconds(45);

    /// <summary>The ask's pace with no rhythm known, after a failure and once the rhythm is lost: five minutes.</summary>
    public static TimeSpan Fallback => GameDataLoader.DeskPricesAge;

    /// <summary>The gaps between two Last-Modified values seen one after the other that count as a beat: one to thirty
    /// minutes. A longer one is files missed (the desk off screen, the PC asleep), not a beat.</summary>
    public static readonly TimeSpan ShortestBeat = TimeSpan.FromMinutes(1), LongestBeat = TimeSpan.FromMinutes(30);

    /// <summary>How many of the newest beats the rhythm is taken from.</summary>
    public const int Beats = 6;

    /// <summary>
    /// The file's rhythm from the Last-Modified values seen, oldest first: the shortest of the last <see cref="Beats"/>
    /// gaps that count as a beat (a gap of two beats, a file missed between, never sets it while one of one beat is there);
    /// null while fewer than two values one beat apart were seen.
    /// </summary>
    public static TimeSpan? Rhythm(IReadOnlyList<DateTimeOffset> modified)
    {
        TimeSpan? shortest = null;
        var beats = 0;
        for (var i = modified.Count - 1; i > 0 && beats < Beats; i--)
        {
            var gap = modified[i] - modified[i - 1];
            if (gap < ShortestBeat || gap > LongestBeat)
                continue;
            beats++;
            if (shortest is null || gap < shortest)
                shortest = gap;
        }
        return shortest;
    }

    /// <summary>When the next ask is due.</summary>
    /// <param name="modifiedAt">The file in hand's Last-Modified; null where tarkov.dev sent none.</param>
    /// <param name="rhythm">The file's rhythm (<see cref="Rhythm"/>); null while none is known.</param>
    /// <param name="pricesAt">When the file in hand was last confirmed current.</param>
    /// <param name="asked">The last ask in this mode, whatever came of it; null for none.</param>
    /// <param name="failed">That ask failed, or tarkov.dev wasn't reached and the saved copy came back.</param>
    public static DateTimeOffset Due(DateTimeOffset? modifiedAt, TimeSpan? rhythm, DateTimeOffset pricesAt, DateTimeOffset? asked, bool failed)
    {
        var least = asked is { } a ? a + Least : DateTimeOffset.MinValue;
        if (failed && asked is { } tried)
            return tried + Fallback;
        if (modifiedAt is { } modified && rhythm is { } beat)
        {
            // A whole beat late and asked since: the rhythm is lost, every five minutes again until a new file sets it.
            if (asked is { } last && last >= modified + 2 * beat + Margin)
                return Later(last + Fallback, least);
            // Before the next file is due, at that time; once it is, each minute while the answer is unchanged.
            return Later(modified + beat + Margin, least);
        }
        var since = asked is { } at && at > pricesAt ? at : pricesAt;
        return Later(since + Fallback, least);
    }

    private static DateTimeOffset Later(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
