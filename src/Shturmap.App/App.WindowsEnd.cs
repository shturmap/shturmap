using System.Runtime.InteropServices;
using Shturmap.Session;
using Shturmap.Session.Reporting;

namespace Shturmap.App;

/// <summary>
/// Windows ending the user's session while Shturmap runs (docs/DESIGN.md §8, "Reports"; WindowsSessionEnd). Without
/// this, a shutdown ended the process where it stood, the running marker stayed, and the next start took it for a
/// crash. WinUI's Application has no event for it, so Shturmap follows its own main window's messages (comctl32's
/// window subclass: its own window only, no hook, nothing of another process).
/// </summary>
public partial class App
{
    private const uint NcDestroy = 0x0082; // WM_NCDESTROY: the window is gone, and the subclass with it
    private const nuint SubclassId = 1;

    private WindowsSessionEnd? _windowsEnd;
    private Microsoft.UI.Dispatching.DispatcherQueue? _windowQueue;

    // Held here for as long as the window is: Windows holds only its address.
    private SubclassProc? _windowMessages;

    private void FollowWindowsEnd(MainWindow window, GameSession session)
    {
        // The close's steps as the window's Closed and App's run them, without the window closing: the window stops and
        // keeps its place, the marker goes, then the session's own close.
        _windowsEnd = new WindowsSessionEnd(() =>
        {
            window.EndForClose();
            _marker?.Dispose();
        }, () => session.DisposeAsync().AsTask(), WindowsSessionEnd.Wait);
        _windowQueue = window.DispatcherQueue;
        _windowMessages = OnWindowMessage;
        if (!SetWindowSubclass(WinRT.Interop.WindowNative.GetWindowHandle(window), _windowMessages, SubclassId, 0))
            AppLog.Warn("Windows ending the session can't be followed (no window subclass): a shutdown may be taken for a crash");
    }

    // Every message of the main window passes here first and goes on unchanged: WM_QUERYENDSESSION is answered as
    // before, so nothing here holds a shutdown up.
    private nint OnWindowMessage(nint window, uint message, nuint wParam, nint lParam, nuint id, nuint data)
    {
        if (WindowsSessionEnd.Ends(message, wParam) && _windowsEnd?.End(lParam) is { } closing)
        {
            // Then the process ends, as after RESTART NOW, unless Windows ends it first. An installer's request
            // (Restart Manager's ENDSESSION_CLOSEAPP) ends no session: this is its close.
            var queue = _windowQueue;
            closing.ContinueWith(_ =>
            {
                try
                {
                    queue?.TryEnqueue(() => Environment.Exit(0));
                }
                catch (COMException)
                {
                    // The queue is gone with the window: Windows ends the process.
                }
            }, TaskScheduler.Default);
        }
        else if (message == NcDestroy && _windowMessages is not null)
        {
            RemoveWindowSubclass(window, _windowMessages, SubclassId);
        }
        return DefSubclassProc(window, message, wParam, lParam);
    }

    private delegate nint SubclassProc(nint window, uint message, nuint wParam, nint lParam, nuint id, nuint data);

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint window, SubclassProc proc, nuint id, nuint data);

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint window, SubclassProc proc, nuint id);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint window, uint message, nuint wParam, nint lParam);
}
