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
/// Nothing of it runs in the app; DESIGN.md §2 is unchanged. The pictures are the owner's own, in the git-ignored
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
                case "--ways": TaskTableOcr.Ways = TaskTableOcr.ParseWays(args[++i]); break;
                case "--dump": TaskTableOcr.Dump = Directory.CreateDirectory(args[++i]).FullName; break;
                default: inputs.Add(args[i]); break;
            }
        }
        if (inputs.Count == 0)
        {
            Console.WriteLine("Use: shturmap-cli tasks <png or folder>... [--bands] [--words] [--ways plain@1,binary@2,...] [--zoom <page zoom>] [--dump <folder>]");
            return;
        }
        var ocr = TaskTableOcr.Create();
        if (ocr is null)
        {
            Console.WriteLine("This Windows has no English text recognition: no reading.");
            return;
        }
        Console.WriteLine($"Recognition: {ocr.LanguageTag}; the page at {zoom}x; each band {TaskTableOcr.Ways.Count} ways, by vote");
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
                foreach (var w in (await ocr.ReadAsync(picture, zoom, CancellationToken.None)).OrderBy(w => w.Y).ThenBy(w => w.X))
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
            TaskScreen.TraderScreen => "a trader's tasks screen: told, not read",
            _ => "no Tasks table: not read",
        };
        Console.WriteLine($"== {Path.GetFileName(file)}: {screen}; {ms} ms; file time {taken:yyyy-MM-dd HH:mm:ss}");
        if (page.Screen != TaskScreen.Table)
            return;
        foreach (var row in page.Rows)
        {
            var left = row.TimeLeft is { } t ? $"{TaskTable.Hms(t)} ({row.TimeVotes} votes)" : "?";
            var progress = row.Progress is { } p ? $"{p}% ({row.ProgressVotes} votes)" : "?";
            var expiry = row.TimeLeft is { } l ? (taken + l).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "?";
            Console.WriteLine($"   {row.Name,-20} | {row.Location ?? "?",-12} | {row.Status ?? "?",-6} | left {left,-20} | {progress,-14} | expires {expiry}");
            foreach (var o in row.Objectives)
                Console.WriteLine($"      objective \"{o.Text}\"" + (o.Of is not null ? $" {o.Done}/{o.Of}" : ""));
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
            tally.Time.Score(row.TimeLeft is { } left ? TaskTable.Hms(left) : null, t.Time);
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
}
