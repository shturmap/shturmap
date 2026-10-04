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
    private bool _partialReplay;
    private LogRecord? _pending;
    private readonly StringBuilder _body = new();
    private bool _bodyOpen;

    /// <summary>
    /// Whether the text appended from now on was in the log before its follower started (a tailer sets it while it
    /// reads what a file held at first sight). A record carries the value its header line began under
    /// (<see cref="LogRecord.IsReplay"/>), however late the rest of it arrives: a log's last line is only known to
    /// be complete a moment later, or when the next line comes.
    /// </summary>
    public bool Replay { get; set; }

    /// <summary>Feeds text; returns every record that is now known to be complete.</summary>
    public IReadOnlyList<LogRecord> Append(string text)
    {
        var done = new List<LogRecord>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
                continue;
            // A line whose start came with an earlier text began under that text's flag.
            var replay = _partialLine.Length > 0 ? _partialReplay : Replay;
            _partialLine.Append(text, start, i - start);
            start = i + 1;
            var line = _partialLine.ToString().TrimEnd('\r');
            _partialLine.Clear();
            OnLine(line, replay, done);
        }
        if (start < text.Length)
        {
            if (_partialLine.Length == 0)
                _partialReplay = Replay;
            _partialLine.Append(text, start, text.Length - start);
        }
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

    private void OnLine(string line, bool replay, List<LogRecord> done)
    {
        var header = Header().Match(line);
        // Digits in a header's shape that are no time ("2026-13-45 …") are a continuation line, not a reason to stop.
        if (header.Success && DateTime.TryParseExact(header.Groups["ts"].Value, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var timestamp))
        {
            if (_pending is not null)
                done.Add(TakePending());
            _pending = new LogRecord(
                timestamp,
                header.Groups["ver"].Value,
                header.Groups["lvl"].Value,
                header.Groups["ch"].Value,
                header.Groups["msg"].Value,
                null) { IsReplay = replay };
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
