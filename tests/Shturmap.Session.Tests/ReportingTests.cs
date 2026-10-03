using System.Net;
using System.Text.Json;
using Sentry.Protocol.Envelopes;
using Shturmap.Session.Reporting;

namespace Shturmap.Session.Tests;

// Reports and crash reports (owner, 2026-10-03; docs/DESIGN.md §8, "Reports"): one way out, only on the player's word.
public class ReportingTests : IDisposable
{
    private const string Profile = @"C:\Users\tester";
    private static readonly ReportInfo Info = new("0.1.0+abc1234", "single exe", "Windows 11 (10.0.26200)");

    private readonly string _root = Directory.CreateTempSubdirectory("shturmap-reports-").FullName;

    public void Dispose() => Directory.Delete(_root, true);

    private Reporter NewReporter(string? dsn, bool muted = false) =>
        new(_root, ReportEndpoint.Parse(dsn), muted, Info, new HttpClient { Timeout = TimeSpan.FromSeconds(10) }, Profile);

    private static UserReport Report(string? diagnostics = "Shturmap diagnostics\nScreenshots: C:\\Users\\tester\\Documents") =>
        new("a1b2c3d4", new DateTime(2026, 10, 3, 12, 0, 0), ReportKind.Problem, "The map stayed on Woods.", "someone#1234", diagnostics);

    private static CrashRecord Thrown(bool fatal = true, string message = @"Failed at C:\Users\tester\Documents\x.png for 5f2a9b3c4d5e6f708192a3b4")
    {
        try
        {
            throw new InvalidOperationException(message, new IOException("disk"));
        }
        catch (InvalidOperationException e)
        {
            return CrashRecords.FromException(e, "session1", "ui", fatal, Info, [@"2026-10-03 12:00:00.000 INFO Screenshots: C:\Users\tester\Pictures"],
                new DateTime(2026, 10, 3, 12, 0, 0), Profile);
        }
    }

    // ---- the setting and the policy ----

    [Fact]
    public void Crash_reports_ask_by_default()
    {
        Assert.Equal(CrashMode.Ask, CrashModes.Parse(null));
        Assert.Equal(CrashMode.Ask, CrashModes.Parse("ask"));
        Assert.Equal(CrashMode.Always, CrashModes.Parse("always"));
        Assert.Equal(CrashMode.Never, CrashModes.Parse("never"));
        Assert.Equal("always", CrashModes.Format(CrashMode.Always));
    }

    [Theory]
    [InlineData(CrashMode.Ask, 1, true, false, CrashAction.Ask)]
    [InlineData(CrashMode.Always, 2, true, false, CrashAction.Send)]
    [InlineData(CrashMode.Never, 1, true, false, CrashAction.Keep)]
    [InlineData(CrashMode.Ask, 0, true, false, CrashAction.None)]
    [InlineData(CrashMode.Always, 1, true, true, CrashAction.None)]
    [InlineData(CrashMode.Ask, 1, false, false, CrashAction.Keep)]
    public void The_start_asks_sends_or_keeps_as_the_setting_says(CrashMode mode, int pending, bool configured, bool muted, CrashAction expected) =>
        Assert.Equal(expected, CrashPolicy.Decide(mode, pending, configured, muted));

    [Fact]
    public void A_report_needs_text_and_stays_short()
    {
        Assert.NotNull(UserReport.Invalid("  ", ""));
        Assert.NotNull(UserReport.Invalid(new string('x', UserReport.MaxText + 1), ""));
        Assert.NotNull(UserReport.Invalid("ok", new string('x', UserReport.MaxContact + 1)));
        Assert.Null(UserReport.Invalid("The map stayed on Woods.", ""));
    }

    // ---- crash records ----

