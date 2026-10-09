using Shturmap.Core;
using SkiaSharp;

namespace Shturmap.Map;

/// <summary>A position of the raid replay: when the screenshot was taken, in minutes since the raid's start, and where.</summary>
public sealed record ReplayFix(double Minute, WorldPoint Position);

/// <summary>
/// The raid just over, as its replay draws it (owner, 2026-10-07; docs/DESIGN.md "Map drawing", *The raid replay*): the
/// positions of its screenshots on the map it ended on, each with its minute, and how long the raid ran. Nothing in the
/// logs says how a raid ended, so the replay shows where the player took screenshots and when they were last seen.
/// </summary>
/// <param name="MapNormalizedName">The map it is drawn on: the one the raid ended on (a transit starts anew).</param>
/// <param name="Minutes">How long the raid ran, from its start line to its end line.</param>
public sealed record RaidReplay(string MapNormalizedName, string MapName, IReadOnlyList<ReplayFix> Fixes, double Minutes)
{
    /// <summary>When objectives were ticked as done during the raid (minutes), for the timeline's checks.</summary>
    public IReadOnlyList<double> Ticks { get; init; } = [];

    /// <summary>When a screenshot first showed the game's extract list (minutes), or null.</summary>
    public double? ListRead { get; init; }

    /// <summary>Fewer positions than this make no replay: today's RAID OVER cue stands.</summary>
    public const int MinFixes = 3;

    /// <summary>Positions closer together than this (first to last) make no replay either.</summary>
    public const double MinSpanMinutes = 5;

    /// <summary>
    /// Whether the raid has enough to replay: 3 positions or more over 5 minutes or more, and a length (in a week of
    /// the study log, about half the raids). Fewer would be a dot or two, not a raid.
    /// </summary>
    public bool Plays => Fixes.Count >= MinFixes && Minutes > 0 && Fixes[^1].Minute - Fixes[0].Minute >= MinSpanMinutes;

    /// <summary>How long before the raid's end the last position was taken.</summary>
    public double LastSeenBefore => Fixes.Count == 0 ? 0 : Math.Max(0, Minutes - Fixes[^1].Minute);

    /// <summary>The tag on the last position at the end: "LAST SEEN · 2 MIN BEFORE THE END".</summary>
    public string LastSeenText => LastSeenBefore < 1 ? "LAST SEEN · JUST BEFORE THE END" : $"LAST SEEN · {(int)LastSeenBefore} MIN BEFORE THE END";

    /// <summary>A position's tag: its minute, "8 MIN IN" (as the raid clock says it: bare minutes on a map read as a
    /// time to get somewhere, owner, 2026-10-04).</summary>
    public static string MinuteText(double minute) => $"{(int)Math.Max(0, minute)} MIN IN";
}

/// <summary>
/// The replay's timing in the RAID OVER cue: the cue enters as every cue does, its band goes down to the map's foot and
/// becomes the timeline, the raid plays in <see cref="Play"/>, holds on its end, and fades. About 17 s in all (owner,
/// 2026-10-08: "can easily be a bit longer"; until then 7.5 s of play and 2 s of hold, about 12.7 s).
/// </summary>
public static class ReplayTiming
{
    /// <summary>RAID OVER enters in the middle, as every cue does.</summary>
    public static readonly TimeSpan Entrance = TimeSpan.FromSeconds(1.6);

    /// <summary>The title goes and the band slides to the map's foot.</summary>
    public static readonly TimeSpan Down = TimeSpan.FromSeconds(1.0);

    /// <summary>The whole raid, whatever its length.</summary>
    public static readonly TimeSpan Play = TimeSpan.FromSeconds(11);

    /// <summary>The end stays up.</summary>
    public static readonly TimeSpan Hold = TimeSpan.FromSeconds(3.0);

    /// <summary>Then it fades.</summary>
    public static readonly TimeSpan Fade = TimeSpan.FromSeconds(0.6);

    public static TimeSpan PlayStarts => Entrance + Down;

    public static TimeSpan Total => Entrance + Down + Play + Hold + Fade;

    /// <summary>The raid's minute the playhead is at, <paramref name="sincePlay"/> after the trace began to play.</summary>
    public static double MinuteAt(TimeSpan sincePlay, double raidMinutes) =>
        raidMinutes * Math.Clamp(sincePlay / Play, 0, 1);

    /// <summary>How many seconds of the replay one minute of the raid takes.</summary>
    public static double SecondsPerMinute(double raidMinutes) => raidMinutes > 0 ? Play.TotalSeconds / raidMinutes : 0;
}

/// <summary>
/// The pen's colour and width by the raid's time (owner, 2026-10-07: "encode raid time to the color of the pen stroke.
/// Use a fitting color map based on the apps design"). The player's sand mixed into the ground, opaque: from
/// <see cref="FromSand"/> of sand at the raid's start to all of it at its end, and wider as it goes. One hue, since
/// sand is the player's alone and any map of several hues runs through the map's taken colours (green and teal for
/// extracts, violet for transits, amber for quests, red for bosses); light means late, as the newest position is the
/// brightest thing on the map; and width is the second cue, since one hue has little range on a dark map ("Visual
/// language", shape as a second cue). Sand by alpha was tried: the artwork showed through and the pieces' overlaps
/// dotted the line; a range from 35 % was too little.
/// </summary>
public static class ReplayInk
{
    /// <summary>How much sand the pen has at the raid's start.</summary>
    public const float FromSand = 0.22f;

    /// <summary>The pen's width at the raid's start and end, in DIPs.</summary>
    public const float WidthFrom = 1.4f, WidthTo = 3.2f;

    private static readonly SKColor Ground = Palette.Sk(Palette.Ground);
    private static readonly SKColor Sand = Palette.Sk(Palette.Sand);

    /// <summary>The pen's colour at <paramref name="t"/>, the raid's time from 0 (start) to 1 (end).</summary>
    public static SKColor At(double t)
    {
        var m = FromSand + (1 - FromSand) * Math.Clamp(t, 0, 1);
        byte Mix(byte g, byte s) => (byte)Math.Round(g + (s - g) * m);
        return new SKColor(Mix(Ground.Red, Sand.Red), Mix(Ground.Green, Sand.Green), Mix(Ground.Blue, Sand.Blue));
    }

    /// <summary>The pen's width at <paramref name="t"/>, in DIPs.</summary>
    public static float Width(double t) => WidthFrom + (WidthTo - WidthFrom) * (float)Math.Clamp(t, 0, 1);
}
