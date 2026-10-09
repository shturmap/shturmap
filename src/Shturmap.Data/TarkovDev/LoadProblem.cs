using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Shturmap.Data.TarkovDev;

public enum LoadFailure
{
    /// <summary>No connection to tarkov.dev (no internet, DNS, refused, TLS) and no saved copy.</summary>
    Unreachable,

    /// <summary>tarkov.dev didn't answer in time, and there's no saved copy.</summary>
    TimedOut,

    /// <summary>tarkov.dev answered 5xx or 429: busy or down for a moment.</summary>
    ServerBusy,

    /// <summary>tarkov.dev answered another error (404, 403, …): Shturmap asks for something it doesn't have.</summary>
    Refused,

    /// <summary>The data arrived as JSON, but not in a shape Shturmap can read: tarkov.dev changed its format.</summary>
    Unreadable,

    /// <summary>What arrived isn't JSON at all, and there's no saved copy to use (<see cref="Http.NotDataAnswerException"/>,
    /// or <see cref="NotDataException"/> for such a file saved before 2026-10-09): a sign-in page, a network filter's or
    /// a CDN's page sent with 200 in place of the data, or a body that is empty or cut off.</summary>
    NotData,

    /// <summary>The data couldn't be saved or read on this PC (disk full, no access).</summary>
    Disk,

    Unknown,
}

/// <summary>
/// Why loading tarkov.dev's data failed, in plain words for the player: what failed, with the status where there is
/// one, and what to do. The exception itself only goes to the app log (owner, 2026-10-03: raw exception text never
/// reaches the UI; docs/DESIGN.md §8, "Error messages").
/// </summary>
public sealed record LoadProblem(LoadFailure Kind, int? Status, string What, string Advice)
{
    /// <summary>Asks for a report, for problems only a report can fix; the notice offers the Report dialog.</summary>
    public const string Report = "Please report it.";

    /// <summary>Worth trying again by itself: the connection or tarkov.dev may be back in a while, or what sent a page
    /// in place of the data may be gone (the player signed in to the network).</summary>
    public bool Transient => Kind is LoadFailure.Unreachable or LoadFailure.TimedOut or LoadFailure.ServerBusy or LoadFailure.NotData;

    public string Text => $"{What} {Advice}";

    // No report: a report can't change what sends the page. The notices name the wait (GameSession.DataNotice).
    private static readonly LoadProblem NotData = new(LoadFailure.NotData, null,
        "tarkov.dev's answer wasn't its data: a sign-in page or a filter in between?", "Shturmap tries again in a few minutes.");

    public static LoadProblem Explain(Exception e)
    {
        switch (e)
        {
            // An answer that isn't the data, with no saved copy to use (with one, CachedHttp used that). First: it is an
            // HttpRequestException without a status, which would read as no connection.
            case Http.NotDataAnswerException:
                return NotData;
            case HttpRequestException { StatusCode: { } status }:
                var code = (int)status;
                return code >= 500 || status == HttpStatusCode.TooManyRequests
                    ? new(LoadFailure.ServerBusy, code, $"tarkov.dev answered {code}.", "It is busy or down for a moment; try again in a few minutes.")
                    : new(LoadFailure.Refused, code, $"tarkov.dev answered {code}.", Report);
            case HttpRequestException:
                return new(LoadFailure.Unreachable, null, "Couldn't reach tarkov.dev, and there's no saved copy yet.", "Check the internet connection.");
            case TaskCanceledException or TimeoutException:
                return new(LoadFailure.TimedOut, null, "tarkov.dev didn't answer in time, and there's no saved copy yet.", "Check the internet connection.");
            case SocketException:
                return new(LoadFailure.Unreachable, null, "Couldn't reach tarkov.dev, and there's no saved copy yet.", "Check the internet connection.");
            case NotDataException:
                return NotData;
            case JsonException or FormatException or InvalidOperationException or KeyNotFoundException or NullReferenceException:
                return new(LoadFailure.Unreadable, null, "tarkov.dev's data has changed in a way Shturmap can't read.", Report);
            case IOException or UnauthorizedAccessException:
                return new(LoadFailure.Disk, null, "Couldn't save tarkov.dev's data on this PC.",
                    @"Check the free disk space and that %LOCALAPPDATA%\Shturmap can be written to.");
            default:
                return new(LoadFailure.Unknown, null, "Loading tarkov.dev's data failed.", Report);
        }
    }
}

/// <summary>
/// A load of tarkov.dev's data got files that aren't JSON at all (review of 2026-10-09): a page a sign-in portal, a
/// network filter or a CDN sent with 200 in place of the data, or a body that is empty or cut off. That says nothing
/// about tarkov.dev's format, so it isn't <see cref="LoadFailure.Unreadable"/>: <see cref="GameDataLoader"/> throws this
/// in place of the parse error, once it has forgotten those files (<see cref="LoadFailure.NotData"/>). The message
/// names the files (their cache keys, no folder), never what they hold.
/// </summary>
public sealed class NotDataException(IReadOnlyList<string> files, Exception parseError)
    : Exception($"Not JSON: {string.Join(", ", files)}", parseError);
