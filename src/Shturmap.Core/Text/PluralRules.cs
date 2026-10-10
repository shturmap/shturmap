namespace Shturmap.Core.Text;

/// <summary>
/// Which plural form a number takes in a language: Unicode's CLDR categories ("one", "few", "many", "other") for
/// whole numbers, for every language tarkov.dev has (docs/LANGUAGES.md), so a new language needs no code here. English
/// and German have two forms; Czech, Slovak and Romanian three; Polish and Russian need "many" besides "few" (2 pliki,
/// 5 plików). A number with decimals is "other".
/// </summary>
public static class PluralRules
{
    /// <summary>The categories a language uses for whole numbers, which a text in it must cover when it has a plural;
    /// "other" is always among them, since the syntax asks for it (<see cref="TextFormat"/>).</summary>
    public static IReadOnlyList<string> Categories(string language) => language switch
    {
        "ja" or "ko" or "zh" => ["other"],
        "fr" or "pt" or "es" or "it" => ["one", "many", "other"],
        "ru" or "uk" or "pl" => ["one", "few", "many", "other"],
        "cs" or "sk" or "ro" => ["one", "few", "other"],
        _ => ["one", "other"],
    };

    /// <summary>The category of <paramref name="number"/> in <paramref name="language"/> (an ISO 639-1 code).</summary>
    public static string Category(string language, decimal number)
    {
        if (number != decimal.Truncate(number))
            return "other";
        var i = decimal.ToUInt64(Math.Abs(number));
        var millions = i != 0 && i % 1_000_000 == 0;
        var twoToFour = i % 10 is >= 2 and <= 4 && i % 100 is < 12 or > 14;
        return language switch
        {
            "ja" or "ko" or "zh" => "other",
            "fr" or "pt" => i is 0 or 1 ? "one" : millions ? "many" : "other",
            "es" or "it" => i == 1 ? "one" : millions ? "many" : "other",
            "ru" or "uk" => i % 10 == 1 && i % 100 != 11 ? "one" : twoToFour ? "few" : "many",
            "pl" => i == 1 ? "one" : twoToFour ? "few" : "many",
            "cs" or "sk" => i == 1 ? "one" : i is >= 2 and <= 4 ? "few" : "other",
            "ro" => i == 1 ? "one" : i == 0 || i % 100 is >= 1 and <= 19 ? "few" : "other",
            _ => i == 1 ? "one" : "other",
        };
    }
}
