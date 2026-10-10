using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Sentry;
using Sentry.Protocol;
using Sentry.Protocol.Envelopes;

namespace Shturmap.Session.Reporting;

/// <summary>
/// Where reports go: the Sentry project's envelope address and public key, from the DSN the release is built with
/// (eng\release.ps1). Only Sentry's hosts over HTTPS, or this PC over HTTP for tests.
/// </summary>
public sealed record ReportEndpoint(Uri Envelope, string PublicKey)
{
    public static ReportEndpoint? Parse(string? dsn)
    {
        if (string.IsNullOrWhiteSpace(dsn) || !Uri.TryCreate(dsn.Trim(), UriKind.Absolute, out var uri))
            return null;
        var sentry = uri.Scheme == Uri.UriSchemeHttps && uri.Host.EndsWith(".sentry.io", StringComparison.OrdinalIgnoreCase);
        var local = uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
        var key = uri.UserInfo.Split(':')[0];
        var path = uri.AbsolutePath.Trim('/').Split('/');
        var project = path[^1];
        if (!(sentry || local) || key.Length == 0 || project.Length == 0 || !project.All(char.IsAsciiDigit))
            return null;
        var prefix = string.Concat(path[..^1].Where(p => p.Length > 0).Select(p => "/" + p));
        return new ReportEndpoint(new UriBuilder(uri.Scheme, uri.Host, uri.Port, $"{prefix}/api/{project}/envelope/").Uri, key);
    }
}

/// <summary>
/// Reports as Sentry envelopes, built here from what the player saw: no SDK client, so nothing is added on the way
/// (no machine name, no installation id, no IP, no breadcrumbs).
/// </summary>
public static class ReportEnvelopes
{
    /// <summary>A problem or an idea: Sentry user feedback, with the diagnostics attached when the player kept them.</summary>
    public static Envelope Feedback(UserReport report, ReportInfo info)
    {
        var contact = report.Contact.Trim();
        var email = contact.Contains('@') && !contact.Contains(' ') ? contact : null;
        var evt = Base(info, report.At, SentryLevel.Info);
        evt.Contexts.Feedback = new SentryFeedback(report.Text.Trim(), email, email is null && contact.Length > 0 ? contact : null);
        evt.SetTag("kind", report.Kind == ReportKind.Problem ? "problem" : "idea");
        evt.SetTag("report", report.Id);
        evt.SetTag("diagnostics", report.Diagnostics is null ? "no" : "yes");
        var attachments = report.Diagnostics is { } text ? new[] { Text("diagnostics.txt", text) } : [];
        return Envelope.FromFeedback(evt, null, attachments);
    }

    /// <summary>A crash or error: its exception chain with frames, so the same crash is grouped; the log lines attached.</summary>
    public static Envelope Crash(CrashRecord record)
    {
        var info = new ReportInfo(record.Version, record.Build, record.Windows);
        var evt = Base(info, record.At, record.Fatal ? SentryLevel.Fatal : SentryLevel.Error);
        evt.SetTag("kind", "crash");
        evt.SetTag("report", record.Id);
        evt.SetTag("source", record.Source);
        if (record.UnexpectedExit)
        {
            evt.Message = new SentryMessage { Message = "Shturmap closed unexpectedly (no error was caught)" };
            evt.Fingerprint = ["unexpected-exit"];
        }
        else
        {
            // Sentry lists a chain oldest first (the cause, then what it caused), and each stack's caller first.
            evt.SentryExceptions = record.Exceptions.AsEnumerable().Reverse().Select((e, i) => new SentryException
            {
                Type = e.Type,
                Value = e.Message,
                Mechanism = new Mechanism { Type = "Shturmap." + record.Source, Handled = !record.Fatal },
                Stacktrace = new SentryStackTrace
                {
                    Frames = e.Frames.AsEnumerable().Reverse().Select(f => new SentryStackFrame
                    {
                        Module = f.Module,
                        Function = f.Function,
                        FileName = f.File,
                        LineNumber = f.Line,
                        InApp = f.InApp,
                    }).ToList(),
                },
            }).ToList();
        }
        var log = string.Join(Environment.NewLine, record.LogTail);
        return Envelope.FromEvent(evt, null, log.Length > 0 ? [Text("log.txt", log)] : []);
    }

    /// <summary>Sentry's environment for a report: "dev" from a dev build, else "release".</summary>
    public static string EnvironmentOf(ReportInfo info) => info.Build.StartsWith("dev", StringComparison.Ordinal) ? "dev" : "release";

