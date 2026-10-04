using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Shturmap.App.Controls;

/// <summary>
/// The tick at the end of an objective's row on a quest card: the player says the objective is done (owner,
/// 2026-10-04). The game's logs never say it, so a quest that takes several raids kept leading to places already
/// dealt with; a tick takes the objective out of the map's guide, NEXT and the plans until its quest is over or the
/// box is clicked again. It is never asked for. The box looks like the ticks in settings: a small square, amber with
/// a check when ticked.
/// </summary>
public sealed partial class TickBox : Grid
{
    public static readonly DependencyProperty ObjectiveIdProperty = DependencyProperty.Register(
        nameof(ObjectiveId), typeof(string), typeof(TickBox), new PropertyMetadata(null));

    public static readonly DependencyProperty TickedProperty = DependencyProperty.Register(
        nameof(Ticked), typeof(bool), typeof(TickBox), new PropertyMetadata(false, (d, _) => ((TickBox)d).Update()));

    /// <summary>A tick box was clicked: tick this objective as done, or untick it.</summary>
    public static event Action<string>? Requested;

    private readonly Border _box = new() { Width = 13, Height = 13, BorderThickness = new Thickness(1) };

    // Segoe Fluent Icons "CheckMark", as in settings' ticks.
    private readonly FontIcon _check = new() { Glyph = char.ConvertFromUtf32(0xE73E), FontSize = 9 };
    private bool _over;

    public TickBox()
    {
        // A larger area than the box itself, so it is easy to hit.
        Width = 24;
        Height = 24;
        VerticalAlignment = VerticalAlignment.Top;
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        _box.Child = _check;
        _box.HorizontalAlignment = HorizontalAlignment.Center;
        _box.VerticalAlignment = VerticalAlignment.Center;
        Children.Add(_box);
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
        // Handled here, so the row and the card under it don't take the click for themselves.
        Tapped += (_, e) =>
        {
            e.Handled = true;
            if (ObjectiveId is { } objective)
                Requested?.Invoke(objective);
        };
        DoubleTapped += (_, e) => e.Handled = true;
        Update();
    }

    public string? ObjectiveId
    {
        get => (string?)GetValue(ObjectiveIdProperty);
        set => SetValue(ObjectiveIdProperty, value);
    }

    public bool Ticked
    {
        get => (bool)GetValue(TickedProperty);
        set => SetValue(TickedProperty, value);
    }

    private static Brush Resource(string key) => (Brush)Application.Current.Resources[key];

    private void Update()
    {
        _box.BorderBrush = Resource(Ticked ? "AmberBrush" : _over ? "InkBrush" : "LineStrongBrush");
        _box.Background = Ticked ? Resource("AmberBrush") : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        _check.Foreground = Resource("GroundBrush");
        _check.Visibility = Ticked ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(this, Ticked ? "Done, ticked by you" : "Tick as done");
        ToolTipService.SetToolTip(this, Ticked
            ? "Ticked as done by you: the map, NEXT and the plan leave it out. Click to untick"
            : "Tick when you have done this objective. The game's logs don't say it, so until then the map and the plan keep leading here");
    }
}
