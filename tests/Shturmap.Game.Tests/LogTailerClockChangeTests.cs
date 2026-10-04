using System.Text;
using Shturmap.Core.Logs;
using Shturmap.Game.Logs;

namespace Shturmap.Game.Tests;

// On the night the clocks go back a log runs through 02:00 to 03:00 twice. Its events were put in order by their
// wall-clock times, so a raid's end at 02:10 came before its start at 02:50 (review of 2026-10-04, A41). They go by
// the instants of their lines now: a log's own times never go backwards. Central Europe's zone is given, not the
// PC's: there the clocks went back on 25 October 2026.
public sealed class LogTailerClockChangeTests : IDisposable
{
    private const string Session = "2026.10.25_01-58-00_1.1.5.1.47510";
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    private readonly string _root = Directory.CreateTempSubdirectory("shturmap-logs-").FullName;
    private readonly string _folder;

    public LogTailerClockChangeTests()
    {
        _folder = Path.Combine(_root, "log_" + Session);
        Directory.CreateDirectory(_folder);
        // Written in this order: the mode before the change, a raid loaded and started in the first pass through the
        // hour, and its end in the second.
        File.WriteAllText(Path.Combine(_folder, Session + " application_000.log"),
            Line("01:59:00", "Session mode: Pve") +
            Line("02:47:00", "scene preset path:maps/city_preset.bundle rcid:city.scenespreset.asset") +
            Line("02:50:00", "GameStarted:117.83(10.06) real:132.11(12) diff:14.27") +
            Line("02:10:00", "PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0") +
            Line("03:20:00", "Session mode: Regular"),
            new UTF8Encoding(false));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string Line(string time, string message) => $"2026-10-25 {time}.000|1.1.5.1.47510|Info|application|{message}\r\n";

    private static readonly string[] AsWritten =
        [nameof(SessionModeEvent), nameof(MapLoadingEvent), nameof(GameStartedEvent), nameof(ProfileLoadedEvent), nameof(SessionModeEvent)];

    [Fact]
    public void A_session_read_back_keeps_its_events_in_the_order_they_were_written()
    {
        var events = LogTailer.ReadSession(_folder, null, Zone);
        Assert.Equal(AsWritten, events.Select(e => e.GetType().Name).ToArray());
    }

    [Fact]
    public async Task A_session_followed_keeps_its_events_in_the_order_they_were_written()
    {
        var now = new DateTime(2026, 10, 25, 2, 30, 0, DateTimeKind.Utc);
        await using var tailer = new LogTailer(_root) { Zone = Zone, UtcNow = () => now };
        tailer.PollOnce();
        // The last line is only known to be whole once the log has been quiet a moment.
        now = now.AddSeconds(2);
        tailer.PollOnce();
        var events = new List<LogEvent>();
        while (tailer.Events.TryRead(out var e))
            events.Add(e);
        Assert.Equal(AsWritten, events.Select(e => e.Event.GetType().Name).ToArray());
    }
}
