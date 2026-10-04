using Shturmap.Core.Logs;

namespace Shturmap.App.Rules;

/// <summary>
/// How long the raid has run and how long it still runs: what a player looks for in a raid (owner, 2026-10-04: "In
/// the raid we do not care when the start time was and all this info, we typically want to see how much time we're
/// in the raid and how much time is left. This is crucial information"). The time in the raid is counted from the
/// raid's start line in the game's log. The time left is the map's raid length (tarkov.dev) minus that: a figure
/// the game's log never states, so it is given only where the two it is made from are known, and its tooltip says
/// how it is made and when it can be off. A Scav joins a raid already under way, and the log doesn't say how long
/// that raid still runs: no time left for a Scav.
/// </summary>
public static class RaidTime
{
    /// <param name="Text">"12 min in · 28 min left".</param>
    /// <param name="Tip">Where the figures come from.</param>
    public sealed record Reading(string Text, string Tip);

    public const string Tip = "Time in the raid: since the raid's start in the game's log. Time left: this map's raid length minus that. It can be off after a reconnect.";
    public const string ScavTip = "Time in the raid: since you joined, by the game's log. A Scav joins a raid already under way, and the log doesn't say how long it still runs.";
    public const string NoLengthTip = "Time in the raid: since the raid's start in the game's log. The data gives no raid length for this map, so there is no time left to show.";

    /// <summary>The reading for a raid that is running, or null outside one and where the log has no start for it.</summary>
    /// <param name="raidMinutes">The map's raid length; 0 when the data gives none.</param>
    public static Reading? Of(bool inRaid, bool scav, DateTime? startedAt, int raidMinutes, DateTime now)
    {
        if (!inRaid || startedAt is not { } started)
            return null;
        var gone = Math.Max(0, (int)WallClock.Elapsed(started, now).TotalMinutes);
        if (scav)
            return new Reading($"{gone} min in · time left not known", ScavTip);
        if (raidMinutes <= 0)
            return new Reading($"{gone} min in", NoLengthTip);
        var left = raidMinutes - gone;
        return new Reading(left > 0 ? $"{gone} min in · {left} min left" : $"{gone} min in · past the raid's {raidMinutes} min", Tip);
    }
}
