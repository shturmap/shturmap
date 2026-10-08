namespace Shturmap.App.Rules;

/// <summary>
/// Plan's list of the suggested maps, above the open map's card. A row is what shows a map: a click on it switches
/// the map on screen, and nothing inside the card does (review of 2026-10-04, B13: the card was one button, so a
/// click on a quest's row or its pen in the card of a map that wasn't on screen also switched the map).
/// </summary>
public static class PlanList
{
    /// <summary>
    /// Whether the list is shown: whenever a row is the way to a map that isn't on screen. One suggested map that is
    /// on screen needs none: its card says it all.
    /// </summary>
    /// <param name="rows">The maps with a row, by the name Plan counts them under.</param>
    /// <param name="shown">The map on screen, by the same name.</param>
    public static bool Shown(IReadOnlyList<string> rows, string? shown) => rows.Any(r => r != shown);

    /// <summary>
    /// The card that is open: the map on screen's own (owner, 2026-10-08: the rail follows the map on screen; until
    /// then a map that wasn't suggested left the best suggestion's card open, so nothing in the rail was about the map
    /// shown). Its row's card when it is suggested; otherwise one more, after the rows, when Plan has a plan for it.
    /// Only with no map on screen, the best suggestion's.
    /// </summary>
    /// <param name="suggested">The suggested maps, best first.</param>
    /// <param name="shown">The map on screen.</param>
    /// <param name="shownPlanned">Whether there is a plan for the map on screen (with or without quests).</param>
    /// <returns>The index of the open card among the cards (the suggested ones, then the added one), or -1 for none;
    /// whether the map on screen's card is added after them.</returns>
    public static (int Open, bool Added) Open(IReadOnlyList<string> suggested, string? shown, bool shownPlanned)
    {
        if (shown is null)
            return (suggested.Count > 0 ? 0 : -1, false);
        var index = suggested.ToList().IndexOf(shown);
        if (index >= 0)
            return (index, false);
        return shownPlanned ? (suggested.Count, true) : (-1, false);
    }
}
