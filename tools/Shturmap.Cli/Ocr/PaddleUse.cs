using System.Diagnostics;
using System.Globalization;
using Shturmap.Game.Screenshots;
using SkiaSharp;

namespace Shturmap.Cli.Ocr;

/// <summary>
/// The app's second reader for the CLI's measurements of the readers (desk, tasks): PaddleOCR's recognition of the model
/// beside the CLI (<see cref="PaddleReader.Bundled"/>, as the app has it), on the bands Windows' recognition left unread,
/// unless <c>--no-paddle</c> (Windows' alone, as before 2026-10-26); <c>--paddle-threads &lt;n&gt;</c> its threads (1, as
/// the app's). It counts the bands it read and the time they took, the model's load apart.
/// </summary>
internal sealed class PaddleUse(PaddleReader reader) : ILineReader
{
    private int _bands;
    private long _ticks;

    public int Bands => _bands;

    public double Ms => _ticks * 1000.0 / Stopwatch.Frequency;

    public long? LoadMs { get; private set; }

    public async Task<string?> ReadAsync(SKBitmap line, CancellationToken ct)
    {
        // The model's load (once) isn't a band's time.
        if (LoadMs is null)
            await reader.ReadAsync(line, ct);
        var start = Stopwatch.GetTimestamp();
        var text = await reader.ReadAsync(line, ct);
        Interlocked.Add(ref _ticks, Stopwatch.GetTimestamp() - start);
        Interlocked.Increment(ref _bands);
        return text;
    }

    /// <summary>The reader the options ask for (null with --no-paddle), and what it says of itself.</summary>
    public static PaddleUse? From(IReadOnlyList<string> args)
    {
        if (args.Contains("--no-paddle"))
        {
            Console.WriteLine("Text recognition: Windows' alone (--no-paddle)");
            return null;
        }
        var at = args.ToList().IndexOf("--paddle-threads");
        if (at >= 0 && at + 1 < args.Count)
            PaddleReader.Threads = int.Parse(args[at + 1], CultureInfo.InvariantCulture);
        if (!PaddleReader.IsBundled)
        {
            Console.WriteLine($"Text recognition: Windows' alone (no PaddleOCR model in {PaddleReader.Folder}\\ beside the CLI)");
            return null;
        }
        PaddleUse? use = null;
        var reader = PaddleReader.Bundled(loaded: ms => use!.LoadMs = ms, failed: why => Console.WriteLine($"PaddleOCR's model couldn't be loaded: {why}"));
        use = new PaddleUse(reader);
        Console.WriteLine($"Text recognition: Windows', then PaddleOCR's on the bands it leaves unread ({PaddleReader.Threads} thread(s))");
        return use;
    }

    /// <summary>The options this adds to a command's usage line.</summary>
    public const string Usage = "[--no-paddle] [--paddle-threads <n>]";

    /// <summary>The options' arguments, to be left out of a command's inputs.</summary>
    public static bool IsOption(IReadOnlyList<string> args, ref int i)
    {
        switch (args[i])
        {
            case "--no-paddle":
                return true;
            case "--paddle-threads":
                i++;
                return true;
            default:
                return false;
        }
    }

    public string Summary(int pictures) => string.Create(CultureInfo.InvariantCulture,
        $"PaddleOCR read {Bands} bands Windows' recognition left unread, {Ms:0} ms in all ({(pictures > 0 ? Ms / pictures : 0):0} ms a picture, {(Bands > 0 ? Ms / Bands : 0):0.0} ms a band); its model loaded in {LoadMs?.ToString(CultureInfo.InvariantCulture) ?? "-"} ms");
}
