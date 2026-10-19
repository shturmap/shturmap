using Shturmap.Core.Operational;

namespace Shturmap.App.Rules;

/// <summary>
/// What the cue for a Tasks screenshot's reading says (owner, 2026-10-19, after trying the dev build: "they should get
/// the same animation as the other quests. Be mindful of multiple missions started at the same time, we might want to
/// have them in one animation included"; docs/DESIGN.md §4, principle 11): "FROM YOUR SCREENSHOT" over the kind and the
/// count, "3 DAILIES AND WEEKLIES" ("1 DAILY OR WEEKLY": the table's kind isn't read, so the words never say which), then
/// a row per task, the one that ends soonest first: its glyph, its synopsis, and in muted capitals its map or ANY MAP
/// and its time left ("CUSTOMS · 8 H LEFT"). Every task its run of screenshots read, in one cue
/// (<see cref="OperationalReadings"/>).
/// </summary>
public static class OperationalCueWords
{
    /// <summary>The cue's eyebrow and title for <paramref name="count"/> tasks.</summary>
    public static (string Eyebrow, string Title) Of(int count) =>
        (RuleTexts.OperationalCueEyebrow, RuleTexts.OperationalCueTitle(count: count));

    /// <summary>The cue's rows, in the card's order: the task that ends soonest first (<see cref="OperationalRail.Soonest"/>).</summary>
    public static IReadOnlyList<OperationalShown> Rows(IReadOnlyList<OperationalShown> tasks) => OperationalRail.Soonest(tasks);

    /// <summary>
    /// A row's muted line, before capitals: the task's map (<paramref name="mapName"/>, as the data names it) or "any map",
    /// and its time left as Plan says it ("customs · 8 h left", "any map · 2 d left"); the map alone when its countdown
    /// wasn't taken.
    /// </summary>
    public static string Line(OperationalShown shown, string? mapName, DateTimeOffset now)
    {
        var where = shown.Task.AnyMap ? RuleTexts.OperationalAnyMap : mapName ?? "";
        var left = shown.Task.ExpiresAt is { } end && end > now ? OperationalRail.Left(end - now) : "";
        return string.Join(" · ", new[] { where, left }.Where(p => p.Length > 0));
    }
}
