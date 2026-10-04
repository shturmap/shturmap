namespace Shturmap.App.Rules;

/// <summary>How strongly a row is lit by what the pointer is on.</summary>
public enum LinkLevel
{
    /// <summary>Not lit.</summary>
    None,

    /// <summary>It belongs to what is pointed at without being it: the items a pointed-at quest needs, the quests a
    /// pointed-at item is for. A weaker tint.</summary>
    Related,

    /// <summary>It shows the very thing pointed at (the same quest, item, objective or way out), here or elsewhere.</summary>
    Same,
}

/// <summary>
/// The linked highlight's two strengths (owner, 2026-10-04, from the review's E2: one tint stood for three things,
/// the row pointed at, the same thing elsewhere, and what is only related). The same thing keeps the full tint;
/// what is related gets a weaker one; and what is neither is not lit: pointing at an item used to light every other
/// item of the quests it is for, because rows matched on any quest they shared.
/// </summary>
public static class LinkStrength
{
    /// <param name="quest">The quest a row is (a quest's row), or null.</param>
    /// <param name="serves">The quests a row's item is for (a BRING row), or none.</param>
    /// <param name="item">The item a row shows, or null.</param>
    /// <param name="marker">The map marker a row stands for (a way out), or null.</param>
    /// <param name="objective">The objective a row is, or null.</param>
    public static LinkLevel Of(string? quest, IEnumerable<string> serves, string? item, string? marker, string? objective,
        IReadOnlySet<string> focusQuests, string? focusItem, string? focusMarker, string? focusObjective)
    {
        if ((item is not null && item == focusItem) || (marker is not null && marker == focusMarker)
            || (objective is not null && objective == focusObjective))
            return LinkLevel.Same;
        if (focusItem is not null)
        {
            // An item is pointed at. The quests it is for are related; another item is neither, whatever quests the
            // two share.
            return item is null && quest is not null && focusQuests.Contains(quest) ? LinkLevel.Related : LinkLevel.None;
        }
        // A quest is pointed at (or a place of one on the map): its own rows are the same thing, the rows of what it
        // needs are related.
        if (quest is not null && focusQuests.Contains(quest))
            return LinkLevel.Same;
        return serves.Any(focusQuests.Contains) ? LinkLevel.Related : LinkLevel.None;
    }
}
