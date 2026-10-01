using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Spotter.App.Controls;

/// <summary>
/// Cards that open from what the pointer rests on, and from things on those cards, like the nested tooltips in
/// Crusader Kings III. Rest on a quest, key or item for 0.4 s and its card opens beside it; keep resting while the
/// bar fills (or move into the card) and it holds; point at something on a held card and that one's card opens
/// beside it, and so on. Leaving a card closes it and everything opened from it; nothing needs a click.
/// One stack per window: the main window, and each pinned card window.
/// </summary>
public sealed class CardStack
{
    private static readonly TimeSpan ShowAfter = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan SwapAfter = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan HoldAfter = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan SettleAfter = TimeSpan.FromMilliseconds(350);
    private const int NoSource = -2;

    private static readonly Dictionary<XamlRoot, CardStack> Stacks = [];

    private sealed record Level(Popup Popup, FrameworkElement Card)
    {
        public ICard Face => (ICard)Card;
    }

    private readonly List<Level> _levels = [];
    private readonly FrameworkElement _root;
    private readonly Func<CardKey, FrameworkElement?> _create;
    private readonly Func<Rect> _space;
    private readonly bool _besideRoot;
    private readonly DispatcherQueueTimer _show;
    private readonly DispatcherQueueTimer _settle;

    private CardKey? _pendingKey;
    private int _pendingLevel;
    private Rect _pendingAnchor;
    private object? _source;
    private CardKey? _sourceKey;
    private int _sourceLevel = NoSource;
    private int _over = -1;

    /// <param name="root">The window content; positions are in its coordinates.</param>
    /// <param name="create">A new card (QuestCard or ItemCard) for a key, or null if there is nothing to show.</param>
    /// <param name="space">Where cards may go, in root coordinates (the window, or the screen around a small window).</param>
    /// <param name="besideRoot">First cards open beside the whole window (a pinned card) rather than beside the row.</param>
    public CardStack(FrameworkElement root, Func<CardKey, FrameworkElement?> create, Func<Rect> space, bool besideRoot)
    {
        _root = root;
        _create = create;
        _space = space;
        _besideRoot = besideRoot;
        _show = root.DispatcherQueue.CreateTimer();
        _show.IsRepeating = false;
        _show.Tick += (_, _) => ShowPending();
        _settle = root.DispatcherQueue.CreateTimer();
        _settle.IsRepeating = false;
        _settle.Interval = SettleAfter;
        _settle.Tick += (_, _) => Settle();
        if (root.XamlRoot is { } xamlRoot)
            Stacks[xamlRoot] = this;
        root.Loaded += (_, _) => Stacks[root.XamlRoot] = this;
        root.Unloaded += (_, _) =>
        {
            if (root.XamlRoot is { } r)
                Stacks.Remove(r);
        };
    }

    public static CardStack? For(XamlRoot? root) => root is not null && Stacks.TryGetValue(root, out var stack) ? stack : null;

    /// <summary>A card was created (to hook its pin button, for example).</summary>
    public event Action<FrameworkElement>? CardOpened;

    /// <summary>The open cards, first to last.</summary>
    public IReadOnlyList<FrameworkElement> Cards => _levels.Select(l => l.Card).ToList();

    /// <summary>The card a pinned window was opened from: where it was, in root coordinates.</summary>
    public Point PositionOf(FrameworkElement card) =>
        _levels.FirstOrDefault(l => l.Card == card) is { } level ? new Point(level.Popup.HorizontalOffset, level.Popup.VerticalOffset) : default;

    // ---- the pointer ----

    /// <summary>The pointer rests on an element that shows <paramref name="key"/> (or on a row with no card: null).</summary>
    public void Enter(FrameworkElement element, CardKey? key)
    {
        var level = LevelOf(element);
        Enter(element, key, AnchorOf(element, level), level);
    }

    /// <summary>The pointer rests on something outside the cards, e.g. a map marker, at <paramref name="anchor"/>.</summary>
    public void Enter(object source, CardKey? key, Rect anchor, int level = -1)
    {
        _source = source;
        _sourceKey = key;
        _sourceLevel = level;
        Restart(_settle);
        if (key is null)
            return;
        var target = level + 1;
        // Pointing at a card's own subject (its header) or at what is already open beside it opens nothing new.
        if ((level >= 0 && level < _levels.Count && _levels[level].Face.Key == key) ||
            (target < _levels.Count && _levels[target].Face.Key == key))
        {
            _show.Stop();
            return;
        }
        _pendingKey = key;
        _pendingLevel = target;
        _pendingAnchor = anchor;
        // Skimming a list swaps an unheld card quickly; a held one waits, so crossing other markers on the way
        // into it doesn't replace it.
        _show.Interval = target < _levels.Count && _levels[target].Face.Mode == CardMode.Hover ? SwapAfter : ShowAfter;
        Restart(_show);
    }

