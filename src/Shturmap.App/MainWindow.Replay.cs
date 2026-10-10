using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Shturmap.Map;
using Shturmap.Session;

namespace Shturmap.App;

// The raid replay (owner, 2026-10-07: "B and D", then "C, but encode raid time to the color of the pen stroke", "Replay
// timeline 1", "Replay link: ii"; docs/DESIGN.md "Map drawing", *The raid replay*). At a raid's end with a few
// screenshots, RAID OVER enters as every cue does; then its title goes, the band slides to the map's foot and becomes the
// timeline, the view frames the raid's positions above it, and the pen draws them in the raid's time; the end holds, then
// it all fades. REPLAY on the last raid's line plays it again until the next raid loads. With Windows' animation effects
// off (and in snapshots) the end shows at once, for the time it would have played.
public sealed partial class MainWindow
{
    private RaidReplay? _replay;
    private string _replayHow = "";
    private DateTime _replayStarted;
    private bool _replayFramed;
    private bool _replayStill;
    private DispatcherQueueTimer? _replayFrames;

    // How long the middle cue stays before it fades and the band goes down (ShowCue).
    private static TimeSpan ReplayCueLength => ReplayTiming.Entrance + TimeSpan.FromSeconds(0.5);

    /// <summary>Plays a raid's replay: <paramref name="how"/> is "end" (RAID OVER) or "link" (REPLAY).</summary>
    private void PlayReplay(RaidReplay replay, string how)
    {
        StopReplay("again");
        // The replay takes the map from a preview (of another map, or of What's New).
        if (_previewing is not null)
        {
            _previewTimer?.Stop();
            EndPreview(restore: true);
        }
        _replay = replay;
        _replayHow = how;
        _replayStarted = DateTime.Now;
        _replayFramed = false;
        _replayStill = SnapshotMode || !new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
        ReplayBand.Show(replay, ReplayWords(replay));
        Study.Ui("replay.play", ("how", how), ("positions", replay.Fixes.Count), ("minutes", replay.Minutes));
        if (_replayStill)
        {
            // The end at once, for as long as it would have played (a snapshot keeps it up to be looked at).
            ReplayBand.SetMinute(replay.Minutes);
            ReplayBand.Opacity = 1;
            ReplayBandShift.Y = 0;
            ReplayBand.Visibility = Visibility.Visible;
            CuePanel.Visibility = Visibility.Collapsed;
            ApplyReplay();
            if (!SnapshotMode)
                StartReplayFrames();
            return;
        }
        ReplayBand.Visibility = Visibility.Collapsed;
        StartReplayFrames();
    }

    // "CUSTOMS · 33 MIN": the raid as the cue's eyebrow says it.
    private static string ReplayWords(RaidReplay replay) => AppTexts.CueMapAndLength(map: Caps.Of(replay.MapName), minutes: (int)replay.Minutes);

    private void StartReplayFrames()
    {
        if (_replayFrames is null)
        {
            _replayFrames = DispatcherQueue.CreateTimer();
            _replayFrames.Interval = TimeSpan.FromMilliseconds(15);
            _replayFrames.Tick += (_, _) => ReplayFrame();
        }
        _replayFrames.Start();
    }

    // One frame: where the replay is by the time since it began. The phases are ReplayTiming's.
    private void ReplayFrame()
    {
        if (_replay is not { } replay)
            return;
        var t = DateTime.Now - _replayStarted;
        if (t >= ReplayTiming.Total)
        {
            StopReplay("done");
            return;
        }
        if (_replayStill)
        {
            ApplyReplay();
            return;
        }
        if (t < ReplayTiming.Entrance)
            return;
        // The band comes up from the foot and the view frames the raid, as the middle cue fades (ShowCue).
        if (ReplayBand.Visibility != Visibility.Visible)
            ReplayBand.Visibility = Visibility.Visible;
        var down = Math.Clamp((t - ReplayTiming.Entrance) / ReplayTiming.Down, 0, 1);
        var eased = 1 - Math.Pow(1 - down, 3);
        var fadeStarts = ReplayTiming.Total - ReplayTiming.Fade;
        var fade = t > fadeStarts ? 1 - (t - fadeStarts) / ReplayTiming.Fade : 1;
        ReplayBandShift.Y = (1 - eased) * Controls.ReplayBand.Foot;
        ReplayBand.Opacity = Math.Min(eased, fade);
        ReplayBand.SetMinute(ReplayMinuteAt(t, replay));
        ApplyReplay();
    }

