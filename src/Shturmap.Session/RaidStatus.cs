using System.Globalization;
using Shturmap.Core.Raid;

namespace Shturmap.Session;

/// <summary>
/// The raid state in the status bar, saying only what the game's log shows (owner, 2026-10-03: "in the menus" while
/// the game wasn't even running was misleading). The application log has no line that marks the game quitting: the
/// sequence "Disposing BEClient … Dll released" that ends some sessions also comes at startup and between raids, and
/// about half the sessions just stop after an ordinary line (all 22 sessions, 2026-08-15 to 2026-10-03). So the log
/// can't tell the menus from a closed game, and outside a raid the state is the one thing that is always true.
/// </summary>
public static class RaidStatus
{
    /// <summary>The words in the status bar ("In raid · Streets of Tarkov · PMC · 12 min").</summary>
    public static string Text(RaidState raid, string? map, DateTime now)
    {
        var side = raid.Side switch { RaidSide.Pmc => " · PMC", RaidSide.Scav => " · Scav", _ => "" };
        return raid.Phase switch
        {
            RaidPhase.Loading => $"Loading {map}".TrimEnd(),
            RaidPhase.InRaid when raid.RaidStartedAt is { } started => $"In raid · {map}{side} · {(int)(now - started).TotalMinutes} min",
            RaidPhase.InRaid => $"In raid · {map}{side}",
            _ => "Not in a raid",
        };
    }

    /// <summary>Where the words come from, for the tooltip.</summary>
    public static string Tooltip(RaidState raid, DateTime now) => raid.Phase switch
    {
        RaidPhase.Loading => "The game's log shows a raid loading",
        RaidPhase.InRaid when raid.RaidStartedAt is { } started =>
            "The game's log shows a raid, started " + started.ToString(started.Date == now.Date ? "HH:mm" : "d MMM HH:mm", CultureInfo.CurrentCulture),
        RaidPhase.InRaid => "The game's log shows a raid",
        _ => "The game's log shows no raid. It doesn't say whether the game is open, so Shturmap doesn't claim the menus",
    };
}
