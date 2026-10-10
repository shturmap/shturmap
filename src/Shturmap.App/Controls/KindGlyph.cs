using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Shturmap.Core.Quests;
using Shturmap.Map;

namespace Shturmap.App.Controls;

/// <summary>The glyph for a quest type (docs/DESIGN.md §5), the same one the map draws in its markers.</summary>
public sealed partial class KindGlyph : Grid
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(ObjectiveKind), typeof(KindGlyph), new PropertyMetadata(ObjectiveKind.Trader, (d, _) => ((KindGlyph)d).Update()));

    public static readonly DependencyProperty BrushProperty = DependencyProperty.Register(
        nameof(Brush), typeof(Brush), typeof(KindGlyph), new PropertyMetadata(null, (d, _) => ((KindGlyph)d).Update()));

    public KindGlyph()
    {
        Width = 16;
        Height = 16;
        VerticalAlignment = VerticalAlignment.Top;
        Update();
        // Its tooltip names the type in the language in use.
        SaidAgain.OnLanguage(this, Update);
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
        // The type's shape, fitted into 15 px by its own bounds, as the map fits it into a marker. XAML path markup
        // fills even-odd unless told otherwise; the shapes are drawn for nonzero ("F1").
        var icon = new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), "F1 " + Glyphs.Path(Kind)),
            Stretch = Stretch.Uniform,
            Width = 15,
            Height = 15,
            Fill = Brush ?? (Brush)Application.Current.Resources["InkBrush"],
        };
        ToolTipService.SetToolTip(this, QuestTaxonomy.Tip(Kind));
        Children.Add(icon);
    }
}
