using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Shturmap.App.Controls;

/// <summary>
/// The highlighter on a quest: keeps the quest highlighted (cyan on the map) until it is clicked again. It is its
/// own control so that keeping a quest lit and keeping its card open are two different clicks (owner, 2026-10-01:
/// one click doing both was misleading). In rail rows it shows while the quest is pointed at anywhere, or while it is
/// the kept one; on a card it is always there, framed like the pop-out button beside it.
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
            Linked.SelectedChanged += Update;
            Update();
        };
        Unloaded += (_, _) =>
        {
            Linked.FocusChanged -= Update;
            Linked.SelectedChanged -= Update;
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
        var kept = QuestId is not null && Linked.Selected == QuestId;
        var pointed = QuestId is not null && Linked.Current?.Quests.Contains(QuestId) == true;
        Opacity = kept || Framed || pointed || _over ? 1 : 0;
        _icon.Foreground = Resource(kept ? "KeptBrush" : _over ? "InkBrush" : "MutedBrush");
        if (Framed)
        {
            Width = 30;
            Height = 28;
            BorderThickness = new Thickness(1);
            BorderBrush = Resource(kept ? "KeptBrush" : "LineBrush");
            _icon.FontSize = 14;
        }
        ToolTipService.SetToolTip(this, kept
            ? "Highlighted on the map. Click to stop"
            : "Highlight on the map: keeps this quest lit (cyan) until you click again");
    }
}
