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
/// across reads), and switches to a new session folder when the game starts again, once the session before is read
/// to its end.
/// Appends from the game's open handle are not reliably reported by file notifications, so this polls:
/// every 500 ms while the log is active, every 2 s when it has been quiet for a while.
/// Nothing in a log ends the following: a file that can't be opened is read at the next poll, and a record the
/// parser can't take is left out (<see cref="ReadProblem"/> says so).
/// </summary>
public sealed partial class LogTailer(string logsRoot) : IAsyncDisposable
{
    [GeneratedRegex(@" (?:application|push-notifications|notifications)_\d+\.log$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WatchedFile();

    private static readonly TimeSpan ActivePoll = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan IdlePoll = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan IdleAfter = TimeSpan.FromMinutes(2);

    private readonly Channel<LogEvent> _events = Channel.CreateUnbounded<LogEvent>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Dictionary<string, FileState> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Type> _reported = [];
    private readonly CancellationTokenSource _stop = new();
    private Task? _loop;
    private bool _firstSession = true;
    // When anything was last read, old text or new: what the polling's pace goes by.
    private DateTime? _lastRead;
    // Polls that put off a new session because a file of the one before couldn't be opened.
    private int _putOff;

    /// <summary>How many polls a new session waits for a file of the session before that can't be opened; then it goes on without it.</summary>
    internal const int PutOffAtMost = 3;

    public string LogsRoot { get; } = logsRoot;

    public ChannelReader<LogEvent> Events => _events.Reader;

    /// <summary>Folder name of the session being followed, e.g. "log_2026.01.01_15-00-00_1.1.5.1.47510".</summary>
    public string? CurrentSession { get; private set; }

    /// <summary>
    /// When the game last wrote to a followed log while Shturmap was following it. What a log already held when it
    /// was first opened doesn't count, whenever it is read: reading the last session at start is no sign of a game
    /// that runs (the LOGS light said "live" for ten minutes after every start, with the game closed).
    /// </summary>
    public DateTime? LastActivityUtc { get; private set; }

    /// <summary>
    /// Something in the logs couldn't be read and was left out, while the following goes on: what ("a record in
    /// … application_000.log", "a poll of the game's logs") and why. The log isn't a documented format, so a patch
    /// may write a record the parser throws on. Raised on the polling thread, once per kind of exception per log
    /// session; the text names the file, never the record's content (which may hold ids).
    /// </summary>
    public event Action<string, Exception>? ReadProblem;

    /// <summary>How many records and polls were left out so far (see <see cref="ReadProblem"/>).</summary>
    public int ReadProblems { get; private set; }

    /// <summary>What turns a record into an event; tests put a parser that throws here.</summary>
    internal Func<LogRecord, GameEvent?> Parser { get; init; } = GameLogParser.Parse;

    /// <summary>The time, for telling a quiet log from a growing one; tests set their own.</summary>
    internal Func<DateTime> UtcNow { get; init; } = () => DateTime.UtcNow;

    /// <summary>The time zone of the logs' times, for putting the events of several logs in order across a clock
    /// change (<see cref="WallClock.Sequence"/>); the PC's own unless a test gives another.</summary>
    public TimeZoneInfo? Zone { get; init; }

    /// <summary>How many bytes of one file a poll reads at most; a longer log takes several polls. Tests make it small.</summary>
    internal int MaxReadPerPoll { get; init; } = 4 * 1024 * 1024;

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
            catch (Exception e)
            {
                // Whatever else goes wrong in one poll must not be the last poll: the task would end unseen, and the
                // app would stop following the game with nothing to show for it.
                Report("a poll of the game's logs", e);
            }
            var quiet = _lastRead is null || UtcNow() - _lastRead > IdleAfter;
            await Task.Delay(quiet ? IdlePoll : ActivePoll, ct);
        }
    }

