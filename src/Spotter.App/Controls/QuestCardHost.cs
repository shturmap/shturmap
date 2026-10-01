using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Spotter.Session;
using Windows.Foundation;

namespace Spotter.App.Controls;

/// <summary>
/// Shows the quest card for the quest the pointer rests on, wherever it appears (rail rows, map markers). Like the
/// pinnable tooltips in Crusader Kings III: rest briefly and the card appears; keep resting while the bar fills and
/// it holds, so the pointer can move into it (moving into it also holds it); leave it and it goes. The pin turns it
/// into its own window. Nothing here needs a click.
/// </summary>
public sealed class QuestCardHost
{
    private static readonly TimeSpan ShowAfter = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan SwapAfter = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan HoldAfter = TimeSpan.FromMilliseconds(900);

    private readonly Popup _popup = new() { ShouldConstrainToRootBounds = true };
    private readonly QuestCard _card = new();
    private readonly DispatcherQueueTimer _show;
    private readonly DispatcherQueueTimer _hide;
    private readonly FrameworkElement _root;
    private readonly Func<string, QuestCardView?> _build;
    private string? _pendingQuest;
    private Rect _pendingAnchor;
    private bool _overCard;

    /// <param name="root">The window content; anchors and the card's position are in its coordinates.</param>
    /// <param name="build">The card for a quest id, or null if there is nothing to show.</param>
    public QuestCardHost(FrameworkElement root, DispatcherQueue queue, Func<string, QuestCardView?> build)
    {
        _root = root;
        _build = build;
        _popup.Child = _card;
        _show = queue.CreateTimer();
        _show.IsRepeating = false;
        _show.Tick += (_, _) => ShowPending();
        _hide = queue.CreateTimer();
        _hide.IsRepeating = false;
        _hide.Tick += (_, _) => Close();

        _card.PointerEntered += (_, _) =>
        {
            _overCard = true;
            _hide.Stop();
            if (_card.Mode == CardMode.Hover)
                _card.SetMode(CardMode.Held);
        };
        _card.PointerExited += (_, _) =>
        {
            _overCard = false;
            HideAfter(TimeSpan.FromMilliseconds(250));
        };
        _card.PinClicked += card =>
        {
            if (card.View is not { } view)
                return;
            var at = new Point(_popup.HorizontalOffset, _popup.VerticalOffset);
            Close();
            PinRequested?.Invoke(view, at);
        };
    }

    /// <summary>The pin was clicked: the card's quest and where the card was, in root coordinates.</summary>
    public event Action<QuestCardView, Point>? PinRequested;

    public string? ShownQuest => _popup.IsOpen ? _card.View?.QuestId : null;

    /// <summary>The card element, for snapshots.</summary>
    public UIElement Card => _card;

    public CardMode Mode => _card.Mode;

    /// <summary>The pointer rests on a quest shown at <paramref name="anchor"/> (root coordinates).</summary>
    public void Hover(string questId, Rect anchor)
    {
        if (_overCard)
            return; // moving about inside a held card
        if (ShownQuest == questId)
        {
            _hide.Stop();
            return;
        }
        _pendingQuest = questId;
        _pendingAnchor = anchor;
        // Skimming down a list swaps cards quickly once one is open.
        _show.Interval = _popup.IsOpen ? SwapAfter : ShowAfter;
        _show.Stop();
        _show.Start();
    }

    public void Leave()
    {
        _pendingQuest = null;
        _show.Stop();
        if (_popup.IsOpen && !_overCard)
            HideAfter(TimeSpan.FromMilliseconds(_card.Mode == CardMode.Held ? 500 : 150));
    }

    /// <summary>Shows a quest's card at once, already held (developer snapshots).</summary>
    public void ShowHeld(string questId, Rect anchor)
    {
        _pendingQuest = questId;
        _pendingAnchor = anchor;
        ShowPending();
        _card.SetMode(CardMode.Held);
    }

    public void Close()
    {
        _show.Stop();
        _hide.Stop();
        _card.StopHold();
        _popup.IsOpen = false;
        _overCard = false;
    }

    /// <summary>Brings an open card up to date; closes it if its quest is no longer active.</summary>
    public void Refresh()
    {
        if (ShownQuest is not { } quest)
            return;
        if (_build(quest) is { State: Spotter.Core.Quests.QuestState.Active } view)
            _card.Show(view);
        else
            Close();
    }

    private void HideAfter(TimeSpan delay)
    {
        _hide.Interval = delay;
        _hide.Stop();
        _hide.Start();
    }

    private void ShowPending()
    {
        if (_pendingQuest is not { } quest || _build(quest) is not { } view)
            return;
        _popup.XamlRoot ??= _root.XamlRoot;
        _card.Show(view);
        _card.SetMode(CardMode.Hover);
        _card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Place(_card.DesiredSize, _pendingAnchor);
        _hide.Stop();
        _popup.IsOpen = true;
        _card.StartHold(HoldAfter);
    }

    // Beside the anchor (right, or left if there's no room), top-aligned with it, inside the window.
    private void Place(Size size, Rect anchor)
    {
        var width = _root.ActualWidth;
        var height = _root.ActualHeight;
        var x = anchor.Right + 12;
        if (x + size.Width > width - 8)
            x = anchor.Left - 12 - size.Width;
        _popup.HorizontalOffset = Math.Clamp(x, 8, Math.Max(8, width - size.Width - 8));
        _popup.VerticalOffset = Math.Clamp(anchor.Top - 10, 8, Math.Max(8, height - size.Height - 8));
    }
}
