using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Shturmap.Game.Screenshots;

/// <summary>One word as the recognition read it, in the picture's pixels; Line is the recognition's own line number
/// (not used by the reading: the recognition joins words across the table's cells into one line at some
/// zooms).</summary>
public sealed record TaskWord(string Text, double X, double Y, double W, double H, int Line = 0)
{
    public double Cx => X + W / 2;
    public double Cy => Y + H / 2;
    public double Right => X + W;
    public double Bottom => Y + H;
}

/// <summary>A run of words on one line with a word's gap between them: a table cell's text, or a sentence.</summary>
internal sealed record TaskChain(IReadOnlyList<TaskWord> Words)
{
    public double X => Words[0].X;
    public double Right => Words[^1].Right;
    public double Y => Words.Min(w => w.Y);
    public double Bottom => Words.Max(w => w.Bottom);
    public double Cy => (Y + Bottom) / 2;
    public string Text => string.Join(" ", Words.Select(w => w.Text));
}

public enum TaskScreen
{
    /// <summary>Neither the table nor a trader's tasks: nothing is read.</summary>
    None,

    /// <summary>Character › TASKS › OPERATIONAL: the table, read.</summary>
    Table,

    /// <summary>A Tasks table without a countdown in any row (STORY or SIDE, presumably): not read.</summary>
    OtherTable,

    /// <summary>A trader's TASKS tab (the list at the left, REPLACE, one task at the right): the task its header names
    /// when that task stands in the list's OPERATIONAL TASKS (<see cref="TaskPage.Selected"/>), and the countdowns of the
    /// list's entries (<see cref="TaskPage.Rows"/>; owner, 2026-10-25). Never read by the stash's reading: its grids are
    /// the task's rewards.</summary>
    TraderScreen,
}

/// <summary>A part of the picture to read on its own (the countdown, the progress), in the picture's
/// pixels.</summary>
public readonly record struct TaskBand(int X, int Y, int W, int H);

public sealed record TaskObjective(string Text, int? Done, int? Of);

/// <summary>One row of the table: the cells read from the page, and the countdown and progress read from their
/// bands.</summary>
public sealed class TaskRow
{
    public required string Name { get; init; }
    public string? Location { get; set; }
    public string? Status { get; set; }
    public string? TimeText { get; set; }
    public TimeSpan? TimeLeft { get; set; }
    public string? ProgressText { get; set; }
    public int? Progress { get; set; }
    public List<TaskObjective> Objectives { get; } = [];

    /// <summary>On the table: one band per objective of an expanded row, in the order of <see cref="Objectives"/>: its
    /// line from its first word (the icon left out) to the read part's edge, where the count stands at the line's end
    /// after the progress bar (2026-10-25). The caller reads the count there by vote when the page's words gave
    /// none.</summary>
    public List<TaskBand> ObjectiveBands { get; } = [];

    /// <summary>How many of the ways a band was read in gave the value taken (0 when the page's words carried
    /// it).</summary>
    public int TimeVotes { get; set; }
    public int ProgressVotes { get; set; }

    /// <summary>Whether the countdown is PaddleOCR's reading of its band, which no way of Windows' recognition read
    /// (2026-10-26; <see cref="TasksReader"/>); its votes are then 0.</summary>
    public bool TimeByPaddle { get; set; }

    /// <summary>The row's middle (the task name's): the bands are measured from it.</summary>
    public double Cy { get; init; }

    public TaskBand TimeBand { get; init; }
    public TaskBand ProgressBand { get; init; }
}

/// <summary>
/// What a Tasks screen showed: the screen, the table's rows (on a trader's screen, the entries of its list's OPERATIONAL
/// TASKS: a name and the countdown beside it, nothing else), and the text unit.
/// </summary>
public sealed record TaskPage(TaskScreen Screen, IReadOnlyList<TaskRow> Rows, double Unit)
{
    /// <summary>On a trader's screen: the task its header names when that name stands in the list's OPERATIONAL TASKS, an
    /// operational task (owner, 2026-10-25), with the header's location and status (<see cref="TaskTable.TakeHeader"/>):
    /// the list's entry of that name, which carries the countdown; a row of its own without a countdown when two entries
    /// have the name (which of them is selected can't be told). Null for a story or side quest, whose name isn't in the
    /// list.</summary>
    public TaskRow? Selected { get; init; }

    /// <summary>On a trader's screen with a <see cref="Selected"/> task: the header line from the picture's left part to
    /// its right edge, where the task's location and status stand.</summary>
    public TaskBand HeaderBand { get; init; }

