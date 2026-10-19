namespace Shturmap.App.Rules;

/// <summary>The window's three states (docs/DESIGN.md §4, principle 3): Plan or Desk in the menus, Raid while one loads or
/// runs.</summary>
public enum WindowState
{
    Plan,
    Desk,
    Raid,
}

/// <summary>
/// PLAN | DESK, the one choice of view (owner, 2026-10-19, from a panel: A1; docs/DESIGN.md §4, principle 3 and "Screen
/// anatomy", *The stash desk*, *Its place*): the desk takes the map's place out of a raid; it opens by itself when a
/// stash or inventory screenshot is read in the menus, and the switch (or Esc) leaves it. A raid loading always takes the
/// window to Raid, with no click, and back to Plan when it is over: the desk keeps its page for next time.
/// </summary>
public static class DeskSwitch
{
    /// <summary>What the window shows: Raid while one loads or runs, else the view chosen.</summary>
    public static WindowState Shown(bool inRaid, bool deskChosen) => inRaid ? WindowState.Raid : deskChosen ? WindowState.Desk : WindowState.Plan;

    /// <summary>
    /// Whether the desk is chosen after a snapshot. A raid (loading or running) leaves it, so the window comes back to
    /// Plan after; a new reading in the menus opens it, unless the tour is up (it owns the window: the reading waits
    /// on the switch); otherwise it stays as it was.
    /// </summary>
    /// <param name="newReading">The snapshot's desk brought a reading this window hasn't seen (<see cref="IsNew"/>).</param>
    public static bool ChosenAfter(bool chosen, bool inRaid, bool newReading, bool tourUp) =>
        !inRaid && (newReading && !tourUp || chosen);

    /// <summary>
    /// Whether a desk's reading counter says a reading was taken since the last one this window saw. The counter counts
    /// the readings taken while Shturmap runs: a page read back from its file at start is 0 and opens nothing; before
    /// any desk, nothing was seen (0).
    /// </summary>
    public static bool IsNew(int? reading, int seen) => reading is { } r && r > seen;

    /// <summary>The map's keys (docs/DESIGN.md §4, "Keyboard": F, + and −, 0, PgUp and PgDn), as Windows' virtual-key codes:
    /// while the desk is up they leave the map, out of sight, alone, and PgUp and PgDn scroll the desk's page.</summary>
    public static bool MapKey(int virtualKey) =>
        virtualKey is 0x46 or 0x30 or 0x60 or 0x21 or 0x22 || ZoomKeys.In.Any(k => k.Key == virtualKey) || ZoomKeys.Out.Any(k => k.Key == virtualKey);
}
