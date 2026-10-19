using System.Diagnostics;
using System.Runtime.CompilerServices;
using Shturmap.Core;
using SkiaSharp;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace Shturmap.Game.Screenshots;

/// <summary>An item the reading may name: its id, the short name the game writes on the cell (in the game's language),
/// and tarkov.dev's grid image for it when the cache has one (PNG or WebP bytes), else null.</summary>
public sealed record StashCandidate(string ItemId, string ShortName, byte[]? GridImage);

/// <summary>One item seen in a grid: its id, the count seen (a stack's number when readable, else 1), and whether the
/// found-in-raid mark was seen (null when the reading doesn't read it).</summary>
public sealed record StashItemSeen(string ItemId, int Count, bool? FoundInRaid);

public enum StashGridKind
{
    Stash,
    Window,
}

public sealed record StashGridReading(StashGridKind Kind, IReadOnlyList<StashItemSeen> Items);

/// <summary>What one menu screenshot showed. IsTradingScreen: the trading screen, whose stash cells show prices instead
/// of names; then Grids is empty. The messages screen's grid of a message's attachments (at the left; not the player's
/// until taken) is never among the grids: only the stash beside it is.</summary>
public sealed record StashReading(bool IsTradingScreen, IReadOnlyList<StashGridReading> Grids);

/// <summary>
/// Reads the items in the stash and in open container windows from a screenshot the player took of the game's gear
/// screen (docs/NEXT.md, "what you have"; the spike of 2026-10-11). The item grids are found by their lines
/// (<see cref="StashGrids"/>); each cell's short name, which the game writes at its top right, is read with the text
/// recognition built into Windows (Windows.Media.Ocr; on this PC, nothing is sent) and matched to the candidates'
/// names (<see cref="StashLabels"/>): the label is the identity, since lookalike keys share their art and differ by a
/// digit. An item is named by its label only; the candidate's grid image, when there is one, can only veto a label
/// whose cell shows clearly other art (<see cref="StashTemplate"/>), never name an item by itself. A stack's count is
/// read at the cell's bottom right. The file is opened for reading only, shared with the game that may still be
/// writing it, and nothing of the picture is kept or written anywhere.
/// </summary>
public sealed class StashReader
{
    // The least a picture can be for its labels to be readable: the cell is 42 px at 1280×720.
    private const int LeastWidth = 1280;
    private const int LeastHeight = 720;

    // The labels are read enlarged to a cell of about this many pixels, four times the cell at 1440p (the game's
    // letters are 10 px tall there, 40 enlarged), five times at 1080p, three at 4K.
    private const int ReadCell = 336;

    private static int ZoomFor(int pitch) => Math.Clamp((int)Math.Round(ReadCell / (double)pitch), 2, 8);

    // The margin set around a band before reading it: the recognition reads letters at an edge badly.
    private const int Margin = 24;
    private static readonly SKColor MarginColour = new(24, 29, 33);

    // Below this agreement between the cell's art and the named candidate's grid image, the label is contradicted
    // and nothing is said for the cell. Measured on the owner's three truths (2026-10-11, 150 cells): items read right
    // agree at 0.45 to 0.99, except one whose image is stale (the game draws the key otherwise now) at −0.15; another
    // item's image of the same size agrees at 0.25 in the median and, for lookalike keys, up to 0.98. So the art can
    // only catch a label read as an item of clearly other art, and it must not take right readings with it.
    internal const float VetoBelow = 0.3f;

    // When a label fits two items of one name alike (variants of a magazine, two bandages), the art may tell them
    // apart: the better agreement must be this much ahead of the next.
    internal const float TieMargin = 0.1f;

    /// <summary>A candidate's grid image as the reading uses it: its size in cells from the header, and the template
    /// decoded the first time a label names the item. Kept per image bytes, so a session that passes the same
    /// candidates for every screenshot decodes each once.</summary>
    private sealed class Art(byte[] image)
    {
        public (int W, int H)? Size { get; } = StashTemplate.Size(image);
        private StashTemplate? _template;
        private bool _loaded;

        public StashTemplate? Template
        {
            get
            {
                if (!_loaded)
                {
                    _template = Size is null ? null : StashTemplate.Load(image);
                    _loaded = true;
                }
                return _template;
            }
        }
    }

    private static readonly ConditionalWeakTable<byte[], Art> Arts = new();

    private sealed record Entry(StashCandidate Candidate, Art? Art)
    {
        public bool Fits(StashRect rect) => Art?.Size is not { } size || (size.W == rect.W && size.H == rect.H);
    }

    private readonly OcrEngine _engine;

    private StashReader(OcrEngine engine) => _engine = engine;