    /// <summary>On a trader's screen with a <see cref="Selected"/> task: one band per objective line, from the
    /// "Objective(s)" label's left edge to the picture's right edge, between that label and "Rewards" (owner, 2026-10-25),
    /// read by the caller and handed to <see cref="TaskTable.ObjectiveOf"/>. With <see cref="HeaderBand"/>, the bands of
    /// the picture's right third that are read; nothing above the label (the task's text) or below "Rewards".</summary>
    public IReadOnlyList<TaskBand> ObjectiveBands { get; init; } = [];
}

/// <summary>
/// The operational tasks' table read from the words of a menu screenshot (owner, 2026-10-19: from a panel of four, B;
/// docs/DESIGN.md §2, "Menu screenshots"; the spike of the same day, docs/NEXT.md). Pure: the words come from
/// <see cref="TasksReader"/> (or a test), the countdown's and the progress's own bands are read by the caller and
/// handed back to <see cref="TakeTime"/> and <see cref="TakeProgress"/>.
/// <list type="bullet">
/// <item>The screen: the table's header (Trader, Type, Class, Task, Location, Status, Progress on one line, five of
/// them at least) is the table; TRADING with SERVICES (a trader's tabs), or REPLACE with TASKS, is a trader's screen,
/// told and never read (neither by this reading nor by the stash's: its grids are the task's rewards).</item>
/// <item>The columns: from the header words' places (Location, Status and Progress stand over their cells' middles;
/// the task names start just right of Class), with the 1440p fractions where a header word went unread.</item>
/// <item>The rows: a run of words that starts at the task names' left edge and stays in the Task column, with a
/// location or a status beside it. The expanded row's text, its objectives and its rewards start elsewhere or run
/// across the columns, so they make no row.</item>
/// <item>An expanded row: the lines between "Objective(s)" and "Rewards" (or the next row) are its objectives, each
/// with a count n/m at its end where the game shows one; they go to the row above them. Where the page's words drop a
/// count or misread it ("øn" for the weekly's "Ø/7"), the caller reads it from the line's band by vote (2026-10-25);
/// a misread count is never a word of the objective.</item>
/// <item>A trader's TASKS tab (owner, 2026-10-25: "when a daily or weekly is screenshotted from the traders quest accept
/// view, it is not added"): the entries under the list's OPERATIONAL TASKS heading, each a name and a countdown band at
/// the list's right edge; the header's task (right of its glyph, on the line of COMPLETE | REPLACE) is an operational
/// task when an entry has its name. Its location and status stand at the header's right end, in the picture's right
/// third: that band is read by the caller and handed to <see cref="TakeHeader"/>. Its objectives stand centred across
/// the screen's width in a bar under "Objective(s)", their counts in the right third (measured at 1440p, 2026-10-25), so
/// the left part holds them cut: each line the left part's words show gets a band of its own across the picture, read
/// by the caller and handed to <see cref="ObjectiveOf"/>.</item>
/// </list>
/// </summary>
public static class TaskTable
{
    /// <summary>The part of the picture's width that is read: the table ends at 0.66 of it at 16:9.</summary>
    public const double LeftPart = 0.665;

    private static readonly string[] Header = ["trader", "type", "class", "task", "location", "status", "progress"];

    // Where the header words stand at 1440p, as fractions of the picture's width, for a header word not read.
    private const double ClassRightAt = 332 / 2560.0;
    private const double LocationAt = 1125 / 2560.0;
    private const double StatusAt = 1314 / 2560.0;
    private const double ProgressAt = 1558 / 2560.0;

    // The text unit: a header word's height, 17 px at 1440p.
    private const double UnitAt = 17.5 / 1440.0;

