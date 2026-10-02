using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Shturmap.App.Controls;
using Shturmap.Map;
using Shturmap.Session;
using Point = Windows.Foundation.Point;

namespace Shturmap.App;

// Developer aid for the website's hero clip: "--demo <quest>" (with --fake-game) plays one scripted interaction when
// the raid's sixth position fix arrives. tools\fake-raid.ps1 -Demo sends fixes at two places in turn (A, B, A, B, A,
// then B starts the demo and A ends it), so the view, the raid card, the marker and the trail look the same at the
// clip's start and end; the log lines "Demo: armed", "start" and "back" give it the cut points.
// A drawn pointer goes to the quest in the raid card (the map highlights it, its card opens), clicks its highlighter
// (kept, cyan), the map zooms to the quest and the player, the pointer rests on the quest's nearest marker on the map,
// then everything is let go and the view zooms back. It calls the app's own code paths: nothing is sent to the system
// as input, and nothing is captured here (the recorder is tools\record-window, which records this window only).
public sealed partial class MainWindow
{
    private string? _demoQuest;

    /// <summary>"--demo &lt;part of a quest name&gt;": plays the scripted interaction with that quest.</summary>
    public string? DemoQuest
    {
        get => _demoQuest;
        set
        {
            _demoQuest = value;
            // The clip shows the ping and the pulses even where Windows' animation effects are off.
            Map.AlwaysAnimate = value is not null;
        }
    }

    public bool DemoMode => _demoQuest is not null;

    private const int DemoStartsAtFix = 6;
    private int _demoFixes;
    private PlayerFix? _demoLastFix;

    // Counts position fixes. From the one before the demo on there are no notices, so the clip has none.
    private bool DemoQuiet => DemoMode && _demoFixes >= DemoStartsAtFix - 1;

    private void DemoOnSnapshot(SessionSnapshot s)
    {
        if (!DemoMode || s.Fix is not { } fix || (_demoLastFix is { } last && last.At == fix.At && last.Position == fix.Position))
            return;
        _demoLastFix = fix;
        _demoFixes++;
        AppLog.Info($"Demo: fix {_demoFixes} at {fix.Position}");
        if (DemoQuiet)
            ViewModel.NoticeOpen = false;
        if (_demoFixes == DemoStartsAtFix - 1)
            AppLog.Info("Demo: armed");
        else if (_demoFixes == DemoStartsAtFix)
            PlayDemo();
        else if (_demoFixes == DemoStartsAtFix + 1)
            AppLog.Info("Demo: back");
    }

    private async void PlayDemo()
    {
        var root = (FrameworkElement)Content;
        AppLog.Info("Demo: start");
        var startView = Map.View;
        var quest = _snapshot?.Objectives.FirstOrDefault(o => o.QuestName.Contains(_demoQuest!, StringComparison.OrdinalIgnoreCase))?.QuestId;
        if (quest is null || Map.Scene is not { } scene)
        {
            AppLog.Error($"Demo: no quest with objectives here matches \"{_demoQuest}\"");
            return;
        }
        // The new position pings and the raid card re-sorts first.
        await Task.Delay(1200);
        if (Linked.RowOf(quest, root.XamlRoot) is not { } row)
        {
            AppLog.Error("Demo: the quest's row isn't in the raid card");
            return;
        }
        var rest = new Point(root.ActualWidth * 0.64, root.ActualHeight * 0.7);
        ShowPointer(rest);
        await MovePointer(Inside(row, root, 64, 12), 950);
        Linked.PointAt(row);
        await Task.Delay(1050);
        if (FindChild<KeepToggle>(row) is { } toggle)
            await MovePointer(Inside(toggle, root, toggle.ActualWidth / 2, toggle.ActualHeight / 2), 450);
        await PressPointer();
        Linked.RequestKeep(quest);

        // The quest's objectives and the player, framed.
        var markers = scene.Markers.Where(m => m.Group == quest && m.Objective is not null).ToList();
        var points = markers.Select(m => m.Position).ToList();
        if (scene.Player is { } player)
            points.Add(player.Position);
        await Task.Delay(250);
        Map.AnimateView(Map.FramingOf(points, 110), TimeSpan.FromMilliseconds(1300));
        await Task.Delay(1550);

        // Onto the quest's nearest marker on the map: its card opens beside it.
        var target = scene.Player is { } me ? markers.MinBy(m => m.Position.HorizontalDistanceTo(me.Position)) : markers.FirstOrDefault();
        if (target is not null)
        {
            Linked.PointAt(null);
            var at = Map.PointOf(target.Position);
            await MovePointer(Map.TransformToVisual(root).TransformPoint(new Point(at.X + 3, at.Y + 3)), 900);
            OnMarkerHovered(target, at);
            await Task.Delay(1300);
            OnMarkerHovered(null, at);
        }
        else
        {
            Linked.PointAt(null);
        }

        // Let go, as with Esc, and back to where the clip started.
        await MovePointer(rest, 650);
        _ = HidePointer();
        _cards.CloseAll();
        ClearSelection();
        Map.AnimateView(startView, TimeSpan.FromMilliseconds(1300));
        await Task.Delay(1400);
        AppLog.Info("Demo: end");
    }

