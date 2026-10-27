namespace Shturmap.App.Rules;

/// <summary>
/// A new desk page after a raid (owner, 2026-10-26: "Button + fresh after raid"; docs/DESIGN.md §4, *The stash desk*, *A new
/// page after a raid*): the first desk reading after a raid of the mode starts an empty page, and its one-line notice "A new
/// desk page after the raid." offers UNDO. The session keeps the page (GameSession.UndoDeskAsync); this is the view's rule.
/// CLEAR, which emptied the page by hand with the same notice and UNDO, went on 2026-10-27 (owner, from the decision page:
/// D1 B, the newest look in front).
/// </summary>
public static class DeskFreshPage
{
    /// <summary>
    /// Whether the notice's UNDO still stands for the desk's view (owner, 2026-10-26: "as long as the notice stands and no new
    /// reading came meanwhile"): the view's reading counter not past the one it was offered at. The notice's own going (its
    /// seconds, its ×, another notice in its place) takes the link with it.
    /// </summary>
    /// <param name="offeredAt">The desk's reading counter of the reading that started the new page.</param>
    /// <param name="reading">The reading counter of the desk's view now; null without a view.</param>
    public static bool UndoStands(int offeredAt, int? reading) => reading is not { } r || r <= offeredAt;
}
