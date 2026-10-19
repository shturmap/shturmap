using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Shturmap.Core.Planning;
using Shturmap.Core.Stash;
using Shturmap.Data.TarkovDev;
using Shturmap.Session;

namespace Shturmap.App;

// SEEN IN YOUR STASH (owner, 2026-10-11: "a little eyecandy animation when the app recognises the player updates the
// stash to reflect the player's action and give proper feedback and look cool visually"; docs/DESIGN.md §4,
// principle 11 and "What you have"): a menu screenshot showed items the quests need, and the cue pictures them under
// its title, each with the count seen, under the stash glyph stamped in as QUEST COMPLETE's check is. A stash is
// paged through in a run of screenshots, so a reading that comes while the cue is up joins it: its new items come
// in after the ones there, the stamp pops again, and the cue stays a little longer. Nothing for a reading that
// showed none (silence stays honest). While a replay plays or QUEST COMPLETE is up, the one-line notice says it
// instead: neither is cut for it. On the rail and the quest card the marks that just arrived glow in by themselves
// (Controls.Glow), from the snapshot that brought them.
public sealed partial class MainWindow
{
    // The items the cue on screen pictures, and until when it stays.
    private readonly List<CueItem> _stashSeen = [];
    private DispatcherQueueTimer? _stashHide;
    private DateTime _stashSince;
    // What's New's example: it ends with the preview, not by itself.
    private bool _stashExample;

    // How long the cue stays after the last reading joined it, and at most in all.
    private static readonly TimeSpan StashStays = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StashAtMost = TimeSpan.FromSeconds(12);

    // How many pictures the cue holds (three rows of eight, as the kit reminder); the rest is "+N".
    private const int StashCueShown = 24;

    private bool StashShowing => _stashSeen.Count > 0;

    // The cues that stay as long as their kind keeps coming and fade by themselves (HideAfter), not after CueLength:
    // YOUR EXTRACTS THIS RAID until 1.5 s after its last icon landed (MainWindow.RollCall).
    private static bool Stays(ViewCue cue) => cue.Kind is CueKind.QuestComplete or CueKind.StashSeen or CueKind.ExitsListed or CueKind.TasksRead;

    private void HideAfter(ViewCue cue)
    {
        if (cue.Kind == CueKind.QuestComplete)
            HideCompletedAfter();
        else if (cue.Kind == CueKind.ExitsListed)
            HideRollCallAfter();
        else if (cue.Kind == CueKind.TasksRead)
            HideTasksAfter();
        else
            HideStashAfter();
    }

    // The stamp's shape: the check for a quest completed, the stash glyph for SEEN IN YOUR STASH, the generic operational
    // glyph (a stopwatch) for the tasks read from the Tasks screen.
    private void StampShape(CueKind kind)
    {
        var glyph = kind is CueKind.StashSeen or CueKind.TasksRead;
        CueStampCheck.Visibility = glyph ? Visibility.Collapsed : Visibility.Visible;
        CueStampGlyph.Visibility = glyph ? Visibility.Visible : Visibility.Collapsed;
        CueStampGlyph.Glyph = kind == CueKind.TasksRead ? OperationalGlyph : StashGlyph;
    }

    // Segoe Fluent Icons: the stash grid the marks carry, and the stopwatch that stands for an operational task.
    private const string StashGlyph = "";
    private const string OperationalGlyph = "";

