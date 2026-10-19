using System.Globalization;
using Shturmap.Core;
using Shturmap.Core.Operational;

namespace Shturmap.App.Rules;

/// <summary>
/// The daily and weekly operational tasks where the map's work is (owner, 2026-10-19: from a panel of four, "Use B, the
/// recommended one"; docs/DESIGN.md §4, "Screen anatomy", *Operational tasks*). A NEXT RAID row shows its map's tasks as
/// framed glyphs after its quests' (never the any-map ones: they would stand on every row); the open card and the raid
/// card have an OPERATIONAL section: the map's tasks first, then the any-map tasks after a hairline (in every card, not
/// listed once: owner, 2026-10-19). Each row is a glyph, a synopsis and a muted line: in Plan its progress and time left
/// ("4 OF 7 · 8 H LEFT"; a weekly "2 D LEFT"; without an expiry "READ 3 H AGO"), in a raid its progress and, only when the
/// task ends before the raid could, "ENDS IN 24 MIN" in the clock's red. Every time left is rounded down, in both (QA,
/// 2026-10-19: Plan's 24 and the raid's 25 at the same moment): a time never says more than there is. No portrait, no
/// card, no click.
/// </summary>
public static class OperationalRail
{
    /// <summary>The map's own tasks, in the table's order: a list row's framed glyphs.</summary>
    public static IReadOnlyList<OperationalShown> OnRow(IReadOnlyList<OperationalShown> all, string? mapKey) =>
        mapKey is null ? [] : all.Where(t => t.MapKey == mapKey).ToList();

    /// <summary>The OPERATIONAL section of a map's card: the map's tasks, then the any-map tasks (after a hairline).</summary>
    public static (IReadOnlyList<OperationalShown> Here, IReadOnlyList<OperationalShown> Anywhere) Section(IReadOnlyList<OperationalShown> all, string? mapKey) =>
        (OnRow(all, mapKey), all.Where(t => t.MapKey is null).ToList());

    /// <summary>
    /// What Plan says of a task in muted capitals: on any map, its progress, a hand-over's "hand over after", and its time
    /// left, or the reading's age when the countdown wasn't taken ("4 of 7 · 8 h left", "any map · hand over after ·
    /// 2 d left", "read 3 h ago").
    /// </summary>
    public static string PlanLine(OperationalShown shown, DateTimeOffset now)
    {
        var task = shown.Task;
        var time = task.ExpiresAt is { } at ? Left(at - now) : ReadAgo(now - task.ReadAt);
        return Join(task.AnyMap ? RuleTexts.OperationalAnyMap : "", Progress(task),
            task.Kind == OperationalKind.HandOver ? RuleTexts.OperationalHandOverAfter : "", time);
    }

    /// <summary>What the raid card says of a task in muted capitals: its progress, a hand-over's "hand over after the
    /// raid" (its time, when it matters, is <see cref="EndsIn"/>). Not "any map": the row's distance column says
    /// ANYWHERE for it (QA, 2026-10-19: one place says it).</summary>
    public static string RaidLine(OperationalShown shown) =>
        Join(Progress(shown.Task),
            shown.Task.Kind == OperationalKind.HandOver ? RuleTexts.OperationalHandOverAfterRaid : "");

    /// <summary>
    /// In a raid, the task's time only when it ends before the raid could ("ends in 24 min", in the clock's red;
    /// rounded down as Plan's, <see cref="Minutes"/>): null when it ends later, has no expiry, or the raid's time left
    /// isn't known (a Scav, a map without a raid length).
    /// </summary>
    /// <param name="minutesLeftInRaid">The raid's time left in whole minutes (the clock's), or null.</param>
    public static string? EndsIn(OperationalTask task, DateTimeOffset now, int? minutesLeftInRaid)
    {
        if (task.ExpiresAt is not { } at || minutesLeftInRaid is not { } left)
            return null;
        var until = at - now;
        if (until >= TimeSpan.FromMinutes(left) || until <= TimeSpan.Zero)
            return null;
        return RuleTexts.OperationalEndsIn(count: Number(Minutes(until)));
    }

    /// <summary>A time left in whole minutes, rounded down (24:59 is 24: a time never says more than there is), and 1
    /// in its last minute. Plan's "24 min left" and the raid's "ends in 24 min" both count so.</summary>
    public static int Minutes(TimeSpan left) => Math.Max(1, (int)left.TotalMinutes);

