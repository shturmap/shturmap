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
/// The raid card shows it as its largest figure over a rule of the raid's length (owner, 2026-10-05: "The remaining
/// time is a crucial piece of information and should be more visible"; Controls.RaidClock), so a reading also
/// carries its numbers.
/// </summary>
public static class RaidTime
{
    /// <param name="Text">"12 min in · 28 min left".</param>
    /// <param name="Tip">Where the figures come from.</param>
    public sealed record Reading(string Text, string Tip)
    {
        /// <summary>Whole minutes since the raid's start line.</summary>
        public int In { get; init; }

        /// <summary>Whole minutes left, at least 1; null where it isn't known (a Scav, a map without a raid length) or
        /// the raid's length is over.</summary>
        public int? Left { get; init; }

        /// <summary>The map's raid length in minutes; 0 where the reading has none to show against.</summary>
        public int RaidMinutes { get; init; }

        /// <summary>A Scav's raid: the time left isn't known, and the readout says so.</summary>
        public bool Scav { get; init; }

        /// <summary>Past the raid's length: the figure is the time in the raid, and nothing is left to count.</summary>
        public bool Over => RaidMinutes > 0 && Left is null;

        /// <summary>The last <see cref="LowMinutes"/> minutes, or past them: shown in red, as the game's own timer is.</summary>
        public bool Low => Over || Left <= LowMinutes;

        /// <summary>The share of the raid's length that is gone, 0 to 1; 0 where no length is known.</summary>
        public double Gone => RaidMinutes <= 0 ? 0 : Math.Clamp((double)In / RaidMinutes, 0, 1);
    }

    /// <summary>From this many minutes left the time is shown in red: the game's own timer turns red for its last ten.</summary>
    public const int LowMinutes = 10;

    public const string Tip = "Time left: the map's raid length minus the time since the raid's start in the log. Can be off after a reconnect.";
    public const string ScavTip = "Time since you joined, by the log. A Scav joins mid-raid, so the time left isn't known.";
    public const string NoLengthTip = "Time since the raid's start, by the log. No raid length for this map, so no time left.";

    /// <summary>The reading for a raid that is running, or null outside one and where the log has no start for it.</summary>
    /// <param name="raidMinutes">The map's raid length; 0 when the data gives none.</param>
    public static Reading? Of(bool inRaid, bool scav, DateTime? startedAt, int raidMinutes, DateTime now)
    {
        if (!inRaid || startedAt is not { } started)
            return null;
        var gone = Math.Max(0, (int)WallClock.Elapsed(started, now).TotalMinutes);
        if (scav)
            return new Reading($"{gone} min in · time left not known", ScavTip) { In = gone, Scav = true };
        if (raidMinutes <= 0)
            return new Reading($"{gone} min in", NoLengthTip) { In = gone };
        var left = raidMinutes - gone;
        return left > 0
            ? new Reading($"{gone} min in · {left} min left", Tip) { In = gone, Left = left, RaidMinutes = raidMinutes }
            : new Reading($"{gone} min in · past the raid's {raidMinutes} min", Tip) { In = gone, RaidMinutes = raidMinutes };
    }
}