    /// <summary>One polling pass; returns the number of events written. Exposed for tests.</summary>
    internal int PollOnce()
    {
        var now = UtcNow();
        var newest = NewestSession(LogsRoot);
        if (newest is null)
            return 0;

        // Listed before anything is noted: a folder that can't be read now is met again, as new, at the next poll.
        var paths = WatchedFiles(Path.Combine(LogsRoot, newest));
        var finished = 0;
        if (!string.Equals(newest, CurrentSession, StringComparison.OrdinalIgnoreCase))
        {
            // The game has started again. What the session before still holds unread goes first: the rest of a log
            // longer than one read, a file added since the last poll, the last line. Skipped, a raid's end or a
            // quest's message was missing for the rest of the run (review of 2026-10-04, A44).
            if (CurrentSession is { } before)
            {
                finished = FinishSession(before, now, out var busy);
                if (busy && ++_putOff <= PutOffAtMost)
                    return finished;
            }
            _putOff = 0;
            CurrentSession = newest;
            _files.Clear();
            _reported.Clear();
            // The session found at start was written before Shturmap looked. Whatever its files hold when they are
            // first opened is replay, whenever it is read: a file busy at the first poll, a log longer than one
            // read, the last line that is only complete a moment later. A session the game starts later, or a file
            // it adds to this one, is live from its first line.
            if (_firstSession)
            {
                foreach (var path in paths)
                    _files[path] = new FileState(Zone) { LastGrowth = now, StartsOld = true };
            }
            _firstSession = false;
        }

        var events = new List<(GameEvent Event, bool Replay, DateTime Instant)>();
        try
        {
            foreach (var path in paths)
            {
                if (!_files.TryGetValue(path, out var state))
                    _files[path] = state = new FileState(Zone) { LastGrowth = now };
                try
                {
                    ReadNew(path, state, events, now);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // This file is busy or gone for a moment; the others still count, and it is read at the next poll.
                }
            }
        }
        finally
        {
            // What was read is passed on whatever happened after it: its file's offset has moved on, so it would
            // not be read again.
            foreach (var (e, replay, _) in events.OrderBy(e => e.Instant))
                _events.Writer.TryWrite(new LogEvent(e, newest, replay));
        }
        return finished + events.Count;
    }

    private static List<string> WatchedFiles(string sessionFolder) =>
        Directory.EnumerateFiles(sessionFolder, "*.log").Where(p => WatchedFile().IsMatch(p)).Order(StringComparer.OrdinalIgnoreCase).ToList();

    // Reads the session before to its end, and passes its events on under its own name. A file or folder that is gone
    // has nothing left to read; one that can't be opened now is busy, and the new session waits a few polls for it.
    private int FinishSession(string session, DateTime now, out bool busy)
    {
        busy = false;
        List<string> paths;
        try
        {
            paths = WatchedFiles(Path.Combine(LogsRoot, session));
        }
        catch (DirectoryNotFoundException)
        {
            return 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            busy = true;
            return 0;
        }

        var events = new List<(GameEvent Event, bool Replay, DateTime Instant)>();
        try
        {
            foreach (var path in paths)
            {
                if (!_files.TryGetValue(path, out var state))
                    _files[path] = state = new FileState(Zone) { LastGrowth = now };
                var records = new List<LogRecord>();
                try
                {
                    while (ReadChunk(path, state, records, now))
                    {
                    }
                    // Nothing more will follow its last line.
                    if (state.Reader.Flush(force: true) is { } last)
                        records.Add(last);
                }
                catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
                {
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    busy = true;
                }
                // What was read counts, whatever stopped the reading: the file's offset has moved on.
                ParseInto(events, records, Parser, path, Report, state.Clock);
            }
        }
        finally
        {
            foreach (var (e, replay, _) in events.OrderBy(e => e.Instant))
                _events.Writer.TryWrite(new LogEvent(e, session, replay));
        }
        return events.Count;
    }

