using System.Diagnostics;
using System.Text;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace Shturmap.Game.Screenshots;

/// <summary>A second reader of one line of a picture, for a band Windows' recognition left unread
/// (<see cref="PaddleReader"/>; tests give their own).</summary>
public interface ILineReader
{
    /// <summary>The line's text ("" when it reads none), or null when this reader can't read at all (its model didn't
    /// load).</summary>
    Task<string?> ReadAsync(SKBitmap line, CancellationToken ct);
}

/// <summary>
/// PaddleOCR's text-line recognition (PP-OCRv5 latin mobile, the CTC recogniser as RapidOCR exports it to ONNX; Apache-2.0)
/// through ONNX Runtime on the CPU, recognition only: the band is the line, no detection. The second reader of a label band
/// or a countdown band that Windows' recognition left unread after every way it tries (owner, 2026-10-26, decision 1 of
/// the overnight run; docs/DESIGN.md §2, "Where Windows reads nothing"): never first, never on a count. Its text goes
/// through the same match as Windows' (<see cref="DeskReader"/>, <see cref="TasksReader"/>). The model and its dictionary
/// come with the app (<see cref="Folder"/>, beside the exe; fetched and checked at build time, never in the repository);
/// the model is loaded once, at the first band that needs it, off the caller's thread (about 140 ms), and a model that
/// can't be loaded is said once and never tried again: the readings then read as Windows alone reads. ONNX Runtime's
/// telemetry events are switched off before anything is loaded; nothing of a picture leaves the PC.
/// </summary>
public sealed class PaddleReader : ILineReader, IDisposable
{
    /// <summary>The folder beside the exe that holds the model and its dictionary.</summary>
    public const string Folder = "ocr";

    public const string ModelFile = "latin_PP-OCRv5_rec_mobile.onnx";
    public const string DictionaryFile = "ppocrv5_latin_dict.txt";

    /// <summary>The threads one band's recognition takes. One: Shturmap reads beside a running game, and a band takes
    /// 16 ms on one (7 on four; measured 2026-10-26, docs/NEXT.md, decision 1's built paragraph).</summary>
    internal static int Threads { get; set; } = 1;

    private readonly string _model;
    private readonly string _dictionary;
    private readonly int _threads;
    private readonly Action<long>? _loaded;
    private readonly Action<string>? _failed;
    private readonly object _gate = new();
    private Task<Model?>? _loading;

    /// <param name="loaded">Hears how long the model took to load, once.</param>
    /// <param name="failed">Hears why the model couldn't be loaded, once.</param>
    public PaddleReader(string model, string dictionary, int threads = 1, Action<long>? loaded = null, Action<string>? failed = null)
    {
        _model = model;
        _dictionary = dictionary;
        _threads = Math.Max(1, threads);
        _loaded = loaded;
        _failed = failed;
    }

    /// <summary>The reader of the model that came with the app (<see cref="Folder"/> beside the exe).</summary>
    public static PaddleReader Bundled(Action<long>? loaded = null, Action<string>? failed = null) =>
        new(Path.Combine(AppContext.BaseDirectory, Folder, ModelFile), Path.Combine(AppContext.BaseDirectory, Folder, DictionaryFile), Threads, loaded, failed);

    /// <summary>Whether the app's model and dictionary are beside the exe (a test or the CLI asks before measuring).</summary>
    public static bool IsBundled => File.Exists(Path.Combine(AppContext.BaseDirectory, Folder, ModelFile)) &&
                                    File.Exists(Path.Combine(AppContext.BaseDirectory, Folder, DictionaryFile));

    public async Task<string?> ReadAsync(SKBitmap line, CancellationToken ct)
    {
        Task<Model?> loading;
        lock (_gate)
            loading = _loading ??= Task.Run(Load, CancellationToken.None);
        var model = await loading.WaitAsync(ct);
        if (model is null)
            return null;
        ct.ThrowIfCancellationRequested();
        return model.Read(line);
    }

