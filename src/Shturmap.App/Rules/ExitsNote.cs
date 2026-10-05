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
    public const string HowToCheck = "Not checked against your list yet. Screenshot the game's extract list (raid start, or O twice) to check.";
    public const string NoReader = "Can't read your extract list: Windows has no text recognition language. Add one in Windows Settings → Time & language → Language.";

    /// <summary>The line under EXTRACTS AND TRANSITS; empty outside a raid and with the reading unticked.</summary>
    /// <param name="listed">How many of the side's extracts the list names.</param>
    /// <param name="extracts">How many extracts the side has on this map.</param>
    public static string Of(bool inRaid, bool readOn, bool noReader, DateTime? readAt, int listed, int extracts) =>
        !inRaid || !readOn ? ""
        : readAt is { } at ? $"Your list this raid: {listed} of {extracts} extracts (screenshot at {at.ToString("HH:mm", CultureInfo.InvariantCulture)})."
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

    public const string GlanceListedTip = "On the game's extract list for this raid, read from your screenshot.";
    public const string GlanceUnsureTip = "On your list, but the game shows ??:??:??: maybe closed, or needs something first.";
    public const string GlanceUncheckedTip = "Nearest extract for your side, maybe not open this raid. Screenshot the game's extract list (raid start, or O twice) to check.";
    public const string GlanceNoReadingTip = "Nearest extract for your side, maybe not open this raid. Check your list in the game.";

    public static string GlanceTip(bool listed, bool unsure, bool readable) =>
        listed ? GlanceListedTip : unsure ? GlanceUnsureTip : readable ? GlanceUncheckedTip : GlanceNoReadingTip;
}