    /// <summary>The recognition the labels are read with, for the stash desk's reading of the same picture (its words).</summary>
    internal OcrEngine Engine => _engine;

    /// <summary>The language the labels are read in ("en-US").</summary>
    public string LanguageTag => _engine.RecognizerLanguage.LanguageTag;

    /// <summary>
    /// A reader for the game's language if Windows has that language's text recognition, else English (which reads
    /// every Latin-script name), else whatever the user's Windows languages offer. Null if Windows has none at all.
    /// </summary>
    /// <param name="gameLanguage">The game's own code for its language ("en", "ge", "ru"), or null.</param>
    public static StashReader? Create(string? gameLanguage)
    {
        try
        {
            var available = OcrEngine.AvailableRecognizerLanguages.ToList();
            var tag = GameLanguage.Common(gameLanguage);
            var wanted = available.FirstOrDefault(l => tag is not null && l.LanguageTag.StartsWith(tag, StringComparison.OrdinalIgnoreCase))
                         ?? available.FirstOrDefault(l => l.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase));
            var engine = wanted is not null ? OcrEngine.TryCreateFromLanguage(new Language(wanted.LanguageTag)) : OcrEngine.TryCreateFromUserProfileLanguages();
            return engine is null ? null : new StashReader(engine);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // A Windows without the recognition's parts: the same as no language for it.
            return null;
        }
    }

    /// <summary>
    /// What the screenshot shows: null when the file isn't a whole picture after the tries, or is gone; a reading
    /// with no grids when the picture shows none. The game creates the file and then writes it, so a picture that
    /// can't be decoded yet is tried again.
    /// </summary>
    /// <param name="candidates">The items the reading may name.</param>
    /// <param name="tries">How often to try a file that isn't a whole picture yet.</param>
    /// <param name="wait">How long to wait between tries.</param>
    public async Task<StashReading?> ReadAsync(string path, IReadOnlyList<StashCandidate> candidates, int tries = 8, TimeSpan? wait = null, CancellationToken ct = default)
    {
        var look = await LookAsync(path, candidates, tries, wait, ct);
        if (look is null)
            return null;
        var grids = look.Grids.Select(g => new StashGridReading(
            g.Grid.Place == StashGridPlace.Stash ? StashGridKind.Stash : StashGridKind.Window,
            g.Cells.Where(c => c.ItemId is not null).Select(c => new StashItemSeen(c.ItemId!, c.Count, c.FoundInRaid)).ToList())).ToList();
        return new StashReading(look.IsTradingScreen, look.IsTradingScreen ? [] : grids);
    }

    /// <summary>What a decoded picture shows (<see cref="MenuScreenshotReader"/> opens the file once for the menus'
    /// readings): a reading with no grids when the picture shows none.</summary>
    public async Task<StashReading> ReadAsync(SKBitmap picture, IReadOnlyList<StashCandidate> candidates, CancellationToken ct = default)
    {
        var look = await LookAsync(picture, candidates, ct);
        var grids = look.Grids.Select(g => new StashGridReading(
            g.Grid.Place == StashGridPlace.Stash ? StashGridKind.Stash : StashGridKind.Window,
            g.Cells.Where(c => c.ItemId is not null).Select(c => new StashItemSeen(c.ItemId!, c.Count, c.FoundInRaid)).ToList())).ToList();
        return new StashReading(look.IsTradingScreen, look.IsTradingScreen ? [] : grids);
    }

    /// <summary>The reading with everything it saw on the way (where the grids and cells stand, what each label
    /// read, the art's agreement), for measuring it against a hand-written truth (shturmap-cli stash).</summary>
    internal async Task<StashLook?> LookAsync(string path, IReadOnlyList<StashCandidate> candidates, int tries, TimeSpan? wait, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (await LookOnceAsync(path, candidates, ct) is { } look)
                    return look;
            }
            catch (FileNotFoundException)
            {
                return null; // deleted in the meantime
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
            catch (Exception e) when (e is not OperationCanceledException and not OutOfMemoryException)
            {
                // Not a whole picture yet (the decoder says so in several ways), or still held by the game.
            }
            if (attempt >= tries)
                return null;
            await Task.Delay(wait ?? TimeSpan.FromMilliseconds(400), ct);
        }
    }

    // One look: null when the file isn't a picture yet (empty, or written only so far).
    private async Task<StashLook?> LookOnceAsync(string path, IReadOnlyList<StashCandidate> candidates, CancellationToken ct)
    {
        SKBitmap? bitmap;
        await using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            if (!ExitListReader.IsWhole(file))
                return null;
            bitmap = StashPicture.Decode(file);
        }
        if (bitmap is null)
            throw new InvalidDataException("Not a picture");
        using (bitmap)
            return await LookAsync(bitmap, candidates, ct);
    }

    /// <summary>The reading of a decoded picture.</summary>
    internal async Task<StashLook> LookAsync(SKBitmap bitmap, IReadOnlyList<StashCandidate> candidates, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        int w = bitmap.Width, h = bitmap.Height;
        var scale = Math.Min(w / 1920.0, h / 1080.0);
        var pitch = (int)Math.Round(StashTemplate.BaseCell * scale);
        if (w < LeastWidth || h < LeastHeight)
            return new StashLook(w, h, pitch, false, [], sw.ElapsedMilliseconds, sw.ElapsedMilliseconds);
        var gray = StashGray.Of(bitmap);
        var lines = new StashLines(gray);
        var grids = StashGrids.Find(lines, gray, w, h, pitch);
        var gridMs = sw.ElapsedMilliseconds;
        if (grids.Any(g => g.Place == StashGridPlace.Left))
            return new StashLook(w, h, pitch, true, [], gridMs, sw.ElapsedMilliseconds);
        var highPassed = gray.HighPass((int)Math.Round(4 * scale));

        var entries = candidates.Select(c => new Entry(c, c.GridImage is null ? null : Arts.GetValue(c.GridImage, image => new Art(image)))).ToList();
        var looks = new List<StashGridLook>();
        // A message's attachments (the messages screen's grid at the left) aren't the player's until taken: not read.
        foreach (var grid in grids.Where(g => g.Place != StashGridPlace.Message))
        {
            var hidden = StashGrids.Hidden(grid, grids, pitch);
            var empty = StashGrids.Empty(grid, highPassed, pitch);
            var cells = new List<StashCellLook>();
            foreach (var rect in StashGrids.Items(grid, lines, hidden, empty, pitch))
            {
                ct.ThrowIfCancellationRequested();
                var pixels = SKRectI.Create(grid.X0 + rect.I0 * pitch, grid.Y0 + rect.J0 * pitch, rect.W * pitch, rect.H * pitch);
                if (rect.Odd)
                {
                    cells.Add(new StashCellLook(rect, pixels, null, null, 0, null, false, null, 1, null));
                    continue;
                }
                cells.Add(await LookAtCellAsync(bitmap, rect, pixels, pitch, entries, ct));
            }
            looks.Add(new StashGridLook(grid, hidden, empty, cells));
        }
        return new StashLook(w, h, pitch, false, looks, gridMs, sw.ElapsedMilliseconds);
    }

    private async Task<StashCellLook> LookAtCellAsync(SKBitmap bitmap, StashRect rect, SKRectI pixels, int pitch, List<Entry> candidates, CancellationToken ct)
    {
        // The candidates this rectangle can be: a grid image of another size rules its item out.
        var fitting = candidates.Where(c => c.Fits(rect)).ToList();
        string? label = null;
        double fit = 0;
        var best = new List<Entry>();
        // The label is read in several ways until one fits a name: the recognition reads a short label or nothing
        // by a pixel's difference in where the band starts, or by the zoom. Most labels fit at the first reading.
        // A label the first readings read well as clearly another name (half its letters off every candidate) is
        // another item's, and the other ways aren't tried: a window of a hundred such items would take seconds.
        for (var step = 0; step < LabelReadings.Length; step++)
        {
            if (step == FirstReadings && label is not null && fit < AnotherName)
                break;
            var reading = await ReadLabelAsync(bitmap, pixels, pitch, LabelReadings[step], ct);
            if (reading.Length == 0)
                continue;
            if (fitting.Count == 0)
            {
                label ??= reading;
                break;
            }
            var fits = fitting.Select(c => (c, Fit: StashLabels.Fit(reading, c.Candidate.ShortName))).ToList();
            var top = fits.Max(f => f.Fit);
            if (label is null || top > fit)
            {
                label = reading;
                fit = top;
                best = fits.Where(f => f.Fit == top).Select(f => f.c).ToList();
            }
            if (fit >= StashLabels.Enough)
                break;
        }
        string? itemId = null;
        float? agreement = null;
        var vetoed = false;
        if (fit >= StashLabels.Enough)
        {
            var ids = best.Select(b => b.Candidate.ItemId).Distinct(StringComparer.Ordinal).ToList();
            if (ids.Count == 1)
            {
                var entry = best[0];
                if (entry.Art?.Template is { } template)
                {
                    agreement = template.Agreement(bitmap, pixels);
                    vetoed = agreement < VetoBelow;
                }
                itemId = vetoed ? null : entry.Candidate.ItemId;
            }
            else if (best.All(b => b.Art?.Template is not null))
            {
                // The label fits two items of one name alike: their art may tell them apart, when it is clearly one's.
                var block = StashTemplate.Block(bitmap, pixels, rect.W, rect.H);
                var scored = best.GroupBy(b => b.Candidate.ItemId, StringComparer.Ordinal)
                    .Select(g => (Entry: g.First(), Agreement: g.Max(b => b.Art!.Template!.Agreement(block))))
                    .OrderByDescending(s => s.Agreement).ToList();
                agreement = scored[0].Agreement;
                if (agreement >= VetoBelow && agreement - scored[1].Agreement >= TieMargin)
                    itemId = scored[0].Entry.Candidate.ItemId;
            }
        }
        string? countText = null;
        var count = 1;
        bool? foundInRaid = null;
        if (itemId is not null)
        {
            countText = await ReadCountAsync(bitmap, pixels, pitch, ct);
            count = StashLabels.Count(countText);
            foundInRaid = StashMarks.Seen(bitmap, pixels, pitch);
        }
        return new StashCellLook(rect, pixels, label, itemId, fit, agreement, vetoed, countText, count, foundInRaid);
    }

    /// <summary>
    /// A cell's label read in the ways below until one fits a name well enough (the stash desk's reading, against the
    /// whole catalogue at its own fit): the best reading, how well it fit and what it fit, by <paramref name="match"/>.
    /// The first readings that read a label clearly another item's (half its letters off every name) end it, as above.
    /// </summary>
    /// <param name="confirm">A label that fits but must be read once more (a short one): a later reading that fits as
    /// well and is longer wins, and the reading ends after it.</param>
    internal async Task<(string? Label, double Fit, T? Best)> ReadLabelAsync<T>(SKBitmap bitmap, SKRectI pixels, int pitch,
        Func<string, (double Fit, T Best)> match, double enough, CancellationToken ct, Func<string, bool>? confirm = null)
    {
        string? label = null;
        double fit = 0;
        T? best = default;
        var confirming = false;
        for (var step = 0; step < LabelReadings.Length; step++)
        {
            if (step == FirstReadings && label is not null && fit < AnotherName)
                break;
            var reading = await ReadLabelAsync(bitmap, pixels, pitch, LabelReadings[step], ct);
            if (reading.Length == 0)
                continue;
            var (top, what) = match(reading);
            if (confirming)
            {
                if (top >= enough && reading.Length > label!.Length)
                    (label, fit, best) = (reading, top, what);
                break;
            }
            if (label is null || top > fit)
            {
                label = reading;
                fit = top;
                best = what;
            }
            if (fit >= enough)
            {
                if (confirm?.Invoke(label) != true)
                    break;
                confirming = true;
            }
        }
        return (label, fit, best);
    }

    // The ways a label is read, in order, until one fits a name: the cell's upper part below the line, enlarged,
    // with the words of the label's band kept, and the band alone, set in a dark margin; then both again enlarged
    // without smoothing (and the part larger still), then softly, then from the line's own row a step smaller. The
    // recognition reads some three-letter labels in one way only (one only with the blocky enlargement, another only
    // from the line's row), and which one isn't to be told beforehand. Most labels fit at the first reading.
    private static readonly (bool Band, int Gap, int ZoomStep, SKSamplingOptions Sampling)[] LabelReadings =
    [
        (false, LineGap, 0, StashPicture.Sampling),
        (true, LineGap, 0, StashPicture.Sampling),
        (false, LineGap, 0, StashPicture.BlockySampling),
        (true, LineGap, 0, StashPicture.BlockySampling),
        (false, LineGap, 2, StashPicture.BlockySampling),
        (false, LineGap, 0, StashPicture.SoftSampling),
        (true, LineGap, 0, StashPicture.SoftSampling),
        (false, 1, -1, StashPicture.Sampling),
        (true, 1, -1, StashPicture.Sampling),
    ];

    // After this many readings, a label that fits no candidate better than this is left as another item's.
    private const int FirstReadings = 2;
    private const double AnotherName = 0.5;

    private Task<string> ReadLabelAsync(SKBitmap bitmap, SKRectI rect, int pitch, (bool Band, int Gap, int ZoomStep, SKSamplingOptions Sampling) way, CancellationToken ct)
    {
        var zoom = Math.Max(2, ZoomFor(pitch) + way.ZoomStep);
        return way.Band ? ReadLabelBandAsync(bitmap, rect, pitch, way.Gap, zoom, way.Sampling, ct) : ReadLabelPartAsync(bitmap, rect, pitch, way.Gap, zoom, way.Sampling, ct);
    }

    // The label at the top of the item, read from the item's upper part enlarged; only the words that stand in the
    // label's band count (the art below gives words of its own). Cut below the cell's line (the grid stands on the
    // line's middle): with the line's edge in the picture, the recognition read a label or nothing by a pixel's
    // difference.
    private async Task<string> ReadLabelPartAsync(SKBitmap bitmap, SKRectI rect, int pitch, int gap, int zoom, SKSamplingOptions sampling, CancellationToken ct)
    {
        var part = SKRectI.Create(rect.Left + 1, rect.Top + gap, rect.Width - 2, (int)Math.Round(pitch * 0.42));
        using var big = StashPicture.Block(bitmap, part, part.Width * zoom, part.Height * zoom, sampling);
        using var soft = StashPicture.ToSoftwareBitmap(big);
        var result = await _engine.RecognizeAsync(soft).AsTask(ct);
        // The label's top stands within the first sixth of the cell; words that start lower are the art's.
        var words = result.Lines.SelectMany(l => l.Words)
            .Where(w => w.BoundingRect.Y <= pitch * 0.14 * zoom)
            .OrderBy(w => w.BoundingRect.X).Select(w => w.Text);
        return string.Join(" ", words).Trim();
    }

    // The label's band alone, cut above the art and set in a dark margin: it reads the digits that touch a key's bow,
    // and some labels the whole part doesn't.
    private Task<string> ReadLabelBandAsync(SKBitmap bitmap, SKRectI rect, int pitch, int gap, int zoom, SKSamplingOptions sampling, CancellationToken ct) =>
        ReadBandAsync(bitmap, SKRectI.Create(rect.Left + 2, rect.Top + gap, rect.Width - 4, (int)Math.Round(pitch * 0.2)), zoom, sampling, ct);

    // How far below a cell's top the label's band starts: past the line.
    private const int LineGap = 3;

    /// <summary>The number at the item's bottom right as read: a stack's count, or a magazine's load. The band is cut
    /// to the number's own height (at 1440p the digits stand 18 to 4 px over the bottom): the found-in-raid mark
    /// right above it read as a digit before it.</summary>
    /// <param name="cells">How wide the band is, in cells (the stash desk reads a long condition once more, wider).</param>
    internal async Task<string?> ReadCountAsync(SKBitmap bitmap, SKRectI rect, int pitch, CancellationToken ct, double cells = 0.9, bool lit = false)
    {
        // The number ends a pixel before the cell's line: the band goes up to the line.
        var width = Math.Min(rect.Width - 3, (int)Math.Round(pitch * cells));
        var height = (int)Math.Round(pitch * 0.215);
        var band = SKRectI.Create(rect.Right - 1 - width, rect.Bottom - 2 - height, width, height);
        var text = await ReadBandAsync(bitmap, band, ZoomFor(pitch), StashPicture.Sampling, ct, lit);
        return text.Length > 0 ? text : null;
    }

    // A band of the picture, enlarged and set in a dark margin, as one line of text; lit, in grey by its brightest
    // channel (StashPicture.Lit).
    private async Task<string> ReadBandAsync(SKBitmap bitmap, SKRectI band, int zoom, SKSamplingOptions sampling, CancellationToken ct, bool lit = false)
    {
        using var block = StashPicture.Block(bitmap, band, band.Width * zoom, band.Height * zoom, sampling);
        using var litBlock = lit ? StashPicture.Lit(block) : null;
        using var framed = StashPicture.Framed(litBlock ?? block, Margin, MarginColour);
        using var soft = StashPicture.ToSoftwareBitmap(framed);
        var result = await _engine.RecognizeAsync(soft).AsTask(ct);
        return string.Join(" ", result.Lines.Select(l => l.Text)).Trim();
    }
}

/// <summary>One item rectangle as the reading saw it: the label read, the item it named (null for none), how well
/// the label fit, the art's agreement with the named item's grid image (null without one), the count read.</summary>
internal sealed record StashCellLook(StashRect Rect, SKRectI Pixels, string? Label, string? ItemId, double Fit, float? Agreement, bool Vetoed,
    string? CountText, int Count, bool? FoundInRaid);

internal sealed record StashGridLook(StashGrid Grid, bool[] Hidden, bool[] Empty, IReadOnlyList<StashCellLook> Cells);

/// <summary>Everything one look at a picture saw, with the time the grids took and the whole took.</summary>
internal sealed record StashLook(int Width, int Height, int Pitch, bool IsTradingScreen, IReadOnlyList<StashGridLook> Grids, long GridMs, long TotalMs);
