namespace Shturmap.App.Rules;

/// <summary>
/// Where a name may break a line: between its words only. WinUI's wrapping (WrapWholeWords too) also breaks after a
/// hyphen, so YOUR EXTRACTS THIS RAID's rows broke "Primorsky Ave Taxi V-" over "Ex" in the pseudo-language at 900×560
/// (QA pass, 2026-10-15; docs/DESIGN.md §4, principle 11). A word joiner (U+2060, drawn as nothing) after each hyphen
/// holds the two halves together; Bahnschrift has no non-breaking hyphen (U+2011) to put in the hyphen's place, and
/// another font's would show.
/// </summary>
public static class WordBreaks
{
    /// <summary>The joiner that follows each hyphen.</summary>
    public const char Joiner = '⁠';

    /// <summary>The name as it is drawn where it may break only between words (MainWindow.RollCall's rows).</summary>
    public static string BetweenWordsOnly(string name) => name.Replace("-", "-" + Joiner, StringComparison.Ordinal);
}
