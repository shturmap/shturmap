#if DEVTOOLS
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace Shturmap.App.Dev;

/// <summary>
/// "--text-scale &lt;factor&gt;" (developer builds only; docs/DESIGN.md §8, "Developer aids"; 2026-10-19, the beta's "text
/// hard to read on some setups"): the window as Windows' text size (Settings › Accessibility › Text size, 1 to 2.25) would
/// show it, without touching that setting, so snapshots and the layout check show what clips or wraps. Every text in the
/// window and its open popups (a TextBlock's, its runs', a RichTextBlock's, a text box's, a FontIcon's glyph) takes the
/// size WinUI gives it at that factor, by WinUI's own curve (smaller text grows more), and the map's labels and symbols
/// grow by the factor (MapView). A simulation: text measured off the window before it was scaled here can differ from
/// what Windows' setting would give.
/// </summary>
internal static class TextScale
{
    /// <summary>The factor, 1 (off) unless "--text-scale" gave one.</summary>
    public static double Factor { get; private set; } = 1;

    public static bool On => Factor != 1;

    /// <summary>Scales a text measured off the window (a fit's probe, a card before it is shown) as the window's are.</summary>
    public static void Probe(FrameworkElement probe)
    {
        if (On)
            Apply(probe);
    }

    public static void Read(string? arg)
    {
        if (double.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out var factor))
            Factor = Math.Clamp(factor, 1, 2.25);
    }

    /// <summary>The size WinUI draws a text of <paramref name="size"/> at a text scale factor (its
    /// TextFormatting::GetScaledFontSize): s + max(18 − e·ln s, 0)·(f − 1).</summary>
    public static double Scaled(double size, double factor) =>
        size + Math.Max(18 - Math.E * Math.Log(Math.Max(size, 1)), 0) * (factor - 1);

    // The size this aid last gave each element: one that differs since was set anew (by code or a binding), and is
    // scaled again from there. Kept on the element itself: a managed wrapper of an element nothing in C# holds (an icon in
    // a template) is collected and made anew, so a table keyed by it forgot the element and scaled it again and again.
    private static readonly DependencyProperty GivenProperty = DependencyProperty.RegisterAttached(
        "Given", typeof(double), typeof(TextScale), new PropertyMetadata(0.0));

    /// <summary>Scales every text in the window and its open popups, every 200 ms while the window is open.</summary>
    public static void Start(Window window)
    {
        if (!On)
            return;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += (_, _) => ApplyTo(window);
        window.Closed += (_, _) => timer.Stop();
        timer.Start();
    }

    public static void ApplyTo(Window window)
    {
        if (!On || window.Content is not UIElement content)
            return;
        Apply(content);
        if (content.XamlRoot is { } root)
            foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(root))
                if (popup.Child is { } child)
                    Apply(child);
    }

    private static void Apply(DependencyObject element)
    {
        switch (element)
        {
            case TextBlock { IsTextScaleFactorEnabled: true } text:
                Scale(text, TextBlock.FontSizeProperty, text.FontSize);
                foreach (var inline in text.Inlines)
                    Apply(inline);
                return;
            case RichTextBlock { IsTextScaleFactorEnabled: true } rich:
                Scale(rich, RichTextBlock.FontSizeProperty, rich.FontSize);
                foreach (var block in rich.Blocks)
                    if (block is Paragraph paragraph)
                        foreach (var inline in paragraph.Inlines)
                            Apply(inline);
                return;
            case FontIcon { IsTextScaleFactorEnabled: true } icon:
                Scale(icon, FontIcon.FontSizeProperty, icon.FontSize);
                return;
            // A text box draws its own text; its placeholder takes the box's size.
            case TextBox { IsTextScaleFactorEnabled: true } box:
                Scale(box, Control.FontSizeProperty, box.FontSize);
                return;
        }
        var count = VisualTreeHelper.GetChildrenCount(element);
        for (var i = 0; i < count; i++)
            Apply(VisualTreeHelper.GetChild(element, i));
        // Not yet in the visual tree (a card measured before it is shown): its content as written.
        if (count == 0)
        {
            var child = element switch
            {
                UserControl user => user.Content,
                Border border => border.Child,
                ContentControl control => control.Content as UIElement,
                _ => null,
            };
            if (child is not null)
                Apply(child);
            else if (element is Panel panel)
                foreach (var each in panel.Children)
                    Apply(each);
        }
    }

    // A run's own size, where it has one; otherwise it takes its text block's.
    private static void Apply(Inline inline)
    {
        if (inline.ReadLocalValue(TextElement.FontSizeProperty) != DependencyProperty.UnsetValue)
            Scale(inline, TextElement.FontSizeProperty, inline.FontSize);
        if (inline is Span span)
            foreach (var each in span.Inlines)
                Apply(each);
    }

    // Every size this aid gave, as WinUI keeps it (a float): a text that copies another's size (the roll call's halo, a
    // fit's probe) has it already, and isn't scaled twice.
    private static readonly HashSet<float> Produced = [];

    private static void Scale(DependencyObject element, DependencyProperty property, double size)
    {
        if ((float)(double)element.GetValue(GivenProperty) == (float)size)
            return;
        if (!Produced.Contains((float)size))
        {
            element.SetValue(property, Scaled(size, Factor));
            size = (double)element.GetValue(property);
            Produced.Add((float)size);
        }
        element.SetValue(GivenProperty, size);
    }
}
#endif
