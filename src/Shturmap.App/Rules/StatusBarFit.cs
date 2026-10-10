using Shturmap.Core.Raid;

namespace Shturmap.App.Rules;

/// <summary>
/// The status bar's words where they apply, and what gives way in a narrow window (review of 2026-10-04, C4).
/// </summary>
public static class StatusBarFit
{
    /// <summary>
    /// The bar's words while there is no position: only in a raid, where pressing the screenshot key gives one.
    /// Outside a raid, and while one loads, there is nothing to press, so nothing is said ("NO POSITION YET · PRESS
    /// PRTSC IN RAID" used to stand there all the time).
    /// </summary>
    /// <param name="keys">The game's screenshot keys, as help names them ("PrtSc or Home").</param>
    public static string NoPosition(RaidPhase phase, string keys) => phase == RaidPhase.InRaid ? RuleTexts.StatusNoPosition(keys: keys) : "";

    /// <summary>How much wider than needed the bar must be before the lights' words come back, so that a word
    /// more or less in the last fix ("9 S" to "10 S") doesn't switch them on and off.</summary>
    public const double Slack = 40;

    /// <summary>
    /// Whether the three lights keep their words (LOGS LIVE, SCREENSHOTS, DATA). The words are the first thing to
    /// go when the bar gets too narrow for all it says: the lights themselves stay, each with its tooltip, and the
    /// raid state and the last fix keep their room. Below that the last fix trims.
    /// </summary>
    /// <param name="shown">Whether the words are shown now.</param>
    /// <param name="room">The bar's width.</param>
    /// <param name="needed">The width of everything in the bar, the words and the whole last fix included.</param>
    public static bool Words(bool shown, double room, double needed) => shown ? needed <= room : needed + Slack <= room;
}
