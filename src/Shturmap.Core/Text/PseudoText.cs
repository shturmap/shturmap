using System.Collections.Concurrent;
using System.Text;

namespace Shturmap.Core.Text;

/// <summary>
/// The made-up language of developer checks (<see cref="UiLanguage.Pseudo"/>; docs/LANGUAGES.md): an English text's
/// letters accented, the text made about 40% longer and put in brackets, its placeholders kept: "Bring: {item}" becomes
/// "[Ɓŕîñğ: {item} ···]", a long text's padding in words of twelve dots at most. Longer, since German runs about a
/// third longer than English and other languages more; accented with letters the app's font has (Bahnschrift, checked
/// 2026-10-10), so the text keeps its font and its line height is a real one's. A text on screen without brackets
/// wasn't moved into the texts.
/// </summary>
public static class PseudoText
{
    private const string Plain = "abcdefgiklnoprstuyzACDEFGIKLNOPRSTUYZ";
    private const string Accented = "åbçðéƒğîķļñöþŕšţûýžÅÇÐÉƑĞÎĶĻÑÖÞŔŠŢÛÝŽ";

    private static readonly ConcurrentDictionary<string, string> Made = new(StringComparer.Ordinal);

    /// <summary>The pseudo-language's version of an English text.</summary>
    public static string Of(string english) => Made.GetOrAdd(english, static text =>
    {
        var letters = 0;
        var accented = TextFormat.MapLiterals(text, literal =>
        {
            var changed = new StringBuilder(literal.Length);
            foreach (var c in literal)
            {
                var at = Plain.IndexOf(c);
                changed.Append(at < 0 ? c : Accented[at]);
                if (char.IsLetter(c))
                    letters++;
            }
            return changed.ToString();
        });
        if (letters == 0)
            return text;
        var padding = Math.Max(2, (int)Math.Round(letters * 0.4));
        var dots = new StringBuilder(padding + padding / PaddingWord);
        for (var i = 0; i < padding; i++)
        {
            if (i > 0 && i % PaddingWord == 0)
                dots.Append(' ');
            dots.Append('·');
        }
        return "[" + accented + " " + dots + "]";
    });

    /// <summary>
    /// The padding's longest word: a long text's padding is words of this many dots (release QA pass of 2026-10-19, F6: a
    /// paragraph's forty per cent was one word of 130 dots and more, which no language has, and the layout check found
    /// it breaking inside the word in every paragraph that wraps). Shorter than a long German word ("Menü-Screenshots").
    /// </summary>
    public const int PaddingWord = 12;

    /// <summary>Whether a text on screen reads as the pseudo-language's (in brackets, as <see cref="Of"/> makes it).</summary>
    public static bool IsPseudo(string shown) => shown.Contains('[') && shown.Contains('·');
}