    private Model? Load()
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var model = new Model(_model, _dictionary, _threads);
            _loaded?.Invoke(watch.ElapsedMilliseconds);
            return model;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // A model missing or damaged, or the runtime's native library not loading: the readings read as Windows alone.
            _failed?.Invoke($"{e.GetType().Name}: {e.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        Task<Model?>? loading;
        lock (_gate)
            loading = _loading;
        if (loading is { IsCompletedSuccessfully: true, Result: { } model })
            model.Dispose();
    }

    /// <summary>
    /// The model in an ONNX Runtime session. A line is resized to the model's height (48) keeping its proportion, padded to
    /// at least 320 wide (PaddleOCR's resize_norm_img, the rest zero), in the model's BGR order normalised to −1..1; the
    /// output's best class per column, repeats and blanks dropped, is the text (as the spikes of 2026-10-19 and
    /// 2026-10-25 read it, shturmap-cli ocr-compare).
    /// </summary>
    private sealed class Model : IDisposable
    {
        private readonly InferenceSession _session;
        private readonly string _input;
        private readonly string[] _chars;
        private readonly int _height;

        public Model(string model, string dictionary, int threads)
        {
            // ONNX Runtime's Windows build writes telemetry events (ETW) unless told not to: the reading stays on the PC.
            OrtEnv.Instance().DisableTelemetryEvents();
            var chars = new List<string> { "" };
            chars.AddRange(File.ReadAllLines(dictionary, Encoding.UTF8));
            using var options = new SessionOptions
            {
                IntraOpNumThreads = threads,
                InterOpNumThreads = 1,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            };
            _session = new InferenceSession(model, options);
            var input = _session.InputMetadata.First();
            _input = input.Key;
            var dims = input.Value.Dimensions;
            _height = dims.Length == 4 && dims[2] > 0 ? dims[2] : 48;
            // The class count is the output's last dimension when the model says it; else the dictionary plus a space.
            var classes = _session.OutputMetadata.First().Value.Dimensions[^1];
            if (classes <= 0 || classes > chars.Count)
                chars.Add(" ");
            _chars = chars.ToArray();
        }

        public string Read(SKBitmap line)
        {
            var w = Math.Max(1, (int)Math.Ceiling(_height * line.Width / (double)line.Height));
            var padded = Math.Max(w, _height * 320 / 48);
            using var resized = line.Resize(new SKImageInfo(w, _height, SKColorType.Bgra8888, SKAlphaType.Premul), new SKSamplingOptions(SKFilterMode.Linear))
                                ?? throw new InvalidOperationException("Resize failed");
            var values = new float[3 * _height * padded];
            var pixels = resized.GetPixelSpan();
            var plane = _height * padded;
            for (var y = 0; y < _height; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var p = y * resized.RowBytes + x * 4;
                    var at = y * padded + x;
                    values[at] = pixels[p] / 127.5f - 1f; // B
                    values[plane + at] = pixels[p + 1] / 127.5f - 1f; // G
                    values[2 * plane + at] = pixels[p + 2] / 127.5f - 1f; // R
                }
            }
            var tensor = new DenseTensor<float>(values, [1, 3, _height, padded]);
            using var results = _session.Run([NamedOnnxValue.CreateFromTensor(_input, tensor)]);
            var output = results.First().AsTensor<float>();
            int steps = output.Dimensions[1], classes = output.Dimensions[2];
            var scores = output is DenseTensor<float> dense ? dense.Buffer.Span : output.ToArray();
            var text = new StringBuilder();
            var previous = -1;
            for (var t = 0; t < steps; t++)
            {
                var best = 0;
                var max = float.MinValue;
                for (var c = 0; c < classes; c++)
                {
                    var v = scores[t * classes + c];
                    if (v > max)
                    {
                        max = v;
                        best = c;
                    }
                }
                if (best != 0 && best != previous && best < _chars.Length)
                    text.Append(_chars[best]);
                previous = best;
            }
            return text.ToString().Trim();
        }

        public void Dispose() => _session.Dispose();
    }
}