    /// <summary>The task's progress as of the screenshot: its one counted objective's "4 of 7", else the game's
    /// percentage "57%", else nothing.</summary>
    public static string Progress(OperationalTask task) =>
        task.Count is var (done, of) ? RuleTexts.OperationalCount(done: Number(done), of: Number(of))
        : task.Progress is { } percent ? RuleTexts.OperationalPercent(percent: Number(percent))
        : "";

    /// <summary>A time left as Plan says it: minutes under an hour, hours under two days, else days, each rounded
    /// down ("25 min left", "8 h left", "2 d left").</summary>
    public static string Left(TimeSpan left)
    {
        if (left < TimeSpan.FromHours(1))
            return RuleTexts.OperationalMinutesLeft(count: Number(Minutes(left)));
        if (left < TimeSpan.FromDays(2))
            return RuleTexts.OperationalHoursLeft(count: Number((int)left.TotalHours));
        return RuleTexts.OperationalDaysLeft(count: (int)left.TotalDays);
    }

    /// <summary>A reading's age, for a task kept without an expiry ("read 3 h ago").</summary>
    public static string ReadAgo(TimeSpan age)
    {
        if (age < TimeSpan.FromMinutes(1))
            return RuleTexts.OperationalReadJustNow;
        if (age < TimeSpan.FromHours(1))
            return RuleTexts.OperationalReadMinutesAgo(count: Number((int)age.TotalMinutes));
        if (age < TimeSpan.FromDays(2))
            return RuleTexts.OperationalReadHoursAgo(count: Number((int)age.TotalHours));
        return RuleTexts.OperationalReadDaysAgo(count: (int)age.TotalDays);
    }

    /// <summary>The eyebrow's right part: when the tasks were read, "As of 14:02"; on an earlier day with the date, as
    /// the language in use writes a day and month ("18 Oct", German "18. Okt."; UiLanguage.DayMonth).</summary>
    public static string AsOf(DateTimeOffset readAt, DateTimeOffset now)
    {
        var local = readAt.ToLocalTime();
        var time = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        return local.Date == now.ToLocalTime().Date
            ? RuleTexts.OperationalAsOf(time: time)
            : RuleTexts.OperationalAsOfDay(date: UiLanguage.DayMonth(local.DateTime), time: time);
    }

    /// <summary>Under the eyebrow, muted, while tasks ran out since the last table screenshot: "1 ended at 22:35.
    /// Screenshot your tasks for the new one."; empty without.</summary>
    public static string Ended(OperationalEnded? ended) =>
        ended is null ? "" : RuleTexts.OperationalEnded(count: ended.Count, time: ended.LastAt.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture));

    /// <summary>Whether a map's card shows the section: tasks on the map or on any map, or tasks that ran out.</summary>
    public static bool Shows(IReadOnlyList<OperationalShown> all, string? mapKey, OperationalEnded? ended) =>
        ended is not null || all.Any(t => t.MapKey is null || t.MapKey == mapKey);

    /// <summary>
    /// The parts of a muted line, for a row that breaks it only between them (QA, 2026-10-19: "HAND OVER AFTER THE /
    /// RAID"): "4 of 7 · 8 h left" is "4 of 7" and "8 h left".
    /// </summary>
    public static IReadOnlyList<string> Parts(string line) =>
        line.Split(Dot, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// The synopsis as the row shows it: a hand-over's item named in two words is kept on one line ("CPU fan", never
    /// "CPU" at a line's end and "fan" under it; QA, 2026-10-19). A longer name may still wrap between its words: kept
    /// whole it could be wider than the line, which then breaks inside a word.
    /// </summary>
    public static string Synopsis(string synopsis, IEnumerable<string> items)
    {
        foreach (var name in items)
        {
            if (name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries) is [var first, var second])
                synopsis = synopsis.Replace(first + " " + second, first + NoBreak + second, StringComparison.Ordinal);
        }
        return synopsis;
    }

    private const string Dot = " · ";
    private const char NoBreak = '\u00A0';

    private static string Join(params string[] parts) => string.Join(Dot, parts.Where(p => p.Length > 0));

    private static string Number(int n) => n.ToString(UiLanguage.Culture);
}
