using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Spotter.Core.Planning;
using Spotter.Map;
using Spotter.Session;

namespace Spotter.App.Controls;

/// <summary>The quest card (docs/DESIGN.md §4, "Quest cards"): the same card on hover, held, nested and pinned.</summary>
public sealed partial class QuestCard : UserControl, ICard
{
    private Storyboard? _hold;

    public QuestCard(QuestCardView view)
    {
        InitializeComponent();
        PinGlyph.Glyph = char.ConvertFromUtf32(0xE718);
        Key = new CardKey.Quest(view.QuestId);
        Show(view);
    }

    public CardKey Key { get; }

    public QuestCardView View { get; private set; } = null!;

    public CardMode Mode { get; private set; } = CardMode.Hover;

    public event Action<QuestCard>? PinClicked;

    public event Action<ICard>? Held;

    public void Show(QuestCardView view)
    {
        View = view;
        Bindings.Update();
    }

    public void SetMode(CardMode mode)
    {
        Mode = mode;
        StopHold();
        Frame.BorderBrush = (Brush)Application.Current.Resources[mode == CardMode.Hover ? "LineBrush" : "AmberBrush"];
        Frame.CornerRadius = new CornerRadius(mode == CardMode.Pinned ? 0 : 8);
        Frame.BorderThickness = new Thickness(mode == CardMode.Pinned ? 0 : 1);
        HoldBar.Visibility = mode == CardMode.Pinned ? Visibility.Collapsed : Visibility.Visible;
        HoldScale.ScaleX = mode == CardMode.Held ? 1 : 0;
        PinButton.Visibility = mode == CardMode.Pinned ? Visibility.Collapsed : Visibility.Visible;
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

    private void OnPinClick(object sender, RoutedEventArgs e) => PinClicked?.Invoke(this);

    // ---- x:Bind helpers ----

    public static Visibility ShownIf(string? text) => string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility Shown(string? text) => ShownIf(text);

    public Visibility ShownIfAny(IEnumerable? items) => items?.GetEnumerator().MoveNext() == true ? Visibility.Visible : Visibility.Collapsed;

    public static string NeedGlyph(RequirementKind kind) => kind == RequirementKind.Key ? Glyphs.Key : Glyphs.Bring;

    public Uri? WikiUri(string? link) => Uri.TryCreate(link, UriKind.Absolute, out var uri) ? uri : null;
}

/// <summary>The hold bar's fill, shared by the quest and item cards.</summary>
internal static class HoldAnimation
{
    public static Storyboard Start(ScaleTransform bar, TimeSpan duration, Action completed)
    {
        var fill = new DoubleAnimation { From = 0, To = 1, Duration = duration };
        Storyboard.SetTarget(fill, bar);
        Storyboard.SetTargetProperty(fill, "ScaleX");
        var storyboard = new Storyboard();
        storyboard.Children.Add(fill);
        storyboard.Completed += (_, _) => completed();
        storyboard.Begin();
        return storyboard;
    }
}
