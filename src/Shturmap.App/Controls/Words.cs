using Microsoft.UI.Xaml;

namespace Shturmap.App.Controls;

/// <summary>
/// A text that stays English in every language by design: what a report or a crash report sends, shown before it goes
/// (docs/DESIGN.md §8, "The app's own language": diagnostics and reports are English, for whoever fixes Shturmap). The
/// layout check doesn't look for untranslated words in it in the pseudo-language, nor for words that break in it: its
/// paths and log lines break anywhere by nature (developer builds; docs/LANGUAGES.md, "Layout check"). Only there may
/// it be set.
/// </summary>
public static class Words
{
    public static readonly DependencyProperty EnglishProperty = DependencyProperty.RegisterAttached(
        "English", typeof(bool), typeof(Words), new PropertyMetadata(false));

    public static bool GetEnglish(DependencyObject element) => (bool)element.GetValue(EnglishProperty);

    public static void SetEnglish(DependencyObject element, bool value) => element.SetValue(EnglishProperty, value);
}
