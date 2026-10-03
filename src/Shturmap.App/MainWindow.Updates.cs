using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Shturmap.Session;

namespace Shturmap.App;

// New versions (owner, 2026-10-03; docs/DESIGN.md §8, "Distribution"): asked for at start and every 6 hours,
// downloaded in the background, applied at the next start; one quiet line between raids, and RESTART NOW on a click
// only, never during a raid. "Updates" in help: Automatic, Tell me only, Off.
public sealed partial class MainWindow
{
    private Updater? _updater;
    private DispatcherQueueTimer? _updateTimer;
    private bool _updating;

    public Visibility ShownIfUpdateMode(string mode, string value) => mode == value ? Visibility.Visible : Visibility.Collapsed;

    public Brush UpdateModeBrush(string mode, string value) => Resource(mode == value ? "AmberBrush" : "MutedBrush");

    private UpdateMode CurrentUpdateMode => UpdateModes.Parse(_session.GetSetting(UpdateModes.Setting));

    /// <summary>The session has started: start asking for new versions, as the setting says.</summary>
    public void StartUpdates(Updater updater)
    {
        _updater = updater;
        ViewModel.UpdatesAvailable = updater.CanUpdate;
        ViewModel.UpdateMode = CurrentUpdateMode.ToString();
        updater.Changed += () => DispatcherQueue.TryEnqueue(RefreshUpdates);
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.InRaid))
                RefreshUpdates();
        };
        RefreshUpdates();
        if (!updater.CanUpdate)
            return;
        _updateTimer = DispatcherQueue.CreateTimer();
        _updateTimer.Interval = UpdatePolicy.Interval;
        _updateTimer.IsRepeating = true;
        _updateTimer.Tick += (_, _) => _ = StepUpdatesAsync();
        _updateTimer.Start();
        _ = StepUpdatesAsync();
    }

    // One step: ask when it is due, and download what was found when the setting says so.
    private async Task StepUpdatesAsync()
    {
        if (_updater is not { CanUpdate: true } updater || _updating)
            return;
        _updating = true;
        try
        {
            var decision = Decide();
            if (decision.Check && UpdatePolicy.CheckDue(updater.LastCheck, DateTime.Now))
                await updater.CheckAsync();
            if (Decide().Download)
                await updater.DownloadAsync();
        }
        finally
        {
            _updating = false;
            DispatcherQueue.TryEnqueue(RefreshUpdates);
        }
    }

    private UpdateDecision Decide() =>
        UpdatePolicy.Decide(CurrentUpdateMode, _updater?.CanUpdate == true, ViewModel.InRaid, _updater?.Stage ?? UpdateStage.None, _updater?.Version);

    private void RefreshUpdates()
    {
        var decision = Decide();
        ViewModel.UpdateLine = decision.Line;
        ViewModel.UpdateOffersDownload = decision.OfferDownload;
        ViewModel.UpdateOffersRestart = decision.OfferRestart;
    }

    private void OnUpdateModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag } || !Enum.TryParse<UpdateMode>(tag, out var mode))
            return;
        _session.SetSetting(UpdateModes.Setting, UpdateModes.Format(mode));
        ViewModel.UpdateMode = mode.ToString();
        Study.Ui("updates.mode", ("mode", mode));
        AppLog.Info("Updates: " + UpdateModes.Format(mode));
        RefreshUpdates();
        _ = StepUpdatesAsync();
    }

    private async void OnUpdateDownloadClick(object sender, RoutedEventArgs e)
    {
        if (_updater is null)
            return;
        Study.Ui("updates.download");
        await _updater.DownloadAsync();
        RefreshUpdates();
    }

    // Velopack ends this process at once: the session is closed first, and the running marker let go, so the next
    // start doesn't take the restart for a crash.
    private async void OnUpdateRestartClick(object sender, RoutedEventArgs e)
    {
        if (_updater is null || ViewModel.InRaid || _updater.Stage != UpdateStage.Ready)
            return;
        Study.Ui("updates.restart");
        await _session.DisposeAsync();
        ((App)Application.Current).EndSession();
        _updater.RestartNow();
    }
}
