using System.Diagnostics;
using System.Globalization;
using Shturmap.Game.Screenshots;

namespace Shturmap.Cli.Tasks;

/// <summary>
/// The spike of 2026-10-19 for "Dailies and weeklies from a screenshot" (docs/NEXT.md, "To do later"; owner,
/// 2026-10-17): reads the operational tasks' table from menu screenshots headless (Character › TASKS › OPERATIONAL:
/// each row's task, location, status, time left and progress, and an expanded row's objectives with their counts),
/// tells a trader's tasks screen from it, and says when each task expires (the file's time plus the time left). With a
/// folder, it scores itself per field against hand-written truths beside the pictures (<see cref="TaskTruth"/>).
/// The reading is the app's own since 2026-10-19 (Shturmap.Game.Screenshots.TasksReader; owner, from a panel of four,
/// B; DESIGN.md §2), and this prints what it read, before the app's checks on the countdowns (Shturmap.Core.Operational,
/// OperationalChecks), which it says beside each row. The pictures are the owner's own, in the git-ignored
/// tests\fixtures\ocr\operational; only the table is read (the left two thirds of the picture), and it prints the
/// table's words and scores, never a path's folder.
/// </summary>
internal static class TasksSpike
{
    private sealed class Field
    {
        public int Right, Wrong, Unread;

        public void Score(string? said, string? truth)
        {
            if (truth is null)
                return;
            if (said is null)
                Unread++;
            else if (string.Equals(said, truth, StringComparison.OrdinalIgnoreCase))
                Right++;
            else
                Wrong++;
        }

        public override string ToString() => $"{Right} right, {Wrong} wrong, {Unread} unread (of {Right + Wrong + Unread})";
    }

    private sealed class Tally
    {
        public readonly Field Screen = new(), Rows = new(), Name = new(), Location = new(), Status = new(), Time = new(),
            Progress = new(), Objective = new(), Count = new();

        // A trader's screen: the header's task, its location, status and countdown; the list's entries and their countdowns.
        public readonly Field TraderTask = new(), TraderLocation = new(), TraderStatus = new(), TraderTime = new(), Entry = new(), EntryTime = new();
    }

