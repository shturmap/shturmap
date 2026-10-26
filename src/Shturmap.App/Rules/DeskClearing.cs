using Shturmap.Core.Desk;

namespace Shturmap.App.Rules;

/// <summary>
/// Clearing the desk's page (owner, 2026-10-26: "We need an option to clear the desk because it quickly gets messy"; from
/// the coordinator's options, "Button + fresh after raid"; docs/DESIGN.md §4, *The stash desk*, *Clearing the page*): CLEAR
/// in the desk's head empties the page in hand for the mode at once, with no question (principle 7), and the one-line
/// notice "Desk cleared." offers UNDO; the first reading after a raid starts a new page, its notice offering UNDO too. The
/// session keeps the page (GameSession.ClearDeskAsync, UndoDeskAsync); these are the view's rules.
/// </summary>
public static class DeskClearing
{
    /// <summary>Whether CLEAR stands in the desk's head: there is a page with something on it (a region read). Not on a page
    /// of no region (cleared, or a reading that read nothing), where it would clear nothing, nor without a page.</summary>
    public static bool Offered(DeskView? view) => view is { Sheet.Page.Regions.Count: > 0 };

    /// <summary>
    /// Whether a notice's UNDO still stands for the desk's view (owner, 2026-10-26: "as long as the notice stands and no new
    /// reading came meanwhile"): the view's reading counter not past the one it was offered at. The notice's own going (its
    /// seconds, its ×, another notice in its place) takes the link with it.
    /// </summary>
    /// <param name="offeredAt">The desk's reading counter when the page was taken away (a new page after a raid: that
    /// reading's).</param>
    /// <param name="reading">The reading counter of the desk's view now; null without a view.</param>
    public static bool UndoStands(int offeredAt, int? reading) => reading is not { } r || r <= offeredAt;

    /// <summary>The made-up desk cleared (the developer script's "desk clear" on a developer run's desk): the same view with a
    /// page of no region, every region drawn as never read, the same reading (nothing comes in).</summary>
    public static DeskView Emptied(DeskView view, DateTimeOffset at) => view with
    {
        Sheet = DeskSheet.Of(new DeskPage(at, []), view.FactsOf, view.Flea),
        Changed = new HashSet<string>(StringComparer.Ordinal),
        First = false,
    };
}
