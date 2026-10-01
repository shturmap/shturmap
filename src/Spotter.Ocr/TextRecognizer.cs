using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Spotter.Ocr;

public readonly record struct Box(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public double CenterY => Y + Height / 2;

    public static Box Union(IEnumerable<Box> boxes)
    {
        var list = boxes.ToList();
        var x = list.Min(b => b.X);
        var y = list.Min(b => b.Y);
        return new Box(x, y, list.Max(b => b.Right) - x, list.Max(b => b.Bottom) - y);
    }
}

public sealed record TextWord(string Text, Box Box);

public sealed record TextLine(string Text, Box Box, IReadOnlyList<TextWord> Words);

/// <summary>A decoded screenshot: pixels kept as BGRA for cropping and sampling.</summary>
public sealed class Screenshot : IDisposable
{
    private Screenshot(SoftwareBitmap bitmap, BitmapDecoder decoder, IRandomAccessStream stream)
    {
        Bitmap = bitmap;
        Decoder = decoder;
        Stream = stream;
    }

    private byte[]? _pixels;

    internal SoftwareBitmap Bitmap { get; }

    internal BitmapDecoder Decoder { get; }

    private IRandomAccessStream Stream { get; }

    public int Width => Bitmap.PixelWidth;

    public int Height => Bitmap.PixelHeight;

    public static async Task<Screenshot> LoadAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        var stream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        return new Screenshot(bitmap, decoder, stream);
    }

    /// <summary>Crops a region and scales it; the decoder applies the scale before the crop bounds.</summary>
    internal async Task<SoftwareBitmap> CropAsync(Box region, double scale, BitmapInterpolationMode interpolation = BitmapInterpolationMode.Cubic)
    {
        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)Math.Round(Width * scale),
            ScaledHeight = (uint)Math.Round(Height * scale),
            InterpolationMode = interpolation,
            Bounds = new BitmapBounds
            {
                X = (uint)Math.Max(0, Math.Round(region.X * scale)),
                Y = (uint)Math.Max(0, Math.Round(region.Y * scale)),
                Width = (uint)Math.Min(Math.Round(region.Width * scale), Math.Round(Width * scale) - Math.Round(region.X * scale)),
                Height = (uint)Math.Min(Math.Round(region.Height * scale), Math.Round(Height * scale) - Math.Round(region.Y * scale)),
            },
        };
        return await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform,
            ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
    }

    /// <summary>Mean brightness (0–255) of a region, used to tell the selected tab from the others.</summary>
    internal double MeanLuminance(Box region)
    {
        if (_pixels is null)
        {
            _pixels = new byte[4 * Width * Height];
            Bitmap.CopyToBuffer(_pixels.AsBuffer());
        }
        var buffer = _pixels;
        var x0 = (int)Math.Clamp(region.X, 0, Width - 1);
        var y0 = (int)Math.Clamp(region.Y, 0, Height - 1);
        var x1 = (int)Math.Clamp(region.Right, x0 + 1, Width);
        var y1 = (int)Math.Clamp(region.Bottom, y0 + 1, Height);
        double sum = 0;
        long count = 0;
        for (var y = y0; y < y1; y += 2)
        {
            for (var x = x0; x < x1; x += 2)
            {
                var i = 4 * (y * Width + x);
                sum += 0.0722 * buffer[i] + 0.7152 * buffer[i + 1] + 0.2126 * buffer[i + 2];
                count++;
            }
        }
        return count == 0 ? 0 : sum / count;
    }

    public void Dispose()
    {
        Bitmap.Dispose();
        Stream.Dispose();
    }
}

/// <summary>Windows' built-in OCR (Windows.Media.Ocr). Needs the language's OCR pack, which en-US normally has.</summary>
public sealed class TextRecognizer
{
    private readonly OcrEngine _engine;

    private TextRecognizer(OcrEngine engine) => _engine = engine;

    public string LanguageTag => _engine.RecognizerLanguage.LanguageTag;

    /// <summary>
    /// An engine for the game's language ("en", "de", …) if Windows has that OCR pack, else English, else the
    /// user's profile languages. Null if no OCR language is installed at all.
    /// </summary>
    public static TextRecognizer? Create(string? gameLanguage)
    {
        var available = OcrEngine.AvailableRecognizerLanguages.ToList();
        var wanted = available.FirstOrDefault(l => gameLanguage is not null &&
                         l.LanguageTag.StartsWith(gameLanguage, StringComparison.OrdinalIgnoreCase))
                     ?? available.FirstOrDefault(l => l.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase));
        var engine = wanted is not null ? OcrEngine.TryCreateFromLanguage(new Language(wanted.LanguageTag)) : OcrEngine.TryCreateFromUserProfileLanguages();
        return engine is null ? null : new TextRecognizer(engine);
    }

    public Task<IReadOnlyList<TextLine>> ReadAsync(Screenshot screenshot) => ReadAsync(screenshot.Bitmap, 0, 0, 1);

    /// <summary>Reads a bitmap and maps the boxes back to screenshot pixels.</summary>
    internal async Task<IReadOnlyList<TextLine>> ReadAsync(SoftwareBitmap bitmap, double offsetX, double offsetY, double scale)
    {
        var result = await _engine.RecognizeAsync(bitmap);
        var lines = new List<TextLine>();
        foreach (var line in result.Lines)
        {
            var words = line.Words
                .Select(w => new TextWord(w.Text, new Box(
                    offsetX + w.BoundingRect.X / scale,
                    offsetY + w.BoundingRect.Y / scale,
                    w.BoundingRect.Width / scale,
                    w.BoundingRect.Height / scale)))
                .ToList();
            if (words.Count > 0)
                lines.Add(new TextLine(line.Text, Box.Union(words.Select(w => w.Box)), words));
        }
        return lines;
    }
}
