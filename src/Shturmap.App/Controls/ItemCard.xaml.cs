using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Shturmap.Map;
using Shturmap.Session;

namespace Shturmap.App.Controls;

/// <summary>The item card: shown when pointing at a key or an item on a card or in BRING.</summary>
public sealed partial class ItemCard : UserControl, ICard
{
    public ItemCard(ItemCardView view)
    {
        InitializeComponent();
        Key = new CardKey.Item(view.ItemId);
        Show(view);
        SetMode(CardMode.Hover);
    }

    public CardKey Key { get; }

    public string Title => View.Name;

    public ItemCardView View { get; private set; } = null!;

    public CardMode Mode { get; private set; } = CardMode.Hover;

    public void Show(ItemCardView view)
    {
        View = view;
        Bindings.Update();
    }

    public void SetMode(CardMode mode)
    {
        Mode = mode;
        Opacity = mode == CardMode.Hover ? CardLook.HoverOpacity : 1;
        Frame.BorderBrush = (Brush)Application.Current.Resources[mode == CardMode.Held ? "AmberBrush" : "LineStrongBrush"];
    }

    // ---- x:Bind helpers ----

    public string Glyph(bool isKey) => isKey ? Glyphs.Key : Glyphs.Bring;

    public string KindText(bool isKey) => isKey ? AppTexts.CardKindKey : AppTexts.CardKindItem;

    public Visibility Shown(int count) => count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility Hidden(int count) => count > 0 ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Segoe Fluent Icons for sources without a trader: craft (wrench), flea market (price tag; the shopping bag
    /// is the Find-in-raid quest type, owner 2026-10-03), loose (pin on a surface).</summary>
    public static string SourceGlyph(SourceKind kind) => char.ConvertFromUtf32(kind switch
    {
        SourceKind.Craft => 0xE90F,
        SourceKind.Flea => 0xE8EC,
        SourceKind.Loose => 0xE707,
        _ => 0xE77B,
    });

    public static Visibility ShownIfTrader(string? traderId) => traderId is null ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility ShownIfNoTrader(string? traderId) => traderId is null ? Visibility.Visible : Visibility.Collapsed;
}
