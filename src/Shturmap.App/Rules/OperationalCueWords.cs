using Shturmap.Core;
using Shturmap.Core.Operational;

namespace Shturmap.App.Rules;

/// <summary>
/// What the cue for a Tasks screenshot's reading says (owner, 2026-10-19, after trying the dev build: "they should get
/// the same animation as the other quests. Be mindful of multiple missions started at the same time, we might want to
/// have them in one animation included"; docs/DESIGN.md §4, principle 11): "FROM YOUR SCREENSHOT" over the kind and the
/// count, "3 DAILIES AND WEEKLIES" ("1 DAILY OR WEEKLY": the table's kind isn't read, so the words never say which), then
/// a row per task, the one that ends soonest first: its glyph, its synopsis, and in muted capitals its map or ANY MAP
/// and its time left ("CUSTOMS · 8 H LEFT"). Every task its run of screenshots read, in one cue
/// (<see cref="OperationalReadings"/>). The rows are a reading, not a clock: their time left stays as read, and a row
/// whose task runs out while the cue is up says ENDED in place of it (the coordinator's rule after the QA pass of
/// 2026-10-19, F5).
/// </summary>
public static class OperationalCueWords
{
    /// <summary>The cue's eyebrow and title for <paramref name="count"/> tasks.</summary>
    public static (string Eyebrow, string Title) Of(int count) =>
        (RuleTexts.OperationalCueEyebrow, RuleTexts.OperationalCueTitle(count: count));

    /// <summary>The cue's rows, in the card's order: the task that ends soonest first (<see cref="OperationalRail.Soonest"/>).</summary>
    public static IReadOnlyList<OperationalShown> Rows(IReadOnlyList<OperationalShown> tasks) => OperationalRail.Soonest(tasks);

    /// <summary>How many places the cue has under its title: six rows at most, the last place "+N" past that (the game
    /// gives three to five at a time; the roll call's twelfth place does the same).</summary>
    public const int PlacesMost = 6;

    /// <summary>
    /// How many of <paramref name="count"/> tasks the cue shows as rows, and how many more its last place counts: all of up
    /// to six; of more, five rows and "+N" for the rest (seven tasks: five rows and "+2").
    /// </summary>
    public static (int Rows, int More) Places(int count) =>
        count > PlacesMost ? (PlacesMost - 1, count - (PlacesMost - 1)) : (Math.Max(0, count), 0);

    /// <summary>The last place's words for <paramref name="more"/> tasks without a row: "+2" (a sign, not a word to
    /// translate, as the roll call's).</summary>
    public static string More(int more) => "+" + more.ToString(UiLanguage.Culture);

    /// <summary>
    /// A row's muted line, before capitals: the task's map (<paramref name="mapName"/>, as the data names it) or "any map",
    /// and its time left as Plan says it, as of the reading (<paramref name="readAt"/>: "customs · 8 h left", "any map ·
    /// 2 d left"); "ended" in place of it once the task has run out by <paramref name="now"/> ("woods · ended"); the map
    /// alone when its countdown wasn't taken. The rows are a reading, not a clock: until it ends, the time stays as read.
    /// </summary>
    public static string Line(OperationalShown shown, string? mapName, DateTimeOffset readAt, DateTimeOffset now)
    {
        var where = shown.Task.AnyMap ? RuleTexts.OperationalAnyMap : mapName ?? "";
        var left = shown.Task.ExpiresAt switch
        {
            null => "",
            { } end when end <= now || end <= readAt => RuleTexts.OperationalCueEnded,
            { } end => OperationalRail.Left(end - readAt),
        };
        return string.Join(" · ", new[] { where, left }.Where(p => p.Length > 0));
    }
}
