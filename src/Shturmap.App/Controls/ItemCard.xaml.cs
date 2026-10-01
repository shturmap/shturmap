using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Shturmap.Map;
using Shturmap.Session;

namespace Shturmap.App.Controls;

/// <summary>The item card: shown when pointing at a key or an item on a card or in BRING.</summary>
public sealed partial class ItemCard : UserControl, ICard
{
    private Storyboard? _hold;

    public ItemCard(ItemCardView view)
    {
        InitializeComponent();
        Key = new CardKey.Item(view.ItemId);
        Show(view);
    }

    public CardKey Key { get; }

    public string Title => View.Name;

    public ItemCardView View { get; private set; } = null!;

    public CardMode Mode { get; private set; } = CardMode.Hover;

    public event Action<ICard>? Held;

    public void Show(ItemCardView view)
    {
        View = view;
        Bindings.Update();
    }

    public void SetMode(CardMode mode)
    {
        Mode = mode;
        StopHold();
        Frame.BorderBrush = (Brush)Application.Current.Resources[mode == CardMode.Hover ? "LineStrongBrush" : "AmberBrush"];
        HoldScale.ScaleX = mode == CardMode.Held ? 1 : 0;
    }

    public void StartHold(TimeSpan duration) => _hold = HoldAnimation.Start(HoldScale, duration, () =>
    {
        if (Mode != CardMode.Hover)
            return;
        SetMode(CardMode.Held);
        Held?.Invoke(this);
    });

    public void StopHold()
    {
        _hold?.Stop();
        _hold = null;
    }

    // ---- x:Bind helpers ----

    public string Glyph(bool isKey) => isKey ? Glyphs.Key : Glyphs.Bring;

    public string KindText(bool isKey) => isKey ? "Key" : "Item";

    public Visibility Shown(int count) => count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility Hidden(int count) => count > 0 ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Segoe Fluent Icons for sources without a trader: craft (wrench), flea (shop), loose (pin).</summary>
    public static string SourceGlyph(SourceKind kind) => char.ConvertFromUtf32(kind switch
    {
        SourceKind.Craft => 0xE90F,
        SourceKind.Flea => 0xE719,
        SourceKind.Loose => 0xE707,
        _ => 0xE77B,
    });

    public static Visibility ShownIfTrader(string? traderId) => traderId is null ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility ShownIfNoTrader(string? traderId) => traderId is null ? Visibility.Visible : Visibility.Collapsed;
}
