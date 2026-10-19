using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Shturmap.App.Controls;
using Shturmap.App.Rules;
using Shturmap.Core.Operational;
using Shturmap.Session;

namespace Shturmap.App;

// The daily and weekly tasks a Tasks screenshot read, called out as the quests' are (owner, 2026-10-19, after trying the
// dev build: "they should get the same animation as the other quests. Be mindful of multiple missions started at the
// same time, we might want to have them in one animation included"; docs/DESIGN.md §4, principle 11, and "Screen
// anatomy", *Operational tasks*). A reading that brought tasks the set before didn't have calls the cue: "FROM YOUR
// SCREENSHOT" over "3 DAILIES AND WEEKLIES", the stamp with the operational glyph, and a row for every task the run of
// screenshots read, rising in one after another as the roll call's rows do: the framed glyph, the synopsis, and in muted
// capitals the map and the time left, the one that ends soonest first. A later reading of the same run (screenshots within
// a minute of each other; Shturmap.Core.Operational.OperationalReadings) joins it while it is up and calls none after it;
// a reading that brings no new task calls none. It stays 5 s after the last reading joined it, 12 s at most, as QUEST
// COMPLETE and SEEN IN YOUR STASH do, and steps back to the one-line notice while a replay plays, one of them is up or the
// tour shows its own cue. The rail's rows the reading brought glow in as it shows (Controls.Glow): its after-effect. The
// whole cue keeps to its room as the roll call's does (FitTasksCue), and its rows are a reading, not a clock: their time
// left stays as read, and a row whose task runs out while the cue is up says ENDED in place of it (WatchTasksEnds).
public sealed partial class MainWindow
{
    // A row of the cue: the task's identity, its words, and its parts for the entrance; the task as last read, and when.
    private sealed record TaskCueRow(string Key, Grid Element, TranslateTransform Shift, TextBlock Synopsis, TextBlock Line)
    {
        public OperationalShown Task { get; set; } = null!;
        public DateTimeOffset ReadAt { get; set; }
    }

    // The tasks the cue on screen lists, its rows, and since when it stands.
    private readonly List<OperationalShown> _tasksCue = [];
    private readonly List<TaskCueRow> _tasksRows = [];
    private TextBlock? _tasksMore;
    private DispatcherQueueTimer? _tasksHide;
    private DispatcherQueueTimer? _tasksEnds;
    private DateTime _tasksSince;

    // How long the cue stays after the last reading joined it, and at most in all (QUEST COMPLETE's and SEEN IN YOUR
    // STASH's).
    private static readonly TimeSpan TasksStays = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TasksAtMost = TimeSpan.FromSeconds(12);

    // A row's words at most, in DIPs, and the room its framed glyph and the gap take before them; how far a row rises in.
    private const double TasksTextMost = 420;
    private const double TasksGlyphRoom = 30;
    private const double TasksRiseBy = 8;

    // DIPs the cue keeps above the map's foot (WIKI MAP and the credit), as What's New's plate does.
    private const double TasksCueFoot = 8;

    // The cue's gold rules at their own width (MainWindow.xaml); the tasks' cue keeps them clear of the map's buttons, as
    // its rows.
    private const double TasksRuleMost = 460;

    // The room the cue was last fitted to: the notes' clearance at the map's top, the foot's, and the map's size.
    private (double Head, double Foot, double Width, double Height) _tasksFitted;

    private bool TasksShowing => _tasksCue.Count > 0;

    /// <summary>
    /// The cue of the tasks read arrived. True when ShowCue has nothing more to do: a later reading of the same run joined
    /// the cue on screen (or, the cue gone, calls none: the rail's rows glow), or the notice said it because a replay
    /// plays, QUEST COMPLETE or SEEN IN YOUR STASH is up or the tour shows its own cue.
    /// </summary>
    private bool TasksReadHandled(ViewCue cue)
    {
        var tasks = cue.Tasks ?? [];
        if (tasks.Count == 0)
            return true;
        if (cue.Joins)
        {
            if (TasksShowing)
                JoinTasksCue(tasks);
            return true;
        }
        if (_replay is not null || CompletedShowing || StashShowing || _tourCue)
        {
            ShowNotice(SessionTexts.OperationalRead(count: tasks.Count));
            return true;
        }
        EndTasksRead();
        _tasksCue.AddRange(OperationalCueWords.Rows(tasks));
        _tasksSince = DateTime.Now;
        return false;
    }

    // The cue's words: "FROM YOUR SCREENSHOT" over "3 DAILIES AND WEEKLIES", no line under it (the rows say the rest).
    private (string Eyebrow, string Title, string Detail) TasksCueText()
    {
        var (eyebrow, title) = OperationalCueWords.Of(_tasksCue.Count);
        return (eyebrow, title, "");
    }

