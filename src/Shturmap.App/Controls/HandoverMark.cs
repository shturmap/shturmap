using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Shturmap.Core.Quests;
using Shturmap.Map;

namespace Shturmap.App.Controls;

/// <summary>
/// "This goes to the trader after the raid" (owner, 2026-10-05; <see cref="Shturmap.Data.TarkovDev.Handovers"/>): a
/// tag with the Trader type's handshake and the words, "HAND OVER" after a raid card line, "HAND OVER ×3 TO
/// THERAPIST" on a quest card. Words beside the symbol, since the handshake alone, 12 px and muted, was "barely visible
/// and you cannot tell what that actually is nor what it means" (owner, the same evening). In the badges' style: a
/// dark plate with a hairline, the words and the symbol in ink.
/// </summary>
public sealed partial class HandoverMark : Grid
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(HandoverMark), new PropertyMetadata("", (d, _) => ((HandoverMark)d).Update()));

    public static readonly DependencyProperty TipProperty = DependencyProperty.Register(
        nameof(Tip), typeof(string), typeof(HandoverMark), new PropertyMetadata("", (d, _) => ((HandoverMark)d).Update()));

    public HandoverMark()
    {
        VerticalAlignment = VerticalAlignment.Top;
        HorizontalAlignment = HorizontalAlignment.Left;
        Update();
    }

    /// <summary>The words on the tag, in capitals ("Hand over ×3 to Therapist"); empty hides it.</summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>The tooltip, where the tag says less than the whole ("Hand over to Therapist after the raid"); empty for none.</summary>
    public string Tip
    {
        get => (string)GetValue(TipProperty);
        set => SetValue(TipProperty, value);
    }

    private void Update()
    {
        var shown = !string.IsNullOrEmpty(Label);
        Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(this, string.IsNullOrEmpty(Tip) ? null : Tip);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(new Microsoft.UI.Xaml.Shapes.Path
        {
            // The handshake as the Trader type draws it everywhere (Glyphs; docs/DESIGN.md §5), fitted by its own bounds.
            Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), "F1 " + Glyphs.Path(ObjectiveKind.Trader)),
            Stretch = Stretch.Uniform,
            Width = 12,
            Height = 12,
            Fill = Brush("InkBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        row.Children.Add(new TextBlock
        {
            Text = shown ? Caps.Of(Label) : "",
            Style = (Style)Application.Current.Resources["StatusText"],
            FontSize = 10.5,
            Foreground = Brush("InkBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        Children.Clear();
        Children.Add(new Border
        {
            Background = Brush("GroundBrush"),
            BorderBrush = Brush("LineStrongBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(3, 1, 5, 1),
            Child = row,
        });
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