    public static TaskPage Read(IReadOnlyList<TaskWord> words, int width, int height)
    {
        var header = FindHeader(words);
        if (header.Count < 5)
            return IsTraderScreen(words) ? Trader(words, width, height) : new TaskPage(TaskScreen.None, [], height * UnitAt);
        var u = Median(header.Select(w => w.H));
        double At(string name, Func<TaskWord, double> where, double fraction) =>
            header.FirstOrDefault(w => Fold(w.Text) == name) is { } w ? where(w) : fraction * width;
        var headerBottom = header.Max(w => w.Bottom);
        var taskLeft = At("class", w => w.Right, ClassRightAt);
        var location = At("location", w => w.Cx, LocationAt);
        var status = At("status", w => w.Cx, StatusAt);
        var progress = At("progress", w => w.Cx, ProgressAt);
        var half = (status - location) / 2;
        var taskRight = location - half;
        var locationRight = location + half;
        var statusRight = status + half;
        var progressRight = Math.Min(progress + (progress - statusRight), width * LeftPart);

        var chains = Chains(words.Where(w => w.Y > headerBottom).ToList(), u);
        bool Within(TaskChain c, double left, double right) => c.X >= left - 0.5 * u && c.Right <= right + 0.5 * u;
        bool Near(TaskChain c, double cy) => Math.Abs(c.Cy - cy) <= 3.5 * u;

        // The rows: a name at the names' left edge, in the Task column, with a location or a status beside it.
        var rows = new List<TaskRow>();
        foreach (var name in chains.Where(c => c.X >= taskLeft && c.X <= taskLeft + 4 * u && c.Right <= taskRight).OrderBy(c => c.Cy))
        {
            var beside = chains.Where(c => c != name && Near(c, name.Cy)).ToList();
            var locations = beside.Where(c => Within(c, taskRight, locationRight)).OrderBy(c => c.Cy).ToList();
            var statuses = beside.Where(c => Within(c, locationRight, statusRight)).ToList();
            if (locations.Count == 0 && statuses.Count == 0)
                continue;
            if (rows.Count > 0 && Math.Abs(rows[^1].Cy - name.Cy) < 3.5 * u)
                continue;
            var row = new TaskRow
            {
                Name = Clean(name.Text),
                Cy = name.Cy,
                Location = locations.Count > 0 ? Clean(string.Join(" ", locations.Select(c => c.Text))) : null,
                TimeBand = Band(locationRight, name.Cy + 0.2 * u, statusRight, name.Cy + 2.6 * u),
                ProgressBand = Band(statusRight, name.Cy - 2.4 * u, progressRight, name.Cy + 0.2 * u),
            };
            // The status above the row's middle, the countdown below it.
            foreach (var s in statuses.OrderBy(c => c.Cy))
            {
                if (TimeLeft(s.Text) is not null && s.Cy > name.Cy)
                    TakeTime(row, s.Text);
                else if (s.Cy <= name.Cy + 0.5 * u && row.Status is null)
                    row.Status = Status(s.Text);
            }
            foreach (var p in beside.Where(c => Within(c, statusRight, progressRight) && c.Cy <= name.Cy + 0.5 * u))
                TakeProgress(row, p.Text);
            rows.Add(row);
        }

        // The expanded row's objectives: below "Objective(s)", above "Rewards" or the next row.
        foreach (var label in chains.Where(c => c.Words.Count == 1 && c.X < taskLeft && Fold(c.Text).StartsWith("objective", StringComparison.Ordinal)))
        {
            var owner = rows.LastOrDefault(r => r.Cy < label.Y);
            if (owner is null)
                continue;
            var end = Math.Min(
                chains.Where(c => c.Y > label.Bottom && c.X < taskLeft && Fold(c.Text).StartsWith("reward", StringComparison.Ordinal)).Select(c => c.Y).DefaultIfEmpty(double.MaxValue).Min(),
                rows.Where(r => r.Cy > label.Y).Select(r => r.Cy - 3.5 * u).DefaultIfEmpty(double.MaxValue).Min());
            var inside = chains.Where(c => c.Y > label.Bottom - 0.2 * u && c.Bottom < end && c != label).ToList();
            foreach (var line in Lines(inside))
            {
                if (Objective(line, u) is not { } objective)
                    continue;
                owner.Objectives.Add(objective);
                // The line's band, for its count by vote where the page's words dropped it or read a lookalike (the
                // weekly's "Ø/7" as "øn", a "Ø/3" not at all; 2026-10-25): from half a unit before its first word, the
                // line's icon left out, to the read part's edge, the line's height with half a unit above and below,
                // between the label and the rewards. Measured on the owner's tables: from the label's left edge the
                // "Ø/7" read in no way and the "Ø/3" in 6, with the icon in the band in at most one and 5; so, 2 and 8.
                var text = Glyphless(new TaskChain(line), u) ?? new TaskChain(line);
                var top = Math.Max(text.Y - 0.5 * u, label.Bottom);
                var bottom = Math.Min(text.Bottom + 0.5 * u, end);
                owner.ObjectiveBands.Add(Band(text.X - 0.5 * u, top, width * LeftPart, bottom));
            }
        }

        return new TaskPage(TaskScreen.Table, rows, u);

        TaskBand Band(double left, double top, double right, double bottom) =>
            new((int)Math.Round(left), (int)Math.Round(top), (int)Math.Round(right - left), (int)Math.Round(bottom - top));
    }

