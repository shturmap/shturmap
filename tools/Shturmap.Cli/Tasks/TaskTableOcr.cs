using System.Globalization;
using Shturmap.Game.Screenshots;
using SkiaSharp;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace Shturmap.Cli.Tasks;

/// <summary>
/// Reads a Tasks screenshot with Windows' own text recognition (Windows.Media.Ocr, on the PC, nothing sent) for
/// <see cref="TaskTable"/>: first the words of the picture's left part (<see cref="TaskTable.LeftPart"/>), where the
/// table stands (the right part carries the task items' grids, the player's name and money on a trader's screen, and
/// the game's toasts with the screenshot's path, none of which is ever read); then each row's countdown and progress
/// from their own bands. The recognition reads the table's names and objectives at the picture's own size, but drops
/// most of the game's small digits whatever is done to them (2026-10-19: enlarged, stretched, cut to black and white,
/// stacked with the page's other bands, a word set before them, the glyphs set apart), and drops each band in a
/// different way; so every band is read in all the <see cref="Ways"/> and the value most of them read is taken.
/// </summary>
internal sealed class TaskTableOcr
{
    private readonly OcrEngine _engine;

    private TaskTableOcr(OcrEngine engine) => _engine = engine;

    public string LanguageTag => _engine.RecognizerLanguage.LanguageTag;

    /// <summary>A reader for English (the spike reads the English game), null when this Windows has no English
    /// recognition.</summary>
    public static TaskTableOcr? Create()
    {
        try
        {
            var english = OcrEngine.AvailableRecognizerLanguages.FirstOrDefault(l => l.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase));
            var engine = english is null ? null : OcrEngine.TryCreateFromLanguage(new Language(english.LanguageTag));
            return engine is null ? null : new TaskTableOcr(engine);
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
    public static IReadOnlyList<(string Mode, double Zoom)> Ways { get; set; } =
    [
        ("plain", 1), ("binary", 1), ("inverted", 1), ("stretched", 1), ("plain", 2), ("binary", 2), ("inverted", 2),
        ("binary", 0.75), ("inverted", 0.75), ("binary", 1.5), ("inverted", 1.5), ("binary", 3), ("binary", 4), ("plain", 4),
    ];

    /// <summary>Ways as the command line writes them: "plain@1,binary@2".</summary>
    public static IReadOnlyList<(string Mode, double Zoom)> ParseWays(string text) =>
        text.Split(',').Select(w => w.Split('@')).Select(p => (p[0], double.Parse(p[1], CultureInfo.InvariantCulture))).ToList();

    /// <summary>A folder to write each band into as it is read, to see what the recognition was given
    /// (--dump).</summary>
    public static string? Dump { get; set; }

    /// <summary>The table read from a picture: the page's words, the rows from them, then each row's countdown and
    /// progress from their bands, by vote. <paramref name="note"/> hears each band's votes.</summary>
    public async Task<TaskPage> ReadPageAsync(SKBitmap picture, double zoom = 1, Action<string>? note = null, CancellationToken ct = default)
    {
        var words = await ReadAsync(picture, zoom, ct);
        var page = TaskTable.Read(words, picture.Width, picture.Height);
        foreach (var row in page.Rows)
        {
            if (row.TimeLeft is null)
            {
                var (text, votes) = await VoteAsync(picture, row.TimeBand, t => TaskTable.TimeLeft(t) is { } v ? TaskTable.Hms(v) : null, "time", note, ct);
                if (text is not null)
                    TaskTable.TakeTime(row, text);
                row.TimeVotes = votes;
            }
            if (row.Progress is null)
            {
                var (text, votes) = await VoteAsync(picture, row.ProgressBand, t => TaskTable.Percent(t)?.ToString(CultureInfo.InvariantCulture), "progress", note, ct);
                if (text is not null)
                    TaskTable.TakeProgress(row, text);
                row.ProgressVotes = votes;
            }
        }
        return page with { Screen = TaskTable.Settle(page) };
    }

    /// <summary>The words in the picture's left part, read with the part enlarged by <paramref name="zoom"/>, in the
    /// picture's pixels.</summary>
    public async Task<List<TaskWord>> ReadAsync(SKBitmap picture, double zoom, CancellationToken ct)
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

    // A band read every way; the value most ways read wins (a tie: the earlier way's), with how many read it. A text
    // that doesn't parse is no vote.
    private async Task<(string? Text, int Votes)> VoteAsync(SKBitmap picture, TaskBand band, Func<string, string?> parse, string what, Action<string>? note, CancellationToken ct)
    {
        var votes = new List<(string Value, string Text)>();
        foreach (var way in Ways)
        {
            var text = await ReadBandAsync(picture, band, way, ct);
            if (parse(text) is { } value)
                votes.Add((value, text));
        }
        var tally = votes.GroupBy(v => v.Value).OrderByDescending(g => g.Count()).ThenBy(g => votes.FindIndex(v => v.Value == g.Key)).ToList();
        note?.Invoke($"{what} band {band.X},{band.Y}: " + (tally.Count == 0 ? "nothing read" : string.Join(", ", tally.Select(g => $"{g.Key} ×{g.Count()}"))) +
                     $" of {Ways.Count} ways");
        return tally.Count == 0 ? (null, 0) : (tally[0].First().Text, tally[0].Count());
    }

    // The band is set in a margin before reading: the recognition reads letters at an edge badly (StashReader does
    // the same).
    private const int Margin = 24;

    /// <summary>One band of the picture read as one line of text, the way given.</summary>
    public async Task<string> ReadBandAsync(SKBitmap picture, TaskBand band, (string Mode, double Zoom) way, CancellationToken ct)
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
