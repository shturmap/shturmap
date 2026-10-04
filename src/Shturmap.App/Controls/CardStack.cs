using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Shturmap.App.Rules;
using Windows.Foundation;

namespace Shturmap.App.Controls;

/// <summary>
/// Cards that open from what the pointer rests on, and from things on those cards, like the nested tooltips in
/// Crusader Kings III. Rest on a quest, key or item for 0.65 s (0.4 s on a card) and its card opens beside it, see-through; it stays
/// while the pointer is on it, so things on it can open their own cards, and goes when the pointer leaves. A click
/// on the quest (or on the card) holds it: it turns solid and stays while the pointer is near it, until a click
/// elsewhere, Esc, another click on the quest, a full rest on something else that opens a card in its place, or the
/// pointer moving well away from it and from what it was opened from.
/// One stack per window: the main window, and each pinned card window.
/// </summary>
public sealed class CardStack
{
    // From the window's own lists and the map a card waits longer: the study log had 62 % of those cards closing
    // within a second, opened by a pointer only passing over a list. Inside cards and pinned windows, pointing is
    // deliberate.
    private static readonly TimeSpan ShowFromList = TimeSpan.FromMilliseconds(650);
    private static readonly TimeSpan ShowAfter = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan SwapAfter = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan SettleAfter = TimeSpan.FromMilliseconds(350);
    // While the pointer heads for an open card, what would replace or close that card waits, and is looked at again
    // this often (CardAim): the way to a card leads across the row's other cells and the rows below.
    private static readonly TimeSpan AimAfter = TimeSpan.FromMilliseconds(300);
    // The pointer's last few places say where it is going, for as long as the newest of them is this fresh.
    private const int TrailLength = 6;
    private static readonly TimeSpan TrailFresh = TimeSpan.FromMilliseconds(200);
    private const int NoSource = -2;

    private static readonly Dictionary<XamlRoot, CardStack> Stacks = [];

    /// <param name="Anchor">What the card was opened from (its row, or its marker), in root coordinates.</param>
    private sealed record Level(Popup Popup, FrameworkElement Card, DateTime Opened, Rect Anchor)
    {
        public ICard Face => (ICard)Card;

        public bool Held => Face.Mode == CardMode.Held;
    }

    /// <summary>"main" or "pinned": which kind of window this stack is in (for the study log).</summary>
    public string Where => _besideRoot ? "pinned" : "main";

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
    private readonly Queue<Point> _trail = new();
    private long _trailAt;
    private Point? _showLooked;
    private Point? _settleLooked;

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
        _show.Tick += (_, _) => ShowUnlessPassing();
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

