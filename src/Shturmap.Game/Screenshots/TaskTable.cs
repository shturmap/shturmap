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

    /// <summary>A trader's TASKS tab (the list at the left, REPLACE, one task at the right): told, not read.</summary>
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

    /// <summary>How many of the ways a band was read in gave the value taken (0 when the page's words carried
    /// it).</summary>
    public int TimeVotes { get; set; }
    public int ProgressVotes { get; set; }

    /// <summary>The row's middle (the task name's): the bands are measured from it.</summary>
    public double Cy { get; init; }

    public TaskBand TimeBand { get; init; }
    public TaskBand ProgressBand { get; init; }
}

public sealed record TaskPage(TaskScreen Screen, IReadOnlyList<TaskRow> Rows, double Unit);

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
/// with a count n/m at its end where the game shows one; they go to the row above them.</item>
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
        {
            var screen = IsTraderScreen(words) ? TaskScreen.TraderScreen : TaskScreen.None;
            return new TaskPage(screen, [], height * UnitAt);
        }
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
                if (Objective(line) is { } objective)
                    owner.Objectives.Add(objective);
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

    /// <summary>The countdown's band as read: taken when it reads as hh:mm:ss.</summary>
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

    /// <summary>One objective line: its words without the icons' marks, and a count n/m at its end.</summary>
    internal static TaskObjective? Objective(IReadOnlyList<TaskWord> line)
    {
        var tokens = line.Select(w => w.Text).Where(t => t.Any(char.IsLetterOrDigit)).ToList();
        int? done = null, of = null;
        if (tokens.Count > 0 && CountPattern.Match(Digits(tokens[^1])) is { Success: true } m)
        {
            done = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            of = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            tokens.RemoveAt(tokens.Count - 1);
        }
        if (tokens.Count == 0)
            return null;
        return new TaskObjective(Clean(string.Join(" ", tokens)), done, of);
    }

    private static readonly Regex TimePattern = new(@"^(\d{1,3})[:.,;](\d{2})[:.,;](\d{2})$", RegexOptions.CultureInvariant);
    private static readonly Regex TimeDigits = new(@"^(\d{1,3})(\d{2})(\d{2})$", RegexOptions.CultureInvariant);

    /// <summary>A countdown "hh:mm:ss" as read: the game's slashed zero and lookalike letters taken as digits, a
    /// colon read as a dot or lost between the digits.</summary>
    internal static TimeSpan? TimeLeft(string text)
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

    private static readonly Regex PercentPattern = new(@"^(\d{1,3})%?$", RegexOptions.CultureInvariant);

    /// <summary>A progress "57%" as read, with the slashed zero; 0 to 100.</summary>
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

    /// <summary>A time left as the game writes it: "08:33:19", the hours past a day as they are ("31:05:00").</summary>
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