    [Fact]
    public void A_crash_record_is_masked_and_says_where_in_the_code()
    {
        var record = Thrown();
        Assert.Equal(2, record.Exceptions.Count);
        var outer = record.Exceptions[0];
        Assert.Equal("System.InvalidOperationException", outer.Type);
        Assert.Equal(@"Failed at %USERPROFILE%\Documents\x.png for <id>", outer.Message);
        Assert.Contains(outer.Frames, f => f.InApp && f.Function.EndsWith("ReportingTests.Thrown", StringComparison.Ordinal));
        Assert.All(outer.Frames, f => Assert.True(f.File is null || !f.File.Contains('\\')));
        Assert.Equal(@"2026-10-03 12:00:00.000 INFO Screenshots: %USERPROFILE%\Pictures", Assert.Single(record.LogTail));
        Assert.Equal("InvalidOperationException in Shturmap.Session.Tests.ReportingTests.Thrown", record.Summary);
    }

    [Fact]
    public async Task An_async_frame_is_named_by_its_method()
    {
        static async Task FailAsync()
        {
            await Task.Yield();
            throw new InvalidOperationException("x");
        }
        var e = await Assert.ThrowsAsync<InvalidOperationException>(FailAsync);
        Assert.Contains(CrashRecords.Frames(e), f => f.Function.Contains("FailAsync", StringComparison.Ordinal) && !f.Function.Contains("MoveNext"));
    }

    [Fact]
    public void An_exception_is_written_once_and_errors_the_app_survives_at_most_three_times()
    {
        var records = new CrashRecords(Path.Combine(_root, "crashes"), Profile);
        var e = new InvalidOperationException("boom");
        Assert.NotNull(records.Record(e, "ui", true, Info, [], DateTime.Now));
        Assert.Null(records.Record(e, "background", true, Info, [], DateTime.Now));
        for (var i = 0; i < 5; i++)
            records.Record(new ArgumentException("arg " + i), "task", false, Info, [], DateTime.Now);
        // The same error (by type and place) counts once; different ones up to three.
        Assert.Equal(2, records.All().Count);
        for (var i = 0; i < 5; i++)
        {
            try
            {
                throw i switch { 0 => new KeyNotFoundException(), 1 => new FormatException(), 2 => new TimeoutException(), _ => new NotSupportedException() };
            }
            catch (Exception ex)
            {
                records.Record(ex, "task", false, Info, [], DateTime.Now);
            }
        }
        Assert.Equal(1 + 3, records.All().Count);
    }

    [Fact]
    public void Records_wait_until_answered_and_are_pruned()
    {
        var records = new CrashRecords(Path.Combine(_root, "crashes"), Profile);
        var a = Thrown();
        var b = Thrown() with { Id = "bbbbbbbb", At = a.At.AddSeconds(1) };
        records.Write(a);
        records.Write(b);
        Assert.Equal(2, records.Waiting().Count);
        records.SetState(a, CrashState.Kept);
        Assert.Equal("bbbbbbbb", Assert.Single(records.Waiting()).Id);
        records.Remove(b);
        Assert.Empty(records.Waiting());
        records.Write(a with { At = DateTime.Now.AddDays(-CrashRecords.KeepDays - 1), Id = "cccccccc" });
        records.Prune(DateTime.Now);
        Assert.DoesNotContain(records.All(), r => r.Id == "cccccccc");
    }

    // ---- the running marker ----

    [Fact]
    public void A_marker_nobody_holds_is_an_unexpected_exit_with_the_last_log_lines()
    {
        var folder = Path.Combine(_root, "crashes");
        Directory.CreateDirectory(folder);
        var started = DateTime.Now.AddMinutes(-30);
        File.WriteAllText(Path.Combine(folder, "running-gone.json"),
            JsonSerializer.Serialize(new { Session = "gone", Started = started, Version = "0.0.9", Build = "folder build", Windows = "Windows 11" }));
        var records = new CrashRecords(folder, Profile);
        var found = records.CollectUnexpectedExits(DateTime.Now, DateTime.Now.AddDays(-1), (from, to) => [@"last line C:\Users\tester\x"]);
        var exit = Assert.Single(found);
        Assert.True(exit.UnexpectedExit);
        Assert.Equal("0.0.9", exit.Version);
        Assert.Equal(@"last line %USERPROFILE%\x", Assert.Single(exit.LogTail));
        Assert.False(File.Exists(Path.Combine(folder, "running-gone.json")));
        Assert.Single(records.Waiting());
    }

