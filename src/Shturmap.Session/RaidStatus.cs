using System.Globalization;
using Shturmap.Core.Logs;
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
    /// <summary>
    /// The words in the status bar for a snapshot: they name the raid's own map, never one that is only being looked
    /// at (the MAP list in a raid), and no map while the log names one the data doesn't know.
    /// </summary>
    public static string Text(SessionSnapshot snapshot, DateTime now, TimeZoneInfo? zone = null) => Text(snapshot.Raid, snapshot.RaidMap?.Name, now, zone);

    /// <summary>The words in the status bar ("In raid · Streets of Tarkov · PMC · 12 min").</summary>
    /// <param name="map">The raid's map, or null when it isn't known: then the words name none.</param>
    /// <param name="zone">The time zone of the two clock times, for the minutes passed across a clock change
    /// (<see cref="WallClock"/>); the PC's own unless a test gives another.</param>
    public static string Text(RaidState raid, string? map, DateTime now, TimeZoneInfo? zone = null)
    {
        var side = raid.Side switch { RaidSide.Pmc => "PMC", RaidSide.Scav => "Scav", _ => null };
        return raid.Phase switch
        {
            RaidPhase.Loading => $"Loading {map}".TrimEnd(),
            RaidPhase.InRaid => string.Join(" · ", new[]
                {
                    "In raid", map, side,
                    raid.RaidStartedAt is { } started ? $"{(int)WallClock.Elapsed(started, now, zone).TotalMinutes} min" : null,
                }
                .Where(part => !string.IsNullOrEmpty(part))),
            _ => "Not in a raid",
        };
    }

    /// <summary>
    /// The last raid in one line: "Last raid · Customs · 12 min · PMC". A raid the log never ended (the game was
    /// closed or crashed in it) has no length to say: "Last raid · Customs · end not in the log · PMC".
    /// </summary>
    public static string LastRaid(LastRaidView last) => string.Join(" · ", new[]
    {
        "Last raid",
        last.MapName,
        last.LengthKnown ? $"{(int)last.Duration.TotalMinutes} min" : "end not in the log",
        last.Side switch { RaidSide.Pmc => "PMC", RaidSide.Scav => "Scav", _ => null },
    }.Where(part => !string.IsNullOrEmpty(part)));

    /// <summary>Where the words come from, for the tooltip.</summary>
    public static string Tooltip(RaidState raid, DateTime now) => raid.Phase switch
    {
        RaidPhase.Loading => "The game's log shows a raid loading",
        RaidPhase.InRaid when raid.RaidStartedAt is { } started =>
            "The game's log shows a raid, started " + started.ToString(started.Date == now.Date ? "HH:mm" : "d MMM HH:mm", CultureInfo.CurrentCulture),
        RaidPhase.InRaid => "The game's log shows a raid",
        _ => "The game's log shows no raid (it can't tell the menus from a closed game)",
    };
}
