using Shturmap.Game.Screenshots;
using SkiaSharp;

namespace Shturmap.Game.Tests;

// PaddleOCR's recognition, the second reader of a band Windows' recognition left unread (owner, 2026-10-26, decision 1;
// docs/DESIGN.md §2): a model that can't be loaded reads nothing and says so once; the model the build puts beside the
// tests (ocr\, as beside the app's exe) reads a label drawn as the game draws it. Where a reader asks it is the readers'
// tests' (DeskReaderTests, TaskTableTests), the owner's screenshots' in DeskRealPictureTests and TaskTableTests.
public sealed class PaddleReaderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("shturmap-paddle-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // A label's band as the game draws it at 1440p (StashReaderTests.Page.Item) and the desk cuts it (DeskReader.LabelRect:
    // the band ends three pixels past the label's ink): near-white bold letters with a dark outline on a cell's dark field,
    // set in the narrow margin the desk gives PaddleOCR.
    private static SKBitmap Label(string text)
    {
        using var face = SKTypeface.FromFamilyName("Bahnschrift", SKFontStyle.Bold) ?? SKTypeface.Default;
        using var font = new SKFont(face, 13.5f);
        using var band = new SKBitmap((int)Math.Ceiling(font.MeasureText(text)) + 10, 17);
        using (var canvas = new SKCanvas(band))
        using (var outline = new SKPaint { Color = new SKColor(10, 12, 14), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.2f })
        using (var ink = new SKPaint { Color = new SKColor(226, 229, 231), IsAntialias = true })
        {
            canvas.Clear(new SKColor(26, 29, 32));
            canvas.DrawText(text, 6, 13, SKTextAlign.Left, font, outline);
            canvas.DrawText(text, 6, 13, SKTextAlign.Left, font, ink);
        }
        var framed = new SKBitmap(band.Width + 8, band.Height + 8);
        using (var canvas = new SKCanvas(framed))
        {
            canvas.Clear(new SKColor(24, 29, 33));
            canvas.DrawBitmap(band, 4, 4);
        }
        return framed;
    }

    [Fact]
    public async Task A_model_that_cant_be_loaded_reads_nothing_and_says_so_once()
    {
        var failed = new List<string>();
        var loaded = 0;
        using var reader = new PaddleReader(Path.Combine(_root, "none.onnx"), Path.Combine(_root, "none.txt"), loaded: _ => loaded++, failed: failed.Add);
        using var line = Label("Marl 214");
        Assert.Null(await reader.ReadAsync(line, Ct));
        Assert.Null(await reader.ReadAsync(line, Ct));
        Assert.Single(failed);
        Assert.Equal(0, loaded);
    }

    [Fact]
    public async Task A_reading_asked_to_stop_stops()
    {
        using var reader = new PaddleReader(Path.Combine(_root, "none.onnx"), Path.Combine(_root, "none.txt"));
        using var line = Label("Marl 214");
        using var stop = new CancellationTokenSource();
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(line, stop.Token));
    }

    [Fact]
    public async Task The_bundled_model_reads_a_label_and_loads_once()
    {
        if (!PaddleReader.IsBundled)
            Assert.Skip(@"No PaddleOCR model beside the tests: the build puts it in ocr\ (eng\paddle-model.ps1).");
        var loaded = 0;
        using var reader = PaddleReader.Bundled(loaded: _ => loaded++);
        using var marl = Label("Marl 214");
        using var hollin = Label("Hollin");
        // As the labels' match folds them (a stray dot or space is no letter).
        Assert.Equal(Core.Screenshots.ExitList.Fold("Marl 214"), Core.Screenshots.ExitList.Fold(await reader.ReadAsync(marl, Ct) ?? ""));
        Assert.Equal(Core.Screenshots.ExitList.Fold("Hollin"), Core.Screenshots.ExitList.Fold(await reader.ReadAsync(hollin, Ct) ?? ""));
        Assert.Equal(1, loaded);
    }
}