    [Fact]
    public void A_running_session_a_shutdown_and_a_recorded_crash_are_not_unexpected_exits()
    {
        var folder = Path.Combine(_root, "crashes");
        var running = new CrashRecords(folder, Profile);
        using var marker = running.MarkRunning(DateTime.Now, Info);
        Assert.NotNull(marker);
        // Before Windows last started: the PC was shut down while Shturmap ran.
        File.WriteAllText(Path.Combine(folder, "running-old.json"),
            JsonSerializer.Serialize(new { Session = "old", Started = DateTime.Now.AddDays(-2), Version = "v", Build = "b", Windows = "w" }));
        // That session's crash is already recorded.
        running.Write(Thrown() with { Session = "crashed" });
        File.WriteAllText(Path.Combine(folder, "running-crashed.json"),
            JsonSerializer.Serialize(new { Session = "crashed", Started = DateTime.Now.AddMinutes(-5), Version = "v", Build = "b", Windows = "w" }));

        var next = new CrashRecords(folder, Profile);
        Assert.Empty(next.CollectUnexpectedExits(DateTime.Now, DateTime.Now.AddDays(-1), (_, _) => []));
        Assert.True(File.Exists(marker!.Path));
        marker.Dispose();
        Assert.False(File.Exists(marker.Path));
    }

    // ---- what goes out ----