    public static async Task RunAsync(IReadOnlyList<string> args)
    {
        var zoom = 1.0;
        bool printWords = false, printBands = false;
        var inputs = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--zoom": zoom = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--words": printWords = true; break;
                case "--bands": printBands = true; break;
                case "--ways": TasksReader.Ways = TasksReader.ParseWays(args[++i]); break;
                case "--dump": TasksReader.Dump = Directory.CreateDirectory(args[++i]).FullName; break;
                case "--raw": TasksReader.Raw = printBands = true; break;
                default: inputs.Add(args[i]); break;
            }
        }
        if (inputs.Count == 0)
        {
            Console.WriteLine("Use: shturmap-cli tasks <png or folder>... [--bands] [--raw] [--words] [--ways plain@1,binary@2,...] [--zoom <page zoom>] [--dump <folder>]");
            return;
        }
        var ocr = TasksReader.Create();
        if (ocr is null)
        {
            Console.WriteLine("This Windows has no English text recognition: no reading.");
            return;
        }
        Console.WriteLine($"Recognition: {ocr.LanguageTag}; the page at {zoom}x; each band {TasksReader.Ways.Count} ways, by vote");
        var files = inputs.SelectMany(p => Directory.Exists(p) ? Directory.GetFiles(p, "*.png").Order(StringComparer.Ordinal).ToArray() : [p]).ToList();
        var tally = new Tally();
        var truths = 0;
        foreach (var file in files)
        {
            var watch = Stopwatch.StartNew();
            using var picture = StashPicture.Decode(await File.ReadAllBytesAsync(file));
            if (picture is null)
            {
                Console.WriteLine($"== {Path.GetFileName(file)}: not a picture");
                continue;
            }
            if (printWords)
            {
                foreach (var w in (await ocr.ReadWordsAsync(picture, zoom, CancellationToken.None)).OrderBy(w => w.Y).ThenBy(w => w.X))
                    Console.WriteLine($"     word \"{w.Text}\" at {w.X:0},{w.Y:0} {w.W:0}×{w.H:0}");
            }
            var page = await ocr.ReadPageAsync(picture, zoom, printBands ? n => Console.WriteLine("     " + n) : null);
            Print(file, page, watch.ElapsedMilliseconds);
            var truthFile = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file))!, $"truth-{Path.GetFileNameWithoutExtension(file)}.txt");
            if (File.Exists(truthFile))
            {
                truths++;
                Score(page, TaskTruth.Load(truthFile), tally);
            }
        }
        if (truths > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"Against {truths} truths:");
            Console.WriteLine($"  screen     {tally.Screen}");
            Console.WriteLine($"  rows       {tally.Rows}");
            Console.WriteLine($"  task       {tally.Name}");
            Console.WriteLine($"  location   {tally.Location}");
            Console.WriteLine($"  status     {tally.Status}");
            Console.WriteLine($"  time left  {tally.Time}");
            Console.WriteLine($"  progress   {tally.Progress}");
            Console.WriteLine($"  objective  {tally.Objective}");
            Console.WriteLine($"  count      {tally.Count}");
            Console.WriteLine("  on a trader's screen:");
            Console.WriteLine($"  task       {tally.TraderTask}");
            Console.WriteLine($"  location   {tally.TraderLocation}");
            Console.WriteLine($"  status     {tally.TraderStatus}");
            Console.WriteLine($"  time left  {tally.TraderTime}");
            Console.WriteLine($"  entry      {tally.Entry}");
            Console.WriteLine($"  its time   {tally.EntryTime}");
        }
    }

    private static void Print(string file, TaskPage page, long ms)
    {
        // The game writes the file as the key is pressed: its time is when the countdowns were read. A file written
        // again later (by a tool, or a copy that doesn't keep the time) carries that later time, and its expiries are
        // that much late.
        var taken = File.GetLastWriteTime(file);
        var screen = page.Screen switch
        {
            TaskScreen.Table => $"the operational table, {page.Rows.Count} rows",
            TaskScreen.OtherTable => $"a Tasks table without countdowns ({page.Rows.Count} rows): not the operational one, not read",
            TaskScreen.TraderScreen => page.Selected is null
                ? $"a trader's tasks screen, no operational task in its header; its list's {page.Rows.Count} operational entries"
                : $"a trader's tasks screen with an operational task; its list's {page.Rows.Count} operational entries",
            _ => "no Tasks table: not read",
        };
        Console.WriteLine($"== {Path.GetFileName(file)}: {screen}; {ms} ms; file time {taken:yyyy-MM-dd HH:mm:ss}");
        if (page.Screen is not (TaskScreen.Table or TaskScreen.TraderScreen))
            return;
        // The app's checks on one screenshot's countdowns (OperationalChecks): a countdown that fails them gives no expiry.
        var kept = Shturmap.Core.Operational.OperationalChecks.Countdowns(page.Rows.Select(r => (r.TimeLeft, r.TimeVotes)).ToList());
        if (page.Screen == TaskScreen.TraderScreen)
        {
            PrintTrader(page, taken, kept);
            return;
        }
        foreach (var (row, index) in page.Rows.Select((r, i) => (r, i)))
        {
            var left = row.TimeLeft is { } t ? $"{TaskTable.Hms(t)} ({row.TimeVotes} votes)" : "?";
            var progress = row.Progress is { } p ? $"{p}% ({row.ProgressVotes} votes)" : "?";
            var expiry = row.TimeLeft is null ? "?"
                : kept[index] is { } l ? (taken + l).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                : "none (fails the checks)";
            Console.WriteLine($"   {row.Name,-20} | {row.Location ?? "?",-12} | {row.Status ?? "?",-6} | left {left,-20} | {progress,-14} | expires {expiry}");
            foreach (var o in row.Objectives)
                Console.WriteLine($"      objective \"{o.Text}\"" + (o.Of is not null ? $" {o.Done}/{o.Of}" : ""));
        }
    }

    // A trader's TASKS tab (owner, 2026-10-25): the header's task as the app takes it (the location is matched to a map of
    // the data in the app; "Any location" is every map) with its objectives and their counts, then each entry of the list
    // with its countdown, the selected one its task's, the others refreshing a kept task of their name in the app.
    private static void PrintTrader(TaskPage page, DateTime taken, IReadOnlyList<TimeSpan?> kept)
    {
        string Left(TaskRow row) => row.TimeLeft is { } t ? $"{row.TimeText} = {TaskTable.Hms(t)} ({row.TimeVotes} votes)" : "unread";
        string Expiry(int index) => page.Rows[index].TimeLeft is null ? "?"
            : kept[index] is { } l ? (taken + l).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : "none (fails the checks)";
        if (page.Selected is { } task)
        {
            var index = page.Rows.ToList().IndexOf(task);
            Console.WriteLine($"   task  {task.Name,-20} | map {task.Location ?? "unread",-18} | {task.Status ?? "no status",-9} | left " +
                              (index < 0 ? "none (two entries of its name)" : $"{Left(task)} | expires {Expiry(index)}"));
            foreach (var o in task.Objectives)
                Console.WriteLine($"      objective \"{o.Text}\"" + (o.Of is not null ? $" {o.Done}/{o.Of}" : ""));
        }
        for (var i = 0; i < page.Rows.Count; i++)
        {
            var entry = page.Rows[i];
            if (ReferenceEquals(entry, page.Selected))
                continue;
            Console.WriteLine($"   entry {entry.Name,-20} | left {Left(entry)} | expires {Expiry(i)} (refreshes a kept task of this name)");
        }
    }

    private static void Score(TaskPage page, TaskTruth truth, Tally tally)
    {
        var screen = page.Screen switch
        {
            TaskScreen.Table => "table",
            TaskScreen.TraderScreen => "trader",
            TaskScreen.OtherTable => "other table",
            _ => "none",
        };
        tally.Screen.Score(screen, truth.Screen);
        if (truth.Screen == "trader" && page.Screen == TaskScreen.TraderScreen)
        {
            ScoreTrader(page, truth, tally);
            return;
        }
        if (truth.Screen != "table")
        {
            // A screen not to read: any row read from it is wrong.
            if (page.Screen == TaskScreen.Table)
                tally.Rows.Wrong += page.Rows.Count;
            return;
        }
        var matched = new HashSet<TaskRow>();
        for (var index = 0; index < truth.Rows.Count; index++)
        {
            var t = truth.Rows[index];
            // The row of the truth's name, else the row in its place (a name misread).
            var row = page.Rows.FirstOrDefault(r => !matched.Contains(r) && TaskTruth.Same(r.Name, t.Name));
            if (row is null && page.Rows.ElementAtOrDefault(index) is { } byOrder && !matched.Contains(byOrder))
                row = byOrder;
            if (row is null)
            {
                foreach (var f in new[] { tally.Rows, tally.Name, tally.Location, tally.Status, tally.Time, tally.Progress })
                    f.Unread++;
                continue;
            }
            matched.Add(row);
            tally.Rows.Right++;
            tally.Name.Score(row.Name, t.Name);
            tally.Location.Score(row.Location, t.Location);
            tally.Status.Score(row.Status, TaskTable.Status(t.Status));
            tally.Time.Score(row.TimeLeft is { } left ? TaskTable.Hms(left) : null, Hms(t.Time));
            tally.Progress.Score(row.Progress?.ToString(CultureInfo.InvariantCulture), t.Progress.TrimEnd('%'));
            var wanted = truth.Objectives.Where(o => TaskTruth.Same(o.Row, t.Name)).ToList();
            for (var i = 0; i < wanted.Count; i++)
            {
                var said = row.Objectives.ElementAtOrDefault(i);
                tally.Objective.Score(said?.Text, wanted[i].Text);
                tally.Count.Score(said is null ? null : TaskTruth.Count(said), wanted[i].Count);
            }
            // Objectives read for a row that has none in the truth, or more than it lists: wrong.
            tally.Objective.Wrong += Math.Max(0, row.Objectives.Count - wanted.Count);
        }
        tally.Rows.Wrong += page.Rows.Count(r => !matched.Contains(r));
    }

    // A countdown as a truth writes it, as Hms writes it: "6 day(s) 23:07:57" is "167:07:57".
    private static string Hms(string truth) => TaskTable.TimeLeft(truth) is { } t ? TaskTable.Hms(t) : truth;

    // A trader's screen: the header's task (or none, for a story quest's), then the list's entries in order.
    private static void ScoreTrader(TaskPage page, TaskTruth truth, Tally tally)
    {
        if (truth.Task is { } task)
        {
            tally.TraderTask.Score(page.Selected?.Name, task.Name);
            tally.TraderLocation.Score(page.Selected?.Location, task.Location);
            tally.TraderStatus.Score(page.Selected?.Status, TaskTable.Status(task.Status));
            var entry = truth.Entries.FirstOrDefault(e => TaskTruth.Same(e.Name, task.Name));
            if (entry.Name is not null)
                tally.TraderTime.Score(page.Selected?.TimeLeft is { } left ? TaskTable.Hms(left) : null, Hms(entry.Time));
            var wanted = truth.Objectives.Where(o => TaskTruth.Same(o.Row, task.Name)).ToList();
            var said = page.Selected?.Objectives ?? [];
            for (var i = 0; i < wanted.Count; i++)
            {
                tally.Objective.Score(said.ElementAtOrDefault(i)?.Text, wanted[i].Text);
                tally.Count.Score(said.ElementAtOrDefault(i) is { } o ? TaskTruth.Count(o) : null, wanted[i].Count);
            }
            tally.Objective.Wrong += Math.Max(0, said.Count - wanted.Count);
        }
        else if (page.Selected is not null)
        {
            tally.TraderTask.Wrong++;
        }
        for (var i = 0; i < truth.Entries.Count; i++)
        {
            var row = page.Rows.ElementAtOrDefault(i);
            tally.Entry.Score(row?.Name, truth.Entries[i].Name);
            tally.EntryTime.Score(row?.TimeLeft is { } left ? TaskTable.Hms(left) : null, Hms(truth.Entries[i].Time));
        }
        tally.Entry.Wrong += Math.Max(0, page.Rows.Count - truth.Entries.Count);
    }
}
