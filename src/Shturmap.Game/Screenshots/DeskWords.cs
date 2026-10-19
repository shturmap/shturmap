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
}