    [Theory]
    [InlineData("https://abc123@o4500.ingest.de.sentry.io/4501", "https://o4500.ingest.de.sentry.io/api/4501/envelope/", "abc123")]
    [InlineData("http://testkey@127.0.0.1:5000/1", "http://127.0.0.1:5000/api/1/envelope/", "testkey")]
    public void The_dsn_names_the_envelope_address_and_key(string dsn, string envelope, string key)
    {
        var endpoint = ReportEndpoint.Parse(dsn)!;
        Assert.Equal(envelope, endpoint.Envelope.ToString());
        Assert.Equal(key, endpoint.PublicKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a dsn")]
    [InlineData("http://abc@o1.ingest.de.sentry.io/2")]
    [InlineData("https://abc@example.com/2")]
    [InlineData("https://o1.ingest.de.sentry.io/2")]
    public void Only_sentry_over_https_or_this_pc_is_a_report_address(string? dsn) => Assert.Null(ReportEndpoint.Parse(dsn));

    private static async Task<byte[]> Bytes(Envelope envelope)
    {
        using var stream = new MemoryStream();
        await envelope.SerializeAsync(stream, null);
        return stream.ToArray();
    }

    // The envelope's items: each header with its payload.
    private static List<(JsonElement Header, byte[] Payload)> Items(byte[] raw)
    {
        var items = new List<(JsonElement, byte[])>();
        var pos = Array.IndexOf(raw, (byte)'\n') + 1;
        while (pos > 0 && pos < raw.Length)
        {
            var end = Array.IndexOf(raw, (byte)'\n', pos);
            if (end < 0)
                break;
            var header = JsonDocument.Parse(raw.AsMemory(pos, end - pos)).RootElement.Clone();
            pos = end + 1;
            var length = header.TryGetProperty("length", out var l) ? l.GetInt32()
                : Array.IndexOf(raw, (byte)'\n', pos) is var next and >= 0 ? next - pos : raw.Length - pos;
            items.Add((header, raw[pos..(pos + length)]));
            pos += length + 1;
        }
        return items;
    }

    private static void HasNothingPersonal(string json)
    {
        Assert.DoesNotContain("server_name", json);
        Assert.DoesNotContain("ip_address", json);
        Assert.DoesNotContain("\"user\"", json);
        Assert.DoesNotContain("tester", json);
        Assert.DoesNotMatch(@"\b[0-9a-fA-F]{24}\b", json);
    }

    [Fact]
    public async Task A_problem_goes_as_feedback_with_its_diagnostics_attached()
    {
        var items = Items(await Bytes(ReportEnvelopes.Feedback(Report(Redact.Text("Shturmap diagnostics\nScreenshots: C:\\Users\\tester\\Documents", Profile)), Info)));
        var (header, payload) = Assert.Single(items, i => i.Header.GetProperty("type").GetString() == "feedback");
        var json = System.Text.Encoding.UTF8.GetString(payload);
        var feedback = JsonDocument.Parse(json).RootElement;
        Assert.Equal("The map stayed on Woods.", feedback.GetProperty("contexts").GetProperty("feedback").GetProperty("message").GetString());
        Assert.Equal("someone#1234", feedback.GetProperty("contexts").GetProperty("feedback").GetProperty("name").GetString());
        var tags = feedback.GetProperty("tags");
        Assert.Equal("problem", tags.GetProperty("kind").GetString());
        Assert.Equal("a1b2c3d4", tags.GetProperty("report").GetString());
        Assert.Equal("shturmap@0.1.0+abc1234", feedback.GetProperty("release").GetString());
        HasNothingPersonal(json);
        var attachment = Assert.Single(items, i => i.Header.GetProperty("type").GetString() == "attachment");
        Assert.Equal("diagnostics.txt", attachment.Header.GetProperty("filename").GetString());
        Assert.Contains("%USERPROFILE%", System.Text.Encoding.UTF8.GetString(attachment.Payload));
    }

    [Fact]
    public async Task An_idea_without_diagnostics_has_no_attachment_and_an_email_contact()
    {
        var report = Report(diagnostics: null) with { Kind = ReportKind.Idea, Contact = "me@example.org" };
        var items = Items(await Bytes(ReportEnvelopes.Feedback(report, Info)));
        Assert.DoesNotContain(items, i => i.Header.GetProperty("type").GetString() == "attachment");
        var feedback = JsonDocument.Parse(Assert.Single(items).Payload).RootElement;
        Assert.Equal("idea", feedback.GetProperty("tags").GetProperty("kind").GetString());
        Assert.Equal("me@example.org", feedback.GetProperty("contexts").GetProperty("feedback").GetProperty("contact_email").GetString());
    }

    [Fact]
    public async Task A_crash_goes_as_an_event_cause_first_caller_first_with_its_log()
    {
        var record = Thrown();
        var items = Items(await Bytes(ReportEnvelopes.Crash(record)));
        var evt = JsonDocument.Parse(Assert.Single(items, i => i.Header.GetProperty("type").GetString() == "event").Payload).RootElement;
        Assert.Equal("fatal", evt.GetProperty("level").GetString());
        Assert.Equal("crash", evt.GetProperty("tags").GetProperty("kind").GetString());
        var values = evt.GetProperty("exception").GetProperty("values");
        Assert.Equal("System.IO.IOException", values[0].GetProperty("type").GetString());
        Assert.Equal("System.InvalidOperationException", values[1].GetProperty("type").GetString());
        var frames = values[1].GetProperty("stacktrace").GetProperty("frames");
        Assert.EndsWith("ReportingTests.Thrown", frames[frames.GetArrayLength() - 1].GetProperty("function").GetString());
        HasNothingPersonal(System.Text.Encoding.UTF8.GetString(items[0].Payload));
        Assert.Equal("log.txt", Assert.Single(items, i => i.Header.GetProperty("type").GetString() == "attachment").Header.GetProperty("filename").GetString());
    }

    [Fact]
    public async Task An_unexpected_exit_is_one_group()
    {
        var record = Thrown() with { Exceptions = [], Source = "exit" };
        var evt = JsonDocument.Parse(Items(await Bytes(ReportEnvelopes.Crash(record)))[0].Payload).RootElement;
        Assert.Equal("unexpected-exit", evt.GetProperty("fingerprint")[0].GetString());
        Assert.Contains("closed unexpectedly", ReportEnvelopes.Describe(record));
    }

    // ---- end to end, against a Sentry stand-in on this PC ----

    [Fact]
    public async Task Send_delivers_the_report_and_empties_the_outbox()
    {
        await using var sentry = new FakeSentry();
        var result = await NewReporter(sentry.Dsn).SendAsync(Report());
        Assert.Equal(ReportStatus.Sent, result.Status);
        Assert.Equal("Sent. Thank you. Report a1b2c3d4.", result.Message);
        var request = Assert.Single(sentry.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/1/envelope/", request.Path);
        Assert.Contains("sentry_key=testkey", request.Headers["X-Sentry-Auth"]);
        Assert.Contains("The map stayed on Woods.", request.Body);
        Assert.False(Directory.Exists(Path.Combine(_root, "outbox")) && Directory.EnumerateFiles(Path.Combine(_root, "outbox")).Any());
    }

    [Fact]
    public async Task A_report_that_cant_go_now_waits_in_the_outbox_and_goes_at_the_next_start()
    {
        await using var sentry = new FakeSentry { Status = HttpStatusCode.ServiceUnavailable };
        var result = await NewReporter(sentry.Dsn).SendAsync(Report());
        Assert.Equal(ReportStatus.Kept, result.Status);
        Assert.Single(Directory.EnumerateFiles(Path.Combine(_root, "outbox"), "*.json"));
        sentry.Status = HttpStatusCode.OK;
        Assert.Equal(1, await NewReporter(sentry.Dsn).SendOutboxAsync());
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_root, "outbox")));
        Assert.Equal(2, sentry.Requests.Count);
    }

