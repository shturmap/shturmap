using System.Globalization;

namespace Shturmap.App.Rules;

/// <summary>
/// What the rail says about the player's extracts in a raid (owner, 2026-10-05: "Since the app shows the nearest exit
/// upon placing a marker it should also be visible if the exfils [are] not verified yet"). The game gives each raid
/// its own extracts and shows them in a list; Shturmap knows them only once a screenshot showed that list. Until
/// then every extract is said to be unchecked, with how to check; after, each says whether the list names it.
/// </summary>
public static class ExitsNote
{
    public const string HowToCheck = "Not checked against your list yet. Take a screenshot while the game shows your extracts (at the raid's start, or O twice by default) and Shturmap reads them from it.";
    public const string NoReader = "Your extract list can't be read: this Windows has no text recognition language. Adding one (Windows Settings, Time & language, Language) turns it on.";

    /// <summary>The line under EXTRACTS AND TRANSITS; empty outside a raid and with the reading unticked.</summary>
    /// <param name="listed">How many of the side's extracts the list names.</param>
    /// <param name="extracts">How many extracts the side has on this map.</param>
    public static string Of(bool inRaid, bool readOn, bool noReader, DateTime? readAt, int listed, int extracts) =>
        !inRaid || !readOn ? ""
        : readAt is { } at ? $"Your list this raid: {listed} of {extracts} extracts, read from your screenshot at {at.ToString("HH:mm", CultureInfo.InvariantCulture)}."
        : noReader ? NoReader
        : HowToCheck;

    /// <summary>Beside a row's kind ("PMC EXTRACT · ON YOUR LIST"); empty while unchecked.</summary>
    public static string Row(bool listed, bool unsure, bool notListed) =>
        listed ? "on your list" : unsure ? "on your list · ??? in game" : notListed ? "not on your list" : "";

    /// <summary>
    /// Under the glance's EXIT, for an extract (a transit is open to everyone and gets none). Unchecked, it is only the
    /// nearest for the player's side, and at a raid's start usually not one of theirs: it says so.
    /// </summary>
    /// <param name="readable">The list can be read from a screenshot (ticked, and Windows can).</param>
    public static string Glance(bool listed, bool unsure, bool readable) =>
        listed ? "On your list this raid"
        : unsure ? "On your list · ??? in game"
        : readable ? "Nearest · not checked against your list"
        : "Nearest · check your list in game";

    public const string GlanceListedTip = "The game's own list names this extract for you this raid: read from your screenshot.";
    public const string GlanceUnsureTip = "On your list, but the game marks it ??:??:??: it may be closed, or it needs something first.";
    public const string GlanceUncheckedTip = "The nearest extract for your side. The game opens only some extracts in each raid, by where you started. Take a screenshot while the game shows your extract list (at the raid's start, or O twice by default) and Shturmap reads which are yours.";
    public const string GlanceNoReadingTip = "The nearest extract for your side. The game opens only some extracts in each raid, by where you started, and neither its logs nor tarkov.dev's data say which: check the list in the game.";

    public static string GlanceTip(bool listed, bool unsure, bool readable) =>
        listed ? GlanceListedTip : unsure ? GlanceUnsureTip : readable ? GlanceUncheckedTip : GlanceNoReadingTip;
}
