using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Shturmap.Core.Logs;
using Shturmap.Game.Install;

namespace Shturmap.Game.Logs;

/// <param name="IsReplay">True for entries that were already in the log when Shturmap started reading it.</param>
public sealed record LogEvent(GameEvent Event, string Session, bool IsReplay);

/// <summary>
/// Follows the newest EFT log session. Reads the application and notification logs with shared access, keeps a
/// stateful UTF-8 decoder and a record reader per file (so multi-byte characters and JSON blocks may be split
/// across reads), and switches to a new session folder when the game starts again.
/// Appends from the game's open handle are not reliably reported by file notifications, so this polls:
/// every 500 ms while the log is active, every 2 s when it has been quiet for a while.
/// </summary>
public sealed partial class LogTailer(string logsRoot) : IAsyncDisposable
{
    [GeneratedRegex(@" (?:application|push-notifications|notifications)_\d+\.log$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WatchedFile();

    private static readonly TimeSpan ActivePoll = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan IdlePoll = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan IdleAfter = TimeSpan.FromMinutes(2);
    private const int MaxReadPerPoll = 4 * 1024 * 1024;

    private readonly Channel<LogEvent> _events = Channel.CreateUnbounded<LogEvent>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Dictionary<string, FileState> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _stop = new();
    private Task? _loop;
    private bool _firstSession = true;

    public string LogsRoot { get; } = logsRoot;

    public ChannelReader<LogEvent> Events => _events.Reader;

    /// <summary>Folder name of the session being followed, e.g. "log_2026.01.01_15-00-00_1.1.5.1.47510".</summary>
    public string? CurrentSession { get; private set; }

    /// <summary>When a followed log last grew.</summary>
    public DateTime? LastActivityUtc { get; private set; }

    public void Start() => _loop ??= Task.Run(() => RunAsync(_stop.Token));

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        if (_loop is not null)
        {
            try
            {
                await _loop;
            }
            catch (OperationCanceledException)
            {
            }
        }
        _events.Writer.TryComplete();
        _stop.Dispose();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                PollOnce();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Folder briefly unavailable (game updating, drive asleep): try again next poll.
            }
            var quiet = LastActivityUtc is null || DateTime.UtcNow - LastActivityUtc > IdleAfter;
            await Task.Delay(quiet ? IdlePoll : ActivePoll, ct);
        }
    }

    /// <summary>One polling pass; returns the number of events written. Exposed for tests.</summary>
    internal int PollOnce()
    {
        var newest = NewestSession(LogsRoot);
        if (newest is null)
            return 0;

        var replay = false;
        if (!string.Equals(newest, CurrentSession, StringComparison.OrdinalIgnoreCase))
        {
            CurrentSession = newest;
            _files.Clear();
            replay = _firstSession;
            _firstSession = false;
        }

        var sessionFolder = Path.Combine(LogsRoot, newest);
        var events = new List<GameEvent>();
        foreach (var path in Directory.EnumerateFiles(sessionFolder, "*.log").Where(p => WatchedFile().IsMatch(p)).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (!_files.TryGetValue(path, out var state))
                _files[path] = state = new FileState();
            events.AddRange(ReadNew(path, state));
        }

        foreach (var e in events.OrderBy(e => e.At))
            _events.Writer.TryWrite(new LogEvent(e, newest, replay));
        return events.Count;
    }

    private IEnumerable<GameEvent> ReadNew(string path, FileState state)
    {
        var records = new List<LogRecord>();
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            if (stream.Length < state.Offset)
                state.Reset(); // truncated or replaced
            if (stream.Length > state.Offset)
            {
                stream.Seek(state.Offset, SeekOrigin.Begin);
                var buffer = new byte[Math.Min(stream.Length - state.Offset, MaxReadPerPoll)];
                var read = stream.Read(buffer, 0, buffer.Length);
                state.Offset += read;
                var chars = new char[state.Decoder.GetCharCount(buffer, 0, read)];
                state.Decoder.GetChars(buffer, 0, read, chars, 0);
                records.AddRange(state.Reader.Append(new string(chars)));
                state.LastGrowth = DateTime.UtcNow;
                LastActivityUtc = state.LastGrowth;
            }
        }

        // A lone header line is complete once the file has been quiet briefly; a half-written JSON block only
        // after a long silence.
        var quietFor = DateTime.UtcNow - state.LastGrowth;
        if (quietFor > TimeSpan.FromSeconds(1) && state.Reader.Flush(force: quietFor > TimeSpan.FromSeconds(10)) is { } pending)
            records.Add(pending);

        return records.Select(GameLogParser.Parse).OfType<GameEvent>();
    }

    /// <summary>Folder name of the newest log_* session under a Logs folder.</summary>
    public static string? NewestSession(string logsRoot)
    {
        if (!Directory.Exists(logsRoot))
            return null;
        return Directory.EnumerateDirectories(logsRoot, "log_*")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Select(name => (Name: name, Start: InstallLocator.SessionStart(name)))
            .Where(s => s.Start is not null)
            .OrderByDescending(s => s.Start)
            .Select(s => s.Name)
            .FirstOrDefault();
    }

    /// <summary>Reads a whole session folder at once, e.g. to backfill quest history.</summary>
    public static IReadOnlyList<GameEvent> ReadSession(string sessionFolder)
    {
        var events = new List<GameEvent>();
        foreach (var path in Directory.EnumerateFiles(sessionFolder, "*.log").Where(p => WatchedFile().IsMatch(p)))
        {
            string text;
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                text = reader.ReadToEnd();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            var records = new LogRecordReader();
            var all = records.Append(text).ToList();
            if (records.Flush(force: true) is { } last)
                all.Add(last);
            events.AddRange(all.Select(GameLogParser.Parse).OfType<GameEvent>());
        }
        return events.OrderBy(e => e.At).ToList();
    }

    private sealed class FileState
    {
        public long Offset;
        public Decoder Decoder = new UTF8Encoding(false).GetDecoder();
        public LogRecordReader Reader = new();
        public DateTime LastGrowth = DateTime.UtcNow;

        public void Reset()
        {
            Offset = 0;
            Decoder = new UTF8Encoding(false).GetDecoder();
            Reader = new LogRecordReader();
        }
    }
}
