using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Shturmap.App.Controls;

/// <summary>What the pointer is on: quests (and everything about them), an item, or a map marker such as an extract.</summary>
public sealed record Focus(IReadOnlySet<string> Quests, string? Item = null, string? Marker = null)
{
    public static Focus Quest(string id) => new(new HashSet<string> { id });
}

/// <summary>
/// Linked highlighting (brushing and linking): rows say what they show with attached properties, and pointing at
/// one highlights every other row, card and map marker that shows the same quest, item or marker. Nothing to
/// click; the highlight goes when the pointer leaves.
/// </summary>
public static class Linked
{
    public static readonly DependencyProperty QuestProperty = DependencyProperty.RegisterAttached(
        "Quest", typeof(string), typeof(Linked), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty QuestsProperty = DependencyProperty.RegisterAttached(
        "Quests", typeof(object), typeof(Linked), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty ItemProperty = DependencyProperty.RegisterAttached(
        "Item", typeof(string), typeof(Linked), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty MarkerProperty = DependencyProperty.RegisterAttached(
        "Marker", typeof(string), typeof(Linked), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty AlsoProperty = DependencyProperty.RegisterAttached(
        "Also", typeof(object), typeof(Linked), new PropertyMetadata(null, OnChanged));

    /// <summary>
    /// Quest ids (an IEnumerable&lt;string&gt;) this element lights up when pointed at, without lighting up itself
    /// for them: rows on a quest's own card, which would otherwise all glow whenever that quest is in focus.
    /// </summary>
    public static object? GetAlso(DependencyObject d) => d.GetValue(AlsoProperty);

    public static void SetAlso(DependencyObject d, object? value) => d.SetValue(AlsoProperty, value);

    public static string? GetQuest(DependencyObject d) => (string?)d.GetValue(QuestProperty);

    public static void SetQuest(DependencyObject d, string? value) => d.SetValue(QuestProperty, value);

    /// <summary>Quest ids (an IEnumerable&lt;string&gt;), e.g. the quests an item to bring is for.</summary>
    public static object? GetQuests(DependencyObject d) => d.GetValue(QuestsProperty);

    public static void SetQuests(DependencyObject d, object? value) => d.SetValue(QuestsProperty, value);

    public static string? GetItem(DependencyObject d) => (string?)d.GetValue(ItemProperty);

    public static void SetItem(DependencyObject d, string? value) => d.SetValue(ItemProperty, value);

    public static string? GetMarker(DependencyObject d) => (string?)d.GetValue(MarkerProperty);

    public static void SetMarker(DependencyObject d, string? value) => d.SetValue(MarkerProperty, value);

    // Rows are rebuilt with every snapshot: only loaded ones are kept, and the hook marker doesn't keep them alive.
    private static readonly HashSet<FrameworkElement> Live = [];
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, object> Hooked = [];
    private static FrameworkElement? _source;

    public static Focus? Current { get; private set; }

    /// <summary>The quests picked for the coming raid: their rows, and the rows serving them, keep a quieter tint than the pointer's.</summary>
    public static IReadOnlySet<string> Picks
    {
        get => _picks;
        set
        {
            if (value.SetEquals(_picks))
                return;
            _picks = value;
            foreach (var element in Live)
                Paint(element);
            PicksChanged?.Invoke();
        }
    }

    private static IReadOnlySet<string> _picks = new HashSet<string>();

    /// <summary>Raised when the picks change (the pens follow them).</summary>
    public static event Action? PicksChanged;

    /// <summary>A pen was clicked: pick this quest for the coming raid, or unpick it.</summary>
    public static event Action<string>? KeepRequested;

    public static void RequestKeep(string questId) => KeepRequested?.Invoke(questId);

    /// <summary>Raised when the focus changes, from rows or from <see cref="Set"/> (map markers).</summary>
    public static event Action? FocusChanged;

    /// <summary>
    /// The pointer entered a linked element. The key is the card it opens: its item if it has one, else its quest;
    /// null for rows that only highlight (several quests, a marker).
    /// </summary>
    public static event Action<FrameworkElement, CardKey?>? Hovered;

    /// <summary>The pointer left a linked element.</summary>
    public static event Action<FrameworkElement>? Left;

    /// <summary>A linked element was clicked (tapped), with the card it opens.</summary>
    public static event Action<FrameworkElement, CardKey?>? Clicked;

    /// <summary>Whether something (e.g. the target of a click) is a linked element or inside one.</summary>
    public static bool IsInside(object? target)
    {
        for (var node = target as DependencyObject; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is FrameworkElement element && Hooked.TryGetValue(element, out _))
                return true;
        }
        return false;
    }

    /// <summary>The quests whose rows are at least half visible in a scrolling area (for the study log).</summary>
    public static IEnumerable<string> QuestsVisibleIn(FrameworkElement viewport)
    {
        var seen = new HashSet<string>();
        foreach (var element in Live.ToList())
        {
            if (element.XamlRoot != viewport.XamlRoot || GetQuest(element) is not { } quest || element.ActualHeight <= 0)
                continue;
            if (HalfVisible(element, viewport) && seen.Add(quest))
                yield return quest;
        }
    }

    private static bool HalfVisible(FrameworkElement element, FrameworkElement viewport)
    {
        try
        {
            var box = element.TransformToVisual(viewport).TransformBounds(new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));
            return Math.Min(viewport.ActualHeight, box.Bottom) - Math.Max(0, box.Top) >= box.Height / 2;
        }
        catch (ArgumentException)
        {
            return false; // not in the viewport's tree (a card's row)
        }
    }

    /// <summary>
    /// Points at a row the way the mouse does, leaving the one pointed at before (the website demo's drawn pointer;
    /// it calls the same code as the pointer events, nothing is sent to the system).
    /// </summary>
    public static void PointAt(FrameworkElement? element)
    {
        if (_source is { } previous && previous != element)
            Exit(previous);
        if (element is not null && element != _source)
            Enter(element);
    }

    /// <summary>The quest's block in a window's lists (the tallest loaded element showing just that quest), or null.</summary>
    public static FrameworkElement? RowOf(string questId, XamlRoot root) =>
        Live.Where(e => e.XamlRoot == root && GetQuest(e) == questId && CardStack.For(root)?.Contains(e) != true && e.ActualHeight > 0)
            .OrderByDescending(e => e.ActualHeight)
            .FirstOrDefault();

    /// <summary>Sets the focus from outside the rows, e.g. a map marker under the pointer.</summary>
    public static void Set(Focus? focus)
    {
        _source = null;
        Apply(focus);
    }

    /// <summary>
    /// Lets go of what the pointer is on, as if it had left: when nobody is looking any more (Shturmap's windows are
    /// no longer the active ones), the highlight and the map's pulse stop instead of running on behind the game.
    /// </summary>
    public static void LetGo()
    {
        if (_source is { } source)
            Exit(source);
        else
            Apply(null);
    }

    private static void Apply(Focus? focus)
    {
        if (focus == Current)
            return;
        Current = focus;
        foreach (var element in Live)
            Paint(element);
        FocusChanged?.Invoke();
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;
        if (Hooked.TryAdd(element, true))
        {
            element.PointerEntered += (_, _) => Enter(element);
            element.PointerExited += (_, _) => Exit(element);
            element.Tapped += (_, _) => Clicked?.Invoke(element, KeyOf(element));
            element.Loaded += (_, _) =>
            {
                Live.Add(element);
                Paint(element);
            };
            element.Unloaded += (_, _) =>
            {
                Live.Remove(element);
                // Rows are rebuilt with every snapshot, and a row that goes while the pointer is on it gets no
                // PointerExited: its focus would stay (the quest lit, the map pulsing, its hover card open) until
                // the pointer entered something else. So it lets go as it leaves.
                if (ReferenceEquals(_source, element))
                    Exit(element);
            };
            // Rows need a fill to be hit anywhere, not just on their text.
            if (Background(element) is null)
                SetBackground(element, new SolidColorBrush(Microsoft.UI.Colors.Transparent));
            if (element.IsLoaded)
                Live.Add(element);
        }
        Paint(element);
    }

    private static IEnumerable<string> QuestsOf(FrameworkElement element)
    {
        if (GetQuest(element) is { } quest)
            yield return quest;
        if (GetQuests(element) is IEnumerable<string> quests)
            foreach (var q in quests)
                yield return q;
    }

    private static DateTime _enteredAt;

    private static void Enter(FrameworkElement element)
    {
        _source = element;
        _enteredAt = DateTime.Now;
        var item = GetItem(element);
        var marker = GetMarker(element);
        var quests = QuestsOf(element).ToHashSet();
        if (GetAlso(element) is IEnumerable<string> also)
            quests.UnionWith(also);
        Apply(new Focus(quests, item, marker));
        Hovered?.Invoke(element, KeyOf(element));
    }

    // The card an element opens: its item if it has one, else its quest; none for rows that only highlight.
    private static CardKey? KeyOf(FrameworkElement element) =>
        GetItem(element) is { } item ? new CardKey.Item(item) : GetQuest(element) is { } quest ? new CardKey.Quest(quest) : null;

    private static void Exit(FrameworkElement element)
    {
        Left?.Invoke(element);
        if (_source != element)
            return;
        // Resting on something is the closest the study log gets to "looked at it"; passing over it is not.
        var dwell = DateTime.Now - _enteredAt;
        if (dwell >= TimeSpan.FromMilliseconds(400) && Current is { } focus)
        {
            Study.Ui("hover", ("quests", focus.Quests.ToList()), ("item", focus.Item), ("marker", focus.Marker), ("s", dwell),
                ("where", CardStack.For(element.XamlRoot) is { } stack ? (stack.Contains(element) ? "card" : stack.Where == "pinned" ? "pinned" : "list") : "list"));
        }
        _source = null;
        Apply(null);
    }

    private static bool IsLinked(FrameworkElement element, Focus focus) =>
        (GetItem(element) is { } item && item == focus.Item) ||
        (GetMarker(element) is { } marker && marker == focus.Marker) ||
        QuestsOf(element).Any(focus.Quests.Contains);

    private static readonly Brush Clear = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    private static void Paint(FrameworkElement element) =>
        SetBackground(element, Current is { } focus && IsLinked(element, focus)
            ? (Brush)Application.Current.Resources["LinkBrush"]
            : _picks.Count > 0 && QuestsOf(element).Any(_picks.Contains)
                ? (Brush)Application.Current.Resources["SelectBrush"]
                : Clear);

    private static Brush? Background(FrameworkElement element) => element switch
    {
        Panel p => p.Background,
        Border b => b.Background,
        Control c => c.Background,
        _ => null,
    };

    private static void SetBackground(FrameworkElement element, Brush brush)
    {
        switch (element)
        {
            case Panel p:
                p.Background = brush;
                break;
            case Border b:
                b.Background = brush;
                break;
            case Control c:
                c.Background = brush;
                break;
        }
    }
}
