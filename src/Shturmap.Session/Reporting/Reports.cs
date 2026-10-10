using Shturmap.Core;

namespace Shturmap.Session.Reporting;

// Reports and crash reports (owner, 2026-10-03: "one coherent easy to use thing", from the app, no account; crash
// reports only as the player allows; docs/DESIGN.md §8, "Reports").

public enum ReportKind
{
    Problem,
    Idea,
}

/// <summary>What the player wrote in the Report dialog, kept on this PC until it has gone out.</summary>
/// <param name="Id">A short id the player can quote (8 hex digits).</param>
/// <param name="Diagnostics">The diagnostics shown under "Show what's sent", or null when the player left them out.</param>
public sealed record UserReport(string Id, DateTime At, ReportKind Kind, string Text, string Contact, string? Diagnostics)
{
    public const int MaxText = 4000;
    public const int MaxContact = 120;

    public static string NewId() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>Why the dialog can't send yet, or null when it can.</summary>
    public static string? Invalid(string text, string contact) =>
        string.IsNullOrWhiteSpace(text) ? SessionTexts.ReportWriteSomething
        : text.Length > MaxText ? SessionTexts.ReportTooLong(count: MaxText, max: MaxText.ToString("N0", UiLanguage.Culture))
        : contact.Length > MaxContact ? SessionTexts.ReportContactTooLong(count: MaxContact)
        : null;
}

/// <summary>What happens after a crash: the player's choice in settings ("Crash reports").</summary>
public enum CrashMode
{
    /// <summary>The next start asks: Send, Don't send, Always send. The default.</summary>
    Ask,

    /// <summary>Sent at the next start without asking.</summary>
    Always,

    /// <summary>Only kept on this PC.</summary>
    Never,
}

public static class CrashModes
{
    /// <summary>The setting's key in shturmap.db ("ask", "always" or "never"; absent is ask).</summary>
    public const string Setting = "crashReports";

    public static CrashMode Parse(string? value) => value switch
    {
        "always" => CrashMode.Always,
        "never" => CrashMode.Never,
        _ => CrashMode.Ask,
    };

    public static string Format(CrashMode mode) => mode.ToString().ToLowerInvariant();
}

public enum CrashAction
{
    /// <summary>Nothing to do (nothing pending, or a developer run).</summary>
    None,

    /// <summary>Ask the player whether to send.</summary>
    Ask,

    /// <summary>Send without asking.</summary>
    Send,

    /// <summary>Keep on this PC only.</summary>
    Keep,
}

public static class CrashPolicy
{
    /// <summary>
    /// What the start does with crash records the player hasn't answered: ask, send or keep them, as the setting says.
    /// Developer runs leave them for the next real start; a build that can't send keeps them.
    /// </summary>
    public static CrashAction Decide(CrashMode mode, int pending, bool configured, bool muted) =>
        pending == 0 || muted ? CrashAction.None
        : !configured ? CrashAction.Keep
        : mode switch
        {
            CrashMode.Ask => CrashAction.Ask,
            CrashMode.Always => CrashAction.Send,
            _ => CrashAction.Keep,
        };
}

/// <summary>Where a crash record stands. Sent ones are deleted.</summary>
public enum CrashState
{
    /// <summary>Not answered yet: the next start asks (or sends, or keeps, as the setting says).</summary>
    Pending,

    /// <summary>The player said Send, but it couldn't go out yet: sent at the next start without asking again.</summary>
    Approved,

    /// <summary>"Don't send", or the setting is Never, or the build can't send: only on this PC.</summary>
    Kept,
}

/// <summary>One frame of a crash's stack: where in the code, never what was in memory.</summary>
public sealed record CrashFrame(string Module, string Function, string? File, int? Line, bool InApp);

/// <summary>An exception in a crash, outermost first in <see cref="CrashRecord.Exceptions"/>.</summary>
public sealed record CrashException(string Type, string Message, IReadOnlyList<CrashFrame> Frames);

/// <summary>
/// A crash or error, written on this PC when it happens (masked) and sent only as the player allows. No exceptions
/// means the session ended without one being caught: Shturmap closed unexpectedly (a native crash, or killed).
/// </summary>
/// <param name="Session">The session it happened in, so an unexpected end isn't counted twice.</param>
/// <param name="Fatal">Shturmap closed because of it; false for an error it survived.</param>
/// <param name="Source">"ui", "background", "task" or "exit".</param>
public sealed record CrashRecord(string Id, string Session, DateTime At, bool Fatal, string Source, IReadOnlyList<CrashException> Exceptions,
    string Version, string Build, string Windows, IReadOnlyList<string> LogTail, CrashState State = CrashState.Pending)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool UnexpectedExit => Exceptions.Count == 0;

    /// <summary>One line for the log and the notice: "InvalidOperationException in MainWindow.Apply".</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Summary => Exceptions.FirstOrDefault() is { } e
        ? $"{e.Type.Split('.')[^1]}{(e.Frames.FirstOrDefault(f => f.InApp) is { } at ? " in " + at.Function : "")}"
        : "closed unexpectedly";
}

/// <summary>Who sent it and with what: Shturmap's version, the build kind and Windows' version.</summary>
public sealed record ReportInfo(string Version, string Build, string Windows);

public enum ReportStatus
{
    Sent,

    /// <summary>Couldn't go out now (no connection, the service busy): kept and sent at the next start.</summary>
    Kept,

    /// <summary>The service refused it: kept on this PC, not tried again.</summary>
    Refused,

    /// <summary>This build has no place to send reports to.</summary>
    NotConfigured,

    /// <summary>A developer run (snapshot, fake game, demo): nothing goes out.</summary>
    Muted,
}

/// <summary>How sending went, in words for the dialog or a notice.</summary>
public sealed record ReportResult(ReportStatus Status, string Message, string? Id = null);
