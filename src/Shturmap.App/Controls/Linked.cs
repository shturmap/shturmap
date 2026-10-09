using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Shturmap.App.Rules;

namespace Shturmap.App.Controls;

/// <summary>What the pointer is on: quests (and everything about them), an item, or a map marker such as an extract.</summary>
/// <param name="Objective">One objective of the quest pointed at (its line in a list, one of its places on the map):
/// the whole quest stays lit, and this objective is marked within it.</param>
public sealed record Focus(IReadOnlySet<string> Quests, string? Item = null, string? Marker = null, string? Objective = null)
{
    public static Focus Quest(string id) => new(new HashSet<string> { id });

    /// <summary>
    /// Every item the thing pointed at stands for when it stands for several (an "A or B" key row, gear worn together,
    /// a weapon class); empty for one item. <see cref="Item"/> is the one it pictures, whose card opens.
    /// </summary>
    public IReadOnlyList<string> Alternatives { get; init; } = [];

    /// <summary>The items in focus: <see cref="Item"/> and its <see cref="Alternatives"/>, each once.</summary>
    public IEnumerable<string> Items => Item is null ? Alternatives : Alternatives.Prepend(Item).Distinct();
}

/// <summary>
/// Linked highlighting (brushing and linking): rows say what they show with attached properties, and pointing at
/// one highlights every other row, card and map marker that shows the same quest, item or marker. Nothing to
/// click; the highlight goes when the pointer leaves. Linked elements may lie inside one another (an objective's
/// line in its quest's block, a need cell in its quest's row): the innermost one around the pointer is the one
/// pointed at, and leaving it returns to the one around it (<see cref="PointerNest{T}"/>).
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

    public static readonly DependencyProperty ObjectiveProperty = DependencyProperty.RegisterAttached(
        "Objective", typeof(string), typeof(Linked), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty ItemsProperty = DependencyProperty.RegisterAttached(
        "Items", typeof(object), typeof(Linked), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty AlsoItemProperty = DependencyProperty.RegisterAttached(
        "AlsoItem", typeof(string), typeof(Linked), new PropertyMetadata(null, OnChanged));

    /// <summary>
    /// Every item (an IEnumerable&lt;string&gt;) this element stands for when it stands for several: an "A or B" key
    /// row, gear worn together, a weapon class. It lights up when any of them is pointed at, and pointing at it
    /// lights them all; its card is still that of <see cref="ItemProperty"/>, the one it pictures (the review of
    /// 2026-10-04, E4: such a row was linked to its first item alone).
    /// </summary>
    public static object? GetItems(DependencyObject d) => d.GetValue(ItemsProperty);

    public static void SetItems(DependencyObject d, object? value) => d.SetValue(ItemsProperty, value);

    /// <summary>
    /// An item this element keeps in focus while the pointer is on it, without lighting up itself for it and without
    /// opening its card: the body of the item's own card. Moving from a row onto the card it opened used to let go
    /// of the item, and with it of its locks and loose spots on the map (the review of 2026-10-04, E3).
    /// </summary>
    public static string? GetAlsoItem(DependencyObject d) => (string?)d.GetValue(AlsoItemProperty);

    public static void SetAlsoItem(DependencyObject d, string? value) => d.SetValue(AlsoItemProperty, value);

    // No change handler: it says how a linked element sits, and links nothing by itself.
    public static readonly DependencyProperty InlineProperty = DependencyProperty.RegisterAttached(
        "Inline", typeof(bool), typeof(Linked), new PropertyMetadata(false));

    /// <summary>
    /// The objective (its id) this element shows: its line under its quest in the raid card, its row on the quest's
    /// card, the glance's NEXT. The element lights up when that objective is pointed at, here or at one of its places
    /// on the map, and pointing at it marks the objective within its quest (named beside it with Quest or Also): on
    /// the map only the objective's own places pulse.
    /// </summary>
    public static string? GetObjective(DependencyObject d) => (string?)d.GetValue(ObjectiveProperty);

    public static void SetObjective(DependencyObject d, string? value) => d.SetValue(ObjectiveProperty, value);

    /// <summary>
    /// A small linked thing among others in a row (a need cell, a quest's glyph on a folded Plan card): its card
    /// opens beside the row it lies in (<see cref="RowAround"/>), and it isn't taken for the quest's row.
    /// </summary>
    public static bool GetInline(DependencyObject d) => (bool)d.GetValue(InlineProperty);

    public static void SetInline(DependencyObject d, bool value) => d.SetValue(InlineProperty, value);

    /// <summary>
    /// Quest ids (an IEnumerable&lt;string&gt;, or one id) this element lights up when pointed at, without lighting up
    /// itself for them: rows on a quest's own card, which would otherwise all glow whenever that quest is in focus,
    /// and an objective's line inside its quest's block, which is lit for the quest already.
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

    // The linked elements the pointer is in; the innermost is the one pointed at (_source).
    private static readonly PointerNest<FrameworkElement> Nest = new(IsWithin);
    private static FrameworkElement? _source;

    // The element the cards were last told about (Hovered), until they are told it was left.
    private static FrameworkElement? _cardSource;

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

    /// <summary>
    /// Which of the picks' colours each pick has (QuestPicks.Slots): its row's tint and its pen take it, as its
    /// markers on the map do, so a row and its places are found by their colour (owner, 2026-10-04).
    /// </summary>
    public static IReadOnlyDictionary<string, int> PickSlots
    {
        get => _pickSlots;
        set
        {
            if (value.Count == _pickSlots.Count && value.All(p => _pickSlots.TryGetValue(p.Key, out var slot) && slot == p.Value))
                return;
            _pickSlots = value;
            foreach (var element in Live)
                Paint(element);
            PicksChanged?.Invoke();
        }
    }

    private static IReadOnlyDictionary<string, int> _pickSlots = new Dictionary<string, int>();

    // The map's own colours for the picks, as brushes: full for pens, glyphs and frames, at 15 % behind rows.
    private static readonly Brush[] PickBrushes = [.. Shturmap.Map.MapRenderer.PickColors.Select(c => (Brush)new SolidColorBrush(Windows.UI.Color.FromArgb(255, c.Red, c.Green, c.Blue)))];
    private static readonly Brush[] PickTints = [.. Shturmap.Map.MapRenderer.PickColors.Select(c => (Brush)new SolidColorBrush(Windows.UI.Color.FromArgb(0x26, c.Red, c.Green, c.Blue)))];

    private static int Wrap(int slot) => ((slot % PickBrushes.Length) + PickBrushes.Length) % PickBrushes.Length;

    private static int SlotOf(string? quest) => quest is not null && _pickSlots.TryGetValue(quest, out var slot) ? Wrap(slot) : 0;

    /// <summary>A picked quest's colour.</summary>
    public static Brush PickBrush(string? questId) => PickBrushes[SlotOf(questId)];

    /// <summary>The colour of the n-th of the picks' colours.</summary>
    public static Brush PickBrush(int slot) => PickBrushes[Wrap(slot)];

    /// <summary>Raised when the picks change (the pens follow them).</summary>
    public static event Action? PicksChanged;

    /// <summary>A pen was clicked: pick this quest for the coming raid, or unpick it.</summary>
    public static event Action<string>? KeepRequested;

    public static void RequestKeep(string questId) => KeepRequested?.Invoke(questId);

    /// <summary>Raised when the focus changes, from rows or from <see cref="Set"/> (map markers).</summary>
    public static event Action? FocusChanged;

    /// <summary>
    /// The pointer is on a linked element, as far as cards go. The key is the card it opens: its item if it has one,
    /// else its quest; null for rows that only highlight (several quests, a marker). An element that opens no card
    /// and lies inside one that does (an objective's line in its quest's block) isn't announced: for the cards the
    /// pointer is still on the one around it.
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
            if (element.XamlRoot != viewport.XamlRoot || GetQuest(element) is not { } quest || element.ActualHeight <= 0 || GetInline(element))
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
        if (ReferenceEquals(element, _source))
            return;
        DropKeyRow();
        Nest.Clear();
        if (element is not null)
            Nest.Enter(element);
        Refocus();
    }

    /// <summary>The quest's block in a window's lists (the tallest loaded element showing just that quest), or null.</summary>
    public static FrameworkElement? RowOf(string questId, XamlRoot root) =>
        Live.Where(e => e.XamlRoot == root && GetQuest(e) == questId && !GetInline(e) && CardStack.For(root)?.Contains(e) != true && e.ActualHeight > 0)
            .OrderByDescending(e => e.ActualHeight)
            .FirstOrDefault();

    /// <summary>Sets the focus from outside the rows, e.g. a map marker under the pointer.</summary>
    public static void Set(Focus? focus)
    {
        DropKeyRow();
        Nest.Clear();
        _source = null;
        Apply(focus);
    }

    // ---- the keyboard's row (the review of 2026-10-04, E6: the highlight needed a pointer) ----

    // The linked row the keyboard is on, and the window whose rows it walks. While it walks them, that window's
    // pointer waits: it only takes over again when it moves (PointerTakesOver), since a row scrolled under a resting
    // pointer must not take the highlight from the row the keyboard has just reached.
    private static FrameworkElement? _keyRow;
    private static XamlRoot? _keyWindow;

    /// <summary>The linked row the keyboard is on, or null.</summary>
    public static FrameworkElement? KeyRow => _keyRow;

    /// <summary>
    /// The keyboard's row went by something other than the keyboard: the pointer pointed at something elsewhere (the
    /// map, another window), or nobody is looking any more.
    /// </summary>
    public static event Action? KeyRowTaken;

    /// <summary>
    /// The element of the keyboard's row shows something else now. The lists reuse their rows' elements when a
    /// snapshot fills them anew, so the element stays and what it is linked to changes: the window looks for the
    /// row that shows the thing now.
    /// </summary>
    public static event Action? KeyRowChanged;

    /// <summary>
    /// The keyboard reaches a row: it is in focus exactly as if the pointer were on it (the same focus, the same
    /// tint, the map lighting the same things), but no card opens by itself. With no row, the keyboard is on
    /// something in that window that isn't a linked row (a map of Plan's list): nothing is in focus, and the window's
    /// pointer still waits. The pointer's own row and its hover card let go first, as if the pointer had left.
    /// </summary>
    public static void ReachByKey(XamlRoot window, FrameworkElement? row)
    {
        Nest.Clear();
        Refocus();
        _keyWindow = window;
        _keyRow = row;
        var focus = row is null ? null : FocusOf(row);
        if (!Same(focus, Current))
            Apply(focus);
    }

    /// <summary>The keyboard lets its row go (Esc): nothing is in focus until the pointer moves onto something.</summary>
    public static void LeaveByKey()
    {
        if (_keyWindow is null)
            return;
        _keyRow = null;
        _keyWindow = null;
        Apply(null);
    }

    /// <summary>
    /// The pointer moved in the window whose rows the keyboard walks: it takes over at once, and is on whatever lies
    /// under it now (<paramref name="under"/>: the elements at its place, topmost first, as a hit test gives them).
    /// </summary>
    public static void PointerTakesOver(IEnumerable<UIElement> under)
    {
        if (_keyWindow is null)
            return;
        _keyRow = null;
        _keyWindow = null;
        Nest.Clear();
        foreach (var element in under.OfType<FrameworkElement>().Where(e => Hooked.TryGetValue(e, out _)).Reverse())
            Nest.Enter(element);
        if (Nest.Top is null)
            Apply(null);
        Refocus();
    }

    /// <summary>A click on the keyboard's row (Enter): what a click on it with the mouse does.</summary>
    public static void ClickByKey()
    {
        if (_keyRow is { } row && CardSourceAt(row) is { } source)
            Clicked?.Invoke(source, KeyOf(source));
    }

    /// <summary>The quest the keyboard's row is about (its own, or the one its objective belongs to), for its pen;
    /// null for a row of none or of several.</summary>
    public static string? KeyRowQuest =>
        _keyRow is not { } row ? null
        : GetQuest(row) is { } quest ? quest
        : AlsoOf(row).Take(2).ToList() is [var only] ? only
        : null;

    /// <summary>The linked rows inside an area (the rail): not the small things among others (need cells, glyphs).</summary>
    public static IReadOnlyList<FrameworkElement> RowsWithin(FrameworkElement area) =>
        Live.Where(e => !GetInline(e) && IsWithin(e, area)).ToList();

    /// <summary>What a row shows, to find it again after the rows were made anew.</summary>
    public static RowId IdOf(FrameworkElement row) => RowId.Linked(GetQuest(row), GetObjective(row), GetItem(row), GetMarker(row));

    private static void DropKeyRow()
    {
        if (_keyWindow is null)
            return;
        _keyRow = null;
        _keyWindow = null;
        KeyRowTaken?.Invoke();
    }

    // The pointer entered or left something in the window whose rows the keyboard walks: it waits.
    private static bool KeyboardHolds(FrameworkElement element)
    {
        if (_keyWindow is null)
            return false;
        if (element.XamlRoot == _keyWindow)
            return true;
        // Another window's pointer: it points, and the keyboard's row goes.
        DropKeyRow();
        Apply(null);
        return false;
    }

    private static bool Same(Focus? a, Focus? b) =>
        a is null || b is null ? a is null && b is null
        : a.Quests.SetEquals(b.Quests) && a.Item == b.Item && a.Marker == b.Marker && a.Objective == b.Objective
          && a.Alternatives.SequenceEqual(b.Alternatives);

    /// <summary>
    /// For a small linked thing among others (<see cref="InlineProperty"/>): the row it lies in, which is the
    /// outermost linked element around it or, failing that, the button around it (a folded Plan card). Null for
    /// anything else.
    /// </summary>
    public static FrameworkElement? RowAround(FrameworkElement element)
    {
        if (!GetInline(element))
            return null;
        FrameworkElement? row = null;
        FrameworkElement? button = null;
        for (var node = VisualTreeHelper.GetParent(element); node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is FrameworkElement linked && Hooked.TryGetValue(linked, out _))
                row = linked;
            else if (button is null && node is ButtonBase around)
                button = around;
        }
        return row ?? button;
    }

    /// <summary>
    /// Lets go of what the pointer is on, as if it had left: when nobody is looking any more (Shturmap's windows are
    /// no longer the active ones), the highlight and the map's pulse stop instead of running on behind the game.
    /// </summary>
    public static void LetGo()
    {
        DropKeyRow();
        Nest.Clear();
        Refocus();
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
            // A tap passes from an inner linked element up through the ones around it: only one of them answers, the
            // one the cards take the pointer to be on.
            element.Tapped += (_, e) =>
            {
                if (ReferenceEquals(CardSourceAt(e.OriginalSource), element))
                    Clicked?.Invoke(element, KeyOf(element));
            };
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
                // the pointer entered something else. So it lets go as it leaves, and of everything: what lies
                // around it or inside it is usually leaving with it, and can't be asked where it is any more.
                if (Nest.Leave(element) || ReferenceEquals(_cardSource, element))
                {
                    Nest.Clear();
                    Refocus();
                }
            };
            // Rows need a fill to be hit anywhere, not just on their text.
            if (Background(element) is null)
                SetBackground(element, new SolidColorBrush(Microsoft.UI.Colors.Transparent));
            if (element.IsLoaded)
                Live.Add(element);
        }
        Paint(element);
        if (ReferenceEquals(element, _keyRow))
            KeyRowChanged?.Invoke();
    }

    private static IEnumerable<string> QuestsOf(FrameworkElement element)
    {
        if (GetQuest(element) is { } quest)
            yield return quest;
        if (GetQuests(element) is IEnumerable<string> quests)
            foreach (var q in quests)
                yield return q;
    }

    private static IEnumerable<string> AlsoOf(FrameworkElement element) => GetAlso(element) switch
    {
        string one => [one],
        IEnumerable<string> several => several,
        _ => [],
    };

    private static bool IsWithin(FrameworkElement inner, FrameworkElement outer)
    {
        for (var node = VisualTreeHelper.GetParent(inner); node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, outer))
                return true;
        }
        return false;
    }

    private static DateTime _enteredAt;

    private static void Enter(FrameworkElement element)
    {
        if (KeyboardHolds(element))
            return;
        Nest.Enter(element);
        Refocus();
    }

    private static void Exit(FrameworkElement element)
    {
        if (KeyboardHolds(element))
            return;
        Nest.Leave(element);
        Refocus();
    }