    // Reads what a file has gained, one read's worth at most; false when it has nothing more.
    private bool ReadChunk(string path, FileState state, List<LogRecord> records, DateTime now)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length < state.Offset)
            state.Reset(); // truncated or replaced: what it holds now is new
        // Where the replay ends: the file's length when it is first opened (it can only have shrunk since by
        // being cut, and then what is left is all there was).
        state.ReplayUntil = Math.Min(state.ReplayUntil ?? (state.StartsOld ? stream.Length : 0), stream.Length);
        // Old and new text are read apart, so that a read is wholly one or the other.
        var old = state.Offset < state.ReplayUntil;
        var end = old ? state.ReplayUntil.Value : stream.Length;
        if (end <= state.Offset)
            return false;
        stream.Seek(state.Offset, SeekOrigin.Begin);
        var buffer = new byte[Math.Min(end - state.Offset, MaxReadPerPoll)];
        var read = stream.Read(buffer, 0, buffer.Length);
        state.Offset += read;
        var chars = new char[state.Decoder.GetCharCount(buffer, 0, read)];
        state.Decoder.GetChars(buffer, 0, read, chars, 0);
        state.Reader.Replay = old;
        records.AddRange(state.Reader.Append(new string(chars)));
        state.LastGrowth = now;
        _lastRead = now;
        // Only what the game wrote while it was followed is a sign of a game that runs.
        if (!old)
            LastActivityUtc = now;
        return read > 0;
    }

    private void ReadNew(string path, FileState state, List<(GameEvent Event, bool Replay, DateTime Instant)> events, DateTime now)
    {
        var records = new List<LogRecord>();
        ReadChunk(path, state, records, now);

        // A lone header line is complete once the file has been quiet briefly; a half-written JSON block only
        // after a long silence.
        var quietFor = now - state.LastGrowth;
        if (quietFor > TimeSpan.FromSeconds(1) && state.Reader.Flush(force: quietFor > TimeSpan.FromSeconds(10)) is { } pending)
            records.Add(pending);

        ParseInto(events, records, Parser, path, Report, state.Clock);
    }

    // One record at a time: a record the parser throws on is left out, and the ones around it still count. An event
    // is replay when its record began in what the file held at first sight.
    // Each event gets the instant it is put in order by. Its wall-clock time alone won't do: on the night the clocks
    // go back a log runs through 02:00 to 03:00 twice, and sorted by those times a raid's end would come before its
    // start. A log's own times never go backwards, which says which pass a line is in (its file's sequence); a quest
    // message's time is the server's, an instant of its own.
    private static void ParseInto(List<(GameEvent Event, bool Replay, DateTime Instant)> events, IEnumerable<LogRecord> records, Func<LogRecord, GameEvent?> parse,
        string path, Action<string, Exception> problem, WallClock.Sequence clock)
    {
        foreach (var record in records)
        {
            try
            {
                if (parse(record) is { } e)
                    events.Add((e, record.IsReplay, e.At == record.Timestamp ? clock.Next(e.At) : WallClock.Instants(e.At, clock.Zone)[0]));
            }
            catch (Exception e)
            {
                problem("a record in " + Path.GetFileName(path), e);
            }
        }
    }

    private void Report(string what, Exception e)
    {
        ReadProblems++;
        if (!_reported.Add(e.GetType()))
            return;
        try
        {
            ReadProblem?.Invoke(what, e);
        }
        catch (Exception)
        {
            // A listener's own failure is no reason to stop following either.
        }
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

    /// <summary>
    /// Reads a whole session folder at once, e.g. to backfill quest history. It never throws over what a log holds
    /// or over a file or folder it can't read: those are left out, and what was read counts.
    /// </summary>
    /// <param name="problem">Told what was left out and why, once per kind of exception (see <see cref="ReadProblem"/>).</param>
    /// <param name="zone">The time zone of the logs' times (<see cref="Zone"/>); the PC's own when null.</param>
    public static IReadOnlyList<GameEvent> ReadSession(string sessionFolder, Action<string, Exception>? problem = null, TimeZoneInfo? zone = null) =>
        ReadSession(sessionFolder, problem, GameLogParser.Parse, zone);

    internal static IReadOnlyList<GameEvent> ReadSession(string sessionFolder, Action<string, Exception>? problem, Func<LogRecord, GameEvent?> parse,
        TimeZoneInfo? zone = null)
    {
        var events = new List<(GameEvent Event, bool Replay, DateTime Instant)>();
        var reported = new HashSet<Type>();
        void Report(string what, Exception e)
        {
            if (reported.Add(e.GetType()))
                problem?.Invoke(what, e);
        }
        try
        {
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
                ParseInto(events, all, parse, path, Report, new WallClock.Sequence(zone));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The folder went away while it was read (the game tidying up, a drive asleep).
        }
        return events.OrderBy(e => e.Instant).Select(e => e.Event).ToList();
    }

    private sealed class FileState(TimeZoneInfo? zone)
    {
        public long Offset;
        public Decoder Decoder = new UTF8Encoding(false).GetDecoder();
        public LogRecordReader Reader = new();
        public DateTime LastGrowth;

        /// <summary>The instants of this file's lines, for the order of its events among other logs'.</summary>
        public WallClock.Sequence Clock = new(zone);

        /// <summary>The file was in the session found at start: what it holds when first opened is replay.</summary>
        public bool StartsOld;

        /// <summary>Up to this byte the file is replay; null until the file has been opened once.</summary>
        public long? ReplayUntil;

        public void Reset()
        {
            Offset = 0;
            Decoder = new UTF8Encoding(false).GetDecoder();
            Reader = new LogRecordReader();
            Clock = new WallClock.Sequence(zone);
            StartsOld = false;
            ReplayUntil = 0;
        }
    }
}
