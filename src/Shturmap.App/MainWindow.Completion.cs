using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Shturmap.App.Rules;
using Shturmap.Map;
using Shturmap.Session;

namespace Shturmap.App;

// A quest completed (owner, 2026-10-07: "For completed quests, also make a nice animation"; docs/DESIGN.md §4,
// principle 11, and "Map drawing"). The log says it when the quest is handed in at its trader, between raids, often
// two to four within a minute (the study log of 1–6 October). The QUEST COMPLETE cue says which, with what it unlocks,
// under a gold check stamped in; one that comes while the cue is up joins it ("3 QUESTS COMPLETE") and keeps it up a
// little longer. On the map the quest's places ring out in gold with a check and go, so the eye sees what left.
public sealed partial class MainWindow
{
    // The quests the cue on screen says, and until when it stays.
    private readonly List<CompletedQuest> _completed = [];
    private DispatcherQueueTimer? _completedHide;
    private DateTime _completedSince;
    // Completions that came while a replay played: they show once it is over.
    private readonly List<CompletedQuest> _completedWaiting = [];

    // How long the cue stays after the last quest joined it, and at most in all.
    private static readonly TimeSpan CompletedStays = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CompletedAtMost = TimeSpan.FromSeconds(12);

    private bool CompletedShowing => _completed.Count > 0;