    /// <summary>Whether a table whose bands have been read is the operational one: a countdown in some row (the
    /// STORY and SIDE tabs show the same header; their tasks don't expire).</summary>
    public static TaskScreen Settle(TaskPage page) =>
        page.Screen == TaskScreen.Table && page.Rows.Count > 0 && page.Rows.All(r => r.TimeLeft is null) ? TaskScreen.OtherTable : page.Screen;

    /// <summary>The countdown's band as read: taken when it reads as hh:mm:ss, or days and hh:mm:ss ("6 day(s)
    /// 23:07:57").</summary>
    public static bool TakeTime(TaskRow row, string text)
    {
        if (TimeLeft(text) is not { } left)
            return false;
        row.TimeText = text;
        row.TimeLeft = left;
        return true;
    }

    /// <summary>The progress's band as read: taken when it reads as a number from 0 to 100 (with or without
    /// "%").</summary>
    public static bool TakeProgress(TaskRow row, string text)
    {
        if (Percent(text) is not { } percent)
            return false;
        row.ProgressText = text;
        row.Progress = percent;
        return true;
    }

    // ---- the screen ----

    private static List<TaskWord> FindHeader(IReadOnlyList<TaskWord> words)
    {
        var named = words.Where(w => Header.Contains(Fold(w.Text))).ToList();
        // The biggest group of header words on one line, each name once.
        return named.Select(anchor => named.Where(w => Overlap(w.Y, w.Bottom, anchor.Y, anchor.Bottom) > 0.5)
                .GroupBy(w => Fold(w.Text)).Select(g => g.First()).ToList())
            .OrderByDescending(g => g.Count).FirstOrDefault() ?? [];
    }

    private static bool IsTraderScreen(IReadOnlyList<TaskWord> words)
    {
        var folded = words.Select(w => Fold(w.Text)).ToHashSet(StringComparer.Ordinal);
        // Two words each: a stash page with a lone "replace" on it is no trader's screen.
        return (folded.Contains("replace") && folded.Contains("tasks")) || (folded.Contains("trading") && folded.Contains("services"));
    }

    // ---- a trader's TASKS tab ----

    // Where the list's right edge stands at 1440p (the panel under COMPLETE | REPLACE), for when those words weren't read.
    private const double ListRightAt = 749 / 2560.0;

    // How far left of the list's right edge a countdown may start, in units: "6 day(s) 23:07:57" is 10.6 wide,
    // right-aligned before the selected entry's chevron (measured at 1440p, 2026-10-25). The band starts no further left,
    // so the game's version line under the list's last entry stays out of it.
    private const double CountdownWidth = 14;