    // ShowCue lays the rows out under the title (the kit's place), the one that ends soonest first; returns them for the
    // entrance.
    private List<(FrameworkElement Row, TranslateTransform Shift)> ShowTasksRows()
    {
        ClearTasksRows();
        // Six places, the last "+N" past that (OperationalCueWords.Places).
        var shown = OperationalCueWords.Places(_tasksCue.Count).Rows;
        var rows = new List<(FrameworkElement, TranslateTransform)>();
        var now = DateTimeOffset.Now;
        var keys = OperationalReadings.Keys(_tasksCue.Select(t => t.Task).ToList());
        for (var i = 0; i < shown; i++)
        {
            var row = AddTaskRow(keys[i], _tasksCue[i], now);
            rows.Add((row.Element, row.Shift));
        }
        SayTasksMore();
        CueTasksBox.Visibility = Visibility.Visible;
        WatchTasksEnds();
        return rows;
    }

    // Any other cue: no rows of tasks, and the band the other cues' again where this cue had fitted it to its room.
    private List<(FrameworkElement Row, TranslateTransform Shift)> ClearTasksRows()
    {
        var had = CueTasksBox.Visibility == Visibility.Visible;
        _tasksEnds?.Stop();
        _tasksRows.Clear();
        _tasksMore = null;
        CueTasks.Children.Clear();
        CueTasksBox.Visibility = Visibility.Collapsed;
        CueTasksBox.MaxWidth = CueTasksBox.MaxHeight = double.PositiveInfinity;
        if (had)
        {
            CueFit.MaxHeight = double.PositiveInfinity;
            CuePanel.Margin = new Thickness(0);
            CueRuleTop.Width = CueRuleBottom.Width = TasksRuleMost;
        }
        return [];
    }

