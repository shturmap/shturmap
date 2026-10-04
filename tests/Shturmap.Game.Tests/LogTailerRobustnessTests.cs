using System.Text;
using Shturmap.Core.Logs;
using Shturmap.Game.Install;
using Shturmap.Game.Logs;
using Shturmap.Game.Settings;

namespace Shturmap.Game.Tests;

// Nothing a log holds may end the following (docs/NEXT.md, review of 2026-10-04, A9): the log isn't a documented
// format, and one record the parser threw on used to end the tail loop, and the backfill, without a sign.
public sealed class LogTailerRobustnessTests : IDisposable
{
    private const string Name = "2026.01.01_15-00-00_1.1.5.1.47510";
    private readonly string _root = Directory.CreateTempSubdirectory("shturmap-logs-").FullName;
    private readonly string _session;
    private readonly string _app;
    private readonly string _push;

    public LogTailerRobustnessTests()
    {
        _session = Path.Combine(_root, "log_" + Name);
        Directory.CreateDirectory(_session);
        _app = Path.Combine(_session, Name + " application_000.log");
        _push = Path.Combine(_session, Name + " push-notifications_000.log");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string App(string time, string message) => $"2026-01-01 {time}.000|1.1.5.1.47510|Info|application|{message}\r\n";

    private static string Push(string time, string message) => $"2026-01-01 {time}.000|1.1.5.1.47510|Info|push-notifications|{message}\r\n";

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

    // Stands in for a record of a shape the parser doesn't expect.
    private static GameEvent? ThrowsOnBoom(LogRecord record) =>
        record.Message.StartsWith("boom", StringComparison.Ordinal) ? throw new InvalidOperationException("odd shape") : GameLogParser.Parse(record);

    [Fact]
    public async Task A_record_the_parser_throws_on_is_left_out_and_the_rest_arrives()
    {
        Append(_app, App("20:59:37", "Session mode: Pve") + App("21:00:00", "boom one") +
                     App("21:20:49", "GameStarted:117.83(10.06) real:132.11(12) diff:14.27") + App("21:20:50", "boom two") + App("21:20:51", "last"));
        var problems = new List<(string What, Exception Why)>();
        await using var tailer = new LogTailer(_root) { Parser = ThrowsOnBoom };
        tailer.ReadProblem += (what, why) => problems.Add((what, why));

        tailer.PollOnce();

        Assert.Equal(new[] { typeof(SessionModeEvent), typeof(GameStartedEvent) }, Drain(tailer).Select(e => e.Event.GetType()).ToArray());
        // Said once per kind of exception, by the file's name and never by the record's text.
        var (what, why) = Assert.Single(problems);
        Assert.Equal("a record in " + Name + " application_000.log", what);
        Assert.IsType<InvalidOperationException>(why);
        Assert.Equal(2, tailer.ReadProblems);

        // And the following goes on.
        Append(_app, App("21:23:01", "PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0") + App("21:23:02", "last"));
        tailer.PollOnce();
        Assert.Contains(Drain(tailer), e => e.Event is ProfileLoadedEvent);
    }

    [Fact]
    public async Task A_listener_that_throws_does_not_end_the_poll()
    {
        Append(_app, App("20:59:37", "boom") + App("20:59:38", "Session mode: Pve") + App("20:59:39", "last"));
        await using var tailer = new LogTailer(_root) { Parser = ThrowsOnBoom };
        tailer.ReadProblem += (_, _) => throw new InvalidOperationException("the listener's own");

        Assert.Equal(1, tailer.PollOnce());
        Assert.IsType<SessionModeEvent>(Assert.Single(Drain(tailer)).Event);
    }

    [Fact]
    public async Task What_one_file_gave_is_passed_on_when_the_next_cannot_be_opened()
    {
        Append(_app, App("20:59:37", "Session mode: Pve") + App("20:59:38", "last"));
        Append(_push, Push("21:00:00", "Got notification | GroupMatchRaidReady") + Push("21:00:01", "last"));
        await using var tailer = new LogTailer(_root);

        // Another program holds the second file for itself for a moment.
        using (new FileStream(_push, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Equal(1, tailer.PollOnce());
            Assert.IsType<SessionModeEvent>(Assert.Single(Drain(tailer)).Event);
        }

        tailer.PollOnce();
        Assert.IsType<GroupStatusEvent>(Assert.Single(Drain(tailer)).Event);
    }

    [Fact]
    public async Task The_loop_goes_on_after_a_poll_that_failed()
    {
        Append(_app, App("20:59:37", "Session mode: Pve") + App("20:59:38", "last"));
        var calls = 0;
        var problems = new List<string>();
        await using var tailer = new LogTailer(_root)
        {
            // Not a file error: the first poll fails the way a bug would.
            UtcNow = () => Interlocked.Increment(ref calls) == 1 ? throw new InvalidOperationException("first poll") : DateTime.UtcNow,
        };
        tailer.ReadProblem += (what, _) => { lock (problems) problems.Add(what); };
        tailer.Start();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var first = await tailer.Events.ReadAsync(timeout.Token);

        Assert.IsType<SessionModeEvent>(first.Event);
        lock (problems)
            Assert.Equal(new[] { "a poll of the game's logs" }, problems);
    }

    [Fact]
    public void Reading_a_whole_session_leaves_out_what_it_cannot_read()
    {
        Append(_app, App("20:59:37", "Session mode: Pve") + App("21:00:00", "boom one") + App("21:00:01", "boom two") +
                     App("21:20:49", "GameStarted:117.83(10.06) real:132.11(12) diff:14.27"));
        Append(_push, Push("21:00:00", "Got notification | GroupMatchRaidReady"));
        var problems = new List<string>();

        IReadOnlyList<GameEvent> events;
        using (new FileStream(_push, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            events = LogTailer.ReadSession(_session, (what, _) => problems.Add(what), ThrowsOnBoom);

        Assert.Equal(new[] { typeof(SessionModeEvent), typeof(GameStartedEvent) }, events.Select(e => e.GetType()).ToArray());
        Assert.Equal(new[] { "a record in " + Name + " application_000.log" }, problems);
        // A session folder that is gone gives nothing, not an exception.
        Assert.Empty(LogTailer.ReadSession(Path.Combine(_root, "log_2026.01.01_00-00-00_1.0.0.0.1")));
    }
}

public class OddSettingsTests
{
    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("""{ "keyBindings": { "keyName": "MakeScreenshot" } }""")]
    [InlineData("""{ "keyBindings": [ 5, "x", null, { "keyName": 5 }, { "keyName": null } ] }""")]
    public void Settings_of_another_shape_give_no_keys_and_no_language(string json)
    {
        Assert.Empty(GameSettingsReader.ScreenshotKeys(json));
        Assert.Null(GameSettingsReader.Language(json));
    }

    [Fact]
    public void Odd_entries_beside_the_screenshot_key_are_skipped()
    {
        const string control = """
            { "keyBindings": [ 7, { "keyName": "MakeScreenshot", "variants": [ 3, "x", { "keyCode": "Home" }, { "keyCode": [ 7, null, "Home" ] } ] } ] }
            """;
        Assert.Equal(new[] { "Home" }, GameSettingsReader.ScreenshotKeys(control));
        Assert.Null(GameSettingsReader.Language("""{ "Language": 7 }"""));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"C:\\\\Games\"")]
    [InlineData("null")]
    [InlineData("""{ "gamesRootDir": { "path": "C:\\Games" } }""")]
    public void Launcher_settings_of_another_shape_name_no_folder(string json) =>
        Assert.Null(InstallLocator.LauncherGamesRoot(json));
}
