using Shturmap.Core;

namespace Shturmap.App.Rules;

/// <summary>
/// The tour and What's New in the language in use (docs/DESIGN.md §8, "Texts"; docs/LANGUAGES.md). Each is a Markdown
/// file built into the app, docs/tour.md and docs/whats-new.md in English, with each translation beside it as
/// docs/tour.&lt;code&gt;.md (docs/tour.de.md). Which one is read is decided each time the tour or the card is shown, so
/// a language switch takes effect the next time; the English one stands in where a language has none
/// (<see cref="Tour.InLanguage"/>, <see cref="WhatsNew.InLanguage"/>).
/// </summary>
public static class BuiltDocs
{
    /// <summary>The tour's chapters (<see cref="Rules.Tour"/>), as the app's resources name the file.</summary>
    public const string Tour = "tour.md";

    /// <summary>What's New's lines (<see cref="Rules.WhatsNew"/>).</summary>
    public const string WhatsNew = "whats-new.md";

    /// <summary>A file's translation into a language: "tour.de.md" for "tour.md" in German; null in English, whose file is
    /// the English one itself.</summary>
    /// <param name="code">The language: <see cref="UiLanguage.Code"/>.</param>
    public static string? TranslationOf(string file, string code)
    {
        if (code == UiLanguage.English || string.IsNullOrEmpty(code))
            return null;
        var dot = file.LastIndexOf('.');
        return dot < 0 ? $"{file}.{code}" : $"{file[..dot]}.{code}{file[dot..]}";
    }
}