#if DEVTOOLS
    /// <summary>
    /// Developer scripts: the pointer enters or leaves a linked element, through the code the pointer's own events
    /// call. A script can't move the mouse, and nothing is sent to the system.
    /// </summary>
    internal static void DevPointer(FrameworkElement element, bool enters)
    {
        if (enters)
            Enter(element);
        else
            Exit(element);
    }

    /// <summary>The linked element pointed at now, for a script to leave it.</summary>
    internal static FrameworkElement? DevSource => _source;

    /// <summary>
    /// The loaded linked elements in a window, or of those the ones inside another, for a script to find a line or a
    /// cell.
    /// </summary>
    internal static IEnumerable<FrameworkElement> DevLive(XamlRoot root, FrameworkElement? within = null) =>
        Live.Where(e => e.XamlRoot == root && (within is null || IsWithin(e, within))).ToList();
#endif

    // The pointer entered or left a linked element: what it points at now is the innermost one it is in.
    private static void Refocus()
    {
        var top = Nest.Top;
        if (!ReferenceEquals(top, _source))
        {
            // Resting on something is the closest the study log gets to "looked at it"; passing over it is not.
            var dwell = DateTime.Now - _enteredAt;
            if (_source is { } left && dwell >= TimeSpan.FromMilliseconds(400) && Current is { } focus)
            {
                Study.Ui("hover", ("quests", focus.Quests.ToList()), ("item", focus.Item), ("marker", focus.Marker), ("objective", focus.Objective), ("s", dwell),
                    ("where", CardStack.For(left.XamlRoot)?.Contains(left) == true ? "card" : "list"));
            }
            _source = top;
            _enteredAt = DateTime.Now;
            Apply(top is null ? null : FocusOf(top));
        }
        // For the cards the pointer is on the innermost element that opens one: an objective's line opens none, and
        // on it the cards stay with its quest's block. With no such element around, the one pointed at counts (a
        // row that only highlights lets hover cards go, as before).
        var holder = Nest.Outward().FirstOrDefault(e => KeyOf(e) is not null) ?? top;
        if (ReferenceEquals(holder, _cardSource))
            return;
        if (_cardSource is { } before)
            Left?.Invoke(before);
        _cardSource = holder;
        if (holder is not null)
            Hovered?.Invoke(holder, KeyOf(holder));
    }

    private static Focus FocusOf(FrameworkElement element)
    {
        var parts = LinkFocus.Of(GetQuest(element), GetQuests(element) as IEnumerable<string>, AlsoOf(element), GetItem(element),
            GetAlsoItem(element), GetItems(element) as IEnumerable<string>, GetMarker(element), GetObjective(element));
        return new Focus(parts.Quests, parts.Item, parts.Marker, parts.Objective) { Alternatives = parts.Alternatives };
    }

    // The card an element opens: its item if it has one, else its quest; none for rows that only highlight.
    private static CardKey? KeyOf(FrameworkElement element) =>
        GetItem(element) is { } item ? new CardKey.Item(item) : GetQuest(element) is { } quest ? new CardKey.Quest(quest) : null;

    // Which linked element a click at a target is for: going outward from it, the first that opens a card; if none
    // does, the innermost.
    private static FrameworkElement? CardSourceAt(object? target)
    {
        FrameworkElement? innermost = null;
        for (var node = target as DependencyObject; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is not FrameworkElement element || !Hooked.TryGetValue(element, out _))
                continue;
            if (KeyOf(element) is not null)
                return element;
            innermost ??= element;
        }
        return innermost;
    }

    // How strongly an element is lit by the focus: fully when it shows the very thing pointed at, weakly when it only
    // belongs to it, not at all otherwise (Rules.LinkStrength; owner, 2026-10-04).
    private static Rules.LinkLevel LevelOf(FrameworkElement element, Focus focus) =>
        Rules.LinkStrength.Of(GetQuest(element), GetQuests(element) as IEnumerable<string> ?? [], GetItem(element), GetMarker(element),
            GetObjective(element), focus.Quests, focus.Item, focus.Marker, focus.Objective,
            GetItems(element) as IEnumerable<string>, focus.Alternatives);

    private static readonly Brush Clear = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    private static void Paint(FrameworkElement element) =>
        SetBackground(element, (Current is { } focus ? LevelOf(element, focus) : Rules.LinkLevel.None) switch
        {
            Rules.LinkLevel.Same => (Brush)Application.Current.Resources["LinkBrush"],
            Rules.LinkLevel.Related => (Brush)Application.Current.Resources["LinkSoftBrush"],
            // A pick's row, and a row that serves picks, in the pick's colour (the first's, where it serves several).
            _ => _picks.Count > 0 && QuestsOf(element).Where(_picks.Contains).Select(SlotOf).DefaultIfEmpty(-1).Min() is >= 0 and var slot
                ? PickTints[slot]
                : Clear,
        });

    // A need cell is filled by its own dark cell and its picture: its tint lies over both (Picture.Tint).
    private static Brush? Background(FrameworkElement element) => element switch
    {
        Picture cell => cell.Tint,
        Panel p => p.Background,
        Border b => b.Background,
        Control c => c.Background,
        _ => null,
    };

    private static void SetBackground(FrameworkElement element, Brush brush)
    {
        switch (element)
        {
            case Picture cell:
                cell.Tint = brush;
                break;
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