    public void Exit(object source)
    {
        if (!ReferenceEquals(_source, source))
            return;
        _source = null;
        _sourceKey = null;
        _sourceLevel = NoSource;
        _show.Stop();
        Restart(_settle);
    }

    /// <summary>Opens a card right away, held (developer snapshots).</summary>
    public FrameworkElement? Open(CardKey key, Rect anchor, int level)
    {
        _pendingKey = key;
        _pendingLevel = level;
        _pendingAnchor = anchor;
        ShowPending();
        if (level >= _levels.Count)
            return null;
        _levels[level].Face.SetMode(CardMode.Held);
        return _levels[level].Card;
    }

    public void CloseAll() => CloseFrom(0);

    /// <summary>Brings open cards up to date; a card whose update says no closes, with everything opened from it.</summary>
    public void Refresh(Func<FrameworkElement, bool> update)
    {
        for (var i = 0; i < _levels.Count; i++)
        {
            if (update(_levels[i].Card))
                continue;
            CloseFrom(i);
            return;
        }
    }

    // ---- inside ----

    // Keeps the deepest card the pointer is on, or the card for the source it rests on, and closes the rest.
    private void Settle()
    {
        var keep = _over;
        if (_sourceLevel != NoSource)
            keep = Math.Max(keep, _sourceKey is null ? _sourceLevel : _sourceLevel + 1);
        CloseFrom(keep + 1);
    }

    private void ShowPending()
    {
        if (_pendingKey is not { } key || _pendingLevel > _levels.Count || _create(key) is not { } card)
            return;
        CloseFrom(_pendingLevel);
        var level = _levels.Count;
        var popup = new Popup { Child = card, XamlRoot = _root.XamlRoot, ShouldConstrainToRootBounds = !_besideRoot };
        card.PointerEntered += (_, _) =>
        {
            _over = level;
            // Moving into a card cancels a card pending from outside it.
            if (_pendingLevel <= level)
                _show.Stop();
            if (((ICard)card).Mode == CardMode.Hover)
                ((ICard)card).SetMode(CardMode.Held);
            Restart(_settle);
        };
        card.PointerExited += (_, _) =>
        {
            if (_over == level)
                _over = -1;
            Restart(_settle);
        };
        _levels.Add(new Level(popup, card));
        CardOpened?.Invoke(card);

        card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Place(popup, card.DesiredSize, level);
        popup.IsOpen = true;
        ((ICard)card).SetMode(CardMode.Hover);
        ((ICard)card).StartHold(HoldAfter);
        _pendingKey = null;
    }

    // Beside whatever it opened from: the row (first card in the main window), the window (first card of a pinned
    // window) or the card it came from. Right if there is room, else left; top-aligned with the row.
    private void Place(Popup popup, Size size, int level)
    {
        var space = _space();
        var beside = level > 0 ? Bounds(_levels[level - 1])
            : _besideRoot ? new Rect(0, 0, _root.ActualWidth, _root.ActualHeight)
            : _pendingAnchor;
        var x = beside.Right + 8;
        if (x + size.Width > space.Right - 8)
            x = beside.Left - 8 - size.Width;
        popup.HorizontalOffset = Math.Clamp(x, space.Left + 8, Math.Max(space.Left + 8, space.Right - size.Width - 8));
        popup.VerticalOffset = Math.Clamp(_pendingAnchor.Top - 10, space.Top + 8, Math.Max(space.Top + 8, space.Bottom - size.Height - 8));
    }

    private static Rect Bounds(Level level) =>
        new(level.Popup.HorizontalOffset, level.Popup.VerticalOffset, level.Card.ActualWidth, level.Card.ActualHeight);

    private void CloseFrom(int level)
    {
        for (var i = _levels.Count - 1; i >= Math.Max(0, level); i--)
        {
            _levels[i].Face.StopHold();
            _levels[i].Popup.IsOpen = false;
            _levels.RemoveAt(i);
        }
        if (_over >= _levels.Count)
            _over = -1;
    }

    // Which card an element is on (its index), or -1 for the window itself.
    private int LevelOf(DependencyObject element)
    {
        for (var node = element; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            var index = _levels.FindIndex(l => l.Card == node);
            if (index >= 0)
                return index;
        }
        return -1;
    }

    // An element's bounds in root coordinates; elements on a card are measured from the card's popup.
    private Rect AnchorOf(FrameworkElement element, int level)
    {
        var size = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
        if (level < 0)
            return element.TransformToVisual(_root).TransformBounds(size);
        var onCard = element.TransformToVisual(_levels[level].Card).TransformBounds(size);
        var popup = _levels[level].Popup;
        return new Rect(onCard.X + popup.HorizontalOffset, onCard.Y + popup.VerticalOffset, onCard.Width, onCard.Height);
    }

    private static void Restart(DispatcherQueueTimer timer)
    {
        timer.Stop();
        timer.Start();
    }
}
