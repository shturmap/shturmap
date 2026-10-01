using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Spotter.Core.Planning;
using Spotter.Map;
using Spotter.Session;

namespace Spotter.App.Controls;

public enum CardMode
{
    /// <summary>Shown while the pointer rests on a quest; goes away with it.</summary>
    Hover,

    /// <summary>The pointer rested long enough: the card stays while the pointer moves into it.</summary>
    Held,

    /// <summary>Its own window, open until closed.</summary>
    Pinned,
}

/// <summary>The quest card (docs/DESIGN.md §4, "Quest cards"): the same card on hover, held, and pinned.</summary>
public sealed partial class QuestCard : UserControl
{
    private Storyboard? _hold;

    public QuestCard()
    {
        InitializeComponent();
        PinGlyph.Glyph = char.ConvertFromUtf32(0xE718);
    }

    public QuestCardView? View { get; private set; }

    public CardMode Mode { get; private set; } = CardMode.Hover;

    /// <summary>The header, which pinned windows use as their title bar.</summary>
    public FrameworkElement DragArea => Header;

    public event Action<QuestCard>? PinClicked;

    /// <summary>The hold bar filled up.</summary>
    public event Action<QuestCard>? Held;

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
        // A pinned window's close button sits over the header's right end.
        Header.Margin = new Thickness(0, 0, mode == CardMode.Pinned ? 40 : 0, 0);
    }

    /// <summary>Fills the hold bar over the given time, then holds the card.</summary>
    public void StartHold(TimeSpan duration)
    {
        StopHold();
        var fill = new DoubleAnimation { From = 0, To = 1, Duration = duration };
        Storyboard.SetTarget(fill, HoldScale);
        Storyboard.SetTargetProperty(fill, "ScaleX");
        _hold = new Storyboard();
        _hold.Children.Add(fill);
        _hold.Completed += (_, _) =>
        {
            if (Mode != CardMode.Hover)
                return;
            SetMode(CardMode.Held);
            Held?.Invoke(this);
        };
        _hold.Begin();
    }

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
