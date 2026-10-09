using Shturmap.Core.Raid;

namespace Shturmap.App.Rules;

/// <summary>
/// What the window lets go of, and holds back, around a raid (review of 2026-10-04, A42 and H9). In a raid nobody
/// is at Shturmap's mouse, and the raid card and the map are what the window is for: nothing may lie over them that
/// a click would have to take away.
/// </summary>
public static class WhileInRaid
{
    /// <summary>
    /// A step into a raid: it begins to load, or it starts. What lies over the window at that moment lets go. The
    /// cards: one held in Plan used to stay over the map through the loading and the whole raid, and one opened
    /// while the raid loaded would stay through the raid. And the help panel, which stays open while another window
    /// has the focus, so it lay over the raid card. A card the player opens during the raid stays as any held card
    /// does.
    /// </summary>
    public static bool LetsGo(RaidPhase before, RaidPhase now) => now != before && now != RaidPhase.Menu;

    /// <summary>
    /// What comes up by itself and waits for an answer or a click waits for the raid to be over: the help panel at
    /// the first start, and the question after a crash. Both come at the next chance.
    /// </summary>
    public static bool Waits(RaidPhase phase) => phase != RaidPhase.Menu;
}