    // A row in the roll call's row style: the task's framed glyph as on a NEXT RAID row (the frame says "operational") in
    // the 20 DIP box a row's triangle has; its synopsis in the cue's 15 px; under it, in muted capitals, its map or ANY MAP
    // and its time left ("CUSTOMS · 8 H LEFT"), broken only between words.
    private TaskCueRow AddTaskRow(string key, OperationalShown task, DateTimeOffset now)
    {
        var shift = new TranslateTransform();
        var element = new Grid { ColumnSpacing = 10, RenderTransform = shift, HorizontalAlignment = HorizontalAlignment.Left };
        element.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        element.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var glyph = new Grid { Width = 16, Height = 16 };
        if (task.Task.Kind == OperationalKind.Unknown)
            glyph.Children.Add(new FontIcon { Glyph = OperationalGlyph, FontSize = 14, Foreground = Resource("AmberBrush") });
        else
            glyph.Children.Add(new KindGlyph { Kind = OperationalLine.GlyphOf(task.Task.Kind), Brush = Resource("AmberBrush") });
        element.Children.Add(new Border
        {
            BorderBrush = Resource("LineStrongBrush"), BorderThickness = new Thickness(1), Padding = new Thickness(1), Child = glyph,
            VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false,
        });
        var synopsis = new TextBlock
        {
            Style = TextStyle("TitleText"), FontSize = 15, LineHeight = RollLine, LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            TextWrapping = TextWrapping.WrapWholeWords, MaxWidth = TasksTextMost,
        };
        var line = new TextBlock
        {
            Style = TextStyle("StatusText"), FontSize = 11.5, Foreground = Resource("MutedBrush"), TextWrapping = TextWrapping.WrapWholeWords,
            MaxWidth = TasksTextMost, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 1, 0, 0),
        };
        var words = new StackPanel { Spacing = 1 };
        words.Children.Add(synopsis);
        words.Children.Add(line);
        Grid.SetColumn(words, 1);
        element.Children.Add(words);
        CueTasks.Children.Add(element);
        var row = new TaskCueRow(key, element, shift, synopsis, line);
        SayTaskRow(row, task, now);
        _tasksRows.Add(row);
        return row;
    }

    // A row's words, from the task as last read (a later reading of the run may have brought its objectives), its time left
    // as of that reading.
    private void SayTaskRow(TaskCueRow row, OperationalShown task, DateTimeOffset now)
    {
        row.Task = task;
        row.ReadAt = now;
        row.Synopsis.Text = OperationalRail.Synopsis(OperationalWords.Synopsis(task.Task), task.Items.Select(i => i.Name));
        SayTaskLine(row, now);
    }

    // A row's muted line: its map or ANY MAP and its time left as read, or ENDED once its task has run out.
    private void SayTaskLine(TaskCueRow row, DateTimeOffset now)
    {
        var map = row.Task.MapKey is { } key ? _snapshot?.Data?.MapByNormalizedName(key)?.Name ?? key : null;
        row.Line.Text = Caps.Of(OperationalCueWords.Line(row.Task, map, row.ReadAt, now));
    }

    /// <summary>
    /// The rows are a reading, not a clock (the coordinator's rule after the QA pass of 2026-10-19, F5: a task that ran out
    /// while the cue was up still said "WOODS · 1 MIN LEFT" beside the rail's "1 ended at …"): their time left stays as
    /// read, and a row whose task runs out while the cue is up says ENDED in place of it, at that moment.
    /// </summary>
    private void WatchTasksEnds()
    {
        _tasksEnds?.Stop();
        if (_stopped)
            return;
        var now = DateTimeOffset.Now;
        DateTimeOffset? next = null;
        foreach (var row in _tasksRows)
        {
            if (row.Task.Task.ExpiresAt is not { } end)
                continue;
            if (end <= now)
                SayTaskLine(row, now);
            else if (next is null || end < next)
                next = end;
        }
        // The cue stands 12 s at most (a snapshot's, which keeps it up, some seconds more): an end further off than a
        // minute is never seen in it.
        if (next is not { } at || at - now > TimeSpan.FromMinutes(1))
            return;
        _tasksEnds ??= OneShot(WatchTasksEnds);
        _tasksEnds.Interval = at - now + TimeSpan.FromMilliseconds(50);
        _tasksEnds.Start();
    }

    // The last place says how many more there are than rows ("+2"), in the roll call's way.
    private void SayTasksMore()
    {
        var more = _tasksCue.Count - _tasksRows.Count;
        if (more <= 0)
        {
            if (_tasksMore is not null)
                CueTasks.Children.Remove(_tasksMore);
            _tasksMore = null;
            return;
        }
        if (_tasksMore is null)
        {
            _tasksMore = new TextBlock
            {
                Style = TextStyle("FigureText"), Foreground = Resource("MutedBrush"), HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(TasksGlyphRoom, 0, 0, 0),
            };
            CueTasks.Children.Add(_tasksMore);
        }
        _tasksMore.Text = OperationalCueWords.More(more);
    }

    /// <summary>
    /// The whole cue in its room, as the roll call's (docs/DESIGN.md §4, principle 11, "The whole cue in its room"; QA pass
    /// of 2026-10-19, F1 and F2: at 900×560 under a notice its title stood whole in two lines and its six rows shrank to
    /// about 36 %, unreadable, and without one its bottom rule ran through WIKI MAP): from 8 DIP under the notes at the
    /// map's top (a notice) to <see cref="TasksCueFoot"/> above the map's foot (WIKI MAP and the credit), the title laid out
    /// as it ends, in two lines at most (FitCueTitle). Where it is taller than that room, its rows are drawn smaller
    /// (FitTasksRows), down to 60 % of the size the column's width leaves them; past that the whole cue, title and all, is
    /// drawn smaller to the room, never cut, its rows at the size the column's width leaves them. It stands in the middle
    /// of the map, lower only as far as the notes need and higher only as far as the foot needs, as What's New's plate does.
    /// </summary>
    private void FitTasksCue()
    {
        if (_tasksRows.Count == 0 || Map.ActualWidth <= 0 || Map.ActualHeight <= 0)
            return;
        // The notes laid out first: a notice may have come with the reading.
        CuePanel.UpdateLayout();
        var height = Map.ActualHeight;
        var (head, foot) = TasksCueBounds();
        _tasksFitted = (head, foot, Map.ActualWidth, height);
        var room = Math.Max(4 * RollLine, height - head - foot);
        // The gold rules clear of the map's buttons, as the rows (QA pass of 2026-10-19, F2: at 900×560 the bottom rule ran
        // under the "−" button), at their own width where the column has room.
        CueRuleTop.Width = CueRuleBottom.Width = Math.Min(TasksRuleMost, Math.Max(4 * TasksGlyphRoom, Map.ActualWidth - 2 * RollClearOfButtons));
        CueFit.MaxHeight = double.PositiveInfinity;
        var wide = FitTasksRows(double.PositiveInfinity);
        var tall = FitTasksRows(room);
        if (tall < RollRowsLeast && tall < wide - 1e-3)
        {
            // The rows at the size the column's width leaves them, and the whole cue drawn smaller to the room.
            FitTasksRows(double.PositiveInfinity);
            CueFit.MaxHeight = room;
        }
        CuePanel.UpdateLayout();
        // Centred in the map, the panel moves by half of its margins' difference: down to clear the notes, up to clear the
        // foot.
        var cue = CueFit.ActualHeight;
        var at = Math.Clamp((height - cue) / 2, head, Math.Max(head, height - foot - cue));
        var shift = 2 * at - (height - cue);
        var margin = shift >= 0 ? new Thickness(0, shift, 0, 0) : new Thickness(0, 0, 0, -shift);
        if (Math.Abs(CuePanel.Margin.Top - margin.Top) > 0.5 || Math.Abs(CuePanel.Margin.Bottom - margin.Bottom) > 0.5)
            CuePanel.Margin = margin;
        CuePanel.UpdateLayout();
        // The sizes in the app log (developer detail), for the QA's stills.
        static double Scale(FrameworkElement box, FrameworkElement child) => child.ActualWidth > 0 ? box.ActualWidth / child.ActualWidth : 1;
        Shturmap.Session.AppLog.Debug(FormattableString.Invariant(
            $"Tasks cue: {cue:0} DIP of a room of {room:0} under {head:0} and above {foot:0}; drawn at {Scale(CueFit, CueStack):0.00}, its title at {Scale(CueTitleBox, (FrameworkElement)CueTitleBox.Child):0.00}, its {_tasksRows.Count} rows at {Scale(CueTasksBox, CueTasks):0.00}"));
    }

    // DIPs at the map's top the cue keeps under (the notes there and 8 more), and at its foot it keeps above.
    private (double Head, double Foot) TasksCueBounds() =>
        (NotesClearance(), MapFoot.ActualHeight >= 1 && Shown(MapFoot) ? MapFoot.Margin.Bottom + MapFoot.ActualHeight + TasksCueFoot : 0);

    // The notes at the map's top or its foot changed while the cue is up: a notice that comes (SEEN IN YOUR STASH's, which
    // steps back to it while this cue is up) has the cue fitted to the smaller room, after the layout pass that brought
    // it; one that goes leaves the cue where it stands, so it doesn't jump while it is read. A new size of the window
    // fits it again.
    private void RefitTasksCue()
    {
        if (_stopped || _tasksRows.Count == 0 || CuePanel.Visibility != Visibility.Visible)
            return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_stopped || _tasksRows.Count == 0 || CuePanel.Visibility != Visibility.Visible)
                return;
            var (head, foot) = TasksCueBounds();
            var was = _tasksFitted;
            if (head > was.Head + 0.5 || foot > was.Foot + 0.5 || Math.Abs(Map.ActualWidth - was.Width) > 0.5 || Math.Abs(Map.ActualHeight - was.Height) > 0.5)
                FitTasksCue();
        });
    }

    /// <summary>
    /// The rows in the room the band has under its title, the cue's room being <paramref name="room"/> DIPs tall
    /// (FitTasksCue): the words as wide as the map's column lets them (clear of the map's buttons, as the roll call's
    /// rows), drawn smaller where the column is too narrow or too short for them all. Returns the scale they are drawn at.
    /// </summary>
    private double FitTasksRows(double room)
    {
        var width = Math.Max(4 * TasksGlyphRoom, Map.ActualWidth - 2 * RollClearOfButtons);
        foreach (var row in _tasksRows)
            row.Synopsis.MaxWidth = row.Line.MaxWidth = Math.Min(TasksTextMost, width - TasksGlyphRoom);
        CueTasksBox.MaxWidth = CueTasksBox.MaxHeight = double.PositiveInfinity;
        CuePanel.UpdateLayout();
        var height = room - (CueStack.ActualHeight - CueTasksBox.ActualHeight);
        CueTasks.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = CueTasks.DesiredSize;
        var scale = Math.Min(1, Math.Min(width / Math.Max(1, size.Width), Math.Max(0, height) / Math.Max(1, size.Height)));
        CueTasksBox.MaxWidth = width;
        CueTasksBox.MaxHeight = Math.Max(RollLine, height);
        CuePanel.UpdateLayout();
        return scale;
    }

    /// <summary>
    /// A later reading of the same run joined the cue on screen: its tasks the cue hasn't listed come in as rows after the
    /// others, rising in as they did; a listed one's words become what it last read (an expanded row's objectives); the
    /// title counts them all and decodes again, the stamp pops once more, and the cue stays a little longer.
    /// </summary>
    private void JoinTasksCue(IReadOnlyList<OperationalShown> tasks)
    {
        var now = DateTimeOffset.Now;
        var keys = OperationalReadings.Keys(tasks.Select(t => t.Task).ToList());
        var listed = OperationalReadings.Keys(_tasksCue.Select(t => t.Task).ToList());
        var fresh = new List<(string Key, OperationalShown Task)>();
        for (var i = 0; i < tasks.Count; i++)
        {
            var at = listed.IndexOf(keys[i]);
            if (at < 0)
            {
                fresh.Add((keys[i], tasks[i]));
                continue;
            }
            _tasksCue[at] = tasks[i];
            if (_tasksRows.FirstOrDefault(r => r.Key == keys[i]) is { } row)
                SayTaskRow(row, tasks[i], now);
        }
        // The new ones the one that ends soonest first, after the rows there (which stay where they stand); as many rows as
        // the cue holds, the last place "+N" past that.
        var before = _tasksRows.Count;
        var most = OperationalCueWords.Places(_tasksCue.Count + fresh.Count).Rows;
        foreach (var task in OperationalCueWords.Rows(fresh.Select(f => f.Task).ToList()))
        {
            _tasksCue.Add(task);
            if (_tasksRows.Count < most)
                AddTaskRow(fresh.First(f => ReferenceEquals(f.Task, task)).Key, task, now);
        }
        SayTasksMore();
        WatchTasksEnds();
        if (fresh.Count == 0)
            return;
        var (eyebrow, title, _) = TasksCueText();
        CueEyebrow.Text = eyebrow;
        FitCueTitle(title);
        CueTitle.Text = title;
        FitTasksCue();
        if (new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            DecodeCueTitle(title);
            Stamp(0);
            var story = new Storyboard();
            for (var i = before; i < _tasksRows.Count; i++)
            {
                var row = _tasksRows[i];
                var at = (RollRiseApart * (i - before)).TotalSeconds + 0.1;
                row.Element.Opacity = 0;
                row.Shift.Y = TasksRiseBy;
                story.Children.Add(Rise(row.Element, "Opacity", at, 0, 1));
                story.Children.Add(Rise(row.Shift, "Y", at, TasksRiseBy, 0));
            }
            story.Begin();
        }
        HideTasksAfter();
        Study.Ui("cue.join", ("kind", CueKind.TasksRead), ("tasks", _tasksCue.Count));
    }

    // One value of a joining row rising in, over the roll call's rise.
    private static DoubleAnimationUsingKeyFrames Rise(DependencyObject target, string property, double at, double from, double to)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var frames = new DoubleAnimationUsingKeyFrames();
        frames.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = from });
        frames.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(at), Value = from });
        frames.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(at) + RollRise, Value = to, EasingFunction = ease });
        Storyboard.SetTarget(frames, target);
        Storyboard.SetTargetProperty(frames, property);
        return frames;
    }

    // The cue goes TasksStays after the last reading joined it, TasksAtMost after it came at most. A developer's snapshot
    // keeps it up.
    private void HideTasksAfter()
    {
        if (KeepsCues)
            return;
        if (_tasksHide is null)
        {
            _tasksHide = WindowTimer(FadeTasksOut);
            _tasksHide.IsRepeating = false;
        }
        _tasksHide.Stop();
        var left = TasksAtMost - (DateTime.Now - _tasksSince);
        _tasksHide.Interval = left < TasksStays ? (left > TimeSpan.Zero ? left : TimeSpan.FromMilliseconds(1)) : TasksStays;
        _tasksHide.Start();
    }

    private void FadeTasksOut()
    {
        _tasksCue.Clear();
        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            CuePanel.Visibility = Visibility.Collapsed;
            ClearTasksRows();
            return;
        }
        var fade = new DoubleAnimation { From = CuePanel.Opacity, To = 0, Duration = TimeSpan.FromSeconds(0.6) };
        Storyboard.SetTarget(fade, CuePanel);
        Storyboard.SetTargetProperty(fade, "Opacity");
        var story = new Storyboard { Children = { fade } };
        story.Completed += (_, _) =>
        {
            if (!ReferenceEquals(story, _cueStory) || TasksShowing || StashShowing || CompletedShowing)
                return;
            CuePanel.Visibility = Visibility.Collapsed;
            ClearTasksRows();
        };
        _cueStory = story;
        story.Begin();
    }

    // The app is closing: the cue's timers stop, as the window's do (StopForExit).
    private void StopTasksCue()
    {
        _tasksHide?.Stop();
        _tasksEnds?.Stop();
    }

    /// <summary>Another cue takes the screen: the cue's state goes (its rows go with the next cue's layout).</summary>
    private void EndTasksRead()
    {
        _tasksCue.Clear();
        _tasksHide?.Stop();
        _tasksEnds?.Stop();
    }
}