    /// <summary>
    /// A QUEST COMPLETE cue arrived. True when ShowCue has nothing more to do: it joined the cue on screen, or waits
    /// for a replay to end. Its places on the map ring out either way.
    /// </summary>
    private bool CompletionHandled(CompletedQuest quest)
    {
        _justCompleted[quest.QuestId] = DateTime.Now;
        MatchLeaving();
        if (_replay is not null)
        {
            _completedWaiting.Add(quest);
            return true;
        }
        if (!CompletedShowing)
        {
            _completed.Add(quest);
            _completedSince = DateTime.Now;
            return false;
        }
        // It joins the cue on screen: new words, the title decodes again, the stamp once more, a little longer.
        _completed.Add(quest);
        var (eyebrow, title, detail) = CompletionText();
        CueEyebrow.Text = eyebrow;
        CueDetail.Text = detail;
        CueDetail.Visibility = detail.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            DecodeCueTitle(title);
            Stamp(0);
        }
        else
        {
            CueTitle.Text = title;
        }
        HideCompletedAfter();
        Study.Ui("cue.join", ("kind", CueKind.QuestComplete), ("quests", _completed.Count));
        return true;
    }

    private (string Eyebrow, string Title, string Detail) CompletionText()
    {
        var (eyebrow, title, detail) = CompletionWords.Of(_completed.Select(q => new CompletionWords.Done(q.Name, q.TraderName, q.Unlocks)).ToList());
        return (eyebrow, Caps.Of(title), detail);
    }

    // The cue goes CompletedStays after the last quest joined it, CompletedAtMost after it came at most.
    private void HideCompletedAfter()
    {
        if (SnapshotMode)
            return;
        if (_completedHide is null)
        {
            _completedHide = DispatcherQueue.CreateTimer();
            _completedHide.IsRepeating = false;
            _completedHide.Tick += (_, _) => FadeCompletedOut();
        }
        _completedHide.Stop();
        var left = CompletedAtMost - (DateTime.Now - _completedSince);
        _completedHide.Interval = left < CompletedStays ? (left > TimeSpan.Zero ? left : TimeSpan.FromMilliseconds(1)) : CompletedStays;
        _completedHide.Start();
    }

    private void FadeCompletedOut()
    {
        _completed.Clear();
        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            CuePanel.Visibility = Visibility.Collapsed;
            return;
        }
        var fade = new DoubleAnimation { From = CuePanel.Opacity, To = 0, Duration = TimeSpan.FromSeconds(0.6) };
        Storyboard.SetTarget(fade, CuePanel);
        Storyboard.SetTargetProperty(fade, "Opacity");
        var story = new Storyboard { Children = { fade } };
        story.Completed += (_, _) =>
        {
            if (ReferenceEquals(story, _cueStory) && !CompletedShowing)
                CuePanel.Visibility = Visibility.Collapsed;
        };
        _cueStory = story;
        story.Begin();
    }

    // The stamp: the disc pops in on the quests' gold, a ring leaves it and fades. From <paramref name="at"/> seconds.
    private void Stamp(double at)
    {
        CueStamp.Visibility = Visibility.Visible;
        var story = new Storyboard();
        void Animate(DependencyObject target, string property, EasingFunctionBase ease, params (double Seconds, double Value)[] keys)
        {
            var frames = new DoubleAnimationUsingKeyFrames();
            foreach (var (seconds, value) in keys)
                frames.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(at + seconds), Value = value, EasingFunction = ease });
            Storyboard.SetTarget(frames, target);
            Storyboard.SetTargetProperty(frames, property);
            story.Children.Add(frames);
        }
        EasingFunctionBase spring = new BackEase { Amplitude = 0.9, EasingMode = EasingMode.EaseOut };
        EasingFunctionBase easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
        // Nothing of it shows until its moment: before a key frame is reached, each value is its base value.
        CueStampDisc.Opacity = 0;
        CueStampRing.Opacity = 0;
        CueStampScale.ScaleX = CueStampScale.ScaleY = 1.8;
        CueStampRingScale.ScaleX = CueStampRingScale.ScaleY = 1;
        Animate(CueStampDisc, "Opacity", easeOut, (0, 0), (0.12, 1));
        Animate(CueStampScale, "ScaleX", spring, (0, 1.8), (0.45, 1));
        Animate(CueStampScale, "ScaleY", spring, (0, 1.8), (0.45, 1));
        Animate(CueStampRing, "Opacity", easeOut, (0, 0), (0.2, 0), (0.25, 0.9), (1.05, 0));
        Animate(CueStampRingScale, "ScaleX", easeOut, (0, 1), (0.2, 1), (1.05, 2.1));
        Animate(CueStampRingScale, "ScaleY", easeOut, (0, 1), (0.2, 1), (1.05, 2.1));
        story.Begin();
    }

    // The stamp still, as it ends (animation effects off, snapshots).
    private void StampStill()
    {
        CueStamp.Visibility = Visibility.Visible;
        CueStampDisc.Opacity = 1;
        CueStampScale.ScaleX = CueStampScale.ScaleY = 1;
        CueStampRing.Opacity = 0;
    }

    /// <summary>A replay is over (or gave way): the completions that waited for it show now, or as a notice when a
    /// raid or another cue took over.</summary>
    private void ShowWaitingCompletions(string why)
    {
        if (_completedWaiting.Count == 0)
            return;
        var waiting = _completedWaiting.ToList();
        _completedWaiting.Clear();
        if (why is "raid" or "cue")
        {
            ShowNotice("Completed: " + string.Join(", ", waiting.Select(q => q.Name)));
            return;
        }
        DispatcherQueue.TryEnqueue(() =>
        {
            foreach (var quest in waiting)
                ShowCue(new ViewCue(CueKind.QuestComplete, quest.Name, Completed: quest));
        });
    }

    // ---- the places of a quest just completed ring out on the map ----

    // The quests the log just reported completed, and the quest markers a snapshot just took away: when both meet,
    // those places ring out. Either can come first (the cue, or the snapshot without the quest).
    private readonly Dictionary<string, DateTime> _justCompleted = new(StringComparer.Ordinal);
    private readonly List<(MapMarker Marker, DateTime At)> _removed = [];
    private static readonly TimeSpan CompletedMatch = TimeSpan.FromSeconds(20);

    /// <summary>The scene's quest markers before a snapshot and after it: what went is kept a moment for the matching.</summary>
    private void NoteRemoved(IReadOnlyList<MapMarker> before, IReadOnlyList<MapMarker> after)
    {
        if (before.Count == 0)
            return;
        var still = after.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        var now = DateTime.Now;
        foreach (var marker in before)
        {
            if (marker.Group is not null && marker.Kind is MarkerKind.Objective or MarkerKind.PossibleLocation or MarkerKind.ObjectiveDone
                && !still.Contains(marker.Id))
                _removed.Add((marker, now));
        }
        MatchLeaving();
    }

    private void MatchLeaving()
    {
        var now = DateTime.Now;
        _removed.RemoveAll(r => now - r.At > CompletedMatch);
        foreach (var quest in _justCompleted.Where(q => now - q.Value > CompletedMatch).Select(q => q.Key).ToList())
            _justCompleted.Remove(quest);
        if (Map.Scene is not { } scene)
            return;
        var leaving = scene.Leaving.Where(l => now - l.Since < MapScene.LeaveLength).ToList();
        var added = false;
        foreach (var (marker, _) in _removed.Where(r => r.Marker.Group is { } quest && _justCompleted.ContainsKey(quest)).ToList())
        {
            if (leaving.Any(l => l.Marker.Id == marker.Id))
                continue;
            leaving.Add((marker, now));
            added = true;
        }
        _removed.RemoveAll(r => r.Marker.Group is { } quest && _justCompleted.ContainsKey(quest));
        scene.Leaving = leaving;
        if (added)
            Map.Redraw();
    }
}
