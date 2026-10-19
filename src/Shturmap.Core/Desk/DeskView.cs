using Shturmap.Core.Logs;

namespace Shturmap.Core.Desk;

/// <summary>
/// What the session publishes for the stash desk's view (<c>SessionSnapshot.Desk</c>; owner, 2026-10-19, from a panel: A1 ·
/// B1 · C1 · D2, and the addendum: A(2) · B1 · C1a · Da; docs/DESIGN.md §4, "Screen anatomy", *The stash desk*): the mode's
/// page in hand with every cell's value and keep notch (<see cref="Sheet"/>: the regions in reading order, the head line's
/// totals), what the last reading changed (the fold's input), and how old the prices are. Immutable; a view can make one
/// of made-up regions with <see cref="DeskSheet.Of"/> or by hand.
/// </summary>
/// <param name="Mode">The game mode the page is of (PvE and PvP keep a page each).</param>
/// <param name="Sheet">The page and its values: <see cref="DeskSheet.Page"/> (when it was read, its regions),
/// <see cref="DeskSheet.Regions"/> (in reading order, each with its part ON YOU, STASH or neither, its total and its "+",
/// its cells' figures and notches), <see cref="DeskSheet.Totals"/> (the head line "ON YOU … · STASH … · KEEP n · n · n").</param>
/// <param name="Changed">The keys of the regions the last reading changed (*The head line and the fold*): the view opens
/// these and folds the rest into "UNCHANGED · POUCH ▸ · STASH ▸"; empty when the last reading changed nothing (then the
/// fold stays as it was).</param>
/// <param name="First">The last reading was the mode's first: every region opens.</param>
/// <param name="Reading">Counts up with every reading taken (a reading that changed nothing too), so a view can tell a new
/// one from a republish.</param>
/// <param name="PricesAt">When the item file with the prices was last confirmed current with tarkov.dev (the card's "Prices 4
/// min old · tarkov.dev"); null while the prices haven't loaded (the cells show no figure).</param>
public sealed record DeskView(GameMode Mode, DeskSheet Sheet, IReadOnlySet<string> Changed, bool First, int Reading, DateTimeOffset? PricesAt)
{
    /// <summary>The player's flea level isn't read from anywhere (docs/DESIGN.md §4, *The click*'s guards): a figure says
    /// the item's flea level and compares nothing.</summary>
    public bool PlayerLevelKnown => false;

    /// <summary>An item's price facts, for the card (the day's range, the traders, the offers) and a click's copy (step C);
    /// null for an item the item file doesn't have.</summary>
    public Func<string, DeskItemFacts?> FactsOf { get; init; } = _ => null;

    /// <summary>The flea's rules the figures were made with, for a click's copy.</summary>
    public DeskFleaRules Flea { get; init; } = DeskFleaRules.Default;
}
