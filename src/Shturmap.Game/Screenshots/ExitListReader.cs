using System.Runtime.InteropServices.WindowsRuntime;
using Shturmap.Core;
using Shturmap.Core.Screenshots;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Shturmap.Game.Screenshots;

/// <summary>
/// Reads the game's extract list from a screenshot the player took, when the picture shows it (owner, 2026-10-05).
/// This is the one place where Shturmap opens a screenshot's picture, and it looks at one corner of it: the top
/// right, where the game shows the list under a green bar. No green bar there, no reading: the picture is let go
/// after its corner was decoded. With the bar, the words in that corner are read with the text recognition built
/// into Windows (Windows.Media.Ocr; on this PC, nothing is sent), and <see cref="ExitList"/> makes rows of them. The
/// file is opened for reading only, shared with the game that may still be writing it, and nothing of the picture is
/// kept or written anywhere. docs/DESIGN.md §2.
/// </summary>
public sealed class ExitListReader
{
    // The corner looked at, in screen heights: the list is laid out by the screen's height and hangs on its right
    // edge (at 2560×1440 it is 768 px wide and, with nine rows, some 700 px tall).
    private const double CornerWidth = 0.60;
    private const double CornerHeight = 0.62;

    // The height the words are read at: the list's letters are about 24 px tall on a 1440 px screen, which the
    // recognition reads well; a smaller screen's corner is enlarged to that, a larger one's reduced.
    private const double ReadHeight = 1440;

    private readonly OcrEngine _engine;

    private ExitListReader(OcrEngine engine) => _engine = engine;

    /// <summary>The language the words are read in ("en-US").</summary>
    public string LanguageTag => _engine.RecognizerLanguage.LanguageTag;