    /// <summary>
    /// A trader's TASKS tab: the entries of the list's OPERATIONAL TASKS (the names under that heading at its left edge,
    /// their glyph aside, down to the next section's heading in capitals), each with its countdown band at the list's
    /// right end (on the name's line, or a little under it where a hand-over's icon stands over it); and the header's task
    /// when an entry has its name. Nothing else of the page makes a row: the traders' row, the quest's text, its
    /// objectives and rewards, the other sections' quests.
    /// </summary>
    private static TaskPage Trader(IReadOnlyList<TaskWord> words, int width, int height)
    {
        var u = height * UnitAt;
        var told = new TaskPage(TaskScreen.TraderScreen, [], u);
        var replace = words.Where(w => Fold(w.Text) == "replace").OrderBy(w => w.Y).FirstOrDefault();
        if (replace is null)
            return told;
        // The list under COMPLETE | REPLACE, each word over its half: the list's right edge stands as far right of
        // REPLACE's middle as half the distance between the two middles.
        var complete = words.FirstOrDefault(w => Fold(w.Text) == "complete" && w.Right < replace.X && OnLine(w, replace));
        var listRight = complete is not null ? replace.Cx + (replace.Cx - complete.Cx) / 2 : ListRightAt * width;
        var heading = words
            .Where(w => Fold(w.Text) == "operational" && w.Y > replace.Bottom && w.Right < listRight &&
                        words.Any(t => Fold(t.Text) == "tasks" && OnLine(t, w) && t.X > w.Right && t.X - w.Right <= 1.5 * u))
            .OrderBy(w => w.Y).FirstOrDefault();
        if (heading is null)
            return told;

        var below = Chains(words.Where(w => w.Y > heading.Bottom && w.Right <= listRight + u).ToList(), u);
        var entries = new List<TaskRow>();
        foreach (var chain in below.Select(c => Glyphless(c, u)).OfType<TaskChain>().OrderBy(c => c.Cy))
        {
            if (chain.X < heading.X - u || chain.X > heading.X + 3 * u || !chain.Text.Any(char.IsLetter))
                continue;
            var letters = chain.Text.Where(char.IsLetter).ToList();
            if (letters.Count >= 4 && letters.All(char.IsUpper))
                break;
            if (entries.Count > 0 && Math.Abs(entries[^1].Cy - chain.Cy) < 2 * u)
                continue;
            var left = Math.Max(chain.Right + u, listRight - CountdownWidth * u);
            var top = chain.Y - 0.2 * u;
            var entry = new TaskRow
            {
                Name = Clean(chain.Text),
                Cy = chain.Cy,
                TimeBand = new TaskBand((int)Math.Round(left), (int)Math.Round(top), (int)Math.Round(listRight - left),
                    (int)Math.Round(chain.Bottom + 1.4 * u - top)),
            };
            // The countdown where the page's words carry it (the selected entry's, white; the others' orange digits the
            // page mostly drops, and their band is read by vote).
            var beside = below.Where(c => c.X >= chain.Right + u && c.Cy >= top && c.Cy <= chain.Bottom + 1.4 * u).OrderBy(c => c.X).ToList();
            if (beside.Count > 0)
                TakeTime(entry, string.Join(" ", beside.Select(c => c.Text)));
            entries.Add(entry);
        }
        var page = told with { Rows = entries };

        // The header's task: the run right of its glyph on REPLACE's line, right of the list.
        var header = Chains(words.Where(w => w.X >= listRight && OnLine(w, replace)).ToList(), u)
            .OrderBy(c => c.X).Select(c => Glyphless(c, u)).OfType<TaskChain>().FirstOrDefault();
        if (header is null)
            return page;
        var named = entries.Where(e => SameName(e.Name, header.Text)).ToList();
        if (named.Count == 0)
            return page;
        var bandLeft = (int)Math.Round(width * LeftPart);
        return page with
        {
            Selected = named.Count == 1 ? named[0] : new TaskRow { Name = Clean(header.Text), Cy = header.Cy },
            HeaderBand = new TaskBand(bandLeft, (int)Math.Round(header.Y - 0.5 * u), width - bandLeft, (int)Math.Round(header.Bottom - header.Y + u)),
            ObjectiveBands = ObjectiveBands(words, header, listRight, width, height, u),
        };
    }

    // The menu bar's words at the picture's foot, where the objectives end when no "Rewards" label was read.
    private static readonly HashSet<string> MenuBar = ["mainmenu", "main", "hideout", "character", "traders", "flea", "market", "builds", "handbook", "messenger", "survey", "expansions"];

    /// <summary>
    /// The task's objective lines on a trader's tab (owner, 2026-10-25): the lines the left part's words show in the bar
    /// under "Objective(s)" (right of the list, under the header) and above "Rewards" (or the game's menu bar at the
    /// picture's foot where no rewards are shown), each one band from the label's left edge to the picture's right edge,
    /// the line's height with half a unit above and below, never reaching above the label or into the rewards. The
    /// task's text above the label and the rewards below are in no band.
    /// </summary>
    private static List<TaskBand> ObjectiveBands(IReadOnlyList<TaskWord> words, TaskChain header, double listRight, int width, int height, double u)
    {
        var label = words.Where(w => w.X >= listRight && w.Y > header.Bottom && Fold(w.Text).StartsWith("objective", StringComparison.Ordinal))
            .OrderBy(w => w.Y).FirstOrDefault();
        if (label is null)
            return [];
        var rewards = words.Where(w => w.Y > label.Bottom && Math.Abs(w.X - label.X) <= 2 * u && Fold(w.Text).StartsWith("reward", StringComparison.Ordinal))
            .OrderBy(w => w.Y).FirstOrDefault();
        var end = rewards?.Y ?? words.Where(w => w.Y > height * 0.8 && MenuBar.Contains(Fold(w.Text))).Select(w => w.Y).DefaultIfEmpty(height).Min();
        var top = label.Bottom;
        var inside = Chains(words.Where(w => w.X >= label.X - 0.5 * u && w.Y > top && w.Bottom < end).ToList(), u);
        var bands = new List<TaskBand>();
        foreach (var line in Lines(inside))
        {
            var y = Math.Max(line.Min(w => w.Y) - 0.5 * u, top);
            var bottom = Math.Min(line.Max(w => w.Bottom) + 0.5 * u, end);
            var x = (int)Math.Round(label.X);
            bands.Add(new TaskBand(x, (int)Math.Round(y), width - x, (int)Math.Round(bottom - y)));
        }
        return bands;
    }

