namespace Shturmap.Core.Operational;

/// <summary>
/// Which readings of the OPERATIONAL table call a cue, and which tasks it lists (owner, 2026-10-19, after trying the dev
/// build: "they should get the same animation as the other quests. Be mindful of multiple missions started at the same
/// time, we might want to have them in one animation included"; docs/DESIGN.md §4, principle 11). The game shows a
/// row's objectives only while it is expanded, and up to three rows a page, so a player takes a few screenshots in a
/// row: readings of screenshots taken within <see cref="Batch"/> of the one before are one reading, a batch. The first
/// of a batch's readings that brings a task the set before it didn't have calls the cue, listing every task the batch
/// read, one animation for all of them; a later one joins it (<see cref="OperationalCueCall.Joins"/>: its tasks come in
/// as rows while the cue is up, and none comes after it). A reading that brings no new task calls none: the same set
/// again, the objectives of a row expanded since, a task handed in. Its rows still say what changed (the rail's "As of",
/// a row's new progress).
/// </summary>
public sealed class OperationalReadings
{
    /// <summary>How far apart two screenshots of the table may be taken and still be one reading.</summary>
    public static readonly TimeSpan Batch = TimeSpan.FromMinutes(1);

    // The batch: its last screenshot's time, whether it called the cue, and every task it read (the newest reading of
    // each), in the order they were first read.
    private DateTimeOffset? _last;
    private bool _called;
    private readonly List<OperationalTask> _read = [];

    /// <summary>
    /// A table screenshot taken at <paramref name="at"/> was read: <paramref name="before"/> is the mode's set as it was
    /// (less what ran out), <paramref name="read"/> the tasks the screenshot showed. Returns the cue to call, or null.
    /// </summary>
    public OperationalCueCall? Read(DateTimeOffset at, IReadOnlyList<OperationalTask> before, IReadOnlyList<OperationalTask> read)
    {
        if (_last is not { } last || at - last > Batch || at < last)
        {
            _read.Clear();
            _called = false;
        }
        _last = at;
        var batch = Keys(_read);
        var keys = Keys(read);
        for (var i = 0; i < read.Count; i++)
        {
            var j = batch.IndexOf(keys[i]);
            if (j >= 0)
            {
                _read[j] = read[i];
                continue;
            }
            _read.Add(read[i]);
            batch.Add(keys[i]);
        }
        var known = Keys(before).ToHashSet(StringComparer.Ordinal);
        if (keys.All(known.Contains))
            return null;
        var joins = _called;
        _called = true;
        return new OperationalCueCall(_read.ToList(), joins);
    }

    /// <summary>
    /// Each task's identity, as the ledger tells one task on two screenshots (<see cref="OperationalLedger.ApplyTable"/>):
    /// its name (case aside), its map, and which of that name and map it is (two dailies of one name on one map stay
    /// apart).
    /// </summary>
    public static List<string> Keys(IReadOnlyList<OperationalTask> tasks)
    {
        var keys = new List<string>(tasks.Count);
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var task in tasks)
        {
            var key = task.Name.ToLowerInvariant() + "\n" + task.MapId;
            var nth = seen.GetValueOrDefault(key);
            seen[key] = nth + 1;
            keys.Add(key + "\n" + nth.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        return keys;
    }
}

/// <summary>A reading's cue: every task its batch read, in the order first read; <paramref name="Joins"/> when the batch
/// called its cue before (it joins the cue while that is up, and calls none after it).</summary>
public sealed record OperationalCueCall(IReadOnlyList<OperationalTask> Tasks, bool Joins);
