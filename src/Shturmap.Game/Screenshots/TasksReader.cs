using System.Globalization;
using SkiaSharp;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace Shturmap.Game.Screenshots;

/// <summary>
/// Reads a Tasks screenshot with Windows' own text recognition (Windows.Media.Ocr, on the PC, nothing sent) for
/// <see cref="TaskTable"/> (owner, 2026-10-19: from a panel of four, B; docs/DESIGN.md §2, "Menu screenshots"): first
/// the words of the picture's left part (<see cref="TaskTable.LeftPart"/>), where the table stands (the right part
/// carries the task items' grids, the player's name and money on a trader's screen, and the game's toasts with the
/// screenshot's path, none of which is ever read); then each row's countdown and progress from their own bands. The
/// recognition reads the table's names and objectives at the picture's own size, but drops most of the game's small
/// digits whatever is done to them (the spike of 2026-10-19: enlarged, stretched, cut to black and white, stacked with
/// the page's other bands, a word set before them, the glyphs set apart), and drops each band in a different way; so
/// every band is read in all the <see cref="Ways"/> and the value most of them read is taken, with how many did
/// (<see cref="TaskRow.TimeVotes"/>), for the checks that follow (Shturmap.Core.Operational.OperationalChecks). On a
/// trader's TASKS tab (owner, 2026-10-25) the same: the page's words, then each entry of the list's OPERATIONAL TASKS
/// its countdown band by vote, and, when the header names one of them, the bands across the picture's right third that
/// are ever read there: the header line's, for the task's location and status, and one per objective line under
/// "Objective(s)", for its words and its count by vote (the traders' row with the player's name and money stands above
/// the header, the task's text between the header and the objectives, its rewards below them: none of it in a band).
/// The file is opened for reading only, shared with the game that may still be writing it, and nothing of the picture
/// is kept or written anywhere.
/// </summary>
public sealed class TasksReader
{
    private readonly OcrEngine _engine;

    private TasksReader(OcrEngine engine) => _engine = engine;

    /// <summary>The language the table is read in ("en-US").</summary>
    public string LanguageTag => _engine.RecognizerLanguage.LanguageTag;