    /// <summary>
    /// The pointer left a source, in whichever window it was: a row that has just been unloaded can't say which any
    /// more, and only the stack it was the source of takes it.
    /// </summary>
    public static void Leave(object source)
    {
        foreach (var stack in Stacks.Values.ToList())
            stack.Exit(source);
    }

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
        SettleSoon();
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
        // Skimming a list swaps an unheld card quickly; a held one gives way only to a full rest on something else.
        _show.Interval = target < _levels.Count && !_levels[target].Held ? SwapAfter
            : level < 0 && !_besideRoot ? ShowFromList
            : ShowAfter;
        // On its way to the card this would replace, the pointer is only passing over: it waits longer.
        _showLooked = null;
        if (HeadingFor(target, ref _showLooked) && _show.Interval < AimAfter)
            _show.Interval = AimAfter;
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
        SettleSoon();
    }

    /// <summary>A click on an element that shows <paramref name="key"/>: hold its card (open it first if needed), or let go of it if held.</summary>
    public void Click(FrameworkElement element, CardKey? key)
    {
        var level = LevelOf(element);
        Click(key, AnchorOf(element, level), level);
    }

    /// <summary>A click on something outside the cards, e.g. a map marker.</summary>
    public void Click(CardKey? key, Rect anchor, int level = -1)
    {
        if (key is null || (level >= 0 && level < _levels.Count && _levels[level].Face.Key == key))
            return;
        var target = level + 1;
        if (target < _levels.Count && _levels[target].Face.Key == key)
        {
            if (_levels[target].Held)
            {
                Study.Ui("card.release", ("card", key.ToString()), ("name", _levels[target].Face.Title), ("level", target), ("window", Where));
                CloseFrom(target);
            }
            else
            {
                // Held from here now (the card may have opened from the quest's marker and be clicked in the list).
                _levels[target] = _levels[target] with { Anchor = anchor };
                Hold(target);
            }
            return;
        }
        _pendingKey = key;
        _pendingLevel = target;
        _pendingAnchor = anchor;
        ShowPending();
        if (target < _levels.Count && _levels[target].Face.Key == key)
            Hold(target);
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

    /// <summary>Whether any card is held (a click elsewhere lets go of it).</summary>
    public bool AnyHeld => _levels.Any(l => l.Held);

    /// <summary>
    /// The pointer moved in the window (root coordinates). A held card closes, with everything opened from it, once
    /// the pointer is well away from it and from what it was opened from (<see cref="CardReach"/>). And its last
    /// places are kept, to tell where it is heading (<see cref="CardAim"/>).
    /// </summary>
    public void PointerAt(Point p)
    {
        _trail.Enqueue(p);
        if (_trail.Count > TrailLength)
            _trail.Dequeue();
        _trailAt = Environment.TickCount64;
        for (var i = 0; i < _levels.Count; i++)
        {
            if (!_levels[i].Held || _over >= i)
                continue;
            if (CardReach.Away(p.X, p.Y, Box(Bounds(_levels[i])), Box(_levels[i].Anchor)))
            {
                Study.Ui("card.release", ("card", _levels[i].Face.Key.ToString()), ("name", _levels[i].Face.Title), ("level", i),
                    ("window", Where), ("how", "away"));
                CloseFrom(i);
                return;
            }
        }
    }

    public void CloseAll() => CloseFrom(0);

    /// <summary>Closes one card, with everything opened from it (e.g. a card that has just been pinned).</summary>
    public void Close(FrameworkElement card)
    {
        var level = _levels.FindIndex(l => l.Card == card);
        if (level >= 0)
            CloseFrom(level);
    }

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

    private void Hold(int level)
    {
        var face = _levels[level].Face;
        if (face.Mode != CardMode.Hover)
            return;
        face.SetMode(CardMode.Held);
        Study.Ui("card.hold", ("card", face.Key.ToString()), ("name", face.Title), ("level", level), ("window", Where));
    }

    // Keeps held cards, the deepest card the pointer is on, and the card for the source it rests on; closes the rest.
    private void Settle()
    {
        var keep = Math.Max(_over, _levels.FindLastIndex(l => l.Held));
        if (_sourceLevel != NoSource)
            keep = Math.Max(keep, _sourceKey is null ? _sourceLevel : _sourceLevel + 1);
        // The pointer is on its way to a card that would close now, across a gap in its row, say: the card stays,
        // and this looks again.
        if (HeadingFor(keep + 1, ref _settleLooked))
        {
            Restart(_settle);
            return;
        }
        _settleLooked = null;
        CloseFrom(keep + 1);
    }

    // Something happened under the pointer: which cards stay is looked at afresh a moment later.
    private void SettleSoon()
    {
        _settleLooked = null;
        Restart(_settle);
    }

    // Whether the pointer is heading for the card at a level: since this was last asked (looked), or the first
    // time over its last few places, if it is still moving. Without a pointer's moves (a script) it never is.
    private bool HeadingFor(int level, ref Point? looked)
    {
        if (level < 0 || level >= _levels.Count || _trail.Count == 0)
            return false;
        var now = _trail.Last();
        Point from;
        if (looked is { } before)
            from = before;
        else if (Environment.TickCount64 - _trailAt <= TrailFresh.TotalMilliseconds)
            from = _trail.Peek();
        else
            return false;
        looked = now;
        return CardAim.Toward(from.X, from.Y, now.X, now.Y, Box(Bounds(_levels[level])));
    }

    // The wait for a card is over. If it would replace a card the pointer is still heading for, it waits on; a
    // pointer that rests on the new thing, or turns away from the card, gets its card.
    private void ShowUnlessPassing()
    {
        if (_pendingKey is not null && HeadingFor(_pendingLevel, ref _showLooked))
        {
            _show.Interval = AimAfter;
            Restart(_show);
            return;
        }
        ShowPending();
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
            SettleSoon();
        };
        card.PointerExited += (_, _) =>
        {
            if (_over == level)
                _over = -1;
            SettleSoon();
        };
        // A click anywhere on a card holds it (rows on it hold their own cards first).
        card.Tapped += (_, _) =>
        {
            if (level < _levels.Count && _levels[level].Card == card)
                Hold(level);
        };
        _levels.Add(new Level(popup, card, DateTime.Now, _pendingAnchor));
        CardOpened?.Invoke(card);
        var face = (ICard)card;
        Study.Ui("card.open", ("card", face.Key.ToString()), ("name", face.Title), ("level", level), ("window", Where));

        // No taller than the room there is: a long quest's card scrolls inside instead of running off the window.
        card.MaxHeight = Math.Max(MinCardHeight, _space().Height - 16);
        card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Place(popup, card.DesiredSize, level);
        popup.IsOpen = true;
        _pendingKey = null;
    }

    // In a window too small for it a card still gets this much height.
    private const double MinCardHeight = 160;

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

    private static CardReach.Box Box(Rect rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    private void CloseFrom(int level)
    {
        for (var i = _levels.Count - 1; i >= Math.Max(0, level); i--)
        {
            Study.Ui("card.close", ("card", _levels[i].Face.Key.ToString()), ("name", _levels[i].Face.Title), ("level", i),
                ("window", Where), ("openS", DateTime.Now - _levels[i].Opened), ("held", _levels[i].Held));
            _levels[i].Popup.IsOpen = false;
            _levels.RemoveAt(i);
        }
        if (_over >= _levels.Count)
            _over = -1;
    }

    /// <summary>Whether an element is on one of the open cards.</summary>
    public bool Contains(FrameworkElement element) => LevelOf(element) >= 0;

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

    // What a card opens beside. A small thing among others in a row (a need cell, a glyph on a folded Plan card)
    // opens its card beside that row, at its own height: beside itself, the card would lie over the row's other cells.
    private Rect AnchorOf(FrameworkElement element, int level)
    {
        var box = BoundsOf(element, level);
        if (Linked.RowAround(element) is not { } row)
            return box;
        var wide = BoundsOf(row, level);
        return new Rect(wide.X, box.Y, wide.Width, box.Height);
    }

    // An element's bounds in root coordinates; elements on a card are measured from the card's popup.
    private Rect BoundsOf(FrameworkElement element, int level)
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
