namespace Shturmap.App.Rules;

/// <summary>Where a row of the rail stands, in the rail's own coordinates (scrolling doesn't change them).</summary>
public readonly record struct RowBox(double Top, double Left, double Height)
{
    public double Bottom => Top + Height;
}

/// <summary>
/// What a row shows, so that it is found again after the rail's rows were made anew (they are, with every snapshot):
/// a map of Plan's list, or the quest, objective, item and marker a linked row names. Rows that show the same thing
/// (a quest in NEXT and in its block has another objective; an item in two BRING lists is the same row twice) are
/// told apart by their order on screen (<see cref="RowSteps.Find"/>).
/// </summary>
public sealed record RowId(string Kind, string Key)
{
    public static RowId Map(string normalizedName) => new("map", normalizedName);

    public static RowId Linked(string? quest, string? objective, string? item, string? marker) =>
        new("row", string.Join('|', quest ?? "", objective ?? "", item ?? "", marker ?? ""));
}

/// <summary>
/// The keyboard's way through the rail's rows (the review of 2026-10-04, E6: the linked highlight needed a pointer).
/// Down and Up step through the rows in the order they stand on screen, top to bottom. There is no wrapping: Down
/// on the last row stays there, as a list does, since a jump from the foot of a long raid card back to its head
/// would lose the place. With no row yet, Down starts at the first row in view and Up at the last one in view, so a
/// rail that was scrolled is entered where the player is looking.
/// </summary>
public static class RowSteps
{
    /// <summary>
    /// The rows' indices in the order they stand on screen: top to bottom; of two that start at the same height, the
    /// taller first (a quest's block before its first line), then left to right.
    /// </summary>
    public static IReadOnlyList<int> Order(IReadOnlyList<RowBox> rows) =>
        Enumerable.Range(0, rows.Count)
            .OrderBy(i => rows[i].Top).ThenByDescending(i => rows[i].Height).ThenBy(i => rows[i].Left).ThenBy(i => i)
            .ToList();

    /// <summary>The row a step leads to, as a place in the rows' order; -1 when there are none.</summary>
    /// <param name="rows">The rows in their order on screen (<see cref="Order"/>).</param>
    /// <param name="current">The place of the keyboard's row in that order, or -1 when it has none yet.</param>
    /// <param name="viewTop">Where the part of the rail in view begins and ends, in the rows' coordinates.</param>
    public static int Step(IReadOnlyList<RowBox> rows, int current, bool down, double viewTop, double viewBottom)
    {
        if (rows.Count == 0)
            return -1;
        if (current >= 0 && current < rows.Count)
            return down ? Math.Min(current + 1, rows.Count - 1) : Math.Max(current - 1, 0);
        if (down)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].Bottom > viewTop && rows[i].Top < viewBottom)
                    return i;
            }
            return 0;
        }
        for (var i = rows.Count - 1; i >= 0; i--)
        {
            if (rows[i].Top < viewBottom && rows[i].Bottom > viewTop)
                return i;
        }
        return rows.Count - 1;
    }

    /// <summary>
    /// A row found again among rows made anew: the one that shows the same thing, and of several that do, the one in
    /// the same place among them (the <paramref name="nth"/>, counted from 0), or their last when there are fewer
    /// now. -1 when none shows it any more (the quest was completed, the raid ended).
    /// </summary>
    public static int Find(IReadOnlyList<RowId> rows, RowId wanted, int nth)
    {
        var same = Enumerable.Range(0, rows.Count).Where(i => rows[i] == wanted).ToList();
        return same.Count == 0 ? -1 : same[Math.Clamp(nth, 0, same.Count - 1)];
    }

    /// <summary>Which of the rows that show the same thing a row is, counted from 0 (for <see cref="Find"/>).</summary>
    public static int NthOf(IReadOnlyList<RowId> rows, int index) =>
        Enumerable.Range(0, index).Count(i => rows[i] == rows[index]);
}
