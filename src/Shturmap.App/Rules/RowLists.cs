namespace Shturmap.App.Rules;

/// <summary>
/// The rows the rail shows (Plan's cards, ANY MAP, the raid card's quests, kit and BRING, the extracts) are made anew
/// from every snapshot. The list on screen stays when the new one says the same, row by row, so its rows aren't built
/// again (review of 2026-10-09: every snapshot rebuilt them, which lost the hover and the preview under a resting
/// pointer and drew the pictures and pens anew). A row whose content changed makes the new list the one shown.
/// </summary>
public static class RowLists
{
    /// <summary>The list on screen when <paramref name="made"/> says the same; otherwise <paramref name="made"/>.</summary>
    public static IReadOnlyList<T> Keep<T>(IReadOnlyList<T> shown, IReadOnlyList<T> made, Func<T, T, bool> same) =>
        Same(shown, made, same) ? shown : made;

    /// <inheritdoc cref="Keep{T}(IReadOnlyList{T}, IReadOnlyList{T}, Func{T, T, bool})"/>
    public static IReadOnlyList<T> Keep<T>(IReadOnlyList<T> shown, IReadOnlyList<T> made) => Same(shown, made) ? shown : made;

    /// <summary>The same rows in the same order. Null is the same as null only.</summary>
    public static bool Same<T>(IReadOnlyList<T>? a, IReadOnlyList<T>? b, Func<T, T, bool> same)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a is null || b is null || a.Count != b.Count)
            return false;
        for (var i = 0; i < a.Count; i++)
        {
            if (!same(a[i], b[i]))
                return false;
        }
        return true;
    }

    /// <summary>The same values in the same order (strings, records without lists in them).</summary>
    public static bool Same<T>(IReadOnlyList<T>? a, IReadOnlyList<T>? b) => Same(a, b, EqualityComparer<T>.Default.Equals);
}
