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

    /// <summary>The data arrived but isn't what Shturmap can read: tarkov.dev changed its format.</summary>
    Unreadable,

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
    /// <summary>Points the player to the help panel's diagnostics, for problems only a report can fix.</summary>
    public const string Report = "Please report it: Copy diagnostics in help (?).";

    /// <summary>Worth trying again by itself: the connection or tarkov.dev may be back in a while.</summary>
    public bool Transient => Kind is LoadFailure.Unreachable or LoadFailure.TimedOut or LoadFailure.ServerBusy;

    public string Text => $"{What} {Advice}";

    public static LoadProblem Explain(Exception e)
    {
        switch (e)
        {
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
