using System.Text;
using Shturmap.Core.Logs;
using Shturmap.Game.Logs;

namespace Shturmap.Game.Tests;

// When the game starts again, the session before is read to its end first (docs/NEXT.md, review of 2026-10-04, A44:
// a log longer than one read lost its rest), and only what the game writes while it is followed counts as a sign of
// life (A43: reading the last session at start made the LOGS light say "live").
public sealed class LogTailerSessionChangeTests : IDisposable
{
    private const string First = "2026.01.01_15-00-00_1.1.5.1.47510";
    private const string Second = "2026.01.01_19-00-00_1.1.5.1.47510";
    private readonly string _root = Directory.CreateTempSubdirectory("shturmap-logs-").FullName;
    private readonly string _app;
    private DateTime _now = new(2026, 1, 1, 19, 0, 30, DateTimeKind.Utc);

    public LogTailerSessionChangeTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "log_" + First));
        _app = Path.Combine(_root, "log_" + First, First + " application_000.log");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private LogTailer Tailer(int maxRead = 4 * 1024 * 1024) => new(_root) { UtcNow = () => _now, MaxReadPerPoll = maxRead };

    private static string App(string time, string message) => $"2026-01-01 {time}.000|1.1.5.1.47510|Info|application|{message}\r\n";

    private const string Mode = "Session mode: Pve";
    private const string Scene = "scene preset path:maps/city_preset.bundle rcid:city.scenespreset.asset";
    private const string Started = "GameStarted:117.83(10.06) real:132.11(12) diff:14.27";
    private const string Menu = "PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0";

    private static void Append(string file, string text)
    {
        using var stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        stream.Write(Encoding.UTF8.GetBytes(text));
    }

    // The game starts again: a new session folder with its application log.
    private void StartAgain()
    {
        var session = Path.Combine(_root, "log_" + Second);
        Directory.CreateDirectory(session);
        Append(Path.Combine(session, Second + " application_000.log"),
            "2026-01-01 19:00:10.000|1.1.5.1.47510|Info|application|Session mode: Regular\r\n" +
            "2026-01-01 19:00:11.000|1.1.5.1.47510|Info|application|Application awaken\r\n");
    }

    private static List<LogEvent> Drain(LogTailer tailer)
    {
        var list = new List<LogEvent>();
        while (tailer.Events.TryRead(out var e))
            list.Add(e);
        return list;
    }

    private static (string Kind, string Session, bool Replay)[] Seen(IEnumerable<LogEvent> events) =>
        events.Select(e => (e.Event.GetType().Name, e.Session, e.IsReplay)).ToArray();

    [Fact]
    public async Task A_log_longer_than_one_read_is_read_to_its_end_before_the_next_session()
    {
        Append(_app, App("20:59:37", Mode) + App("21:18:40", Scene) + App("21:20:49", Started) + App("21:40:00", Menu));
        await using var tailer = Tailer(maxRead: 64);

        // One read of 64 bytes, and the game has started again before the rest is read.
        tailer.PollOnce();
        StartAgain();
        tailer.PollOnce();
        Assert.Equal("log_" + Second, tailer.CurrentSession);
        // The new session's own log takes a few reads of that size.
        for (var i = 0; i < 4; i++)
            tailer.PollOnce();

        Assert.Equal(
            new[]
            {
                (nameof(SessionModeEvent), "log_" + First, true), (nameof(MapLoadingEvent), "log_" + First, true),
                (nameof(GameStartedEvent), "log_" + First, true), (nameof(ProfileLoadedEvent), "log_" + First, true),
                (nameof(SessionModeEvent), "log_" + Second, false),
            },
            Seen(Drain(tailer)));
        Assert.Equal("log_" + Second, tailer.CurrentSession);
    }

    [Fact]
    public async Task The_last_line_of_the_session_before_is_not_lost()
    {
        Append(_app, App("20:59:37", Mode) + App("20:59:38", "first"));
        await using var tailer = Tailer();
        tailer.PollOnce();
        Drain(tailer);

        // The raid's end is the old session's last line: nothing follows it, and the game starts again at once.
        Append(_app, App("21:40:00", Menu));
        tailer.PollOnce();
        Assert.Empty(Drain(tailer));
        StartAgain();
        tailer.PollOnce();

        Assert.Equal(
            new[] { (nameof(ProfileLoadedEvent), "log_" + First, false), (nameof(SessionModeEvent), "log_" + Second, false) },
            Seen(Drain(tailer)));
    }

    [Fact]
    public async Task A_file_added_to_the_session_before_since_the_last_poll_is_read_too()
    {
        Append(_app, App("20:59:37", Mode) + App("20:59:38", "last"));
        await using var tailer = Tailer();
        tailer.PollOnce();
        Drain(tailer);

        Append(Path.Combine(_root, "log_" + First, First + " push-notifications_000.log"),
            "2026-01-01 15:01:00.000|1.1.5.1.47510|Info|push-notifications|Got notification | GroupMatchRaidReady\r\n");
        StartAgain();
        tailer.PollOnce();

        Assert.Equal(
            new[] { (nameof(GroupStatusEvent), "log_" + First, false), (nameof(SessionModeEvent), "log_" + Second, false) },
            Seen(Drain(tailer)));
    }

    [Fact]
    public async Task A_busy_file_of_the_session_before_puts_the_next_one_off_for_a_few_polls_only()
    {
        Append(_app, App("20:59:37", Mode) + App("20:59:38", "first"));
        await using var tailer = Tailer();
        tailer.PollOnce();
        Drain(tailer);
        Append(_app, App("21:40:00", Menu));
        StartAgain();

        // Something else holds the old log: the new session waits, and is followed once the old one could be read.
        using (new FileStream(_app, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            tailer.PollOnce();
            Assert.Equal("log_" + First, tailer.CurrentSession);
            Assert.Empty(Drain(tailer));
        }
        tailer.PollOnce();
        Assert.Equal(
            new[] { (nameof(ProfileLoadedEvent), "log_" + First, false), (nameof(SessionModeEvent), "log_" + Second, false) },
            Seen(Drain(tailer)));
    }

    [Fact]
    public async Task A_file_that_stays_busy_does_not_hold_the_next_session_up_for_good()
    {
        Append(_app, App("20:59:37", Mode) + App("20:59:38", "first"));
        await using var tailer = Tailer();
        tailer.PollOnce();
        Drain(tailer);
        StartAgain();

        using (new FileStream(_app, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            for (var i = 0; i < LogTailer.PutOffAtMost; i++)
            {
                tailer.PollOnce();
                Assert.Equal("log_" + First, tailer.CurrentSession);
            }
            tailer.PollOnce();
        }
        Assert.Equal("log_" + Second, tailer.CurrentSession);
        Assert.Equal(new[] { (nameof(SessionModeEvent), "log_" + Second, false) }, Seen(Drain(tailer)));
    }

    [Fact]
    public async Task A_session_folder_that_is_gone_has_nothing_left_to_read()
    {
        Append(_app, App("20:59:37", Mode) + App("20:59:38", "first"));
        await using var tailer = Tailer();
        tailer.PollOnce();
        Drain(tailer);

        StartAgain();
        Directory.Delete(Path.Combine(_root, "log_" + First), recursive: true);
        tailer.PollOnce();

        Assert.Equal(new[] { (nameof(SessionModeEvent), "log_" + Second, false) }, Seen(Drain(tailer)));
    }

    // ---- what counts as a sign of life ----

    [Fact]
    public async Task Reading_what_the_log_held_at_start_is_no_sign_of_a_running_game()
    {
        Append(_app, App("20:59:37", Mode) + App("21:18:40", Scene) + App("21:20:49", Started));
        await using var tailer = Tailer(maxRead: 64);
        for (var i = 0; i < 8; i++)
            tailer.PollOnce();
        _now += TimeSpan.FromSeconds(2);
        tailer.PollOnce();

        Assert.Equal(3, Drain(tailer).Count);
        Assert.Null(tailer.LastActivityUtc);
    }

    [Fact]
    public async Task A_line_the_game_writes_while_it_is_followed_is()
    {
        Append(_app, App("20:59:37", Mode) + App("20:59:38", "last"));
        await using var tailer = Tailer();
        tailer.PollOnce();
        Assert.Null(tailer.LastActivityUtc);

        _now += TimeSpan.FromMinutes(3);
        Append(_app, App("21:20:49", "anything at all"));
        tailer.PollOnce();
        Assert.Equal(_now, tailer.LastActivityUtc);
    }

    [Fact]
    public async Task So_is_a_session_the_game_starts_later()
    {
        Append(_app, App("20:59:37", Mode) + App("20:59:38", "last"));
        await using var tailer = Tailer();
        tailer.PollOnce();
        Assert.Null(tailer.LastActivityUtc);

        StartAgain();
        tailer.PollOnce();
        Assert.Equal(_now, tailer.LastActivityUtc);
    }
}