    private static readonly Regex CountAtEnd = new(@"(\S+)\s*$", RegexOptions.CultureInvariant);

    /// <summary>An objective line's count n/m at its end as a band read it ("0/7", the slashed zero as 0), or null.</summary>
    internal static string? CountOf(string text)
    {
        if (CountAtEnd.Match(text.Trim()) is not { Success: true } m || CountPattern.Match(Digits(m.Groups[1].Value)) is not { Success: true } count)
            return null;
        return $"{count.Groups[1].Value}/{count.Groups[2].Value}";
    }

    /// <summary>
    /// One objective line of a trader's tab from its band (owner, 2026-10-25): its words as a plain look at the band read
    /// them, up to the first gap of more than four units (the progress bar a counted objective has before its count, which
    /// a plain look reads as "ø/7" or "øn"), the line's icon left aside; and its count n/m when the band's vote read one
    /// (<paramref name="count"/>, as <see cref="CountOf"/> writes it), else none. The objective bar's TURN IN button, where
    /// a hand-over is ready, is no word of it. Null when no word is left.
    /// </summary>
    public static TaskObjective? ObjectiveOf(IReadOnlyList<TaskWord> words, string? count, double u)
    {
        var line = words.OrderBy(w => w.X).ToList();
        if (line.Count > 0 && Glyphless(new TaskChain(line), u) is { } chain)
            line = [.. chain.Words];
        var gap = Enumerable.Range(1, Math.Max(0, line.Count - 1)).FirstOrDefault(i => line[i].X - line[i - 1].Right > 4 * u);
        if (gap > 0)
            line = line[..gap];
        while (line.Count > 1 && line[^1].Text is "IN" && line[^2].Text is "TURN")
            line.RemoveRange(line.Count - 2, 2);
        if (Objective(line, u) is not { } read)
            return null;
        return CountValue(count) is { } value ? read with { Done = value.Done, Of = value.Of } : read with { Done = null, Of = null };
    }

