using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Shturmap.App.Rules;
using Shturmap.Core;
using Shturmap.Core.Raid;
using Shturmap.Session;

namespace Shturmap.App;

// New versions (owner, 2026-10-03; docs/DESIGN.md §8, "Distribution"): asked for at start and every 6 hours,
// downloaded in the background, applied at the next start, and RESTART NOW on a click only, never during a raid.
// "Updates" in settings: Automatic, Tell me only, Off. Said by a band pinned under MAP between raids, and by the UPDATE
// READY cue (owner, 2026-10-15: "A Beta-tester did not realize there is an update, which shows me that it is currently
// too subtle. I, however, do not want to have a blocking modal like a pop-up"; from a panel of four: B, "A cue, then
// the band"): the cue when a download completes in the menus, and once more at the first menu after each raid while
// the update waits, after RAID OVER and its replay, QUEST COMPLETE and SEEN IN YOUR STASH (Shturmap.Session.UpdateCue);
// as it fades, the band glows.
public sealed partial class MainWindow
{
    private Updater? _updater;
    private DispatcherQueueTimer? _updateTimer;
    private bool _updating;

    // When the UPDATE READY cue is due, and the wait for the map's middle to be free of other cues.
    private readonly UpdateCue _updateCue = new();
    private DispatcherQueueTimer? _updateCueWait;
    private DateTime? _updateCueQuietSince;
    // The band is on screen: it glows as it comes, and comes back after a raid.
    private bool _updateBandShown;
    private DispatcherQueueTimer? _updateGlow;

    // How long the map's middle has been free before the cue comes: what another cue's end queues (QUEST COMPLETE's after
    // a replay comes a moment after the replay ends) is up by then.
    private static readonly TimeSpan UpdateCueQuiet = TimeSpan.FromSeconds(0.5);

    // The band glows as the cue fades (its last 0.6 s of 5 s): the eye goes from the one to the other.
    private static readonly TimeSpan UpdateGlowAt = TimeSpan.FromSeconds(4.6);

    public Visibility ShownIfUpdateMode(string mode, string value) => mode == value ? Visibility.Visible : Visibility.Collapsed;

    public Brush UpdateModeBrush(string mode, string value) => Resource(mode == value ? "AmberBrush" : "MutedBrush");

    // The band's buttons: RESTART NOW or DOWNLOAD; none while a version downloads.
    public Visibility ShownIfEither(bool one, bool other) => one || other ? Visibility.Visible : Visibility.Collapsed;

    private UpdateMode CurrentUpdateMode => UpdateModes.Parse(_session.GetSetting(UpdateModes.Setting));

    /// <summary>The kind of build in words, after the version at the foot of settings (<see cref="App.BuildKind"/> is the
    /// English one reports and diagnostics carry).</summary>
    private static string BuildKindText(string kind) => kind switch
    {
        "installed" => AppTexts.BuildInstalled,
        "dev build" => AppTexts.BuildDev,
        _ => AppTexts.BuildFolder,
    };

