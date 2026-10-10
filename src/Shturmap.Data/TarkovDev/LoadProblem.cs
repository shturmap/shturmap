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
/// reaches the UI; docs/DESIGN.md §8, "Error messages"). The words follow from the kind and the status, and are looked
/// up in the language in use each time they are read, so a problem kept while the language changes reads in the new
/// one (docs/DESIGN.md §8, "Texts").
/// </summary>
public sealed record LoadProblem(LoadFailure Kind, int? Status)
{
    /// <summary>Asks for a report, for problems only a report can fix; the notice offers the Report dialog.</summary>
    public static string Report => DataTexts.LoadAdviceReport;

    /// <summary>Worth trying again by itself: the connection or tarkov.dev may be back in a while, or what sent a page
    /// in place of the data may be gone (the player signed in to the network).</summary>
    public bool Transient => Kind is LoadFailure.Unreachable or LoadFailure.TimedOut or LoadFailure.ServerBusy or LoadFailure.NotData;

    /// <summary>What failed, a sentence: "tarkov.dev answered 503."</summary>
    public string What => Kind switch
    {
        LoadFailure.ServerBusy or LoadFailure.Refused => DataTexts.LoadFailedStatus(status: Status),
        LoadFailure.Unreachable => DataTexts.LoadFailedUnreachable,
        LoadFailure.TimedOut => DataTexts.LoadFailedTimedOut,
        LoadFailure.Unreadable => DataTexts.LoadFailedUnreadable,
        LoadFailure.NotData => DataTexts.LoadFailedNotData,
        LoadFailure.Disk => DataTexts.LoadFailedDisk,
        _ => DataTexts.LoadFailedUnknown,
    };

    /// <summary>What to do, a sentence. A page in place of the data asks for no report: a report can't change what sends
    /// the page. The notices name the wait (GameSession.DataNotice).</summary>
    public string Advice => Kind switch
    {
        LoadFailure.ServerBusy => DataTexts.LoadAdviceServerBusy,
        LoadFailure.Unreachable or LoadFailure.TimedOut => DataTexts.LoadAdviceConnection,
        LoadFailure.NotData => DataTexts.LoadAdviceNotData,
        LoadFailure.Disk => DataTexts.LoadAdviceDisk,
        _ => Report,
    };

    public string Text => $"{What} {Advice}";

    public static LoadProblem Explain(Exception e)
    {
        switch (e)
        {
            // An answer that isn't the data, with no saved copy to use (with one, CachedHttp used that). First: it is an
            // HttpRequestException without a status, which would read as no connection.
            case Http.NotDataAnswerException:
                return new(LoadFailure.NotData, null);
            case HttpRequestException { StatusCode: { } status }:
                var code = (int)status;
                return new(code >= 500 || status == HttpStatusCode.TooManyRequests ? LoadFailure.ServerBusy : LoadFailure.Refused, code);
            case HttpRequestException:
                return new(LoadFailure.Unreachable, null);
            case TaskCanceledException or TimeoutException:
                return new(LoadFailure.TimedOut, null);
            case SocketException:
                return new(LoadFailure.Unreachable, null);
            case NotDataException:
                return new(LoadFailure.NotData, null);
            case JsonException or FormatException or InvalidOperationException or KeyNotFoundException or NullReferenceException:
                return new(LoadFailure.Unreadable, null);
            case IOException or UnauthorizedAccessException:
                return new(LoadFailure.Disk, null);
            default:
                return new(LoadFailure.Unknown, null);
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
