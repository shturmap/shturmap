using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Spotter.App.Controls;

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

    /// <summary>Raised when the focus changes, from rows or from <see cref="Set"/> (map markers).</summary>
    public static event Action? FocusChanged;

    /// <summary>The pointer entered (questId) or left (null) an element that shows one quest.</summary>
    public static event Action<FrameworkElement, string?>? QuestHovered;

    /// <summary>Sets the focus from outside the rows, e.g. a map marker under the pointer.</summary>
    public static void Set(Focus? focus)
    {
        _source = null;
        Apply(focus);
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
            element.Loaded += (_, _) =>
            {
                Live.Add(element);
                Paint(element);
            };
            element.Unloaded += (_, _) => Live.Remove(element);
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

    private static void Enter(FrameworkElement element)
    {
        _source = element;
        var item = GetItem(element);
        var marker = GetMarker(element);
        Apply(new Focus(QuestsOf(element).ToHashSet(), item, marker));
        if (item is null && marker is null && GetQuest(element) is { } quest)
            QuestHovered?.Invoke(element, quest);
    }

    private static void Exit(FrameworkElement element)
    {
        if (_source != element)
            return;
        _source = null;
        Apply(null);
        if (GetItem(element) is null && GetMarker(element) is null && GetQuest(element) is not null)
            QuestHovered?.Invoke(element, null);
    }

    private static bool IsLinked(FrameworkElement element, Focus focus) =>
        (GetItem(element) is { } item && item == focus.Item) ||
        (GetMarker(element) is { } marker && marker == focus.Marker) ||
        QuestsOf(element).Any(focus.Quests.Contains);

    private static readonly Brush Clear = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    private static void Paint(FrameworkElement element) =>
        SetBackground(element, Current is { } focus && IsLinked(element, focus)
            ? (Brush)Application.Current.Resources["LinkBrush"]
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
