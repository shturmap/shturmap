using System.Text;
using Shturmap.Core.Logs;
using Shturmap.Game.Logs;
using Shturmap.Game.Screenshots;
using Shturmap.Game.Settings;

namespace Shturmap.Game.Tests;

public class GameSettingsTests
{
    [Fact]
    public void Reads_screenshot_keys_and_language()
    {
        const string control = """
            { "Version": 1, "keyBindings": [
              { "keyName": "LeanLockRight", "variants": [ { "keyCode": ["E"] }, { "keyCode": [] } ], "pressType": "Continuous" },
              { "keyName": "MakeScreenshot", "variants": [ { "keyCode": ["SysReq"] }, { "keyCode": ["Home"] } ], "pressType": "Press" }
            ] }
            """;
        Assert.Equal(new[] { "PrtSc", "Home" }, GameSettingsReader.ScreenshotKeys(control));
        Assert.Equal("en", GameSettingsReader.Language("""{ "Version": 1, "Language": "en" }"""));
    }

    [Fact]
    public void Unbound_screenshot_key_is_empty()
    {
        const string control = """{ "keyBindings": [ { "keyName": "MakeScreenshot", "variants": [ { "keyCode": [] }, { "keyCode": [] } ] } ] }""";
        Assert.Empty(GameSettingsReader.ScreenshotKeys(control));
        Assert.Empty(GameSettingsReader.ScreenshotKeys("not json"));
    }
}

public sealed class LogTailerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("shturmap-logs-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Session(string name)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Append(string file, string text)
    {
        using var stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        var bytes = Encoding.UTF8.GetBytes(text);
        stream.Write(bytes);
    }

    private static List<LogEvent> Drain(LogTailer tailer)
    {
        var list = new List<LogEvent>();
        while (tailer.Events.TryRead(out var e))
            list.Add(e);
        return list;
    }

    [Fact]
    public async Task Follows_a_growing_log_and_switches_sessions()
    {
        var first = Session("log_2026.01.01_15-00-00_1.1.5.1.47510");
        var app = Path.Combine(first, "2026.01.01_15-00-00_1.1.5.1.47510 application_000.log");
        Append(app, "2026-01-01 15:00:10.000|1.1.5.1.47510|Info|application|Session mode: Pve\r\n");
        Append(app, "2026-01-01 15:18:00.000|1.1.5.1.47510|Info|application|scene preset path:maps/city_preset.bundle rcid:city.scenespreset.asset\r\n");

        await using var tailer = new LogTailer(_root);
        tailer.PollOnce();
        var replayed = Drain(tailer);
        Assert.All(replayed, e => Assert.True(e.IsReplay));
        Assert.IsType<SessionModeEvent>(replayed[0].Event); // the last line is complete once a line follows or after a pause

        // A line split across two writes, including a multi-byte character, arrives once and intact.
        var line = "2026-01-01 15:20:00.000|1.1.5.1.47510|Info|application|GameStarted:117.83(10.06) real:132.11(12) diff:14.27 — ü\r\n";
        var bytes = Encoding.UTF8.GetBytes(line);
        var cut = Array.IndexOf(bytes, (byte)0xC3) + 1; // inside "ü"
        Append(app, Encoding.UTF8.GetString(bytes, 0, 0)); // no-op write
        using (var s = new FileStream(app, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            s.Write(bytes, 0, cut);
        tailer.PollOnce();
        using (var s = new FileStream(app, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            s.Write(bytes, cut, bytes.Length - cut);
        Append(app, "2026-01-01 15:23:00.000|1.1.5.1.47510|Info|application|PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0\r\n");
        tailer.PollOnce();
        var later = Drain(tailer);
        // The scene line was the log's last line at start: only complete now that a line follows it, and replay all
        // the same (as live it gave a RAID LOADING cue for a raid long over). What was written since is live.
        Assert.True(Assert.Single(later, e => e.Event is MapLoadingEvent { ScenePath: "maps/city_preset.bundle" }).IsReplay);
        Assert.False(Assert.Single(later, e => e.Event is GameStartedEvent).IsReplay);
        Assert.Equal(2, later.Count);

        var second = Session("log_2026.01.01_19-00-00_1.1.5.1.47510");
        Append(Path.Combine(second, "2026.01.01_19-00-00_1.1.5.1.47510 application_000.log"),
            "2026-01-01 19:00:10.000|1.1.5.1.47510|Info|application|Session mode: Regular\r\n" +
            "2026-01-01 19:00:11.000|1.1.5.1.47510|Info|application|Application awaken\r\n");
        tailer.PollOnce();
        var next = Drain(tailer);
        Assert.Equal("log_2026.01.01_19-00-00_1.1.5.1.47510", tailer.CurrentSession);
        // The session before is read to its end first: its last line, the raid's end, had nothing after it yet.
        Assert.Equal(2, next.Count);
        Assert.IsType<ProfileLoadedEvent>(next[0].Event);
        Assert.Equal("log_2026.01.01_15-00-00_1.1.5.1.47510", next[0].Session);
        var mode = Assert.IsType<SessionModeEvent>(next[1].Event);
        Assert.Equal(GameMode.Pvp, mode.Mode);
        Assert.Equal("log_2026.01.01_19-00-00_1.1.5.1.47510", next[1].Session);
        Assert.False(next[1].IsReplay);
    }

    [Fact]
    public void Ignores_backend_and_other_logs()
    {
        var dir = Session("log_2026.01.01_15-00-00_1.1.5.1.47510");
        File.WriteAllText(Path.Combine(dir, "2026.01.01_15-00-00_1.1.5.1.47510 backend_000.log"),
            "2026-01-01 15:00:10.000|1.1.5.1.47510|Info|backend|Session mode: Pve\r\nnext\r\n");
        Assert.Empty(LogTailer.ReadSession(dir));
    }
}

public sealed class ScreenshotWatcherTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("shturmap-shots-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task Reports_new_screenshots_but_not_old_ones()
    {
        var folder = Path.Combine(_root, "Screenshots");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "2026-01-01[13-00] (0).png"), "old");

        using var watcher = new ScreenshotWatcher(folder);
        var seen = new List<ScreenshotSeen>();
        var arrived = new TaskCompletionSource();
        watcher.ScreenshotTaken += s => { lock (seen) seen.Add(s); arrived.TrySetResult(); };
        watcher.Start();

        File.WriteAllText(Path.Combine(folder, "2026-01-01[13-35]_40.00, 2.50, 120.00_0.01000, 0.99900, -0.04000, 0.02000_14.13 (0).png"), "new");
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "not a screenshot");
        await arrived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(300);

        var shot = Assert.Single(seen);
        Assert.Equal(new Shturmap.Core.WorldPoint(40.00, 2.50, 120.00), shot.Info.Position);
    }

    [Fact]
    public async Task Waits_for_a_folder_the_game_has_not_created_yet()
    {
        var folder = Path.Combine(_root, "Escape from Tarkov", "Screenshots");
        using var watcher = new ScreenshotWatcher(folder);
        var arrived = new TaskCompletionSource<ScreenshotSeen>();
        watcher.ScreenshotTaken += s => arrived.TrySetResult(s);
        watcher.Start();

        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "2026-01-01[13-30]_11.72 (0).png"), "first");

        var shot = await arrived.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.False(shot.Info.HasPosition);
    }
}
