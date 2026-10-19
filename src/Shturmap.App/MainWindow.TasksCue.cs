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
// tour shows its own cue. The rail's rows the reading brought glow in as it shows (Controls.Glow): its after-effect.
public sealed partial class MainWindow
{
    // A row of the cue: the task's identity, its words, and its parts for the entrance.
    private sealed record TaskCueRow(string Key, Grid Element, TranslateTransform Shift, TextBlock Synopsis, TextBlock Line);

    // The tasks the cue on screen lists, its rows, and since when it stands.
    private readonly List<OperationalShown> _tasksCue = [];
    private readonly List<TaskCueRow> _tasksRows = [];
    private TextBlock? _tasksMore;
    private DispatcherQueueTimer? _tasksHide;
    private DateTime _tasksSince;

    // How long the cue stays after the last reading joined it, and at most in all (QUEST COMPLETE's and SEEN IN YOUR
    // STASH's).
    private static readonly TimeSpan TasksStays = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TasksAtMost = TimeSpan.FromSeconds(12);

    // How many rows the cue holds; past that its last place says "+N". The game gives three to five at a time.
    private const int TasksCueShown = 6;

    // A row's words at most, in DIPs, and the room its framed glyph and the gap take before them; how far a row rises in.
    private const double TasksTextMost = 420;
    private const double TasksGlyphRoom = 30;
    private const double TasksRiseBy = 8;

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
        var shown = _tasksCue.Count > TasksCueShown ? TasksCueShown - 1 : _tasksCue.Count;
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
        return rows;
    }

    // Any other cue: no rows of tasks.
    private List<(FrameworkElement Row, TranslateTransform Shift)> ClearTasksRows()
    {
        _tasksRows.Clear();
        _tasksMore = null;
        CueTasks.Children.Clear();
        CueTasksBox.Visibility = Visibility.Collapsed;
        CueTasksBox.MaxWidth = CueTasksBox.MaxHeight = double.PositiveInfinity;
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

    // A row's words, from the task as last read (a later reading of the run may have brought its objectives).
    private void SayTaskRow(TaskCueRow row, OperationalShown task, DateTimeOffset now)
    {
        row.Synopsis.Text = OperationalRail.Synopsis(OperationalWords.Synopsis(task.Task), task.Items.Select(i => i.Name));
        var map = task.MapKey is { } key ? _snapshot?.Data?.MapByNormalizedName(key)?.Name ?? key : null;
        row.Line.Text = Caps.Of(OperationalCueWords.Line(task, map, now));
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
        _tasksMore.Text = "+" + more.ToString(Shturmap.Core.UiLanguage.Culture);
    }

    /// <summary>
    /// The rows in the room the map's column leaves them, the cue's title laid out as it ends: the words as wide as the
    /// column lets them (clear of the map's buttons, as the roll call's rows), and drawn smaller only where the column is
    /// too short for them all (the smallest window), never cut.
    /// </summary>
    private void FitTasksRows()
    {
        if (_tasksRows.Count == 0 || Map.ActualWidth <= 0)
            return;
        var width = Math.Max(4 * TasksGlyphRoom, Map.ActualWidth - 2 * RollClearOfButtons);
        foreach (var row in _tasksRows)
            row.Synopsis.MaxWidth = row.Line.MaxWidth = Math.Min(TasksTextMost, width - TasksGlyphRoom);
        CueTasksBox.MaxWidth = CueTasksBox.MaxHeight = double.PositiveInfinity;
        CuePanel.UpdateLayout();
        var room = Map.ActualHeight - 2 * NotesClearance() - (CueStack.ActualHeight - CueTasksBox.ActualHeight);
        CueTasksBox.MaxWidth = width;
        CueTasksBox.MaxHeight = Math.Max(2 * RollLine, room);
        CuePanel.UpdateLayout();
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
        var total = _tasksCue.Count + fresh.Count;
        var most = total > TasksCueShown ? TasksCueShown - 1 : TasksCueShown;
        foreach (var task in OperationalCueWords.Rows(fresh.Select(f => f.Task).ToList()))
        {
            _tasksCue.Add(task);
            if (_tasksRows.Count < most)
                AddTaskRow(fresh.First(f => ReferenceEquals(f.Task, task)).Key, task, now);
        }
        SayTasksMore();
        if (fresh.Count == 0)
            return;
        var (eyebrow, title, _) = TasksCueText();
        CueEyebrow.Text = eyebrow;
        FitCueTitle(title);
        CueTitle.Text = title;
        FitTasksRows();
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
        if (SnapshotMode)
            return;
        if (_tasksHide is null)
        {
            _tasksHide = DispatcherQueue.CreateTimer();
            _tasksHide.IsRepeating = false;
            _tasksHide.Tick += (_, _) => FadeTasksOut();
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

    /// <summary>Another cue takes the screen: the cue's state goes (its rows go with the next cue's layout).</summary>
    private void EndTasksRead()
    {
        _tasksCue.Clear();
        _tasksHide?.Stop();
    }
}
