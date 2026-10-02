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

// Developer aid for the website's hero clip: "--demo <quest>" (with --fake-game) plays one scripted interaction. The
// clip must never read as live tracking, so the position changes exactly once, and only after a key press: a drawn
// screenshot key appears and is pressed ("Demo: press" in the log), tools\fake-raid.ps1 -Demo then writes the
// screenshot file the game would, and the fix lands a moment later (marker, ping, raid card re-sorted), with the
// file name the position comes from shown under the key. Then a drawn pointer goes to the quest in the raid card (the
// map highlights it, its card opens), clicks its highlighter (kept, cyan), the map zooms to the quest and the player,
// the pointer rests on the quest's nearest marker on the map (its card stays up to be read), and everything is let go
// and the view zooms back. It calls the app's own code paths: nothing is sent to the system as input, and nothing is
// captured here (the recorder is tools\record-window, which records this window only).
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

    // The first fix sets the scene; the demo starts this long after it (the script starts the recorder in between).
    private static readonly TimeSpan DemoLead = TimeSpan.FromSeconds(6);
    private int _demoFixes;
    private PlayerFix? _demoLastFix;
    private TaskCompletionSource<PlayerFix>? _demoFixWanted;

    // From the first fix on there are no notices, so the clip has none.
    private bool DemoQuiet => DemoMode && _demoFixes >= 1;

    private void DemoOnSnapshot(SessionSnapshot s)
    {
        if (!DemoMode || s.Fix is not { } fix || (_demoLastFix is { } last && last.At == fix.At && last.Position == fix.Position))
            return;
        _demoLastFix = fix;
        _demoFixes++;
        AppLog.Info($"Demo: fix {_demoFixes} at {fix.Position}");
        ViewModel.NoticeOpen = false;
        if (_demoFixes == 1)
            PlayDemo();
        else
            _demoFixWanted?.TrySetResult(fix);
    }

    private async void PlayDemo()
    {
        var root = (FrameworkElement)Content;
        AppLog.Info("Demo: armed");
        await Task.Delay(DemoLead);
        var startView = Map.View;
        var quest = _snapshot?.Objectives.FirstOrDefault(o => o.QuestName.Contains(_demoQuest!, StringComparison.OrdinalIgnoreCase))?.QuestId;
        if (quest is null || Map.Scene is not { } scene)
        {
            AppLog.Error($"Demo: no quest with objectives here matches \"{_demoQuest}\"");
            return;
        }

        // The key press comes first; the position follows it.
        AppLog.Info("Demo: key");
        ShowKey();
        await Task.Delay(650);
        _demoFixWanted = new TaskCompletionSource<PlayerFix>();
        await PressKey(() => AppLog.Info("Demo: press"));
        if (await Task.WhenAny(_demoFixWanted.Task, Task.Delay(4000)) != _demoFixWanted.Task)
        {
            AppLog.Error("Demo: no position arrived after the key press");
            return;
        }
        ShowKeyFileName(_demoFixWanted.Task.Result.Position);
        // The marker pings and the raid card re-sorts while the key stays up.
        await Task.Delay(1500);
        await HideKey();
        await Task.Delay(200);
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
            // The card opens after the usual rest and stays up about five seconds, long enough to read.
            await Task.Delay(5600);
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

    // ---- the drawn screenshot key, bottom left of the map: what makes the position change ----

    private Popup? _key;
    private Border? _keyPanel, _keyCap;
    private TextBlock? _keyLabel, _keyFileName;
    private TranslateTransform? _keyDip;

    private void ShowKey()
    {
        if (_key is null)
        {
            Brush Res(string name) => (Brush)Application.Current.Resources[name];
            var amber = (Windows.UI.Color)Application.Current.Resources["AmberColor"];
            _keyLabel = new TextBlock
            {
                // The key as the app names it from the game's settings (the status bar says the same).
                Text = (_snapshot?.ScreenshotKeys.FirstOrDefault() ?? "PrtSc").ToUpperInvariant(),
                FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontStretch = Windows.UI.Text.FontStretch.SemiCondensed,
                CharacterSpacing = 120, Foreground = Res("InkBrush"), HorizontalAlignment = HorizontalAlignment.Center,
            };
            _keyDip = new TranslateTransform();
            _keyCap = new Border
            {
                MinWidth = 104, Padding = new Thickness(18, 9, 18, 9), Background = Res("RaisedBrush"),
                BorderBrush = Res("LineStrongBrush"), BorderThickness = new Thickness(1), Child = _keyLabel, RenderTransform = _keyDip,
            };
            // The key's side: a darker edge under the cap that the cap sinks into when pressed.
            var key = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(0, 0, 0, 4) };
            key.Children.Add(new Border
            {
                Background = Res("GroundBrush"), BorderBrush = Res("LineStrongBrush"), BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 4, 0, -4),
            });
            key.Children.Add(_keyCap);
            var caption = new TextBlock
            {
                Text = "SCREENSHOT · POSITION FROM THE FILE NAME", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontStretch = Windows.UI.Text.FontStretch.SemiCondensed, CharacterSpacing = 90, Foreground = Res("MutedBrush"),
            };
            _keyFileName = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 12.5, Foreground = new SolidColorBrush(amber), Opacity = 0,
            };
            _keyPanel = new Border
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xF0, 0x0B, 0x0C, 0x0B)), BorderBrush = Res("LineBrush"),
                BorderThickness = new Thickness(1), Padding = new Thickness(16, 14, 16, 12), IsHitTestVisible = false,
                Child = new StackPanel { Spacing = 9, Children = { key, caption, _keyFileName } },
            };
            _key = new Popup { Child = _keyPanel, XamlRoot = Content.XamlRoot, ShouldConstrainToRootBounds = true, IsHitTestVisible = false };
        }
        // Bottom left of the map, above the wiki link and the map credit.
        _keyFileName!.Text = "…_00.00, 0.00, 000.00_…";
        _keyPanel!.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var corner = Map.TransformToVisual(Content).TransformPoint(new Point(20, Map.ActualHeight - 58 - _keyPanel.DesiredSize.Height));
        _key.HorizontalOffset = corner.X;
        _key.VerticalOffset = corner.Y;
        _keyFileName.Opacity = 0;
        _keyPanel.Opacity = 0;
        _key.IsOpen = true;
        _ = Animate(250, t => _keyPanel.Opacity = t);
    }

    // The press: the cap sinks onto its edge and flashes amber (then <paramref name="down"/> runs), and comes back up.
    private async Task PressKey(Action down)
    {
        var amber = (Windows.UI.Color)Application.Current.Resources["AmberColor"];
        var raised = (Windows.UI.Color)Application.Current.Resources["RaisedColor"];
        var ink = (Windows.UI.Color)Application.Current.Resources["InkColor"];
        Windows.UI.Color Mix(Windows.UI.Color a, Windows.UI.Color b, double t) => Windows.UI.Color.FromArgb(255,
            (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
        var flash = new SolidColorBrush(amber);
        _keyCap!.BorderBrush = flash;
        _keyCap.Background = new SolidColorBrush(Mix(raised, amber, 0.45));
        _keyLabel!.Foreground = new SolidColorBrush(amber);
        await Animate(90, t => _keyDip!.Y = 4 * t);
        down();
        await Task.Delay(110);
        await Animate(420, t =>
        {
            _keyDip!.Y = 4 * (1 - Math.Min(1, t * 2.5));
            _keyCap.Background = new SolidColorBrush(Mix(Mix(raised, amber, 0.45), raised, t));
            _keyLabel.Foreground = new SolidColorBrush(Mix(amber, ink, t));
        });
        _keyCap.BorderBrush = (Brush)Application.Current.Resources["LineStrongBrush"];
    }

    // The part of the screenshot's name the position comes from, as the game writes it.
    private void ShowKeyFileName(Shturmap.Core.WorldPoint p)
    {
        _keyFileName!.Text = FormattableString.Invariant($"…_{p.X:0.00}, {p.Y:0.00}, {p.Z:0.00}_…");
        _ = Animate(200, t => _keyFileName.Opacity = t);
    }

    private async Task HideKey()
    {
        await Animate(300, t => _keyPanel!.Opacity = 1 - t);
        _key!.IsOpen = false;
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
