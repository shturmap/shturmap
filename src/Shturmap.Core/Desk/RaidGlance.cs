namespace Shturmap.Core.Desk;

/// <summary>
/// What the last raid screenshot of an inventory screen showed, for the in-raid glance (owner, 2026-10-19, three choices;
/// docs/DESIGN.md §2, "For the stash desk", and §4, *The in-raid glance*): the reading's page with its values, the player's
/// own regions (ON YOU) and the LOOT pane's (a body's slots and parts, a container's grid; LOOT), and the totals of each.
/// Published for the plate and kept nowhere: no file, never the desk's page in hand; it goes with the raid.
/// </summary>
/// <param name="Sheet">The page with its values: each region's part (ON YOU, LOOT, a window attached to none NEITHER), each
/// cell's figure and keep notch; <see cref="DeskTotals.OnYou"/> and <see cref="DeskTotals.Loot"/> the totals, a "+" where
/// contents went unseen (a part not searched, a container without its window, a weapon's mods).</param>
/// <param name="Loot">The LOOT pane's title in the game's words ("LOOT · BODY", "LOOT · DRAWER"), or null when the screen
/// showed the player's inventory alone.</param>
/// <param name="TakenAt">When the screenshot was taken (the file's creation).</param>
/// <param name="Reading">How many inventory screenshots this raid were read up to this one: a new one is a higher number.</param>
public sealed record RaidGlance(DeskSheet Sheet, string? Loot, DateTimeOffset TakenAt, int Reading);
