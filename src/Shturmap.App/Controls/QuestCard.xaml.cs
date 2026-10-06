using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Shturmap.Core.Planning;
using Shturmap.Map;
using Shturmap.Session;

namespace Shturmap.App.Controls;

/// <summary>The quest card (docs/DESIGN.md §4, "Quest cards"): the same card on hover, held, nested and popped out.</summary>
public sealed partial class QuestCard : UserControl, ICard
{
    public QuestCard(QuestCardView view)
    {
        InitializeComponent();
        // Segoe Fluent Icons "OpenInNewWindow": pop out. Not the pushpin, which is the Place quest type (owner,
        // 2026-10-03: one symbol, one meaning).
        PinGlyph.Glyph = char.ConvertFromUtf32(0xE8A7);
        Key = new CardKey.Quest(view.QuestId);
        Show(view);
        SetMode(CardMode.Hover);
    }

    public CardKey Key { get; }

    public string Title => View.Name;

    public QuestCardView View { get; private set; } = null!;

    public CardMode Mode { get; private set; } = CardMode.Hover;

    public event Action<QuestCard>? PinClicked;

    public void Show(QuestCardView view)
    {
        View = view;
        Bindings.Update();
    }

    public void SetMode(CardMode mode)
    {
        Mode = mode;
        Opacity = mode == CardMode.Hover ? CardLook.HoverOpacity : 1;
        Frame.BorderBrush = (Brush)Application.Current.Resources[mode == CardMode.Held ? "AmberBrush" : "LineStrongBrush"];
        Frame.BorderThickness = new Thickness(mode == CardMode.Pinned ? 0 : 1);
        PinButton.Visibility = mode == CardMode.Pinned ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnPinClick(object sender, RoutedEventArgs e) => PinClicked?.Invoke(this);

    private void OnWikiClick(object sender, RoutedEventArgs e) => Study.Ui("wiki.open", ("quest", View.QuestId), ("name", View.Name));

    // ---- x:Bind helpers ----

    public static Visibility ShownIf(string? text) => string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>A hand-over mark's tooltip: "Hand over ×3 to Therapist after the raid"; empty without one.</summary>
    public static string HandoverTip(string handover) => handover.Length > 0 ? handover + " after the raid" : "";

    public static Visibility ShownIfTrue(bool shown) => shown ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>An objective's text: muted once it is ticked as done.</summary>
    public static Brush TextBrush(bool done) => (Brush)Application.Current.Resources[done ? "MutedBrush" : "InkBrush"];

    /// <summary>An objective's type glyph: muted once it is ticked as done (grey means done, on the map too).</summary>
    public static Brush GlyphBrush(bool done) => (Brush)Application.Current.Resources[done ? "MutedBrush" : "AmberBrush"];

    /// <summary>
    /// The state line's colour: the accent while the quest is active, muted for every other state. It was green,
    /// which on the map and in the rail is a PMC extract (one colour, one meaning; the review of 2026-10-04, B2).
    /// </summary>
    public Brush StatusBrush(Shturmap.Core.Quests.QuestState state) =>
        (Brush)Application.Current.Resources[state == Shturmap.Core.Quests.QuestState.Active ? "AmberBrush" : "MutedBrush"];

    public Visibility Shown(string? text) => ShownIf(text);

    public Visibility ShownIfAny(IEnumerable? items) => items?.GetEnumerator().MoveNext() == true ? Visibility.Visible : Visibility.Collapsed;

    public static string NeedGlyph(RequirementKind kind) => kind == RequirementKind.Key ? Glyphs.Key : Glyphs.Bring;

    // The wiki link comes out of tarkov.dev's data: only an https address on the wiki is one (Rules.OutsideLink).
    public Uri? WikiUri(string? link) => Rules.OutsideLink.Wiki(link);

    public Visibility ShownIfWiki(string? link) => Rules.OutsideLink.Wiki(link) is null ? Visibility.Collapsed : Visibility.Visible;
}