    private double ReplayMinuteAt(TimeSpan t, RaidReplay replay) =>
        _replayStill ? replay.Minutes : ReplayTiming.MinuteAt(t - ReplayTiming.PlayStarts, replay.Minutes);

    /// <summary>
    /// The replay's state into the map's scene, when the scene in the view is the replay's map (UpdateMap calls this for
    /// each new scene too); the view frames the raid the first time it can.
    /// </summary>
    private void ApplyReplay()
    {
        if (Map.Scene is not { } scene)
            return;
        if (_replay is not { } replay || !ShowsMapOf(scene, replay))
        {
            if (scene.Replay is not null)
            {
                scene.Replay = null;
                Map.Redraw();
            }
            return;
        }
        var t = DateTime.Now - _replayStarted;
        if (!_replayStill && t < ReplayTiming.Entrance)
            return;
        if (!_replayFramed)
        {
            _replayFramed = true;
            var view = Map.FramingAbove(scene, replay.Fixes.Select(f => f.Position).ToList(), 40, Controls.ReplayBand.Foot, 300);
            if (_replayStill)
                Map.Jump(view);
            else
                Map.AnimateView(view, ReplayTiming.Down);
        }
        var fadeStarts = ReplayTiming.Total - ReplayTiming.Fade;
        scene.Replay = replay;
        scene.ReplayFoot = (float)Controls.ReplayBand.Foot;
        scene.ReplayMinute = ReplayMinuteAt(t, replay);
        scene.ReplayDone = _replayStill || t >= ReplayTiming.PlayStarts + ReplayTiming.Play;
        scene.ReplayOpacity = _replayStill ? 1f
            : (float)Math.Clamp(Math.Min((t - ReplayTiming.Entrance) / ReplayTiming.Down, t > fadeStarts ? 1 - (t - fadeStarts) / ReplayTiming.Fade : 1), 0, 1);
        Map.Redraw();
    }

    private bool ShowsMapOf(MapScene scene, RaidReplay replay) =>
        _snapshot?.Data?.DefinitionFor(replay.MapNormalizedName)?.Key is { } key && scene.Definition.Key == key && _previewing is null;

    /// <summary>Ends a replay (it has played, another begins, a raid loads, a preview takes the map), and says why.</summary>
    private void StopReplay(string why)
    {
        if (_replay is null)
            return;
        Study.Ui("replay.end", ("why", why), ("how", _replayHow), ("s", DateTime.Now - _replayStarted));
        _replay = null;
        _replayFrames?.Stop();
        // Its glide onto the raid's positions, if it is still under way (a raid loading on the same map keeps the scene).
        Map.StopViewAnimation();
        ReplayBand.Visibility = Visibility.Collapsed;
        if (Map.Scene is { Replay: not null } scene)
        {
            scene.Replay = null;
            Map.Redraw();
        }
        // Quests completed while it played show now (MainWindow.Completion).
        ShowWaitingCompletions(why);
    }

    // REPLAY on the last raid's line: the raid's map comes back on screen, and the replay plays on it.
    private async void OnReplayClick(object sender, RoutedEventArgs e)
    {
        if (_snapshot?.Replay is not { Plays: true } replay || ViewModel.InRaid)
            return;
        if (_snapshot.Map?.NormalizedName != replay.MapNormalizedName)
            await _session.SelectMapAsync(replay.MapNormalizedName);
        ShowCue(new ViewCue(CueKind.RaidOver, replay.MapName, TimeSpan.FromMinutes(replay.Minutes), Replay: replay), "link");
    }
}