    // ---- the drawn pointer (an arrow like the system's, above the cards) ----

    private Popup? _pointer;
    private Canvas? _pointerFace;
    private Ellipse? _pointerRing;
    private Point _pointerAt;

    private void ShowPointer(Point at)
    {
        if (_pointer is null)
        {
            var arrow = new Microsoft.UI.Xaml.Shapes.Path
            {
                Data = (Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Geometry),
                    "M0,0 L0,19 L4.6,14.6 L7.9,21.6 L10.6,20.4 L7.4,13.5 L13.6,13.5 Z"),
                Fill = new SolidColorBrush(Microsoft.UI.Colors.White),
                Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 11, 12, 11)),
                StrokeThickness = 1.2,
                RenderTransform = new ScaleTransform { ScaleX = 1.2, ScaleY = 1.2 },
            };
            _pointerRing = new Ellipse
            {
                Width = 36, Height = 36, StrokeThickness = 2, Opacity = 0,
                Stroke = (Brush)Application.Current.Resources["AmberBrush"],
                RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(),
            };
            Canvas.SetLeft(_pointerRing, -18);
            Canvas.SetTop(_pointerRing, -18);
            _pointerFace = new Canvas { IsHitTestVisible = false, Children = { _pointerRing, arrow } };
            _pointer = new Popup { Child = _pointerFace, XamlRoot = Content.XamlRoot, ShouldConstrainToRootBounds = true, IsHitTestVisible = false };
            // Cards open as popups too; the pointer goes back on top of each new one.
            _cards.CardOpened += _ => DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, RaisePointer);
        }
        SetPointer(at);
        _pointerFace!.Opacity = 0;
        _pointer.IsOpen = true;
        _ = Animate(200, t => _pointerFace.Opacity = t);
    }

    private void RaisePointer()
    {
        if (_pointer is not { IsOpen: true } pointer)
            return;
        pointer.IsOpen = false;
        pointer.IsOpen = true;
    }

    private async Task HidePointer()
    {
        if (_pointer is null)
            return;
        await Animate(200, t => _pointerFace!.Opacity = 1 - t);
        _pointer.IsOpen = false;
    }

    private void SetPointer(Point at)
    {
        _pointerAt = at;
        _pointer!.HorizontalOffset = at.X;
        _pointer.VerticalOffset = at.Y;
    }

    private Task MovePointer(Point to, int milliseconds)
    {
        var from = _pointerAt;
        return Animate(milliseconds, t =>
        {
            var e = t * t * (3 - 2 * t);
            SetPointer(new Point(from.X + (to.X - from.X) * e, from.Y + (to.Y - from.Y) * e));
        });
    }

    // A click: a ring leaves the pointer's tip.
    private Task PressPointer() => Animate(380, t =>
    {
        var scale = (ScaleTransform)_pointerRing!.RenderTransform;
        scale.ScaleX = scale.ScaleY = 0.3 + 1.1 * (1 - (1 - t) * (1 - t));
        _pointerRing.Opacity = 0.9 * (1 - t);
    });

    // Runs a step on every frame for a while (t from 0 to 1), in step with the screen.
    private static Task Animate(int milliseconds, Action<double> step)
    {
        var done = new TaskCompletionSource();
        var started = DateTime.Now;
        EventHandler<object>? frame = null;
        frame = (_, _) =>
        {
            var t = Math.Min(1, (DateTime.Now - started).TotalMilliseconds / milliseconds);
            step(t);
            if (t < 1)
                return;
            CompositionTarget.Rendering -= frame;
            done.TrySetResult();
        };
        CompositionTarget.Rendering += frame;
        return done.Task;
    }

    private static Point Inside(UIElement element, UIElement root, double x, double y) =>
        element.TransformToVisual(root).TransformPoint(new Point(x, y));

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found)
                return found;
            if (FindChild<T>(child) is { } deeper)
                return deeper;
        }
        return null;
    }
}
