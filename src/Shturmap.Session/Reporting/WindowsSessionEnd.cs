namespace Shturmap.Session.Reporting;

/// <summary>
/// Windows ending the user's session while Shturmap runs (a shutdown, restart or sign-out; WM_ENDSESSION, which the main
/// window's own messages bring): Shturmap's session ends as a close ends it, at once and without a word, so the running
/// marker goes and the next start takes nothing for a crash (docs/DESIGN.md §8, "Reports"). Windows' question before
/// it (WM_QUERYENDSESSION) is left to the window as it always was, so nothing here holds a shutdown up; and Windows
/// waits only a few seconds for an app, so the session's own close is given <see cref="Wait"/> at most.
/// </summary>
/// <param name="close">The close's own steps (the window stops and keeps its place, the marker goes), at once.</param>
/// <param name="closeData">The session's own close (its files, the database), off the window's thread.</param>
/// <param name="wait">How long the window's message waits for <paramref name="closeData"/>.</param>
public sealed class WindowsSessionEnd(Action close, Func<Task> closeData, TimeSpan wait)
{
    /// <summary>WM_QUERYENDSESSION: Windows asks whether the session may end.</summary>
    public const uint QueryMessage = 0x0011;

    /// <summary>WM_ENDSESSION: whether it ends (its wParam TRUE) or not after all.</summary>
    public const uint EndMessage = 0x0016;

    // The message's lParam (ENDSESSION_CLOSEAPP, ENDSESSION_CRITICAL, ENDSESSION_LOGOFF).
    private const uint CloseApp = 0x00000001;
    private const uint Critical = 0x40000000;
    private const uint LogOff = 0x80000000;

    /// <summary>What the session's own close is given: Windows ends an app that keeps it waiting after about 5 s.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(2);

    private int _ended;

    /// <summary>Whether Shturmap has ended for Windows.</summary>
    public bool Ended => Volatile.Read(ref _ended) == 1;

    /// <summary>Whether a window's message says the session ends now: WM_ENDSESSION with TRUE. With FALSE it was called
    /// off, and WM_QUERYENDSESSION only asks.</summary>
    public static bool Ends(uint message, nuint wParam) => message == EndMessage && wParam != 0;

    /// <summary>The end Windows names in the message's lParam, for the log line.</summary>
    public static string Why(nint lParam)
    {
        var flags = unchecked((uint)lParam);
        var why = (flags & CloseApp) != 0 ? "an installer needs Shturmap closed"
            : (flags & LogOff) != 0 ? "a sign-out"
            : "a shutdown or restart";
        return (flags & Critical) != 0 ? why + ", forced" : why;
    }

    /// <summary>
    /// Ends Shturmap's session for Windows, once: one log line, the close's steps, then the session's own close, waited
    /// for at most <c>wait</c>. Returns that close, to end the process after it (null the second time). Never throws: it
    /// runs inside the window's message, where an exception would end the process as a crash.
    /// </summary>
    public Task? End(nint lParam)
    {
        if (Interlocked.Exchange(ref _ended, 1) == 1)
            return null;
        try
        {
            AppLog.Info($"Windows is ending the session ({Why(lParam)}): Shturmap closes with it");
            close();
        }
        catch (Exception e)
        {
            AppLog.Warn("Closing as Windows ends the session failed", e);
        }
        Task closing;
        try
        {
            // Its failure is said here, so it is never left to the unobserved-task handler (a crash record).
            closing = Task.Run(closeData).ContinueWith(t =>
            {
                if (t.Exception is { } e)
                    AppLog.Warn("Closing the session as Windows ends it failed", e.InnerException ?? e);
            }, TaskScheduler.Default);
            // A wait that handles no messages meanwhile (a blocking wait on this thread would): this runs inside one.
            if (!SpinWait.SpinUntil(() => closing.IsCompleted, wait))
                AppLog.Warn($"Closing the session as Windows ends it took over {wait.TotalSeconds:0.#} s; Windows may end Shturmap first");
        }
        catch (Exception e)
        {
            AppLog.Warn("Closing the session as Windows ends it failed", e);
            closing = Task.CompletedTask;
        }
        return closing;
    }
}
