#if DEVTOOLS
using Microsoft.UI.Xaml;
using Shturmap.App.Controls;
using Shturmap.Session;
using Shturmap.Session.Dev;
using Shturmap.Session.Reporting;

namespace Shturmap.App;

// The developer view's ways into the main window (docs/DESIGN.md §8, "Developer aids"): F12 opens the view, F9 takes
// the last position again; the view's triggers open the window's own dialogs and lines.
public sealed partial class MainWindow
{
    private Dev.DevController? _dev;
    private Dev.DevView? _devView;

    /// <summary>Called once the session has started: the developer view's controller, on the session's fake game
    /// (null when this session reads the real game).</summary>
    public void DevInstall(GameSession session, FakeGame? game)
    {
        _dev = new Dev.DevController(this, session, game);
        if (game is not null)
            Title = "Shturmap · developer view";
    }

    private void AddDevShortcuts(Action<Windows.System.VirtualKey, Action> add)
    {
        add(Windows.System.VirtualKey.F12, DevOpenView);
        add(Windows.System.VirtualKey.F9, () => _dev?.Repeat());
    }

    public void DevOpenView()
    {
        if (_dev is null)
            return;
        if (_devView is null)
        {
            _devView = new Dev.DevView(_dev);
            _devView.Closed += (_, _) =>
            {
                _dev.Picking = false;
                _devView = null;
            };
        }
        _devView.Activate();
    }

    public Task DevRunScriptAsync(string path) => _dev?.RunScriptAsync(path) ?? Task.CompletedTask;

    internal MapView DevMapView => Map;

    internal SessionSnapshot? DevSnapshot => Volatile.Read(ref _snapshot);

    internal void DevShowReport() => OpenReport(ReportKind.Problem, null, "devview");

    internal void DevShowCrash()
    {
        CrashRecord record;
        try
        {
            throw new InvalidOperationException("Example crash from the developer view");
        }
        catch (InvalidOperationException e)
        {
            record = CrashRecords.FromException(e, "devview", "ui", true, App.Reporter.Info, AppLog.Tail(5), DateTime.Now, null);
        }
        AskAboutCrashes([record]);
    }

    /// <summary>The line an update ready to apply shows (with RESTART NOW, which does nothing here: no update is
    /// downloaded).</summary>
    internal void DevShowUpdateReady(string version)
    {
        var decision = UpdatePolicy.Decide(UpdateMode.Automatic, canUpdate: true, ViewModel.InRaid, UpdateStage.Ready, version);
        ViewModel.UpdateLine = decision.Line;
        ViewModel.UpdateOffersDownload = decision.OfferDownload;
        ViewModel.UpdateOffersRestart = decision.OfferRestart;
    }

    /// <summary>The window and the map as PNGs (as "--snapshot" does), and the developer view when it is open.</summary>
    internal async Task DevSnapshotAsync(string folder)
    {
        await SaveSnapshotAsync(folder);
        if (_devView is not null)
            await _devView.SaveSnapshotsAsync(folder);
    }

    internal Task DevRenderAsync(UIElement element, string path) => RenderToPngAsync(element, path);

    internal void DevExit()
    {
        _devView?.Close();
        ((App)Application.Current).EndSession();
        Application.Current.Exit();
    }
}
#endif
