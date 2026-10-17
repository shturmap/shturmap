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

    /// <summary>
    /// How many of a map's quests each trader gave, those to complete and those to progress together: what the trader
    /// focus counts a map's row and its MAP list entry to (owner, 2026-10-17, the trader strip). A quest whose trader
    /// the data doesn't name counts for none.
    /// </summary>
    /// <param name="traders">The trader of each quest on the map's row (null: none named).</param>
    public static IReadOnlyDictionary<string, int> ByTrader(IEnumerable<string?> traders) =>
        traders.OfType<string>().GroupBy(t => t, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

    /// <summary>
    /// A map's count in its row and in the MAP list: its own ("Complete 5 · progress 1"), or with a trader's focus held
    /// that trader's quests there ("Ragman 2"). A map with none of them says nothing, and its row steps back
    /// (<see cref="StepsBack"/>): "Ragman 0" on every other row would be words to read for nothing.
    /// </summary>
    /// <param name="own">The map's own count.</param>
    /// <param name="trader">The held trader's name, or null without a focus.</param>
    /// <param name="count">The held trader's quests on the map.</param>
    public static string Count(string own, string? trader, int count) =>
        trader is null ? own : count > 0 ? RuleTexts.PlanRowTrader(trader: trader, count: count) : "";

    /// <summary>Whether a map's row steps back: a trader's focus is held and none of that trader's quests is there.</summary>
    public static bool StepsBack(string? trader, int count) => trader is not null && count == 0;

    /// <summary>
    /// NEXT RAID's rows in their order: the planner's (picks first, then what can be done), or while a trader's focus is
    /// held by that trader's quests on each map, most first, a tie keeping the planner's order, so the maps with none of
    /// them end the list (stepped back). Let go, the planner's order is back (coordinator, 2026-10-17).
    /// </summary>
    /// <param name="rows">The maps' rows in the planner's order.</param>
    /// <param name="count">The held trader's quests on a row's map.</param>
    /// <param name="held">Whether a trader's focus is held.</param>
    public static IReadOnlyList<T> Ranked<T>(IReadOnlyList<T> rows, Func<T, int> count, bool held) =>
        held ? rows.Select((row, i) => (row, i)).OrderByDescending(r => count(r.row)).ThenBy(r => r.i).Select(r => r.row).ToList() : rows;
}
