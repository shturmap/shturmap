using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shturmap.Session.Reporting;

/// <summary>
/// The one way reports leave the PC (owner, 2026-10-03): a problem or idea when the player presses Send, a crash
/// report as the "Crash reports" setting allows. A report is written to the outbox before it is sent and deleted once
/// it has gone, so a failed send never loses the player's text; the outbox is tried again at the next start.
/// Builds without a report address, and developer runs, send nothing (docs/DESIGN.md §8, "Reports").
/// </summary>
public sealed class Reporter
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    private readonly ReportEndpoint? _endpoint;
    private readonly HttpClient _http;
    private readonly string _outbox;

    /// <param name="root">Shturmap's folder (%LOCALAPPDATA%\Shturmap): crashes\ and outbox\ go in it.</param>
    /// <param name="muted">A developer run (snapshot, fake game, demo): nothing goes out.</param>
    public Reporter(string root, ReportEndpoint? endpoint, bool muted, ReportInfo info, HttpClient? http = null, string? profile = null)
    {
        _endpoint = endpoint;
        Muted = muted;
        Info = info;
        _http = http ?? CreateClient();
        _outbox = Path.Combine(root, "outbox");
        Crashes = new CrashRecords(Path.Combine(root, "crashes"), profile ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    /// <summary>The DSN the release was built with: an assembly attribute, empty in developer builds and forks.</summary>
    public const string DsnMetadata = "SentryDsn";

    /// <summary>This build has somewhere to send reports to.</summary>
    public bool Configured => _endpoint is not null;

    public bool Muted { get; }

    public ReportInfo Info { get; }

    public CrashRecords Crashes { get; }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Shturmap/{GameSession.Version}");
        return client;
    }

    // ---- problems and ideas ----

    /// <summary>Sends a report the player wrote: kept in the outbox first, deleted once it has gone.</summary>
    public async Task<ReportResult> SendAsync(UserReport report, CancellationToken ct = default)
    {
        if (_endpoint is null)
            return new ReportResult(ReportStatus.NotConfigured, "Reporting isn't set up in this build.");
        if (Muted)
            return new ReportResult(ReportStatus.Muted, "Not sent: developer runs send nothing.");
        var path = Store(report);
        var (outcome, why) = await new ReportSender(_endpoint, _http, Info.Version).SendAsync(ReportEnvelopes.Feedback(report, Info), ct);
        switch (outcome)
        {
            case ReportSender.Outcome.Sent:
                TryDelete(path);
                AppLog.Info($"Report {report.Id} sent ({report.Kind.ToString().ToLowerInvariant()})");
                return new ReportResult(ReportStatus.Sent, $"Sent. Thank you. Report {report.Id}.", report.Id);
            case ReportSender.Outcome.Later:
                AppLog.Warn($"Report {report.Id} not sent yet: {why}");
                return new ReportResult(ReportStatus.Kept,
                    $"Couldn't send it now ({why}). It's kept on this PC and goes out by itself the next time Shturmap starts.", report.Id);
            default:
                MarkRefused(path);
                AppLog.Warn($"Report {report.Id} refused: {why}");
                return new ReportResult(ReportStatus.Refused,
                    $"Couldn't send it: {why}. It's kept on this PC, in %LOCALAPPDATA%\\Shturmap\\outbox.", report.Id);
        }
    }

    /// <summary>Sends what an earlier session couldn't; returns how many went out.</summary>
    public async Task<int> SendOutboxAsync(CancellationToken ct = default)
    {
        if (_endpoint is null || Muted || !Directory.Exists(_outbox))
            return 0;
        var sent = 0;
        foreach (var path in Directory.EnumerateFiles(_outbox, "*.json").Order(StringComparer.Ordinal).ToList())
        {
            UserReport? report;
            try
            {
                report = JsonSerializer.Deserialize<UserReport>(File.ReadAllText(path), Json);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                continue;
            }
            if (report is null)
                continue;
            var (outcome, why) = await new ReportSender(_endpoint, _http, Info.Version).SendAsync(ReportEnvelopes.Feedback(report, Info), ct);
            if (outcome == ReportSender.Outcome.Sent)
            {
                TryDelete(path);
                sent++;
                AppLog.Info($"Report {report.Id} sent from the outbox");
            }
            else if (outcome == ReportSender.Outcome.Refused)
            {
                MarkRefused(path);
                AppLog.Warn($"Report {report.Id} refused: {why}");
            }
            else
            {
                AppLog.Warn($"Report {report.Id} still not sent: {why}");
                break;
            }
        }
        return sent;
    }

    private string Store(UserReport report)
    {
        var path = Path.Combine(_outbox, $"{report.At:yyyyMMdd-HHmmss}-{report.Id}.json");
        try
        {
            Directory.CreateDirectory(_outbox);
            File.WriteAllText(path, JsonSerializer.Serialize(report, Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn("Keeping the report on this PC failed", e);
        }
        return path;
    }

    // A refused report stays readable for the player but is not tried again.
    private static void MarkRefused(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Move(path, Path.ChangeExtension(path, ".refused.txt"), overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    // ---- crashes ----

    /// <summary>What to do with the crash records waiting at this start, as the setting says.</summary>
    public CrashAction Decide(CrashMode mode, IReadOnlyList<CrashRecord> waiting)
    {
        // The player already said Send for these: they go without asking again.
        var approved = waiting.Count(r => r.State == CrashState.Approved);
        var pending = waiting.Count - approved;
        var action = CrashPolicy.Decide(mode, pending, Configured, Muted);
        return action == CrashAction.None && approved > 0 && Configured && !Muted ? CrashAction.Send : action;
    }

    /// <summary>Sends crash records the player allowed (now or by the setting); a sent one is deleted, one that
    /// couldn't go yet is marked approved and sent at the next start.</summary>
    public async Task<ReportResult> SendCrashesAsync(IReadOnlyList<CrashRecord> records, CancellationToken ct = default)
    {
        if (_endpoint is null)
            return new ReportResult(ReportStatus.NotConfigured, "Reporting isn't set up in this build.");
        if (Muted)
            return new ReportResult(ReportStatus.Muted, "Not sent: developer runs send nothing.");
        var sent = new List<string>();
        foreach (var record in records.Take(5))
        {
            var (outcome, why) = await new ReportSender(_endpoint, _http, Info.Version).SendAsync(ReportEnvelopes.Crash(record), ct);
            switch (outcome)
            {
                case ReportSender.Outcome.Sent:
                    Crashes.Remove(record);
                    sent.Add(record.Id);
                    AppLog.Info($"Crash report {record.Id} sent ({record.Summary})");
                    break;
                case ReportSender.Outcome.Later:
                    foreach (var rest in records.Where(r => !sent.Contains(r.Id)))
                        Crashes.SetState(rest, CrashState.Approved);
                    AppLog.Warn($"Crash report {record.Id} not sent yet: {why}");
                    return new ReportResult(ReportStatus.Kept,
                        $"Couldn't send the crash report now ({why}). It goes out by itself the next time Shturmap starts.", record.Id);
                default:
                    Crashes.SetState(record, CrashState.Kept);
                    AppLog.Warn($"Crash report {record.Id} refused: {why}");
                    return new ReportResult(ReportStatus.Refused, $"Couldn't send the crash report: {why}.", record.Id);
            }
        }
        // More than five at once: the rest wait for the next start, already approved.
        foreach (var rest in records.Skip(5))
            Crashes.SetState(rest, CrashState.Approved);
        return new ReportResult(ReportStatus.Sent, sent.Count == 1 ? $"Crash report sent. Thank you. Report {sent[0]}." : $"{sent.Count} crash reports sent. Thank you.",
            sent.LastOrDefault());
    }

    /// <summary>"Don't send", or the setting is Never: the records stay on this PC only.</summary>
    public void Keep(IReadOnlyList<CrashRecord> records)
    {
        foreach (var record in records)
            Crashes.SetState(record, CrashState.Kept);
    }
}