    /// <summary>
    /// A SEEN IN YOUR STASH cue arrived. True when ShowCue has nothing more to do: it joined the cue on screen, or
    /// the notice said it because a replay plays, QUEST COMPLETE or the cue of the tasks read is up, or the tour shows its
    /// own cue (neither cue of the player's screenshots is cut for the other).
    /// </summary>
    private bool StashSeenHandled(ViewCue cue, bool example)
    {
        var items = cue.Kit ?? [];
        if (!example && (_replay is not null || CompletedShowing || TasksShowing || _tourCue))
        {
            ShowNotice(StashCueWords(items, cue.KitMore).Notice);
            return true;
        }
        if (!StashShowing)
        {
            _stashSeen.AddRange(items);
            _stashSince = DateTime.Now;
            _stashExample = example;
            return false;
        }
        // It joins the cue on screen: the items not yet pictured come in after the others, a newer count replaces
        // the one shown, the line counts them all, the stamp once more, a little longer.
        var before = _stashSeen.Count;
        foreach (var item in items)
        {
            var i = _stashSeen.FindIndex(s => s.ItemId == item.ItemId);
            if (i >= 0)
                _stashSeen[i] = item;
            else
                _stashSeen.Add(item);
        }
        var joined = StashCue();
        CueDetail.Text = StashCueWords(joined.Kit!, joined.KitMore).Detail;
        CueDetail.Visibility = Visibility.Visible;
        var cells = ShowCueKit(joined);
        CueKitLabel.Visibility = Visibility.Collapsed;
        if (new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            var story = new Storyboard();
            for (var i = Math.Min(before, cells.Count); i < cells.Count; i++)
            {
                cells[i].Opacity = 0;
                var at = 0.1 + (i - before) * 0.05;
                var frames = new DoubleAnimationUsingKeyFrames();
                frames.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(at), Value = 0 });
                frames.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(at + 0.2), Value = 1, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                Storyboard.SetTarget(frames, cells[i]);
                Storyboard.SetTargetProperty(frames, "Opacity");
                story.Children.Add(frames);
            }
            story.Begin();
            Stamp(0);
        }
        HideStashAfter();
        Study.Ui("cue.join", ("kind", CueKind.StashSeen), ("items", _stashSeen.Count));
        return true;
    }

    // The cue as it stands: everything seen so far, the first three rows pictured.
    private ViewCue StashCue() =>
        new(CueKind.StashSeen, "", Kit: _stashSeen.Take(StashCueShown).ToList(), KitMore: Math.Max(0, _stashSeen.Count - StashCueShown));

    /// <summary>The cue's words for the items it pictures: "FROM YOUR SCREENSHOT", "SEEN IN YOUR STASH", "3 keys your
    /// quests need" (keys when everything is a key), and the notice that stands in for it.</summary>
    private static (string Eyebrow, string Title, string Detail, string Notice) StashCueWords(IReadOnlyList<CueItem> items, int more)
    {
        var count = items.Count + more;
        var keys = items.Count > 0 && items.All(i => i.Kind == RequirementKind.Key);
        return (AppTexts.CueStashSeenEyebrow, AppTexts.CueStashSeen,
            keys ? AppTexts.CueStashSeenKeys(count: count) : AppTexts.CueStashSeenItems(count: count),
            keys ? SessionTexts.StashSeenKeys(count: count) : SessionTexts.StashSeenItems(count: count));
    }

    private static (string Eyebrow, string Title, string Detail) StashCueText(ViewCue cue)
    {
        var (eyebrow, title, detail, _) = StashCueWords(cue.Kit ?? [], cue.KitMore);
        return (eyebrow, title, detail);
    }

    // The cue goes StashStays after the last reading joined it, StashAtMost after it came at most; the example stays
    // with its preview.
    private void HideStashAfter()
    {
        if (SnapshotMode || _stashExample)
            return;
        if (_stashHide is null)
        {
            _stashHide = WindowTimer(FadeStashOut);
            _stashHide.IsRepeating = false;
        }
        _stashHide.Stop();
        var left = StashAtMost - (DateTime.Now - _stashSince);
        _stashHide.Interval = left < StashStays ? (left > TimeSpan.Zero ? left : TimeSpan.FromMilliseconds(1)) : StashStays;
        _stashHide.Start();
    }

    private void FadeStashOut()
    {
        _stashSeen.Clear();
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
            if (ReferenceEquals(story, _cueStory) && !StashShowing && !CompletedShowing)
                CuePanel.Visibility = Visibility.Collapsed;
        };
        _cueStory = story;
        story.Begin();
    }

    /// <summary>Another cue takes the screen, or the preview that showed the example ends: the cue's state goes.</summary>
    private void EndStashSeen()
    {
        _stashSeen.Clear();
        _stashHide?.Stop();
        _stashExample = false;
    }

    // ---- examples: What's New's previews, the developer view ----

    /// <summary>The cue with example items: a few keys and items the data's quests ask for, the same for every player,
    /// nobody's stash.</summary>
    internal static ViewCue StashExampleCue(GameData data) =>
        new(CueKind.StashSeen, "", Kit: StashExampleItems(data).Select((item, i) => new CueItem(item.Id, item.Kind, Count: i == 1 ? 2 : 1)).ToList());

    // Three keys and three items from the quests' objectives, in the quests' order by name.
    internal static List<(string Id, RequirementKind Kind)> StashExampleItems(GameData data)
    {
        var objectives = data.Tasks.Values.OrderBy(t => t.Name, StringComparer.Ordinal).SelectMany(t => t.Objectives ?? []).ToList();
        var keys = objectives.SelectMany(o => o.RequiredKeys ?? []).Select(k => k.FirstOrDefault()).OfType<string>()
            .Where(data.ItemNames.ContainsKey).Distinct(StringComparer.Ordinal).Take(3);
        var items = objectives.Select(QuestCards.ItemOf).OfType<string>()
            .Where(data.ItemNames.ContainsKey).Distinct(StringComparer.Ordinal).Take(3);
        return keys.Select(id => (id, RequirementKind.Key)).Concat(items.Select(id => (id, RequirementKind.Bring))).ToList();
    }

    // ---- the marks that just arrived glow in (Controls.Glow) ----

    private Dictionary<string, StashMark>? _marksApplied;
    private readonly Dictionary<string, DateTime> _freshMarks = new(StringComparer.Ordinal);
    private static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(2);

    // The marks this snapshot brings or changes, against the one applied before: fresh for a moment, so the rows and
    // cells built from it glow. Not the first snapshot's (the file's marks, the log read back at start), and none in
    // a snapshot run or the demo.
    private void NoteFreshMarks(SessionSnapshot s)
    {
        var now = DateTime.Now;
        if (_marksApplied is { } before && !SnapshotMode && !DemoMode)
        {
            foreach (var (id, mark) in s.Marks)
            {
                if (!before.TryGetValue(id, out var was) || was != mark)
                    _freshMarks[id] = now;
            }
        }
        _marksApplied = s.Marks.ToDictionary(m => m.Key, m => m.Value, StringComparer.Ordinal);
        foreach (var id in _freshMarks.Where(f => now - f.Value > FreshFor).Select(f => f.Key).ToList())
            _freshMarks.Remove(id);
    }

    /// <summary>Whether the row's mark arrived this moment (any of the items the row stands for).</summary>
    private bool FreshMark(RequirementView r) => _freshMarks.Count > 0 && (r.Alternatives ?? [r.ItemId]).Any(_freshMarks.ContainsKey);

    // A BRING row's mark just arrived: its gold overlay fades to ink, the row flashes (Controls.Glow).
    private void OnGlowLoaded(object sender, RoutedEventArgs e) => Controls.Glow.OnLoaded(sender, e);
}
