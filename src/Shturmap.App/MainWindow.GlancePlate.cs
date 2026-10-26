using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Shturmap.App.Rules;
using Shturmap.Core.Desk;
using Shturmap.Core.Raid;
using Shturmap.Session;

namespace Shturmap.App;

// The in-raid glance's plate (owner, 2026-10-19, three choices, and from its panel Variant 1 with the looted container's or
// body's layout mirrored; docs/DESIGN.md §4, "Screen anatomy", *The in-raid glance*): a raid screenshot of an inventory
// screen read (SessionSnapshot.Glance, a new reading each time) puts the plate up at the top of the map's column, under the
// notes, in the desk's language (Controls.GlancePlate, Rules.GlancePlateRules). It stands 6 s from its arrival and fades
// out over 0.4 s (at once with the animation effects off); a newer reading replaces it at once and its clock starts again.
// It never covers the rail (it stands in the map's column) nor a cue: while a cue is up (QUEST COMPLETE, the extract list's
// roll call, any other) a reading waits for it to go, and a cue that comes ends the plate, as any cue ends another. Nothing
// on it takes a click. It draws from the snapshot alone and keeps nothing: the raid's end takes it.
public sealed partial class MainWindow
{
    // The last reading the window took (on the plate, or waiting for a cue to go); the one waiting.
    private RaidGlance? _glanceTaken;
    private RaidGlance? _glanceWaiting;
    private DispatcherQueueTimer? _glanceHide;
    private DispatcherQueueTimer? _glanceWait;
    private Storyboard? _glanceFade;

    // A developer run's made-up reading (GlanceExample), in the snapshot's place while the raid lasts, and its counter.
    private RaidGlance? _glanceExample;
    private int _glanceExampleReadings;

    /// <summary>"--glance &lt;n&gt;" (a fake game's runs: tools\fake-raid.ps1 -Glance): the made-up glance's reading n
    /// (<see cref="GlanceExample"/>) once a raid is on and the data is there, as a raid screenshot's reading brings it.</summary>
    public int? GlanceOnStart
    {
        get => _glanceOnStart;
        set
        {
            _glanceOnStart = value;
            _glanceRun |= value is not null;
        }
    }

    private int? _glanceOnStart;

    // A run started with "--glance": its picture is the plate's.
    private bool _glanceRun;

    /// <summary>A snapshot run keeps its last cue up for its picture, but not one whose picture is the glance's plate
    /// ("--glance"): there the cues go in their time, as live, and the reading, waiting for them as a live one does, comes
    /// after them (UI QA pass of 2026-10-19, F6: the plate and the roll call stood together in a still, a state the player
    /// never sees).</summary>
    private bool KeepsCues => SnapshotMode && !_glanceRun;

    private bool GlanceUp => RaidGlancePlate.Visibility == Visibility.Visible;

    // A cue holds the map's middle (or QUEST COMPLETE waits to show): a reading waits for it to go, in a snapshot run too.
    private bool GlanceWaits => CuePanel.Visibility == Visibility.Visible || _completedWaiting.Count > 0;

    private void InstallGlance()
    {
        // A new room, or notes that come or go at the map's top: the plate laid out anew where it stands.
        Map.SizeChanged += (_, _) => RefitGlance();
        MapNotes.SizeChanged += (_, _) => RefitGlance();
    }

    // Each snapshot: a new reading in a raid puts the plate up, or waits for a cue; out of a raid it goes.
    private void UpdateGlance(SessionSnapshot s)
    {
        if (!ViewModel.InRaid)
            _glanceExample = null;
        var glance = ViewModel.InRaid ? _glanceExample ?? s.Glance : null;
        if (glance is null)
        {
            if (_glanceTaken is not null || GlanceUp)
                EndGlance("raid");
            _glanceTaken = null;
            return;
        }
        if (ReferenceEquals(glance, _glanceTaken))
            return;
        _glanceTaken = glance;
        if (GlanceWaits)
        {
            _glanceWaiting = glance;
            EndGlancePlate();
            WaitForGlanceRoom();
            Study.Ui("glance.wait", ("reading", glance.Reading));
            return;
        }
        ShowGlance(glance);
    }

    // The cue gone, the reading waiting shows; it is checked every quarter of a second.
    private void WaitForGlanceRoom()
    {
        if (_glanceWait is null)
        {
            _glanceWait = WindowTimer(() =>
            {
                if (_glanceWaiting is not { } waiting || !ViewModel.InRaid)
                {
                    _glanceWait?.Stop();
                    _glanceWaiting = null;
                    return;
                }
                if (GlanceWaits)
                    return;
                _glanceWait?.Stop();
                ShowGlance(waiting);
            });
            _glanceWait.Interval = TimeSpan.FromSeconds(0.25);
            _glanceWait.IsRepeating = true;
        }
        if (!_glanceWait.IsRunning)
            _glanceWait.Start();
    }

