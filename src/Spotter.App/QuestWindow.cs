using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Spotter.App.Controls;
using Spotter.Session;
using Windows.Graphics;

namespace Spotter.App;

/// <summary>
/// A pinned quest card: a small window of its own that stays open until closed, and comes back after a restart.
/// It is owned by the main window, so it stays above it and has no taskbar button; it is never set topmost, so it
/// can't cover the game.
/// </summary>
public sealed partial class QuestWindow : Window
{
    private readonly QuestCard _card = new();

    public QuestWindow(QuestCardView view, Window owner, PointInt32? at)
    {
        QuestId = view.QuestId;
        var root = new Grid { Background = (Brush)Application.Current.Resources["CardBrush"] };
        root.Children.Add(_card);
        Content = root;
        Title = view.Name;
        _card.SetMode(CardMode.Pinned);
        _card.Show(view);

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(_card.DragArea);
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonForegroundColor = ((SolidColorBrush)Application.Current.Resources["InkBrush"]).Color;
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
        root.Loaded += (_, _) => FitToContent();
    }

    public string QuestId { get; }

    public QuestCardView? View => _card.View;

    /// <summary>The card element, for snapshots.</summary>
    public UIElement Card => _card;

    public void Update(QuestCardView view)
    {
        _card.Show(view);
        FitToContent();
    }

    private void FitToContent()
    {
        var scale = Content.XamlRoot?.RasterizationScale ?? 1;
        _card.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _card.DesiredSize;
        AppWindow.ResizeClient(new SizeInt32((int)Math.Ceiling(size.Width * scale), (int)Math.Ceiling(Math.Min(size.Height, 900) * scale)));
    }

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