    /// <summary>A count as <see cref="CountOf"/> writes it ("0/7") as its two numbers; null for none.</summary>
    internal static (int Done, int Of)? CountValue(string? count) =>
        count is not null && CountPattern.Match(count) is { Success: true } m
            ? (int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture))
            : null;

    /// <summary>
    /// The header line's band as read (its words, in the picture's pixels), for a trader's <see cref="TaskPage.Selected"/>
    /// task: the task's location, then its status word apart from it at the line's end ("Shoreline", "active!"). A lone
    /// run is the location, unless it reads as "active" (a task before ACCEPT shows another word there, or none; owner,
    /// 2026-10-25: it counts all the same). The status is one word: the menu's glyph at the line's end, read as a mark or a
    /// letter a little after it, is none of it.
    /// </summary>
    public static void TakeHeader(TaskRow row, IReadOnlyList<TaskWord> words, double u)
    {
        var parts = Chains(words, u).Where(c => c.Text.Count(char.IsLetter) >= 2).OrderBy(c => c.X).ToList();
        if (parts.Count == 0)
            return;
        var status = Status(parts[^1].Words[0].Text);
        if (parts.Count == 1 && status != "active")
        {
            row.Location = Clean(parts[0].Text);
            return;
        }
        if (parts.Count > 1)
            row.Location = Clean(string.Join(" ", parts.SkipLast(1).Select(c => c.Text)));
        row.Status = status;
    }

    /// <summary>Whether two names are one task's as read: their letters and digits, case aside ("Exit the location";
    /// "Part 2" isn't "Part 3").</summary>
    internal static bool SameName(string a, string b) => Letters(a) == Letters(b);

    private static string Letters(string text) => new(Clean(text).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    // A run without the glyph the game sets before a name (a clock, a hand, the list's mark; read as "O", "a", "@", "=",
    // "&"): a first word of one or two characters, as wide as a capital and a half, and as tall unless it's no letter.
    // Null when nothing else is left.
    private static TaskChain? Glyphless(TaskChain chain, double u)
    {
        var first = chain.Words[0];
        var glyph = first.Text.Length <= 2 && first.W >= 1.3 * u && (first.H >= 1.4 * u || !first.Text.All(char.IsLetter));
        if (!glyph)
            return chain;
        return chain.Words.Count > 1 ? new TaskChain(chain.Words.Skip(1).ToList()) : null;
    }

    private static bool OnLine(TaskWord w, TaskWord on) => Overlap(w.Y, w.Bottom, on.Y, on.Bottom) > 0.5;

    // ---- words into runs and lines ----

    /// <summary>The words joined into runs: on one line (their heights overlap by half) with at most 1.5 units
    /// between them. A table's cells stand several units apart; a sentence's words one apart.</summary>
    internal static List<TaskChain> Chains(IReadOnlyList<TaskWord> words, double u)
    {
        var chains = new List<List<TaskWord>>();
        foreach (var w in words.OrderBy(w => w.X))
        {
            var chain = chains.FirstOrDefault(c => c[^1] is var last && Overlap(last.Y, last.Bottom, w.Y, w.Bottom) > 0.5 &&
                                                   w.X - last.Right <= 1.5 * u && w.X - last.Right >= -0.5 * u);
            if (chain is null)
                chains.Add([w]);
            else
                chain.Add(w);
        }
        return chains.Select(c => new TaskChain(c)).ToList();
    }

    /// <summary>Runs on one line (an objective's text and its count, apart by the bar between them), top to
    /// bottom.</summary>
    private static List<List<TaskWord>> Lines(IReadOnlyList<TaskChain> chains)
    {
        var lines = new List<List<TaskChain>>();
        foreach (var c in chains.OrderBy(c => c.Cy))
        {
            var line = lines.FirstOrDefault(l => l.Any(o => Overlap(o.Y, o.Bottom, c.Y, c.Bottom) > 0.5));
            if (line is null)
                lines.Add([c]);
            else
                line.Add(c);
        }
        return lines.Select(l => l.OrderBy(c => c.X).SelectMany(c => c.Words).ToList()).ToList();
    }

    private static double Overlap(double top1, double bottom1, double top2, double bottom2)
    {
        var overlap = Math.Min(bottom1, bottom2) - Math.Max(top1, top2);
        var least = Math.Min(bottom1 - top1, bottom2 - top2);
        return least <= 0 ? 0 : overlap / least;
    }

    // ---- the cells' texts ----

    private static readonly Regex CountPattern = new(@"^(\d{1,4})/(\d{1,4})$", RegexOptions.CultureInvariant);

    /// <summary>One objective line: its words without the icons' marks, and a count n/m at its end. A count misread
    /// into no count is no word of it (2026-10-25: the table's "Ø/7" read as "øn" stood in the objective as "0n"): the
    /// line's last word, five characters at most, when it starts with the game's slashed zero (or a 0), which no word
    /// and no number of an objective does, or with another digit after the progress bar's gap of more than four units
    /// (a distance's "5øm" stands a word's gap after "than", and stays). Its count is then the band's vote's.</summary>
    internal static TaskObjective? Objective(IReadOnlyList<TaskWord> line, double u)
    {
        var words = line.Where(w => w.Text.Any(char.IsLetterOrDigit)).ToList();
        int? done = null, of = null;
        if (words.Count > 0 && CountPattern.Match(Digits(words[^1].Text)) is { Success: true } m)
        {
            done = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            of = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            words.RemoveAt(words.Count - 1);
        }
        else if (words.Count > 1 && MisreadCount(words[^1], words[^2], u))
        {
            words.RemoveAt(words.Count - 1);
        }
        if (words.Count == 0)
            return null;
        return new TaskObjective(Clean(string.Join(" ", words.Select(w => w.Text))), done, of);
    }

    private static bool MisreadCount(TaskWord last, TaskWord before, double u) =>
        last.Text.Length <= 5 && (last.Text[0] is 'ø' or 'Ø' or '0' || (char.IsAsciiDigit(last.Text[0]) && last.X - before.Right > 4 * u));

    private static readonly Regex TimePattern = new(@"^(\d{1,3})[:.,;](\d{2})[:.,;](\d{2})$", RegexOptions.CultureInvariant);
    private static readonly Regex TimeDigits = new(@"^(\d{1,3})(\d{2})(\d{2})$", RegexOptions.CultureInvariant);

    // A weekly's countdown past a day as the game writes it, in the table and in a trader's list alike (measured
    // 2026-10-25): the days, "day(s)" (or what the recognition made of it), then the time of day, "6 day(s) 23:07:57".
    private static readonly Regex DaysPattern = new(@"^(?<days>\S{1,2}?)\s*d\s*a\s*[yv]\s*(?:\(\s*[s5]?\s*\)?|[s5])?\s*(?<time>\S.*)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>A countdown "hh:mm:ss", or days and a time of day ("6 day(s) 23:07:57"), as read: the game's slashed
    /// zero and lookalike letters taken as digits, a colon read as a dot or lost between the digits, the selected list
    /// entry's chevron after it left aside.</summary>
    internal static TimeSpan? TimeLeft(string text)
    {
        var trimmed = text.Trim().TrimEnd('>', '›', '»', ' ');
        if (DaysPattern.Match(trimmed) is not { Success: true } d)
            return Clock(trimmed);
        // The days one digit (a week at most), the time under a day: a ")" read as a 1 makes no 123 hours.
        var days = Digits(d.Groups["days"].Value);
        if (days.Length != 1 || !char.IsAsciiDigit(days[0]) || Clock(d.Groups["time"].Value) is not { TotalHours: < 24 } time)
            return null;
        return TimeSpan.FromDays(days[0] - '0') + time;
    }

    private static readonly Regex DaysStart = new(@"^(?<days>\S{1,2}?)\s*d\s*a\s*[yv]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>The days of a countdown read as "6 day(s)", with or without the time after them; null for another
    /// text.</summary>
    internal static int? DaysOf(string text)
    {
        if (DaysStart.Match(text.Trim()) is not { Success: true } d)
            return null;
        var days = Digits(d.Groups["days"].Value);
        return days.Length == 1 && char.IsAsciiDigit(days[0]) ? days[0] - '0' : null;
    }

    private static TimeSpan? Clock(string text)
    {
        var s = Digits(text.Replace(" ", "", StringComparison.Ordinal));
        if (s.Any(char.IsLetter))
            return null;
        var m = TimePattern.Match(s);
        if (!m.Success)
            m = TimeDigits.Match(new string(s.Where(char.IsAsciiDigit).ToArray()));
        if (!m.Success)
            return null;
        var h = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var min = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        var sec = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        if (min > 59 || sec > 59)
            return null;
        return new TimeSpan(h, min, sec);
    }

    // The number, then the percent sign as the recognition reads it: "%", or "0/0" (its two rings and the bar; the
    // game's "0%" reads "00/0" or "ØO/O", its "57%" "570/0"; measured 2026-10-25). Greedy and anchored, so a "500/0" is
    // 50, never 0.
    private static readonly Regex PercentPattern = new(@"^(\d{1,3})(?:%|0/0)?$", RegexOptions.CultureInvariant);

    /// <summary>A progress "57%" as read, with the slashed zero and the percent sign read as "0/0"; 0 to
    /// 100.</summary>
    internal static int? Percent(string text)
    {
        var s = Digits(text.Replace(" ", "", StringComparison.Ordinal));
        if (PercentPattern.Match(s) is not { Success: true } m)
            return null;
        var value = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        return value <= 100 ? value : null;
    }

    /// <summary>The status word without its "!", which the recognition reads as "!", "1", "|" or, after an e, "l":
    /// "active".</summary>
    internal static string Status(string text)
    {
        var s = Clean(text).ToLowerInvariant();
        if (s.Length > 1 && (s[^1] is '!' or '1' or '|' || (s[^1] == 'l' && s[^2] == 'e')))
            s = s[..^1];
        return s.Trim();
    }

    /// <summary>Lookalikes of digits taken as digits: the game's zero is slashed (read ø, Ø, O), a one reads as l or
    /// I, a nine as g.</summary>
    internal static string Digits(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            sb.Append(c switch
            {
                'ø' or 'Ø' or 'O' or 'o' or 'Q' or 'D' => '0',
                'l' or 'I' or '|' or 'i' or '!' => '1',
                'S' or 's' => '5',
                'B' => '8',
                'Z' or 'z' => '2',
                'g' or 'q' => '9',
                _ => c,
            });
        }
        return sb.ToString();
    }

    /// <summary>A time left for comparing votes and truths: "08:33:19", the hours past a day as hours ("167:07:57" for
    /// the game's "6 day(s) 23:07:57").</summary>
    internal static string Hms(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";

    /// <summary>A name or objective as written: the slashed zero as 0, curly quotes straight.</summary>
    internal static string Clean(string text) =>
        text.Replace('ø', '0').Replace('Ø', '0').Replace('“', '"').Replace('”', '"').Replace('„', '"').Replace('’', '\'').Trim();

    /// <summary>A word for comparing: lower case, letters only ("Status v" → "statusv", "Objective(s)" →
    /// "objectives").</summary>
    internal static string Fold(string text) => new(text.ToLowerInvariant().Where(char.IsLetter).ToArray());

    private static double Median(IEnumerable<double> values)
    {
        var list = values.Order().ToList();
        return list.Count == 0 ? 0 : list[list.Count / 2];
    }
}
