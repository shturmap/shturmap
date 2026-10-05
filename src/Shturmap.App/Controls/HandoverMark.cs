using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Shturmap.Core.Quests;
using Shturmap.Map;

namespace Shturmap.App.Controls;

/// <summary>
/// "This goes to the trader after the raid" (owner, 2026-10-05; <see cref="Shturmap.Data.TarkovDev.Handovers"/>): the
/// Trader type's handshake, small and muted, the colour of what happens after the raid. After a raid card line's text
/// (with <see cref="Text"/> as its tooltip), and as a plate in the corner of an item's cell (<see cref="Plate"/>).
/// </summary>
public sealed partial class HandoverMark : Grid
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(HandoverMark), new PropertyMetadata("", (d, _) => ((HandoverMark)d).Update()));

    public HandoverMark()
    {
        VerticalAlignment = VerticalAlignment.Top;
        Update();
    }

    /// <summary>What it says in words ("Hand over to Therapist after the raid"); empty hides the mark.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private void Update()
    {
        Children.Clear();
        var shown = !string.IsNullOrEmpty(Text);
        Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(this, shown ? Text : null);
        // Room around it to point at, as on the status bar's words with tooltips.
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        Padding = new Thickness(2, 1, 2, 1);
        Children.Add(Shape(12));
    }

    /// <summary>The mark on a plate in an item cell's lower right corner, sized to the cell.</summary>
    public static FrameworkElement Plate(double cell)
    {
        var side = Math.Clamp(Math.Round(cell * 0.42), 11, 15);
        return new Border
        {
            Width = side,
            Height = side,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Background = Brush("GroundBrush"),
            BorderBrush = Brush("LineStrongBrush"),
            BorderThickness = new Thickness(1),
            IsHitTestVisible = false,
            Child = Shape(side - 4),
        };
    }

    // The handshake as the Trader type draws it everywhere (Glyphs; docs/DESIGN.md §5), fitted by its own bounds.
    private static Microsoft.UI.Xaml.Shapes.Path Shape(double size) => new()
    {
        Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), "F1 " + Glyphs.Path(ObjectiveKind.Trader)),
        Stretch = Stretch.Uniform,
        Width = size,
        Height = size,
        Fill = Brush("MutedBrush"),
    };

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
