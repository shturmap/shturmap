using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Shturmap.Map;

namespace Shturmap.App.Controls;

/// <summary>
/// "This goes to the trader after the raid" (owner, 2026-10-05; <see cref="Shturmap.Data.TarkovDev.Handovers"/>): right
/// after the cell of what an objective gets, its count when more than one, an arrow and the portrait of the trader it
/// goes to: "battery ×4 → Therapist" (owner, 2026-10-06, from a panel of five ways: "Hand-over: E"). No symbol to
/// learn, and no line of its own. The first mark, the Trader type's handshake alone, 12 px and muted in the corner of
/// the cell, was "barely visible and you cannot tell what that actually is nor what it means"; the second, a tag
/// with the words under the objective's lines, took a line on every hand-over. The tooltip says it in words.
/// </summary>
public sealed partial class HandoverMark : StackPanel
{
    public static readonly DependencyProperty TraderIdProperty = DependencyProperty.Register(
        nameof(TraderId), typeof(string), typeof(HandoverMark), new PropertyMetadata(null, (d, _) => ((HandoverMark)d).Update()));

    public static readonly DependencyProperty CountProperty = DependencyProperty.Register(
        nameof(Count), typeof(int), typeof(HandoverMark), new PropertyMetadata(0, (d, _) => ((HandoverMark)d).Update()));

    public static readonly DependencyProperty PortraitSizeProperty = DependencyProperty.Register(
        nameof(PortraitSize), typeof(double), typeof(HandoverMark), new PropertyMetadata(22.0, (d, _) => ((HandoverMark)d).Update()));

    public static readonly DependencyProperty TipProperty = DependencyProperty.Register(
        nameof(Tip), typeof(string), typeof(HandoverMark), new PropertyMetadata("", (d, _) => ((HandoverMark)d).Update()));

    public HandoverMark()
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;
        Update();
    }

    /// <summary>The trader it goes to; empty hides the mark.</summary>
    public string? TraderId
    {
        get => (string?)GetValue(TraderIdProperty);
        set => SetValue(TraderIdProperty, value);
    }

    /// <summary>How many go: said as "×4" when more than one.</summary>
    public int Count
    {
        get => (int)GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    /// <summary>The portrait's size: 22 beside a quest card's 30 px cell, 20 beside a raid line's.</summary>
    public double PortraitSize
    {
        get => (double)GetValue(PortraitSizeProperty);
        set => SetValue(PortraitSizeProperty, value);
    }

    /// <summary>The tooltip, the mark in words ("Hand over ×4 to Therapist after the raid").</summary>
    public string Tip
    {
        get => (string)GetValue(TipProperty);
        set => SetValue(TipProperty, value);
    }

    private void Update()
    {
        var shown = !string.IsNullOrEmpty(TraderId);
        Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(this, string.IsNullOrEmpty(Tip) ? null : Tip);
        Children.Clear();
        if (!shown)
            return;
        var small = PortraitSize < 21;
        // The count first, beside the cell as a need cell's is: it is the item's.
        if (Count > 1)
        {
            Children.Add(new TextBlock
            {
                Text = "×" + Count.ToString("N0", Shturmap.Session.UiLanguage.Culture),
                FontSize = small ? 11 : 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush("InkBrush"),
                Margin = new Thickness(3, 0, 0, 0),
            });
        }
        Children.Add(new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), "F1 " + Glyphs.HandoverArrow),
            Stretch = Stretch.Uniform,
            Width = small ? 8 : 10,
            Height = small ? 8 : 10,
            Fill = Brush("MutedBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 4, 0),
        });
        Children.Add(new Picture { TraderId = TraderId, Size = PortraitSize, VerticalAlignment = VerticalAlignment.Center });
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