    [Fact]
    public async Task A_refused_report_is_kept_readable_and_not_tried_again()
    {
        await using var sentry = new FakeSentry { Status = HttpStatusCode.BadRequest };
        Assert.Equal(ReportStatus.Refused, (await NewReporter(sentry.Dsn).SendAsync(Report())).Status);
        Assert.Single(Directory.EnumerateFiles(Path.Combine(_root, "outbox"), "*.refused.txt"));
        Assert.Equal(0, await NewReporter(sentry.Dsn).SendOutboxAsync());
        Assert.Single(sentry.Requests);
    }

    [Fact]
    public async Task Developer_runs_and_builds_without_an_address_send_nothing()
    {
        await using var sentry = new FakeSentry();
        Assert.Equal(ReportStatus.Muted, (await NewReporter(sentry.Dsn, muted: true).SendAsync(Report())).Status);
        Assert.Equal(ReportStatus.NotConfigured, (await NewReporter(null).SendAsync(Report())).Status);
        Assert.Equal(ReportStatus.Muted, (await NewReporter(sentry.Dsn, muted: true).SendCrashesAsync([Thrown()])).Status);
        Assert.Empty(sentry.Requests);
        Assert.False(Directory.Exists(Path.Combine(_root, "outbox")));
    }

    [Fact]
    public async Task Dont_send_and_never_keep_the_crash_on_this_pc_only()
    {
        await using var sentry = new FakeSentry();
        var reporter = NewReporter(sentry.Dsn);
        var record = Thrown();
        reporter.Crashes.Write(record);
        Assert.Equal(CrashAction.Keep, CrashPolicy.Decide(CrashMode.Never, 1, reporter.Configured, reporter.Muted));
        reporter.Keep(reporter.Crashes.Waiting());
        Assert.Empty(reporter.Crashes.Waiting());
        Assert.Single(reporter.Crashes.All());
        Assert.Empty(sentry.Requests);
    }

    [Fact]
    public async Task A_sent_crash_is_deleted_and_one_that_cant_go_is_approved_for_the_next_start()
    {
        await using var sentry = new FakeSentry { Status = HttpStatusCode.ServiceUnavailable };
        var reporter = NewReporter(sentry.Dsn);
        var record = Thrown();
        reporter.Crashes.Write(record);
        Assert.Equal(ReportStatus.Kept, (await reporter.SendCrashesAsync([record])).Status);
        Assert.Equal(CrashState.Approved, Assert.Single(reporter.Crashes.Waiting()).State);

        sentry.Status = HttpStatusCode.OK;
        var result = await reporter.SendCrashesAsync(reporter.Crashes.Waiting());
        Assert.Equal(ReportStatus.Sent, result.Status);
        Assert.Empty(reporter.Crashes.All());
        var body = sentry.Requests[^1].Body;
        Assert.Contains("\"kind\":\"crash\"", body);
        Assert.DoesNotContain("tester", body);
    }
}
