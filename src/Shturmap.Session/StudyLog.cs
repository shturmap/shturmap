using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Shturmap.Session;

/// <summary>
/// The study log: what happened in the game and what the player did with Shturmap, one JSON object per line in
/// <c>%LOCALAPPDATA%\Shturmap\study\yyyy-MM-dd.jsonl</c>, to read and correlate later (docs/DESIGN.md §8, "Study
/// log"). Every line carries the game context (raid phase, map, age of the last fix), so UI events can be lined up
/// with raids and quest completions. Only Shturmap's own windows are observed; nothing global, nothing sent anywhere.
/// </summary>
public sealed class StudyLog(string folder) : IDisposable
{
    private readonly Lock _gate = new();
    private StreamWriter? _writer;
    private DateOnly _day;

    /// <summary>Fields added to every line, e.g. the raid phase and map.</summary>
    public Func<IEnumerable<(string Key, object? Value)>>? Context { get; set; }

    /// <summary>Off for developer runs (snapshots, fake games), which would otherwise mix into the player's study.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Something the player did in Shturmap.</summary>
    public void Ui(string name, params (string Key, object? Value)[] data) => Write("ui", name, data);

    /// <summary>Something that happened in the game (from its logs and screenshots) or in Shturmap by itself.</summary>
    public void Game(string name, params (string Key, object? Value)[] data) => Write("game", name, data);

    private void Write(string source, string name, (string Key, object? Value)[] data)
    {
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
    }

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

    public void Dispose()
    {
        lock (_gate)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
