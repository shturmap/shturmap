using SkiaSharp;
using Windows.Media.Ocr;

namespace Shturmap.Game.Screenshots;

/// <summary>A word the recognition read on the whole picture, with its box in the picture's pixels and the line it
/// stands in.</summary>
internal sealed record DeskWord(string Text, int X, int Y, int W, int H, int Line)
{
    public int Right => X + W;
    public int Bottom => Y + H;
    public int MidX => X + W / 2;
    public int MidY => Y + H / 2;
}

internal static class DeskWords
{
    /// <summary>The words of the whole picture at its own size, read once: the screens' headings (the GEAR tab's slot
    /// names, the middle column's heads, the stash pane's SEARCH and SORT TABLE), a container window's title, a toast.
    /// The cells' labels are too small for this pass and are read cell by cell, enlarged.</summary>
    public static async Task<List<DeskWord>> ReadAsync(OcrEngine engine, SKBitmap picture, CancellationToken ct)
    {
        using var soft = StashPicture.ToSoftwareBitmap(picture);
        var result = await engine.RecognizeAsync(soft).AsTask(ct);
        var words = new List<DeskWord>();
        var line = 0;
        foreach (var l in result.Lines)
        {
            foreach (var w in l.Words)
            {
                var r = w.BoundingRect;
                words.Add(new DeskWord(w.Text, (int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Width), (int)Math.Round(r.Height), line));
            }
            line++;
        }
        return words;
    }

    // The head's band, in parts of the picture's height and width: "LOOT" stands at about 0.65 to 0.68 of the width.
    private const double HeadTop = 0.04, HeadBottom = 0.08, HeadLeft = 0.6, HeadRight = 0.8;

    // The dark margin round the band, as round a cell's band (StashReader).
    private const int HeadMargin = 24;

    /// <summary>
    /// The LOOT pane's head read again where the whole picture's reading has no "LOOT" at its place
    /// (<see cref="DeskLoot.Head"/>): the head's letters are about 8 px high at 1920×1080, too small for the whole picture's
    /// reading (the owner's play session of 2026-10-28: at 1080p "LOOT" was never read and the pane never found). The band
    /// at the top of the picture's right part where the head stands is read as a cell's label is, enlarged to the labels'
    /// size and set in a dark margin; where it has the head, the words of the head's row there replace the whole reading's.
    /// Else the words stay as they were. The container's kind at the right edge is left to the whole reading: read so, the
    /// kind's capitals half under the raid timer's black band came out a word of no kind ("BODY" as "DUUM").
    /// </summary>
    public static async Task<List<DeskWord>> WithLootHeadAsync(OcrEngine engine, SKBitmap picture, List<DeskWord> words, int pitch, CancellationToken ct)
    {
        int w = picture.Width, h = picture.Height;
        var zoom = StashReader.ZoomFor(pitch);
        var band = new SKRectI((int)(w * HeadLeft), (int)(h * HeadTop), (int)(w * HeadRight), (int)(h * HeadBottom));
        if (band.Width <= 0 || band.Height <= 0)
            return words;
        using var big = StashPicture.Block(picture, band, band.Width * zoom, band.Height * zoom);
        using var framed = StashPicture.Framed(big, HeadMargin, HeadMarginColour);
        var line0 = words.Count > 0 ? words.Max(x => x.Line) + 1 : 0;
        var read = (await ReadAsync(engine, framed, ct)).Select(x => new DeskWord(x.Text, band.Left + (x.X - HeadMargin) / zoom,
            band.Top + (x.Y - HeadMargin) / zoom, x.W / zoom, x.H / zoom, line0 + x.Line)).ToList();
        if (DeskLoot.Head(read, w, h) is not { } head)
            return words;
        // The head's row: words of its height level with it (the container's first row of labels stands a few pixels lower).
        bool InRow(DeskWord x) => band.Contains(x.MidX, x.MidY) && Math.Abs(x.MidY - head.MidY) <= head.H;
        return words.Where(x => !InRow(x)).Concat(read.Where(InRow)).ToList();
    }

    private static readonly SKColor HeadMarginColour = new(24, 29, 33);
}
