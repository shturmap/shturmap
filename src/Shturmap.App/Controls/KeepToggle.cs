using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Shturmap.App.Controls;

/// <summary>
/// The pen on a quest: picks it for the coming raid (in a colour of its own on the map, first in the rail) until it is clicked again, the
/// quest is done or the picks are cleared; several quests can be picked (owner, 2026-10-03). It is its own control so
/// that keeping a quest lit and keeping its card open are two different clicks (owner, 2026-10-01: one click doing
/// both was misleading). In rail rows it shows while the quest is pointed at anywhere, or while it is picked; on a
/// card it is always there, framed like the pop-out button beside it.
/// </summary>
public sealed partial class KeepToggle : Grid
{
    public static readonly DependencyProperty QuestIdProperty = DependencyProperty.Register(
        nameof(QuestId), typeof(string), typeof(KeepToggle), new PropertyMetadata(null, (d, _) => ((KeepToggle)d).Update()));

    public static readonly DependencyProperty FramedProperty = DependencyProperty.Register(
        nameof(Framed), typeof(bool), typeof(KeepToggle), new PropertyMetadata(false, (d, _) => ((KeepToggle)d).Update()));

    // Segoe Fluent Icons "Highlight": a highlighter pen.
    private readonly FontIcon _icon = new() { Glyph = char.ConvertFromUtf32(0xE7E6), FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private bool _over;

    public KeepToggle()
    {
        Width = 24;
        Height = 22;
        VerticalAlignment = VerticalAlignment.Top;
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        Children.Add(_icon);
        Loaded += (_, _) =>
        {
            Linked.FocusChanged += Update;
            Linked.PicksChanged += Update;
            Update();
        };
        Unloaded += (_, _) =>
        {
            Linked.FocusChanged -= Update;
            Linked.PicksChanged -= Update;
        };
        PointerEntered += (_, _) =>
        {
            _over = true;
            Update();
        };
        PointerExited += (_, _) =>
        {
            _over = false;
            Update();
        };
        // Handled here, so the row under it doesn't also take the click as "keep the card".
        Tapped += (_, e) =>
        {
            e.Handled = true;
            if (QuestId is { } quest)
                Linked.RequestKeep(quest);
        };
        DoubleTapped += (_, e) => e.Handled = true;
    }

    public string? QuestId
    {
        get => (string?)GetValue(QuestIdProperty);
        set => SetValue(QuestIdProperty, value);
    }

    /// <summary>Always shown, in a frame like a button (on cards).</summary>
    public bool Framed
    {
        get => (bool)GetValue(FramedProperty);
        set => SetValue(FramedProperty, value);
    }

    private static Brush Resource(string key) => (Brush)Application.Current.Resources[key];

    private void Update()
    {
        var kept = QuestId is not null && Linked.Picks.Contains(QuestId);
        var pointed = QuestId is not null && Linked.Current?.Quests.Contains(QuestId) == true;
        Opacity = kept || Framed || pointed || _over ? 1 : 0;
        _icon.Foreground = kept ? Linked.PickBrush(QuestId) : Resource(_over ? "InkBrush" : "MutedBrush");
        if (Framed)
        {
            Width = 30;
            Height = 28;
            BorderThickness = new Thickness(1);
            BorderBrush = kept ? Linked.PickBrush(QuestId) : Resource("LineBrush");
            _icon.FontSize = 14;
        }
        ToolTipService.SetToolTip(this, kept
            ? "Picked for the coming raid: in this colour on the map, first in its card. Click to unpick"
            : "Pick for the coming raid: keeps this quest lit on the map in a colour of its own and first in its card, until it is done or you click again");
    }
}
