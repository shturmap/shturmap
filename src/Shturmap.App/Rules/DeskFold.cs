namespace Shturmap.App.Rules;

/// <summary>One piece of the desk's page in reading order: an open region, or a fold line of folded neighbours
/// ("UNCHANGED · POUCH ▸ · STASH ▸").</summary>
public abstract record DeskBlock
{
    public sealed record Region(string Key) : DeskBlock;

    public sealed record Fold(IReadOnlyList<string> Keys) : DeskBlock;
}

/// <summary>
/// The fold (owner, 2026-10-19, the addendum's B1; docs/DESIGN.md §4, *The head line and the fold*): each reading opens
/// the regions it changed and folds the rest into one line where they stand, "UNCHANGED · POUCH ▸ · STASH ▸" (folded
/// neighbours share it), each name opening its region until the next reading. The first reading opens all; a reading
/// that changed nothing changes nothing. A page first seen without a reading (read back from its file at start) stands
/// open.
/// </summary>
public static class DeskFold
{
    /// <summary>
    /// The regions open after a snapshot. A new reading opens all when it was the mode's first, what it changed when it
    /// changed something, and leaves the fold as it was when it changed nothing. Without a new reading the fold stays,
    /// less the regions gone from the page. <paramref name="before"/> is null until the page was first shown: a page
    /// first shown without a reading that changed something (read back from its file at start) opens every region.
    /// </summary>
    /// <param name="keys">The page's regions in reading order.</param>
    public static IReadOnlySet<string> Open(IReadOnlySet<string>? before, IReadOnlyList<string> keys, IReadOnlySet<string> changed, bool first,
        bool newReading)
    {
        var all = keys.ToHashSet(StringComparer.Ordinal);
        if (newReading && first)
            return all;
        if (newReading && changed.Count > 0)
            return keys.Where(changed.Contains).ToHashSet(StringComparer.Ordinal);
        return before?.Where(all.Contains).ToHashSet(StringComparer.Ordinal) ?? all;
    }

    /// <summary>A name in a fold line clicked: its region opens, until the next reading.</summary>
    public static IReadOnlySet<string> Unfold(IReadOnlySet<string> open, string key) => open.Append(key).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The page in reading order: each open region, and for each run of folded neighbours one fold line. A run doesn't
    /// cross <paramref name="columnBreak"/>, the index where the second column starts (the stash, from a desk 980 DIP
    /// across), so each column's fold line stands in it; −1 for one column.
    /// </summary>
    public static IReadOnlyList<DeskBlock> Blocks(IReadOnlyList<string> keys, IReadOnlySet<string> open, int columnBreak = -1)
    {
        var blocks = new List<DeskBlock>();
        var run = new List<string>();
        void Flush()
        {
            if (run.Count > 0)
                blocks.Add(new DeskBlock.Fold(run.ToList()));
            run.Clear();
        }
        for (var i = 0; i < keys.Count; i++)
        {
            if (i == columnBreak)
                Flush();
            if (open.Contains(keys[i]))
            {
                Flush();
                blocks.Add(new DeskBlock.Region(keys[i]));
            }
            else
                run.Add(keys[i]);
        }
        Flush();
        return blocks;
    }

    /// <summary>The regions whose cells come in, 50 ms apart, as a reading's page does (docs/DESIGN.md §4, *Fun*): of a new
    /// reading, the regions it opened (all on a first); nothing otherwise.</summary>
    public static IReadOnlySet<string> Entering(IReadOnlySet<string> open, IReadOnlySet<string> changed, bool first, bool newReading) =>
        !newReading ? new HashSet<string>(StringComparer.Ordinal)
        : first ? open
        : open.Where(changed.Contains).ToHashSet(StringComparer.Ordinal);
}