    private static SentryEvent Base(ReportInfo info, DateTime at, SentryLevel level)
    {
        var evt = new SentryEvent
        {
            Level = level,
            Release = "shturmap@" + info.Version,
            // A dev build's reports (eng\dev.ps1 -WithReports) are filed apart, so Sentry can filter them out.
            Environment = EnvironmentOf(info),
            ServerName = null,
        };
        // The event is stamped when it is sent; when it happened (a crash, sent at the next start) goes along.
        evt.SetExtra("happened", at.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
        evt.SetTag("build", info.Build);
        evt.Contexts.OperatingSystem.Name = "Windows";
        evt.Contexts.OperatingSystem.Version = info.Windows;
        return evt;
    }

    private static SentryAttachment Text(string name, string text) =>
        new(AttachmentType.Default, new ByteAttachmentContent(Encoding.UTF8.GetBytes(text)), name, "text/plain");

    /// <summary>A crash record as the player reads it before sending ("What's sent"): everything <see cref="Crash"/>
    /// puts in the report.</summary>
    public static string Describe(CrashRecord record)
    {
        var text = new StringBuilder();
        text.AppendLine($"{(record.Fatal ? "Crash" : "Error")} {record.Id}, {record.At:yyyy-MM-dd HH:mm:ss} ({record.Source})");
        text.AppendLine($"Shturmap {record.Version} ({record.Build}), {record.Windows}");
        if (record.UnexpectedExit)
            text.AppendLine("Shturmap closed unexpectedly; no error was caught.");
        foreach (var e in record.Exceptions)
        {
            text.AppendLine($"{e.Type}: {e.Message}");
            foreach (var f in e.Frames)
                text.AppendLine($"  at {f.Function}{(f.File is { } file ? $" ({file}{(f.Line is { } line ? ":" + line : "")})" : "")}");
        }
        if (record.LogTail.Count > 0)
        {
            text.AppendLine($"Last {record.LogTail.Count} log lines:");
            foreach (var line in record.LogTail)
                text.AppendLine(line);
        }
        return text.ToString().TrimEnd();
    }
}

/// <summary>Posts an envelope to the report endpoint and says plainly how it went.</summary>
public sealed class ReportSender(ReportEndpoint endpoint, HttpClient http, string version)
{
    public enum Outcome
    {
        Sent,
        Later,
        Refused,
    }

    /// <returns>How it went, and why it didn't: in English for the app log (<c>Why</c>), and in the language in use for
    /// the player (<c>Said</c>).</returns>
    public async Task<(Outcome Outcome, string Why, string Said)> SendAsync(Envelope envelope, CancellationToken ct = default)
    {
        using var body = new MemoryStream();
        await envelope.SerializeAsync(body, null, ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Envelope) { Content = new ByteArrayContent(Sealed(body.ToArray(), version)) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-sentry-envelope");
        request.Headers.TryAddWithoutValidation("X-Sentry-Auth",
            $"Sentry sentry_version=7, sentry_client=shturmap/{version}, sentry_key={endpoint.PublicKey}");
        try
        {
            using var response = await http.SendAsync(request, ct);
            var code = (int)response.StatusCode;
            if (response.IsSuccessStatusCode)
                return (Outcome.Sent, "", "");
            return response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout || code >= 500
                ? (Outcome.Later, $"the report service answered {code}", SessionTexts.ReportWhyAnswered(status: code))
                : (Outcome.Refused, $"the report service answered {code}", SessionTexts.ReportWhyAnswered(status: code));
        }
        catch (HttpRequestException)
        {
            return (Outcome.Later, "no connection", SessionTexts.ReportWhyNoConnection);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return (Outcome.Later, "no answer in time", SessionTexts.ReportWhyNoAnswer);
        }
    }

    /// <summary>
    /// The serialized envelope with each event's "sdk" filled in: who sent it, and that Sentry must never infer the
    /// sender's IP address. Without it Sentry looked up the reporter's town from the connection and kept it, IP
    /// storage off or not (seen on a report, 2026-10-03). The .NET SDK has no such setting, so it is written here.
    /// </summary>
    public static byte[] Sealed(byte[] envelope, string version)
    {
        using var output = new MemoryStream();
        var at = Array.IndexOf(envelope, (byte)'\n');
        if (at < 0)
            return envelope;
        output.Write(envelope, 0, at + 1); // the envelope's own header
        at++;
        while (at < envelope.Length)
        {
            var end = Array.IndexOf(envelope, (byte)'\n', at);
            if (end < 0)
                end = envelope.Length;
            if (end == at)
            {
                at++;
                continue;
            }
            var header = System.Text.Json.Nodes.JsonNode.Parse(envelope.AsSpan(at, end - at))!.AsObject();
            at = Math.Min(end + 1, envelope.Length);
            var next = Array.IndexOf(envelope, (byte)'\n', at);
            var length = header["length"]?.GetValue<int>() ?? (next < 0 ? envelope.Length : next) - at;
            var payload = envelope.AsSpan(at, length).ToArray();
            at = Math.Min(at + length + 1, envelope.Length);
            if (header["type"]?.GetValue<string>() is "event" or "feedback")
            {
                var evt = System.Text.Json.Nodes.JsonNode.Parse(payload)!.AsObject();
                evt["sdk"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["name"] = "shturmap",
                    ["version"] = version,
                    ["settings"] = new System.Text.Json.Nodes.JsonObject { ["infer_ip"] = "never" },
                };
                // And an empty address: a null ip_address is what tells Sentry's Relay not to take one from the
                // connection. infer_ip alone didn't keep the town out of a report's "User Geography" (owner, 2026-10-09);
                // the project's scrubbing rule for $user.geo.** is the other half (docs/DESIGN.md §8, "Reports").
                if (evt["user"] is not System.Text.Json.Nodes.JsonObject user)
                {
                    user = new System.Text.Json.Nodes.JsonObject();
                    evt["user"] = user;
                }
                user["ip_address"] = null;
                payload = Encoding.UTF8.GetBytes(evt.ToJsonString());
                header["length"] = payload.Length;
            }
            output.Write(Encoding.UTF8.GetBytes(header.ToJsonString()));
            output.WriteByte((byte)'\n');
            output.Write(payload);
            output.WriteByte((byte)'\n');
        }
        return output.ToArray();
    }
}
