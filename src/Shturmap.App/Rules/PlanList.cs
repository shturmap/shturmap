namespace Shturmap.App.Rules;

/// <summary>
/// Plan's list of the suggested maps, above the open map's card. A row is what shows a map: a click on it switches
/// the map on screen, and nothing inside the card does (review of 2026-10-04, B13: the card was one button, so a
/// click on a quest's row or its pen in the card of a map that wasn't on screen also switched the map).
/// </summary>
public static class PlanList
{
    /// <summary>
    /// Whether the list is shown. One suggested map that is on screen needs none: its card says it all. But when the
    /// map on screen is another one (picked in the MAP list), that one map's row is the way to it.
    /// </summary>
    /// <param name="maps">How many maps are suggested.</param>
    /// <param name="open">The map whose card is open.</param>
    /// <param name="shown">The map on screen.</param>
    public static bool Shown(int maps, string? open, string? shown) => maps > 1 || (maps == 1 && open != shown);
}