    /// <summary>The session has started: start asking for new versions, as the setting says.</summary>
    public void StartUpdates(Updater updater)
    {
        _updater = updater;
        ViewModel.UpdatesAvailable = updater.CanUpdate;
        ViewModel.UpdatesNote = Distribution.NoUpdatesText(App.BuildKind == "installed", updater.StartProblem is not null);
        ViewModel.UpdateMode = CurrentUpdateMode.ToString();
        updater.Changed += () => DispatcherQueue.TryEnqueue(RefreshUpdates);
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.InRaid))
                RefreshUpdates();
        };
        // A version still waiting at start has its notice, not the cue, until a raid has ended.
        _updateCue.AtStart(updater.Stage, updater.Version);
        RefreshUpdates();
        if (updater.NotApplied)
        {
            AppLog.Warn($"Updates: {updater.Version} was downloaded but couldn't be applied at this start; Velopack's log says why");
            ShowNotice(UpdatePolicy.NotAppliedNotice(updater.Version), TimeSpan.FromSeconds(20));
        }
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
        UpdatePolicy.Decide(CurrentUpdateMode, _updater?.CanUpdate == true, ViewModel.InRaid, _updater?.Stage ?? UpdateStage.None, _updater?.Version,
            _updater?.NotApplied == true);

    private void RefreshUpdates()
    {
        var decision = Decide();
        var (eyebrow, note) = UpdatePolicy.Words(decision.Band);
        var shown = decision.Band != UpdateBand.None;
        ViewModel.UpdateTitle = shown ? UpdateTitleText(_updater?.Version) : "";
        ViewModel.UpdateNote = note;
        ViewModel.UpdateOffersDownload = decision.OfferDownload;
        ViewModel.UpdateOffersRestart = decision.OfferRestart;
        ViewModel.UpdateEyebrow = eyebrow;
        // It glows in as it shows, the first time and at each return to the menus, and again as the cue fades.
        if (shown && !_updateBandShown)
            GlowUpdateBand();
        _updateBandShown = shown;
        ConsiderUpdateCue();
    }

    /// <summary>"0.6.1 · OKAMI": the version with its release's name, as the What's New card's heading says a version
    /// (<see cref="WhatsNew.Tag"/>), where the app's What's New knows the name (a patch release keeps its line's,
    /// <see cref="WhatsNew.NameOf"/>); else the version alone (a new line's name comes with its release).</summary>
    private string UpdateTitleText(string? version) =>
        version is null ? ""
        : WhatsNew.NameOf(WhatsNewSections, WhatsNew.VersionOf(version)) is { } name ? $"{version} · {UiLanguage.Upper(name)}"
        : version;

    // ---- the UPDATE READY cue (Shturmap.Session.UpdateCue) ----

    // The rule says the cue is due. Never in a snapshot run: a snapshot never shows it by itself (a script's "trigger
    // update" plays it; "--show-update" shows the band).
    private bool UpdateCueDue =>
        !SnapshotMode && _updater is { } updater
        && _updateCue.Due(CurrentUpdateMode, updater.CanUpdate, updater.Stage, updater.Version, ViewModel.InRaid ? RaidPhase.InRaid : RaidPhase.Menu);

    // The map's middle holds something else: a cue (RAID OVER, QUEST COMPLETE, SEEN IN YOUR STASH, the roll call, …), a
    // replay or the completions waiting for it, the tour, a preview. The cue waits its turn, as those wait among
    // themselves, and comes once the middle has been free a moment.
    private bool MapMiddleTaken =>
        CuePanel.Visibility == Visibility.Visible || _replay is not null || _completedWaiting.Count > 0 || TourOpen || _previewing is not null;

    private void ConsiderUpdateCue()
    {
        if (!UpdateCueDue)
        {
            _updateCueWait?.Stop();
            _updateCueQuietSince = null;
            return;
        }
        if (_updateCueWait is null)
        {
            _updateCueWait = DispatcherQueue.CreateTimer();
            _updateCueWait.Interval = TimeSpan.FromSeconds(0.25);
            _updateCueWait.IsRepeating = true;
            _updateCueWait.Tick += (_, _) => ConsiderUpdateCue();
        }
        var now = DateTime.Now;
        if (MapMiddleTaken)
            _updateCueQuietSince = null;
        else
            _updateCueQuietSince ??= now;
        if (_updateCueQuietSince is not { } quiet || now - quiet < UpdateCueQuiet)
        {
            if (!_updateCueWait.IsRunning)
                _updateCueWait.Start();
            return;
        }
        _updateCueWait.Stop();
        _updateCueQuietSince = null;
        ShowUpdateCue();
    }

    private void ShowUpdateCue()
    {
        if (_updater is not { } updater)
            return;
        _updateCue.Shown(updater.Stage, updater.Version);
        AppLog.Info($"Updates: the cue for {updater.Version} ({updater.Stage})");
        Study.Ui("updates.cue", ("stage", updater.Stage));
        ShowCue(new ViewCue(CueKind.UpdateReady, UpdateTitleText(updater.Version)));
        // As the cue fades, the band glows: the eye goes from the one to the other. With animation effects off, neither
        // moves.
        GlowUpdateBand(UpdateGlowAt);
    }

    // The cue's eyebrow is the band's: UPDATE READY, or DOWNLOAD AVAILABLE in "Tell me only"; its title the version.
    private (string Eyebrow, string Title, string Detail) UpdateCueText(ViewCue cue) =>
        (UpdatePolicy.Words(_updater?.Stage == UpdateStage.Found ? UpdateBand.Available : UpdateBand.Ready).Eyebrow, cue.MapName, "");

    /// <summary>The cue's amber line under its title, where the first raid's line stands (CueFirst): where to restart
    /// (or download), and never in a raid. It wraps clear of the map's buttons as the first raid's line does
    /// (<see cref="ShowCueLine"/>): at the smallest window the German line ran under them.</summary>
    private void UpdateCueLine(ViewCue cue)
    {
        if (cue.Kind == CueKind.UpdateReady)
            ShowCueLine(_updater?.Stage == UpdateStage.Found ? AppTexts.CueUpdateDownload : AppTexts.CueUpdateRestart);
    }

    /// <summary>A raid is over, live (RAID OVER; never one read back from the log at start): the cue is due once more.</summary>
    private void UpdatesAfterRaid()
    {
        _updateCue.RaidEnded();
        ConsiderUpdateCue();
    }

    // The band glows in as the stash marks do (Controls.Glow): the title's gold copy eases to ink over 1.2 s and the plate
    // flashes once at 22 % for 0.9 s; with animation effects off, nothing moves. After a delay: as the cue fades.
    private void GlowUpdateBand(TimeSpan? after = null)
    {
        if (after is not { } delay)
        {
            Controls.Glow.Fade(UpdateBandGlow, 1, Controls.Glow.Length);
            Controls.Glow.Fade(UpdateBandFlash, Controls.Glow.FlashOpacity, Controls.Glow.FlashLength);
            return;
        }
        if (_updateGlow is null)
        {
            _updateGlow = DispatcherQueue.CreateTimer();
            _updateGlow.IsRepeating = false;
            _updateGlow.Tick += (_, _) => GlowUpdateBand();
        }
        _updateGlow.Stop();
        _updateGlow.Interval = delay;
        _updateGlow.Start();
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

    // Velopack's updater is started first and waits for this process to end; only once it runs is the session closed
    // and the running marker let go (so the next start doesn't take the restart for a crash), and the app ends as
    // Velopack's own restart ended it. If the updater can't start, the session goes on and a notice says why (review of
    // 2026-10-09: the session was closed first, and a download that was gone left a window that drew nothing). The cue
    // says nothing more after the click, whatever comes of it.
    private async void OnUpdateRestartClick(object sender, RoutedEventArgs e)
    {
        if (_updater is null || ViewModel.InRaid || _updater.Stage != UpdateStage.Ready)
            return;
        Study.Ui("updates.restart");
        _updateCue.Restarting();
        if (_updater.StartRestart() is { } why)
        {
            ShowNotice(why, TimeSpan.FromSeconds(12));
            RefreshUpdates();
            return;
        }
        // From here the updater waits for this process: it ends whatever happens.
        try
        {
            await _session.DisposeAsync();
        }
        catch (Exception problem)
        {
            AppLog.Warn("Updates: closing the session for RESTART NOW failed; restarting anyway", problem);
        }
        try
        {
            ((App)Application.Current).EndSession();
        }
        catch (Exception problem)
        {
            AppLog.Warn("Updates: ending the session for RESTART NOW failed; restarting anyway", problem);
        }
        Environment.Exit(0);
    }
}
