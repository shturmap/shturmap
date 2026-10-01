using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Shturmap.Core.Logs;

/// <summary>
/// Turns appended log text into <see cref="LogRecord"/>s. Text may arrive in arbitrary chunks (a tailer reads
/// whatever the game has flushed), so partial lines and half-written JSON blocks are kept until complete.
/// </summary>
public sealed partial class LogRecordReader
{
    // 2026-01-01 18:00:10.250|1.1.5.1.47510|Info|application|Session mode: Pve
    // Older builds wrote an offset after the time: "2026-01-01 12:00:00.201 +01:00|..."
    [GeneratedRegex(@"^(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3})(?: [+-]\d{2}:\d{2})?\|(?<ver>[^|]*)\|(?<lvl>[^|]*)\|(?<ch>[^|]*)\|(?<msg>.*)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Header();

    private readonly StringBuilder _partialLine = new();
    private LogRecord? _pending;
    private readonly StringBuilder _body = new();
    private bool _bodyOpen;

    /// <summary>Feeds text; returns every record that is now known to be complete.</summary>
    public IReadOnlyList<LogRecord> Append(string text)
    {
        var done = new List<LogRecord>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
                continue;
            _partialLine.Append(text, start, i - start);
            start = i + 1;
            var line = _partialLine.ToString().TrimEnd('\r');
            _partialLine.Clear();
            OnLine(line, done);
        }
        _partialLine.Append(text, start, text.Length - start);
        return done;
    }

    /// <summary>
    /// Emits the record still being assembled. Call when the file has been quiet for a moment: a lone header
    /// line cannot be known to be complete until something follows it. An unfinished JSON block is kept
    /// unless <paramref name="force"/> is set.
    /// </summary>
    public LogRecord? Flush(bool force = false)
    {
        if (_pending is null || (_bodyOpen && !force))
            return null;
        return TakePending();
    }

    private void OnLine(string line, List<LogRecord> done)
    {
        var header = Header().Match(line);
        if (header.Success)
        {
            if (_pending is not null)
                done.Add(TakePending());
            _pending = new LogRecord(
                DateTime.ParseExact(header.Groups["ts"].Value, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                header.Groups["ver"].Value,
                header.Groups["lvl"].Value,
                header.Groups["ch"].Value,
                header.Groups["msg"].Value,
                null);
            return;
        }

        if (_pending is null)
            return; // text before the first header (we started reading mid-entry)

        if (_body.Length == 0 && line.StartsWith('{'))
            _bodyOpen = true;
        _body.Append(line).Append('\n');
        if (_bodyOpen && line == "}")
        {
            _bodyOpen = false;
            done.Add(TakePending());
        }
    }

    private LogRecord TakePending()
    {
        var record = _pending! with { Body = _body.Length > 0 ? _body.ToString() : null };
        _pending = null;
        _body.Clear();
        _bodyOpen = false;
        return record;
    }
}
