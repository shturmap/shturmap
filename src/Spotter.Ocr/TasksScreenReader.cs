using System.Diagnostics;

namespace Spotter.Ocr;

public enum TasksTab
{
    Unknown,
    Story,
    Side,
    Operational,
}

/// <summary>One row of the Tasks table as read: the raw texts of its Task, Location and Status cells.</summary>
public sealed record TasksRow(string Name, string Location, string Status, Box Box);

public sealed record TasksScreen(TasksTab Tab, IReadOnlyList<TasksRow> Rows, TimeSpan Elapsed);

/// <summary>
/// Recognises the Character → Tasks screen in a screenshot and reads its table. The layout is taken from where
/// the column headers are, so resolution, aspect ratio and UI scale don't matter. Measured on 2560×1440
/// screenshots: the whole frame misreads small names, the cropped and doubled columns read them exactly.
/// </summary>
public sealed class TasksScreenReader(TextRecognizer recognizer, double scale = 2.0,
    Windows.Graphics.Imaging.BitmapInterpolationMode interpolation = Windows.Graphics.Imaging.BitmapInterpolationMode.Cubic)
{
    private static readonly string[] Headers = ["Trader", "Type", "Class", "Task", "Location", "Status", "Progress"];
    private static readonly string[] BottomBar = ["MAIN MENU", "HIDEOUT", "CHARACTER", "TRADERS", "FLEA MARKET", "BUILDS", "HANDBOOK", "MESSENGER"];

    /// <summary>Returns null when the screenshot is not the Tasks screen.</summary>
    public async Task<TasksScreen?> ReadAsync(string path)
    {
        var watch = Stopwatch.StartNew();
        using var shot = await Screenshot.LoadAsync(path);
        var frame = await recognizer.ReadAsync(shot);

        var header = FindHeaderRow(frame);
        if (header is null)
            return null;

        var top = header.Values.Max(b => b.Bottom) + 4;
        var bottom = frame
            .Where(l => l.Box.Y > top && BottomBar.Any(b => l.Text.Contains(b, StringComparison.OrdinalIgnoreCase)))
            .Select(l => l.Box.Y)
            .DefaultIfEmpty(shot.Height * 0.965)
            .Min() - 4;

        // Columns: from just after Class to halfway between Status and Progress.
        var left = header.TryGetValue("Class", out var cls) ? cls.Right + 8 : header["Task"].X - (header["Location"].X - header["Task"].Right);
        var right = header.TryGetValue("Progress", out var progress)
            ? (header["Status"].Right + progress.X) / 2
            : header["Status"].Right + header["Status"].Width;
        var nameEnd = (header["Task"].Right + header["Location"].X) / 2;
        var locationEnd = (header["Location"].Right + header["Status"].X) / 2;

        var region = new Box(left, top, right - left, bottom - top);
        using var crop = await shot.CropAsync(region, scale, interpolation);
        var lines = await recognizer.ReadAsync(crop, region.X, region.Y, scale);

        var rows = AssembleRows(lines, nameEnd, locationEnd, region);
        return new TasksScreen(SelectedTab(shot, frame), rows, watch.Elapsed);
    }

    /// <summary>The header words, keyed by header name, if at least four of them sit on one row.</summary>
    private static Dictionary<string, Box>? FindHeaderRow(IReadOnlyList<TextLine> lines)
    {
        var words = lines.SelectMany(l => l.Words)
            .Where(w => Headers.Contains(w.Text.Trim(), StringComparer.OrdinalIgnoreCase))
            .ToList();
        foreach (var anchor in words.Where(w => w.Text.Trim().Equals("Task", StringComparison.OrdinalIgnoreCase)))
        {
            var row = words
                .Where(w => Math.Abs(w.Box.CenterY - anchor.Box.CenterY) < anchor.Box.Height)
                .GroupBy(w => Headers.First(h => h.Equals(w.Text.Trim(), StringComparison.OrdinalIgnoreCase)))
                .ToDictionary(g => g.Key, g => g.First().Box);
            if (row.Count >= 4 && row.ContainsKey("Location") && row.ContainsKey("Status"))
                return row;
        }
        return null;
    }

    internal static List<TasksRow> AssembleRows(IReadOnlyList<TextLine> lines, double nameEnd, double locationEnd, Box region)
    {
        var names = lines.Where(l => l.Box.X < nameEnd).ToList();
        var locations = lines.Where(l => l.Box.X >= nameEnd && l.Box.X < locationEnd).ToList();
        var statuses = lines.Where(l => l.Box.X >= locationEnd).OrderBy(l => l.Box.CenterY).ToList();

        // Rows anchor on the Status cell; without any, on the names.
        var anchors = statuses.Count > 0 ? statuses : names.OrderBy(l => l.Box.CenterY).ToList();
        if (anchors.Count == 0)
            return [];
        var pitch = anchors.Count > 1
            ? Median(anchors.Zip(anchors.Skip(1), (a, b) => b.Box.CenterY - a.Box.CenterY))
            : anchors[0].Box.Height * 5;

        var rows = new List<TasksRow>();
        foreach (var anchor in anchors)
        {
            var y = anchor.Box.CenterY;
            // A row the edge of the table cuts through is read again in the next screenshot.
            if (y - region.Y < pitch * 0.35 || region.Bottom - y < pitch * 0.35)
                continue;
            var name = string.Join(" ", names.Where(l => Math.Abs(l.Box.CenterY - y) < pitch * 0.4).OrderBy(l => l.Box.Y).Select(l => l.Text));
            if (name.Length == 0)
                continue;
            var location = string.Join(" ", locations.Where(l => Math.Abs(l.Box.CenterY - y) < pitch * 0.45).OrderBy(l => l.Box.Y).Select(l => l.Text));
            var status = ReferenceEquals(anchors, statuses) ? anchor.Text : "";
            rows.Add(new TasksRow(name, location, status, anchor.Box));
        }
        return rows;
    }

    private static TasksTab SelectedTab(Screenshot shot, IReadOnlyList<TextLine> frame)
    {
        // The selected tab is drawn dark-on-light, which OCR often fails to read, so the tabs are found from
        // the STORY and OPERATIONAL labels and SIDE is taken as the slot between them. The light one is selected.
        TextWord? Word(string label) => frame.SelectMany(l => l.Words).FirstOrDefault(w => w.Text.Trim().Equals(label, StringComparison.Ordinal));
        var story = Word("STORY");
        var operational = Word("OPERATIONAL");
        if (story is null || operational is null || Math.Abs(story.Box.CenterY - operational.Box.CenterY) > story.Box.Height)
            return TasksTab.Unknown;

        var gap = operational.Box.X - story.Box.Right;
        var side = Word("SIDE")?.Box ?? new Box(story.Box.Right + gap * 0.3, story.Box.Y, gap * 0.4, story.Box.Height);
        var scored = new[]
            {
                (Tab: TasksTab.Story, Light: shot.MeanLuminance(Grow(story.Box, 0.35))),
                (Tab: TasksTab.Side, Light: shot.MeanLuminance(Grow(side, 0.35))),
                (Tab: TasksTab.Operational, Light: shot.MeanLuminance(Grow(operational.Box, 0.35))),
            }
            .OrderByDescending(t => t.Light)
            .ToList();
        return scored[0].Light - scored[1].Light > 25 ? scored[0].Tab : TasksTab.Unknown;
    }

    private static Box Grow(Box b, double f) => new(b.X - b.Width * f, b.Y - b.Height * f, b.Width * (1 + 2 * f), b.Height * (1 + 2 * f));

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
    }
}
