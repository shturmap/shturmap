using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Shturmap.App.Controls;
using Shturmap.Session;
using Windows.Graphics;

namespace Shturmap.App;

/// <summary>
/// A popped-out quest card ("pinned" in the code and the study log): a small window of its own with a normal title
/// bar to move it by, open until closed, back
/// after a restart. In a raid it shows how far each objective on that map is. Things on it open their own cards
/// beside the window. It is owned by the main window, so it stays above it and has no taskbar button; it is never
/// topmost, so it can't cover the game.
/// </summary>
public sealed partial class QuestWindow : Window
{
    private readonly QuestCard _card;
    private readonly Grid _root;

    public QuestWindow(QuestCardView view, Window owner, PointInt32? at, Func<CardKey, FrameworkElement?> createCard)
    {
        QuestId = view.QuestId;
        _card = new QuestCard(view);
        _card.SetMode(CardMode.Pinned);
        _root = new Grid { Background = Brush("CardBrush") };
        _root.Children.Add(_card);
        Content = _root;
        Title = view.Name;
        Stack = new CardStack(_root, createCard, ScreenAround, besideRoot: true);
        AppWindow.SetIcon(App.IconPath);

        var bar = AppWindow.TitleBar;
        var card = Color("CardBrush");
        var line = Color("LineBrush");
        bar.BackgroundColor = card;
        bar.InactiveBackgroundColor = card;
        bar.ForegroundColor = Color("InkBrush");
        bar.InactiveForegroundColor = Color("MutedBrush");
        bar.ButtonBackgroundColor = card;
        bar.ButtonInactiveBackgroundColor = card;
        bar.ButtonForegroundColor = Color("InkBrush");
        bar.ButtonInactiveForegroundColor = Color("MutedBrush");
        bar.ButtonHoverBackgroundColor = line;
        bar.ButtonPressedBackgroundColor = line;
        bar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }
        AppWindow.IsShownInSwitchers = false;
        SetWindowLongPtr(WinRT.Interop.WindowNative.GetWindowHandle(this), OwnerIndex, WinRT.Interop.WindowNative.GetWindowHandle(owner));
        AppWindow.Resize(new SizeInt32(560, 600));
        if (at is { } position)
            AppWindow.Move(position);
        _root.Loaded += (_, _) =>
        {
            FitToContent();
            KeepOnScreen("restore");
        };
        // A move settles a moment after the last position change; then the title bar is brought back on screen if
        // it ended up off it (the study log: a pinned window closed twice with its title bar above the screen).
        _moved = DispatcherQueue.CreateTimer();
        _moved.Interval = TimeSpan.FromMilliseconds(600);
        _moved.IsRepeating = false;
        _moved.Tick += (_, _) => KeepOnScreen("move");
        AppWindow.Changed += (_, e) =>
        {
            if (e.DidPositionChange && !_placing)
            {
                _moved.Stop();
                _moved.Start();
            }
        };
        Closed += (_, _) => Stack.CloseAll();
        // Esc closes the cards opened from this window, as it does in the main window. The window itself stays: it
        // was popped out to be kept, and closes by its own close button (as Esc never drops a pick). Placement
        // hidden, or the key would show as a tooltip over the whole card.
        _root.KeyboardAcceleratorPlacementMode = Microsoft.UI.Xaml.Input.KeyboardAcceleratorPlacementMode.Hidden;
        var escape = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape };
        escape.Invoked += (_, e) =>
        {
            Study.Ui("key", ("key", "Escape"), ("where", "pinned"));
            Stack.CloseAll();
            e.Handled = true;
        };
        _root.KeyboardAccelerators.Add(escape);
        // As in the main window: a click on nothing in particular lets go of held cards.
        _root.Tapped += (_, e) =>
        {
            if (!e.Handled && !Linked.IsInside(e.OriginalSource) && Stack.AnyHeld)
                Stack.CloseAll();
        };
        _root.AddHandler(UIElement.PointerMovedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, e) =>
        {
            Stack.PointerAt(e.GetCurrentPoint(_root).Position);
        }), handledEventsToo: true);
    }

    public string QuestId { get; }

    public QuestCardView View => _card.View;

    /// <summary>Cards opened from things on this card.</summary>
    public CardStack Stack { get; }

    /// <summary>The card element, for snapshots.</summary>
    public UIElement Card => _card;

    public void Update(QuestCardView view)
    {
        var resize = view.Objectives.Count(o => o.Live.Length > 0) != _card.View.Objectives.Count(o => o.Live.Length > 0);
        _card.Show(view);
        if (resize)
            FitToContent();
    }

    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _moved;
    private bool _placing;

    // The title bar (what the window is moved by) must stay inside a screen's work area, or the window can't be
    // grabbed again. Moved the least distance that does it.
    private const int TitleBarHeight = 32;

    private void KeepOnScreen(string how)
    {
        var position = AppWindow.Position;
        var size = AppWindow.Size;
        var area = DisplayArea.GetFromRect(new RectInt32(position.X, position.Y, size.Width, TitleBarHeight), DisplayAreaFallback.Nearest).WorkArea;
        var x = Math.Clamp(position.X, area.X - size.Width + 120, area.X + area.Width - 120);
        var y = Math.Clamp(position.Y, area.Y, area.Y + area.Height - TitleBarHeight);
        var moved = x != position.X || y != position.Y;
        Study.Ui("pinned.move", ("quest", QuestId), ("x", x), ("y", y), ("how", how), ("clamped", moved));
        if (!moved)
            return;
        _placing = true;
        AppWindow.Move(new PointInt32(x, y));
        _placing = false;
    }

    // As tall as its card, up to the screen's work area: a longer card scrolls inside the window. A window that then
    // reaches below the work area moves up by what is over, so its last rows aren't off the screen.
    private void FitToContent()
    {
        var scale = Content.XamlRoot?.RasterizationScale ?? 1;
        _card.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _card.DesiredSize;
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        var frame = Math.Max(0, AppWindow.Size.Height - AppWindow.ClientSize.Height);
        var room = Math.Max((int)(200 * scale), area.Height - frame);
        AppWindow.ResizeClient(new SizeInt32((int)Math.Ceiling(size.Width * scale), Math.Min((int)Math.Ceiling(size.Height * scale), room)));
        var over = AppWindow.Position.Y + AppWindow.Size.Height - (area.Y + area.Height);
        if (over <= 0)
            return;
        _placing = true;
        AppWindow.Move(new PointInt32(AppWindow.Position.X, Math.Max(area.Y, AppWindow.Position.Y - over)));
        _placing = false;
    }

    // The screen's work area around this window, in its content's coordinates: nested cards open outside the window.
    private Windows.Foundation.Rect ScreenAround()
    {
        var scale = Content.XamlRoot?.RasterizationScale ?? 1;
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        var origin = ScreenPoint(this, default);
        return new Windows.Foundation.Rect((area.X - origin.X) / scale, (area.Y - origin.Y) / scale, area.Width / scale, area.Height / scale);
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    private static Windows.UI.Color Color(string key) => ((SolidColorBrush)Application.Current.Resources[key]).Color;

    /// <summary>A point in a window's content (device-independent pixels) on the screen.</summary>
    public static PointInt32 ScreenPoint(Window window, Windows.Foundation.Point at)
    {
        var scale = window.Content.XamlRoot?.RasterizationScale ?? 1;
        var point = new NativePoint { X = (int)(at.X * scale), Y = (int)(at.Y * scale) };
        ClientToScreen(WinRT.Interop.WindowNative.GetWindowHandle(window), ref point);
        return new PointInt32(point.X, point.Y);
    }

    // GWLP_HWNDPARENT: on a top-level window this sets the owner, not a parent.
    private const int OwnerIndex = -8;

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint window, int index, nint value);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(nint window, ref NativePoint point);
}
