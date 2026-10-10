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
    public static string HowToCheck => RuleTexts.ExitsHowToCheck;
    public static string NoReader => RuleTexts.ExitsNoReader;

    /// <summary>The line under EXTRACTS AND TRANSITS; empty outside a raid and with the reading unticked.</summary>
    /// <param name="listed">How many of the side's extracts the list names.</param>
    /// <param name="extracts">How many extracts the side has on this map.</param>
    public static string Of(bool inRaid, bool readOn, bool noReader, DateTime? readAt, int listed, int extracts) =>
        !inRaid || !readOn ? ""
        : readAt is { } at ? RuleTexts.ExitsListRead(extracts: extracts, listed: listed, time: at.ToString("HH:mm", CultureInfo.InvariantCulture))
        : noReader ? NoReader
        : HowToCheck;

    /// <summary>Beside a row's kind ("PMC EXTRACT · ON YOUR LIST"); empty while unchecked.</summary>
    public static string Row(bool listed, bool unsure, bool notListed) =>
        listed ? RuleTexts.ExitsRowListed : unsure ? RuleTexts.ExitsRowUnsure : notListed ? RuleTexts.ExitsRowNotListed : "";

    /// <summary>
    /// Under the glance's EXIT, for an extract (a transit is open to everyone and gets none). Unchecked, it is only the
    /// nearest for the player's side, and at a raid's start usually not one of theirs: it says so.
    /// </summary>
    /// <param name="readable">The list can be read from a screenshot (ticked, and Windows can).</param>
    public static string Glance(bool listed, bool unsure, bool readable) =>
        listed ? RuleTexts.ExitsGlanceListed
        : unsure ? RuleTexts.ExitsGlanceUnsure
        : readable ? RuleTexts.ExitsGlanceUnchecked
        : RuleTexts.ExitsGlanceNoReading;

    public static string GlanceListedTip => RuleTexts.ExitsGlanceListedTip;
    public static string GlanceUnsureTip => RuleTexts.ExitsGlanceUnsureTip;
    public static string GlanceUncheckedTip => RuleTexts.ExitsGlanceUncheckedTip;
    public static string GlanceNoReadingTip => RuleTexts.ExitsGlanceNoReadingTip;

    public static string GlanceTip(bool listed, bool unsure, bool readable) =>
        listed ? GlanceListedTip : unsure ? GlanceUnsureTip : readable ? GlanceUncheckedTip : GlanceNoReadingTip;

    /// <summary>
    /// EXIT: the nearest extract, the nearest on the game's list once it was read, never one it left out (one it left
    /// out only where nothing else is measured). Never a transit: it leads to another map, not out (owner, 2026-10-05:
    /// "It counts transits as exfils. I would show primarily exfils"); transits stand in the list under the card.
    /// </summary>
    /// <param name="exits">The ways out, nearest first: measured from a position, left out by the list, a transit.</param>
    public static int? Exit(IReadOnlyList<(bool Measured, bool NotListed, bool Transit)> exits)
    {
        int? leftOut = null;
        for (var i = 0; i < exits.Count; i++)
        {
            if (!exits[i].Measured || exits[i].Transit)
                continue;
            if (!exits[i].NotListed)
                return i;
            leftOut ??= i;
        }
        return leftOut;
    }

    /// <summary>
    /// The nearest way out the player can simply leave by, under EXIT where EXIT isn't it (owner, 2026-10-05: "also
    /// show the next one you sure is open and where you don't need to bring extra things or do extra things - where you
    /// can simply exfil"): an extract the game's list names without "??:??:??" that takes nothing (no item, money,
    /// flare, climbing gear, switch or second player; ExtractRules.Needs). Null while no list was read (nothing is
    /// sure to be open then), and where EXIT is that one already.
    /// </summary>
    /// <param name="exits">The ways out, nearest first: measured from a position, on the list without "???", a transit,
    /// and what leaving there takes.</param>
    /// <param name="exit">EXIT's place in that list.</param>
    public static int? Plain(IReadOnlyList<(bool Measured, bool Listed, bool Transit, string Needs)> exits, int? exit)
    {
        for (var i = 0; i < exits.Count; i++)
        {
            if (exits[i] is { Measured: true, Listed: true, Transit: false, Needs.Length: 0 })
                return i == exit ? null : i;
        }
        return null;
    }

    public static string PlainNote => RuleTexts.ExitsPlainNote;

    public static string PlainTip => RuleTexts.ExitsPlainTip;
}
