using System.Text;
using Shturmap.Core.Logs;
using Shturmap.Game.Logs;

namespace Shturmap.Game.Tests;

// What the logs hold when Shturmap starts following them is replay, all of it (docs/NEXT.md, review of 2026-10-04,
// A9): replay sets the state and shows no cue, notice or ping, so an old raid flagged live would announce itself.
public sealed class LogTailerReplayTests : IDisposable
{
    private const string Name = "2026.01.01_15-00-00_1.1.5.1.47510";
    private readonly string _root = Directory.CreateTempSubdirectory("shturmap-logs-").FullName;
    private readonly string _app;
    private readonly string _push;
    private DateTime _now = new(2026, 1, 1, 15, 30, 0, DateTimeKind.Utc);

    public LogTailerReplayTests()
    {
        var session = Path.Combine(_root, "log_" + Name);
        Directory.CreateDirectory(session);
        _app = Path.Combine(session, Name + " application_000.log");
        _push = Path.Combine(session, Name + " push-notifications_000.log");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private LogTailer Tailer(int maxRead = 4 * 1024 * 1024) => new(_root) { UtcNow = () => _now, MaxReadPerPoll = maxRead };

    private static string App(string time, string message) => $"2026-01-01 {time}.000|1.1.5.1.47510|Info|application|{message}\r\n";

    private static string Push(string time, string message) => $"2026-01-01 {time}.000|1.1.5.1.47510|Info|push-notifications|{message}\r\n";

    private const string Mode = "Session mode: Pve";
    private const string Scene = "scene preset path:maps/city_preset.bundle rcid:city.scenespreset.asset";
    private const string Started = "GameStarted:117.83(10.06) real:132.11(12) diff:14.27";
    private const string Menu = "PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0";

    private static void Append(string file, string text)
    {
        using var stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        stream.Write(Encoding.UTF8.GetBytes(text));
    }

    private static List<LogEvent> Drain(LogTailer tailer)
    {
        var list = new List<LogEvent>();
        while (tailer.Events.TryRead(out var e))
            list.Add(e);
        return list;
    }

    private static (string Kind, bool Replay)[] Flags(IEnumerable<LogEvent> events) =>
        events.Select(e => (e.Event.GetType().Name, e.IsReplay)).ToArray();

    [Fact]
    public async Task The_last_line_at_start_is_replay_though_it_is_only_complete_after_a_pause()
    {
        Append(_app, App("20:59:37", Mode) + App("21:18:40", Scene));
        await using var tailer = Tailer();

        tailer.PollOnce();
        Assert.Equal(new[] { (nameof(SessionModeEvent), true) }, Flags(Drain(tailer)));

        // Nothing follows the scene line: it counts as complete once the log has been quiet for a second.
        _now += TimeSpan.FromSeconds(2);
        tailer.PollOnce();
        Assert.Equal(new[] { (nameof(MapLoadingEvent), true) }, Flags(Drain(tailer)));
    }

    [Fact]
    public async Task A_line_written_after_the_start_is_live()
    {
        Append(_app, App("20:59:37", Mode) + App("21:18:40", Scene));
        await using var tailer = Tailer();
        tailer.PollOnce();
        Drain(tailer);

        Append(_app, App("21:20:49", Started));
        tailer.PollOnce();
        // The new line completes the old last line, which stays replay.
        Assert.Equal(new[] { (nameof(MapLoadingEvent), true) }, Flags(Drain(tailer)));
        _now += TimeSpan.FromSeconds(2);
        tailer.PollOnce();
        Assert.Equal(new[] { (nameof(GameStartedEvent), false) }, Flags(Drain(tailer)));
    }

    [Fact]
    public async Task A_log_longer_than_one_read_is_replay_up_to_the_length_it_had_at_start()
    {
        var old = App("20:59:37", Mode) + App("21:18:40", Scene) + App("21:20:49", Started) + App("21:40:00", Menu) + App("21:50:00", Scene);
        Append(_app, old);
        await using var tailer = Tailer(maxRead: 64);

        // The first read takes 64 bytes of the old text; the game writes on before the rest is read.
        tailer.PollOnce();
        Append(_app, App("21:52:00", Started) + App("22:10:00", Menu));
        for (var i = 0; i < 2 * (old.Length / 64 + 2) + 8; i++)
            tailer.PollOnce();
        _now += TimeSpan.FromSeconds(2);
        tailer.PollOnce();

        Assert.Equal(
            new[]
            {
                (nameof(SessionModeEvent), true), (nameof(MapLoadingEvent), true), (nameof(GameStartedEvent), true),
                (nameof(ProfileLoadedEvent), true), (nameof(MapLoadingEvent), true), // the old text's last line
                (nameof(GameStartedEvent), false), (nameof(ProfileLoadedEvent), false),
            },
            Flags(Drain(tailer)));
    }

    [Fact]
    public async Task A_file_that_was_busy_at_the_first_poll_is_replay_when_it_can_be_read()
    {
        Append(_app, App("20:59:37", Mode) + App("20:59:38", "last"));
        Append(_push, Push("21:00:00", "Got notification | GroupMatchRaidReady") + Push("21:00:01", "last"));
        await using var tailer = Tailer();

        using (new FileStream(_push, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            tailer.PollOnce();
        tailer.PollOnce();
        Assert.Equal(new[] { (nameof(SessionModeEvent), true), (nameof(GroupStatusEvent), true) }, Flags(Drain(tailer)));

        Append(_push, Push("21:05:00", "Got notification | GroupMatchStartGame") + Push("21:05:01", "last"));
        tailer.PollOnce();
        Assert.Equal(new[] { (nameof(GroupStatusEvent), false) }, Flags(Drain(tailer)));
    }

    [Fact]
    public async Task A_file_the_game_adds_to_the_session_found_at_start_is_live()
    {
        Append(_app, App("20:59:37", Mode) + App("20:59:38", "last"));
        await using var tailer = Tailer();
        tailer.PollOnce();
        Drain(tailer);

        Append(_push, Push("21:00:00", "Got notification | GroupMatchRaidReady") + Push("21:00:01", "last"));
        tailer.PollOnce();
        Assert.Equal(new[] { (nameof(GroupStatusEvent), false) }, Flags(Drain(tailer)));
    }

    [Fact]
    public async Task A_session_the_game_starts_later_is_live_from_its_first_line()
    {
        Append(_app, App("20:59:37", Mode) + App("20:59:38", "last"));
        await using var tailer = Tailer();
        tailer.PollOnce();

        const string next = "2026.01.01_19-00-00_1.1.5.1.47510";
        var session = Path.Combine(_root, "log_" + next);
        Directory.CreateDirectory(session);
        Append(Path.Combine(session, next + " application_000.log"),
            "2026-01-01 19:00:10.000|1.1.5.1.47510|Info|application|Session mode: Regular\r\n" +
            "2026-01-01 19:00:11.000|1.1.5.1.47510|Info|application|Application awaken\r\n");
        tailer.PollOnce();
        tailer.PollOnce();

        Assert.Equal(new[] { (nameof(SessionModeEvent), true), (nameof(SessionModeEvent), false) }, Flags(Drain(tailer)));
    }
}