    private void ShowGlance(RaidGlance glance)
    {
        _glanceWaiting = null;
        _glanceWait?.Stop();
        _glanceFade?.Stop();
        _glanceFade = null;
        PlaceGlance(glance);
        RaidGlancePlate.Opacity = 1;
        RaidGlancePlate.Visibility = Visibility.Visible;
        var laid = RaidGlancePlate.Arrangement;
        Study.Ui("glance.show", ("reading", glance.Reading), ("loot", glance.Loot is not null), ("cell", laid?.Cell ?? 0), ("short", laid?.CutShortCount ?? 0),
            ("cut", laid?.Cut.Count ?? 0));
        var room = RaidGlancePlate.Room;
        AppLog.Info($"In-raid glance: reading {glance.Reading}, {(glance.Loot is not null ? "a LOOT pane" : "no LOOT pane")}" +
                    (laid is null ? "" : $", room {room.Width:0}x{room.Height:0} DIP: {laid.Shown.Count} regions at {laid.Cell:0} DIP, {laid.CutShortCount} cut short, " +
                                         $"{laid.Cut.Count} in the ALSO line"));
        // A snapshot run keeps the plate for its picture.
        _glanceHide?.Stop();
        if (SnapshotMode)
            return;
        if (_glanceHide is null)
        {
            _glanceHide = WindowTimer(FadeGlanceOut);
            _glanceHide.IsRepeating = false;
        }
        _glanceHide.Interval = GlancePlateRules.Stands;
        _glanceHide.Start();
    }

    // The plate in its room: the map column's width less its margins, about half its height, under the notes at its top.
    private void PlaceGlance(RaidGlance glance)
    {
        var width = Math.Max(200, Map.ActualWidth - 2 * GlancePlateRules.Margin);
        var height = Math.Max(120, Map.ActualHeight * GlancePlateRules.HeightShare);
        RaidGlancePlate.Margin = new Thickness(GlancePlateRules.Margin, GlanceTop(), GlancePlateRules.Margin, 0);
        RaidGlancePlate.Show(glance, width, height);
    }

    // DIPs from the map's top: under the notes there and 8 more, or the notes' own margin while they show nothing.
    private double GlanceTop() =>
        MapNotes.ActualHeight >= 1 && Shown(MapNotes) ? MapNotes.Margin.Top + MapNotes.ActualHeight + GlancePlateRules.UnderNotes : GlancePlateRules.Margin + GridStrips.Top;

    // The map's size or the notes changed while the plate stands: laid out anew, after the layout pass that brought it.
    private void RefitGlance()
    {
        if (_stopped || !GlanceUp || RaidGlancePlate.Glance is null)
            return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_stopped && GlanceUp && RaidGlancePlate.Glance is { } glance)
                PlaceGlance(glance);
        });
    }

    // 6 s gone: the plate fades out over 0.4 s, or goes at once with the animation effects off.
    private void FadeGlanceOut()
    {
        _glanceHide?.Stop();
        if (!GlanceUp)
            return;
        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            RaidGlancePlate.Visibility = Visibility.Collapsed;
            return;
        }
        var fade = new DoubleAnimation { From = RaidGlancePlate.Opacity, To = 0, Duration = GlancePlateRules.FadesOut };
        Storyboard.SetTarget(fade, RaidGlancePlate);
        Storyboard.SetTargetProperty(fade, "Opacity");
        var story = new Storyboard { Children = { fade } };
        story.Completed += (_, _) =>
        {
            if (ReferenceEquals(story, _glanceFade))
            {
                RaidGlancePlate.Visibility = Visibility.Collapsed;
                _glanceFade = null;
            }
        };
        _glanceFade = story;
        story.Begin();
    }

    // The plate goes at once (a reading that waits, a cue, the raid's end).
    private void EndGlancePlate()
    {
        _glanceHide?.Stop();
        _glanceFade?.Stop();
        _glanceFade = null;
        RaidGlancePlate.Visibility = Visibility.Collapsed;
    }

    /// <summary>A cue takes the screen: the plate on it goes, as any cue ends another; a reading waiting keeps waiting.</summary>
    private void GlanceGivesWay()
    {
        if (!GlanceUp)
            return;
        EndGlancePlate();
        Study.Ui("glance.end", ("how", "cue"));
    }

    // The raid is over (or the app ends): the plate goes, and a reading waiting with it.
    private void EndGlance(string how)
    {
        var was = GlanceUp || _glanceWaiting is not null;
        _glanceWaiting = null;
        _glanceWait?.Stop();
        EndGlancePlate();
        if (was)
            Study.Ui("glance.end", ("how", how));
    }

    // ---- the made-up glance ----

    // "--glance <n>": the made-up reading once a raid is on and the data is there.
    private void ShowGlanceExampleOnStart(SessionSnapshot s)
    {
        if (GlanceOnStart is not { } reading || s.Data is null || s.Raid.Phase != RaidPhase.InRaid)
            return;
        GlanceOnStart = null;
        ShowGlanceExample(reading, s);
    }

    /// <summary>The made-up glance's reading n as a raid screenshot's reading brings it (<see cref="GlanceExample"/>).</summary>
    private void ShowGlanceExample(int reading, SessionSnapshot s)
    {
        reading = Math.Clamp(reading, 1, GlanceExample.Readings);
        var data = s.Data;
        _glanceExample = GlanceExample.Glance(reading, ++_glanceExampleReadings,
            data is null ? null : id => data.TraderName(id) is { Length: > 0 } name ? name : id, DateTimeOffset.Now);
        AppLog.Info($"Example glance: reading {reading} of {GlanceExample.Readings}");
        UpdateGlance(s);
    }
}
