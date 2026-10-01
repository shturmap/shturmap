using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Spotter.Core.Quests;
using Spotter.Map;

namespace Spotter.App.Controls;

/// <summary>The glyph for a quest type (docs/DESIGN.md §5), the same one the map draws in its markers.</summary>
public sealed partial class KindGlyph : Grid
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(ObjectiveKind), typeof(KindGlyph), new PropertyMetadata(ObjectiveKind.Trader, (d, _) => ((KindGlyph)d).Update()));

    public static readonly DependencyProperty BrushProperty = DependencyProperty.Register(
        nameof(Brush), typeof(Brush), typeof(KindGlyph), new PropertyMetadata(null, (d, _) => ((KindGlyph)d).Update()));

    private static readonly FontFamily IconFont = new(Glyphs.FontFamily);

    public KindGlyph()
    {
        Width = 16;
        Height = 16;
        VerticalAlignment = VerticalAlignment.Top;
        Update();
    }

    public ObjectiveKind Kind
    {
        get => (ObjectiveKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public Brush? Brush
    {
        get => (Brush?)GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    private void Update()
    {
        Children.Clear();
        IconElement icon = Glyphs.Character(Kind) is { } character
            ? new FontIcon { Glyph = character, FontFamily = IconFont, FontSize = 15 }
            // XAML path markup fills even-odd unless told otherwise; the crosshair is drawn for nonzero.
            : new PathIcon { Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), "F1 " + Glyphs.CrosshairPath) };
        if (Brush is not null)
            icon.Foreground = Brush;
        ToolTipService.SetToolTip(this, $"{QuestTaxonomy.Label(Kind)}: {QuestTaxonomy.Explanation(Kind)}");
        Children.Add(icon);
    }
}