    /// <summary>
    /// A reader for the game's language if Windows has that language's text recognition, else English (which reads
    /// every Latin-script name), else whatever the user's Windows languages offer. Null if Windows has none at all:
    /// then no list is read.
    /// </summary>
    /// <param name="gameLanguage">The game's own code for its language ("en", "ge", "ru"), or null.</param>
    public static ExitListReader? Create(string? gameLanguage)
    {
        try
        {
            var available = OcrEngine.AvailableRecognizerLanguages.ToList();
            var tag = WindowsLanguage(gameLanguage);
            var wanted = available.FirstOrDefault(l => tag is not null && l.LanguageTag.StartsWith(tag, StringComparison.OrdinalIgnoreCase))
                         ?? available.FirstOrDefault(l => l.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase));
            var engine = wanted is not null ? OcrEngine.TryCreateFromLanguage(new Language(wanted.LanguageTag)) : OcrEngine.TryCreateFromUserProfileLanguages();
            return engine is null ? null : new ExitListReader(engine);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // A Windows without the recognition's parts: the same as no language for it.
            return null;
        }
    }

    // The game's language codes that aren't Windows' ("ge" is German): the same table as tarkov.dev's. None named, none
    // asked for: Create then reads in English.
    internal static string? WindowsLanguage(string? gameLanguage) => GameLanguage.Common(gameLanguage);

    /// <summary>
    /// The list in the screenshot, or null when its top right corner shows none. The game creates the file and then
    /// writes it, so a picture that can't be decoded yet is tried again.
    /// </summary>
    /// <param name="tries">How often to try a file that isn't a whole picture yet.</param>
    /// <param name="wait">How long to wait between tries.</param>
    public async Task<ExitListReading?> ReadAsync(string path, int tries = 8, TimeSpan? wait = null, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (await ReadOnceAsync(path, ct) is { } read)
                    return read.Reading;
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

    // One look: null when the file isn't a picture yet (empty), else the reading or the fact that there is none.
    private async Task<(ExitListReading? Reading, bool Looked)?> ReadOnceAsync(string path, CancellationToken ct)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (!IsWhole(file))
            return null;
        using var stream = file.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(ct);
        double width = decoder.PixelWidth, height = decoder.PixelHeight;
        if (width < 320 || height < 240)
            return (null, true);
        var scale = Math.Clamp(ReadHeight / height, 0.5, 2);
        var cornerWidth = Math.Min(width, height * CornerWidth);
        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)Math.Round(width * scale),
            ScaledHeight = (uint)Math.Round(height * scale),
            InterpolationMode = BitmapInterpolationMode.Fant,
        };
        // The decoder scales first; the bounds are in the scaled picture.
        var left = (uint)Math.Floor((width - cornerWidth) * scale);
        transform.Bounds = new BitmapBounds
        {
            X = left,
            Y = 0,
            Width = Math.Min((uint)Math.Floor(cornerWidth * scale), transform.ScaledWidth - left),
            Height = Math.Min((uint)Math.Floor(height * CornerHeight * scale), transform.ScaledHeight),
        };
        using var corner = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform,
            ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage).AsTask(ct);
        var pixels = new byte[4 * corner.PixelWidth * corner.PixelHeight];
        corner.CopyToBuffer(pixels.AsBuffer());
        if (FindBar(pixels, corner.PixelWidth, corner.PixelHeight) is not { } bar)
            return (null, true);

        var result = await _engine.RecognizeAsync(corner).AsTask(ct);
        var words = result.Lines.SelectMany(l => l.Words).Select(w => new ReadWord(w.Text, w.BoundingRect.X, w.BoundingRect.Y, w.BoundingRect.Width, w.BoundingRect.Height));
        var (header, lines) = ExitList.Lines(words, bar.Left, bar.Top, bar.Bottom);
        var rows = lines.Select(l => new ExitListRow(l.Text, Marked(pixels, corner.PixelWidth, corner.PixelHeight, bar, l))).ToList();
        return (new ExitListReading(header, rows), true);
    }

    private static readonly byte[] PngEnd = [0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82]; // "IEND" and its checksum

    /// <summary>
    /// Whether the file is a picture written to its end: a PNG ends with its IEND chunk, a JPEG with its end marker.
    /// A picture the game is still writing decodes as far as it goes, and half a list read as the whole one would
    /// call the exits on its missing rows closed.
    /// </summary>
    internal static bool IsWhole(FileStream file)
    {
        if (file.Length < 16)
            return false;
        Span<byte> end = stackalloc byte[8];
        file.Position = file.Length - end.Length;
        file.ReadExactly(end);
        file.Position = 0;
        return end.SequenceEqual(PngEnd) || (end[^2] == 0xFF && end[^1] == 0xD9);
    }

    /// <summary>The green bar over the list, in the corner's pixels.</summary>
    internal readonly record struct Bar(int Left, int Top, int Right, int Bottom);

    // The bar's green (about 124, 167, 14): clearly green, with little blue. Foliage is darker or bluer.
    private static bool IsBarGreen(byte[] pixels, int i) =>
        pixels[i + 1] >= 110 && pixels[i + 1] > pixels[i + 2] * 1.15 && pixels[i + 1] > pixels[i] * 2.2;

    /// <summary>
    /// The green bar: rows near the corner's top that are green over a third of the corner's width or more, at least a
    /// fiftieth of the corner's height of them in one piece. Null: the corner shows no list.
    /// </summary>
    internal static Bar? FindBar(byte[] pixels, int width, int height)
    {
        var lookTo = Math.Min(height, (int)(height * 0.3));
        var needed = width / 3 / 2; // every second pixel is looked at
        int top = -1, bottom = -1;
        for (var y = 0; y < lookTo; y++)
        {
            var green = 0;
            for (var x = 0; x < width; x += 2)
            {
                if (IsBarGreen(pixels, 4 * (y * width + x)))
                    green++;
            }
            if (green >= needed)
            {
                if (top < 0)
                    top = y;
                bottom = y;
            }
            else if (top >= 0)
            {
                if (bottom - top + 1 >= height / 50)
                    break;
                top = bottom = -1;
            }
        }
        if (top < 0 || bottom - top + 1 < height / 50)
            return null;
        var middle = (top + bottom) / 2;
        int left = -1, right = -1;
        for (var x = 0; x < width; x++)
        {
            if (!IsBarGreen(pixels, 4 * (middle * width + x)))
                continue;
            if (left < 0)
                left = x;
            right = x;
        }
        return new Bar(left, top, right + 1, bottom + 1);
    }

    /// <summary>
    /// Whether something stands right of the bar's end on a row's line: the column where the game writes "??:??:??"
    /// or a countdown. Light pixels on the list's dark panel; the panel lets little of the scene through.
    /// </summary>
    internal static bool Marked(byte[] pixels, int width, int height, Bar bar, ExitListLine line)
    {
        var barHeight = bar.Bottom - bar.Top;
        int x0 = Math.Min(width, bar.Right + barHeight / 4), x1 = Math.Max(x0, width - barHeight / 8);
        int y0 = Math.Clamp((int)line.Top, 0, height), y1 = Math.Clamp((int)Math.Ceiling(line.Bottom), y0, height);
        if (x1 - x0 < barHeight || y1 <= y0)
            return false;
        var light = 0;
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                var i = 4 * (y * width + x);
                if (Math.Max(pixels[i], Math.Max(pixels[i + 1], pixels[i + 2])) >= 150)
                    light++;
            }
        }
        return light >= (x1 - x0) * (y1 - y0) * 0.04;
    }
}
