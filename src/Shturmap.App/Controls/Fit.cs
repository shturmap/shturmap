using Microsoft.UI.Xaml;

namespace Shturmap.App.Controls;

/// <summary>
/// Where a text is designed to give way when there is too little room: in a narrow window or a longer language it
/// may end in "…" there (docs/DESIGN.md §4, "Screen anatomy": the status bar's last fix, a Plan row's synopsis after
/// two lines, the notes of EXIT and OR after two lines). The layout check (developer builds; docs/LANGUAGES.md, "Layout
/// check") reports every other text that is cut off, so a text that may trim says so here, and only where the design
/// says it may.
/// </summary>
public static class Fit
{
    public static readonly DependencyProperty MayTrimProperty = DependencyProperty.RegisterAttached(
        "MayTrim", typeof(bool), typeof(Fit), new PropertyMetadata(false));

    public static bool GetMayTrim(DependencyObject element) => (bool)element.GetValue(MayTrimProperty);

    public static void SetMayTrim(DependencyObject element, bool value) => element.SetValue(MayTrimProperty, value);
}