    /// <summary>A reader for English (the table's words are the English game's; the German game's are unknown), null
    /// when this Windows has no English recognition.</summary>
    public static TasksReader? Create()
    {
        try
        {
            var english = OcrEngine.AvailableRecognizerLanguages.FirstOrDefault(l => l.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase));
            var engine = english is null ? null : OcrEngine.TryCreateFromLanguage(new Language(english.LanguageTag));
            return engine is null ? null : new TasksReader(engine);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>The ways a band is read, each a treatment at a zoom: "plain" as it is; "stretched" its brightest
    /// channel stretched to full contrast as grey (the countdown is orange on a brown cell); "inverted" that, dark on
    /// light; "binary" that cut at the middle, black on white. Measured on the owner's four tables (2026-10-19): of the
    /// twelve countdowns, binary at 1x reads 7, plain at 1x 6, inverted at 0.75x 5 (and one wrong), the others 0 to 3;
    /// no way reads more than 3 of the twelve progresses. Plain at 3x read nothing and is left out.</summary>
    internal static IReadOnlyList<(string Mode, double Zoom)> Ways { get; set; } =
    [
        ("plain", 1), ("binary", 1), ("inverted", 1), ("stretched", 1), ("plain", 2), ("binary", 2), ("inverted", 2),
        ("binary", 0.75), ("inverted", 0.75), ("binary", 1.5), ("inverted", 1.5), ("binary", 3), ("binary", 4), ("plain", 4),
    ];

    /// <summary>Ways as the command line writes them: "plain@1,binary@2".</summary>
    internal static IReadOnlyList<(string Mode, double Zoom)> ParseWays(string text) =>
        text.Split(',').Select(w => w.Split('@')).Select(p => (p[0], double.Parse(p[1], CultureInfo.InvariantCulture))).ToList();

    /// <summary>A folder to write each band into as it is read, to see what the recognition was given
    /// (--dump).</summary>
    internal static string? Dump { get; set; }

    /// <summary>Each way's text as the recognition gave it, said with the band's votes (--raw).</summary>
    internal static bool Raw { get; set; }

    /// <summary>
    /// What a screenshot shows: null when the file isn't a whole picture after the tries, or is gone; else the page,
    /// whose <see cref="TaskPage.Screen"/> says whether it is the operational table (its rows read), a trader's tasks
    /// or another Tasks table (told, not read), or something else.
    /// </summary>
    public async Task<TaskPage?> ReadAsync(string path, int tries = 8, TimeSpan? wait = null, CancellationToken ct = default)
    {
        using var picture = await MenuPicture.OpenAsync(path, tries, wait, ct);
        return picture is null ? null : await ReadPageAsync(picture, ct: ct);
    }

    /// <summary>The table read from a picture: the page's words, the rows from them, then each row's countdown and
    /// progress from their bands, by vote (only for the operational table: a page that isn't one asks nothing more of
    /// the recognition). <paramref name="note"/> hears each band's votes.</summary>
    public async Task<TaskPage> ReadPageAsync(SKBitmap picture, double zoom = 1, Action<string>? note = null, CancellationToken ct = default)
    {
        var words = await ReadWordsAsync(picture, zoom, ct);
        var page = TaskTable.Read(words, picture.Width, picture.Height);
        if (page.Screen == TaskScreen.TraderScreen)
            return await ReadTraderAsync(picture, page, note, ct);
        if (page.Screen != TaskScreen.Table)
            return page;
        foreach (var row in page.Rows)
        {
            if (row.TimeLeft is null)
                await VoteCountdownAsync(picture, row, note, ct);
            if (row.Progress is null)
            {
                var (text, votes, _) = await VoteAsync(picture, row.ProgressBand, t => TaskTable.Percent(t)?.ToString(CultureInfo.InvariantCulture), "progress", note, ct);
                if (text is not null)
                    TaskTable.TakeProgress(row, text);
                row.ProgressVotes = votes;
            }
        }
        return page with { Screen = TaskTable.Settle(page) };
    }

    // A trader's TASKS tab: the header's location and status from the header line's band and the objectives from theirs
    // when the header names an operational task (a story quest's tab reads no band of the right third), and each list
    // entry's countdown by vote.
    private async Task<TaskPage> ReadTraderAsync(SKBitmap picture, TaskPage page, Action<string>? note, CancellationToken ct)
    {
        if (page.Selected is { } selected)
        {
            TaskTable.TakeHeader(selected, await ReadBandWordsAsync(picture, page.HeaderBand, ct), page.Unit);
            note?.Invoke($"header band {page.HeaderBand.X},{page.HeaderBand.Y}: location {selected.Location ?? "unread"}, status {selected.Status ?? "unread"}");
            // Each objective line: its words as it is, its count at its end by vote (the counts are the game's small
            // digits, as the countdowns are).
            foreach (var band in page.ObjectiveBands)
            {
                var words = await ReadBandWordsAsync(picture, band, ct);
                var (count, votes, _) = await VoteAsync(picture, band, TaskTable.CountOf, "objective count", note, ct);
                var objective = TaskTable.ObjectiveOf(words, count is null ? null : TaskTable.CountOf(count), page.Unit);
                if (objective is not null)
                    selected.Objectives.Add(objective);
                note?.Invoke($"objective band {band.X},{band.Y}: " + (objective is null ? "no words"
                    : $"\"{objective.Text}\", count {(objective.Of is null ? "none" : $"{objective.Done}/{objective.Of} ({votes} votes)")}"));
            }
        }
        foreach (var entry in page.Rows.Where(r => r.TimeLeft is null))
            await VoteCountdownAsync(picture, entry, note, ct);
        return page;
    }

    /// <summary>
    /// A row's countdown from its band, by vote. A weekly's "6 day(s) 23:07:57" reads poorly whole: in a trader's list
    /// (measured 2026-10-25) 13 of the 14 ways read "6 day(s)" and dropped the digits after it, one read "S day(s)
    /// 2307:57" (five days), while the time alone, in a band of its own, read in 12 of 14. So when most ways read days
    /// and no value with those days won, the time of day is voted on in its own band, right of "day(s)" (where a plain
    /// look at the band finds it), and the days most ways read go before it; a value whose days most ways contradict is
    /// not taken.
    /// </summary>
    private async Task VoteCountdownAsync(SKBitmap picture, TaskRow row, Action<string>? note, CancellationToken ct)
    {
        var (text, votes, texts) = await VoteAsync(picture, row.TimeBand, t => TaskTable.TimeLeft(t) is { } v ? TaskTable.Hms(v) : null, "time", note, ct);
        var days = texts.Select(TaskTable.DaysOf).OfType<int>().GroupBy(d => d).OrderByDescending(g => g.Count()).FirstOrDefault();
        if (days is not null && days.Count() * 2 >= Ways.Count && (text is null || TaskTable.DaysOf(text) != days.Key))
        {
            (text, votes) = (null, 0);
            var band = row.TimeBand;
            if ((await ReadBandWordsAsync(picture, band, ct)).LastOrDefault(w => TaskTable.Fold(w.Text).StartsWith("day", StringComparison.Ordinal)) is { } day)
            {
                var left = (int)Math.Ceiling(day.Right) + 2;
                var clock = new TaskBand(left, band.Y, band.X + band.W - left, band.H);
                var (time, timeVotes, _) = await VoteAsync(picture, clock, t => TaskTable.TimeLeft(t) is { TotalHours: < 24 } v ? TaskTable.Hms(v) : null,
                    $"time of day after {days.Key} day(s)", note, ct);
                if (time is not null)
                    (text, votes) = ($"{days.Key} day(s) {time}", timeVotes);
            }
        }
        if (text is not null)
            TaskTable.TakeTime(row, text);
        row.TimeVotes = votes;
    }

    /// <summary>The words of one band, read as it is at its own size (the header's location and status, white and
    /// orange on the header's bar, in the picture's pixels).</summary>
    internal async Task<List<TaskWord>> ReadBandWordsAsync(SKBitmap picture, TaskBand band, CancellationToken ct)
    {
        var rect = SKRectI.Intersect(SKRectI.Create(band.X, band.Y, band.W, band.H), SKRectI.Create(0, 0, picture.Width, picture.Height));
        if (rect.Width < 4 || rect.Height < 4)
            return [];
        using var part = StashPicture.Block(picture, rect, rect.Width, rect.Height);
        using var framed = StashPicture.Framed(part, Margin, part.GetPixel(1, 1));
        using var soft = StashPicture.ToSoftwareBitmap(framed);
        var result = await _engine.RecognizeAsync(soft).AsTask(ct);
        return result.Lines.SelectMany(l => l.Words).Select(w => new TaskWord(w.Text, w.BoundingRect.X - Margin + rect.Left,
            w.BoundingRect.Y - Margin + rect.Top, w.BoundingRect.Width, w.BoundingRect.Height)).ToList();
    }

    /// <summary>The words in the picture's left part, read with the part enlarged by <paramref name="zoom"/>, in the
    /// picture's pixels.</summary>
    internal async Task<List<TaskWord>> ReadWordsAsync(SKBitmap picture, double zoom, CancellationToken ct)
    {
        var part = SKRectI.Create(0, 0, (int)Math.Round(picture.Width * TaskTable.LeftPart), picture.Height);
        using var big = StashPicture.Block(picture, part, (int)Math.Round(part.Width * zoom), (int)Math.Round(part.Height * zoom));
        using var soft = StashPicture.ToSoftwareBitmap(big);
        var result = await _engine.RecognizeAsync(soft).AsTask(ct);
        var words = new List<TaskWord>();
        var line = 0;
        foreach (var l in result.Lines)
        {
            foreach (var w in l.Words)
            {
                var r = w.BoundingRect;
                words.Add(new TaskWord(w.Text, r.X / zoom, r.Y / zoom, r.Width / zoom, r.Height / zoom, line));
            }
            line++;
        }
        return words;
    }

    // A band read every way; the value most ways read wins (a tie: the earlier way's), with how many read it, and every
    // way's text. A text that doesn't parse is no vote.
    private async Task<(string? Text, int Votes, List<string> Texts)> VoteAsync(SKBitmap picture, TaskBand band, Func<string, string?> parse, string what,
        Action<string>? note, CancellationToken ct)
    {
        var votes = new List<(string Value, string Text)>();
        var texts = new List<string>(Ways.Count);
        foreach (var way in Ways)
        {
            var text = await ReadBandAsync(picture, band, way, ct);
            texts.Add(text);
            if (parse(text) is { } value)
                votes.Add((value, text));
            if (Raw)
                note?.Invoke($"  {way.Mode}@{way.Zoom.ToString(CultureInfo.InvariantCulture)}: \"{text}\"");
        }
        var tally = votes.GroupBy(v => v.Value).OrderByDescending(g => g.Count()).ThenBy(g => votes.FindIndex(v => v.Value == g.Key)).ToList();
        note?.Invoke($"{what} band {band.X},{band.Y}: " + (tally.Count == 0 ? "nothing read" : string.Join(", ", tally.Select(g => $"{g.Key} ×{g.Count()}"))) +
                     $" of {Ways.Count} ways");
        return tally.Count == 0 ? (null, 0, texts) : (tally[0].First().Text, tally[0].Count(), texts);
    }

    // The band is set in a margin before reading: the recognition reads letters at an edge badly (StashReader does
    // the same).
    private const int Margin = 24;

    /// <summary>One band of the picture read as one line of text, the way given.</summary>
    internal async Task<string> ReadBandAsync(SKBitmap picture, TaskBand band, (string Mode, double Zoom) way, CancellationToken ct)
    {
        var rect = SKRectI.Intersect(SKRectI.Create(band.X, band.Y, band.W, band.H), SKRectI.Create(0, 0, picture.Width, picture.Height));
        if (rect.Width < 4 || rect.Height < 4)
            return "";
        using var big = StashPicture.Block(picture, rect, (int)Math.Round(rect.Width * way.Zoom), (int)Math.Round(rect.Height * way.Zoom));
        var light = way.Mode is "inverted" or "binary";
        if (way.Mode != "plain")
            Grey(big, invert: light, binary: way.Mode == "binary");
        var marginColour = light ? SKColors.White : way.Mode == "plain" ? big.GetPixel(1, 1) : SKColors.Black;
        using var framed = StashPicture.Framed(big, Margin, marginColour);
        if (Dump is not null)
        {
            using var image = SKImage.FromBitmap(framed);
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            await File.WriteAllBytesAsync(Path.Combine(Dump, $"band-{band.X}-{band.Y}-{way.Mode}-{way.Zoom.ToString(CultureInfo.InvariantCulture)}.png"), png.ToArray(), ct);
        }
        using var soft = StashPicture.ToSoftwareBitmap(framed);
        var result = await _engine.RecognizeAsync(soft).AsTask(ct);
        return string.Join(" ", result.Lines.Select(l => l.Text)).Trim();
    }

    // Each pixel's brightest channel, stretched from the band's darkest to its brightest, as grey; cut at the middle
    // when binary.
    private static void Grey(SKBitmap bitmap, bool invert, bool binary)
    {
        var pixels = bitmap.GetPixelSpan().ToArray();
        byte lo = 255, hi = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var v = Math.Max(pixels[i], Math.Max(pixels[i + 1], pixels[i + 2]));
            lo = Math.Min(lo, v);
            hi = Math.Max(hi, v);
        }
        var range = Math.Max(1, hi - lo);
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var v = Math.Max(pixels[i], Math.Max(pixels[i + 1], pixels[i + 2]));
            var g = (byte)Math.Clamp((v - lo) * 255 / range, 0, 255);
            if (binary)
                g = g >= 128 ? (byte)255 : (byte)0;
            if (invert)
                g = (byte)(255 - g);
            pixels[i] = pixels[i + 1] = pixels[i + 2] = g;
            pixels[i + 3] = 255;
        }
        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
    }
}
