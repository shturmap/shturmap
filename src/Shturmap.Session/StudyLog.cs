using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Shturmap.Session;

/// <summary>
/// The study log: what happened in the game and what the player did with Shturmap, one JSON object per line in the
/// data folder's <c>study\yyyy-MM-dd.jsonl</c>, to read and correlate later (docs/DESIGN.md §8, "Study log"). Every
/// line carries the game context (raid phase, map, age of the last fix), so UI events can be lined up with raids and
/// quest completions. Only Shturmap's own windows are observed; nothing global, nothing sent anywhere.
/// Developer builds only (owner, 2026-10-03: "The study log should only be part of the dev version and not be in the
/// release version"): a release compiles the writer and the pruning out, and <see cref="Enabled"/> stays false, so
/// the calls scattered through the app are no-ops there.
/// </summary>
public sealed class StudyLog(string folder) : IDisposable
{
#if DEVTOOLS
    /// <summary>Whether this build has a study log at all: developer builds (DEVTOOLS) only.</summary>
    public const bool Available = true;
#else
    public const bool Available = false;
#endif

    private readonly Lock _gate = new();
    private StreamWriter? _writer;
#if DEVTOOLS
    private DateOnly _day;
#endif
    private bool _enabled;

    /// <summary>Where the days are written: the data folder's <c>study</c> folder.</summary>
    public string Folder => folder;

    /// <summary>Fields added to every line, e.g. the raid phase and map.</summary>
    public Func<IEnumerable<(string Key, object? Value)>>? Context { get; set; }

    /// <summary>
    /// In a developer build: on unless switched off in settings, or <c>--study</c> for one session; always off for
    /// snapshot and fake-game runs. In a release it can't be switched on (<see cref="Available"/>).
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = Available && value;
    }

    /// <summary>Days of study log kept; older days are removed at start.</summary>
    public const int KeepDays = 30;

    /// <summary>Removes days older than <see cref="KeepDays"/>, whether the log is on or off; nothing in a release, which
    /// leaves any study files from earlier builds alone.</summary>
    public void Prune(DateTime now)
    {
#if DEVTOOLS
        try
        {
            if (!Directory.Exists(folder))
                return;
            var oldest = DateOnly.FromDateTime(now).AddDays(-KeepDays);
            foreach (var file in Directory.EnumerateFiles(folder, "*.jsonl"))
            {
                if (DateOnly.TryParseExact(Path.GetFileNameWithoutExtension(file), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) &&
                    day < oldest)
                    File.Delete(file);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
#endif
    }

    /// <summary>Something the player did in Shturmap.</summary>
    public void Ui(string name, params (string Key, object? Value)[] data) => Write("ui", name, data);

    /// <summary>Something that happened in the game (from its logs and screenshots) or in Shturmap by itself.</summary>
    public void Game(string name, params (string Key, object? Value)[] data) => Write("game", name, data);

    private void Write(string source, string name, (string Key, object? Value)[] data)
    {
#if DEVTOOLS
        if (!Enabled)
            return;
        try
        {
            var context = Context?.Invoke().ToList() ?? [];
            using var buffer = new MemoryStream();
            using (var json = new Utf8JsonWriter(buffer))
            {
                json.WriteStartObject();
                json.WriteString("t", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture));
                json.WriteString("src", source);
                json.WriteString("ev", name);
                foreach (var (key, value) in data)
                    WriteValue(json, key, value);
                foreach (var (key, value) in context)
                    WriteValue(json, "ctx." + key, value);
                json.WriteEndObject();
            }
            var line = Encoding.UTF8.GetString(buffer.ToArray());
            lock (_gate)
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                if (_writer is null || today != _day)
                {
                    _writer?.Dispose();
                    Directory.CreateDirectory(folder);
                    var stream = new FileStream(Path.Combine(folder, $"{today:yyyy-MM-dd}.jsonl"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                    _day = today;
                }
                _writer.WriteLine(line);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The study log must never get in the player's way.
        }
#endif
    }

#if DEVTOOLS
    private static void WriteValue(Utf8JsonWriter json, string key, object? value)
    {
        switch (value)
        {
            case null:
                json.WriteNull(key);
                break;
            case string s:
                json.WriteString(key, s);
                break;
            case bool b:
                json.WriteBoolean(key, b);
                break;
            case int i:
                json.WriteNumber(key, i);
                break;
            case long l:
                json.WriteNumber(key, l);
                break;
            case double d:
                json.WriteNumber(key, Math.Round(d, 2));
                break;
            case float f:
                json.WriteNumber(key, Math.Round(f, 2));
                break;
            case DateTime t:
                json.WriteString(key, t.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture));
                break;
            case TimeSpan span:
                json.WriteNumber(key, Math.Round(span.TotalSeconds, 1));
                break;
            case Enum e:
                json.WriteString(key, e.ToString());
                break;
            case IEnumerable<string> list:
                json.WriteStartArray(key);
                foreach (var item in list)
                    json.WriteStringValue(item);
                json.WriteEndArray();
                break;
            default:
                json.WriteString(key, Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }
#endif

    public void Dispose()
    {
        lock (_gate)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
