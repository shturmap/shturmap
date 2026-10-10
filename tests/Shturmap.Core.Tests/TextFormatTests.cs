using System.Globalization;
using Shturmap.Core.Text;

namespace Shturmap.Core.Tests;

// Texts are whole sentences with named placeholders and plural forms (docs/DESIGN.md §8, "Texts"), so each language
// puts the words where its grammar wants them.
public class TextFormatTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");

    private const string Extracts = "Your extract list is read: {count, plural, one {# extract} other {# extracts}} for this raid";

    [Fact]
    public void Named_placeholders_take_their_values_wherever_the_sentence_puts_them()
    {
        Assert.Equal("Bring: MS2000 Marker", TextFormat.Format(En, "Bring: {item}", ("item", "MS2000 Marker")));
        Assert.Equal("Woods, then Customs", TextFormat.Format(En, "{first}, then {second}", ("second", "Customs"), ("first", "Woods")));
    }

    [Fact]
    public void Plurals_take_the_form_of_the_number_in_the_language()
    {
        Assert.Equal("Your extract list is read: 1 extract for this raid", TextFormat.Format(En, Extracts, ("count", 1)));
        Assert.Equal("Your extract list is read: 6 extracts for this raid", TextFormat.Format(En, Extracts, ("count", 6)));
        const string polish = "{n, plural, one {# plik} few {# pliki} many {# plików} other {# pliku}}";
        Assert.Equal("1 plik", TextFormat.Format(Pl, polish, ("n", 1)));
        Assert.Equal("3 pliki", TextFormat.Format(Pl, polish, ("n", 3)));
        Assert.Equal("5 plików", TextFormat.Format(Pl, polish, ("n", 5)));
        Assert.Equal("22 pliki", TextFormat.Format(Pl, polish, ("n", 22)));
    }

    [Fact]
    public void An_exact_branch_comes_before_the_category()
    {
        const string text = "{n, plural, =0 {no extracts} one {# extract} other {# extracts}}";
        Assert.Equal("no extracts", TextFormat.Format(En, text, ("n", 0)));
        Assert.Equal("1 extract", TextFormat.Format(En, text, ("n", 1)));
    }

    [Fact]
    public void Numbers_are_written_as_the_language_writes_them_and_grouped_only_by_the_caller()
    {
        Assert.Equal("2.5 km", TextFormat.Format(En, "{distance} km", ("distance", 2.5m)));
        Assert.Equal("2,5 km", TextFormat.Format(De, "{distance} km", ("distance", 2.5m)));
        Assert.Equal("Pay 5,000 ₽", TextFormat.Format(En, "Pay {amount}", ("amount", 5000.ToString("N0", En) + " ₽")));
    }

    [Fact]
    public void A_select_takes_the_branch_its_value_names()
    {
        const string text = "{side, select, pmc {PMC extract} scav {Scav extract} other {Extract}}";
        Assert.Equal("Scav extract", TextFormat.Format(En, text, ("side", "scav")));
        Assert.Equal("Extract", TextFormat.Format(En, text, ("side", "both")));
    }

    [Fact]
    public void A_missing_value_shows_its_placeholder_and_a_hash_outside_a_plural_is_text()
    {
        Assert.Equal("Bring: {item}", TextFormat.Format(En, "Bring: {item}"));
        Assert.Equal("Key #3", TextFormat.Format(En, "Key #{n}", ("n", 3)));
    }

    [Theory]
    [InlineData("Bring: {item")]
    [InlineData("Bring: item}")]
    [InlineData("{}")]
    [InlineData("{n, plural, one {# extract}}")]
    [InlineData("{n, count, other {#}}")]
    [InlineData("{n, plural, one {a} one {b} other {c}}")]
    public void A_text_that_doesnt_parse_says_why_and_shows_as_written(string text)
    {
        Assert.NotNull(TextFormat.Problem(text));
        Assert.Equal(text, TextFormat.Format(En, text, ("n", 1), ("item", "x")));
    }

    [Fact]
    public void Its_placeholders_and_plural_branches_can_be_listed()
    {
        Assert.Equal(["listed", "extracts", "time"], TextFormat.Arguments("Your list: {listed} of {extracts, plural, one {# extract} other {# extracts}} ({time})."));
        Assert.Equal(["one", "other"], TextFormat.PluralBranches(Extracts)!["count"]);
        Assert.Null(TextFormat.Problem(Extracts));
    }

    [Fact]
    public void Placeholders_inside_a_branch_count_too()
    {
        const string text = "{n, plural, one {# key for {door}} other {# keys for {door}}}";
        Assert.Equal(["n", "door"], TextFormat.Arguments(text));
        Assert.Equal("2 keys for Room 314", TextFormat.Format(En, text, ("n", 2), ("door", "Room 314")));
    }
}

public class PluralRulesTests
{
    [Theory]
    [InlineData("en", 1, "one")]
    [InlineData("en", 0, "other")]
    [InlineData("de", 1, "one")]
    [InlineData("de", 2, "other")]
    [InlineData("fr", 0, "one")]
    [InlineData("fr", 1_000_000, "many")]
    [InlineData("ru", 21, "one")]
    [InlineData("ru", 11, "many")]
    [InlineData("ru", 23, "few")]
    [InlineData("ru", 13, "many")]
    [InlineData("pl", 1, "one")]
    [InlineData("pl", 21, "many")]
    [InlineData("pl", 24, "few")]
    [InlineData("cs", 4, "few")]
    [InlineData("cs", 5, "other")]
    [InlineData("ro", 0, "few")]
    [InlineData("ro", 19, "few")]
    [InlineData("ro", 20, "other")]
    [InlineData("ro", 101, "few")]
    [InlineData("ja", 1, "other")]
    public void A_whole_number_takes_its_category_in_the_language(string language, int number, string category) =>
        Assert.Equal(category, PluralRules.Category(language, number));

    [Fact]
    public void A_number_with_decimals_is_other() => Assert.Equal("other", PluralRules.Category("en", 1.5m));

    [Fact]
    public void Every_language_names_the_categories_it_uses_and_other_among_them()
    {
        foreach (var language in new[] { "cs", "de", "en", "es", "fr", "hu", "it", "ja", "ko", "pl", "pt", "ro", "ru", "sk", "tr", "zh" })
        {
            var categories = PluralRules.Categories(language);
            Assert.Contains("other", categories);
            for (var n = 0; n <= 200; n++)
                Assert.Contains(PluralRules.Category(language, n), categories);
        }
    }
}

public class PseudoTextTests
{
    [Fact]
    public void A_text_is_accented_longer_and_bracketed_with_its_placeholders_kept()
    {
        var pseudo = PseudoText.Of("Bring: {item}");
        Assert.StartsWith("[Bŕîñğ: {item} ", pseudo, StringComparison.Ordinal);
        Assert.EndsWith("·]", pseudo, StringComparison.Ordinal);
        Assert.True(PseudoText.IsPseudo(pseudo));
        Assert.Equal(TextFormat.Arguments("Bring: {item}"), TextFormat.Arguments(pseudo));
    }

    [Fact]
    public void A_plural_keeps_its_syntax_and_still_fills_in()
    {
        var pseudo = PseudoText.Of("{count, plural, one {# extract} other {# extracts}} left");
        var shown = TextFormat.Format(CultureInfo.GetCultureInfo("en-US"), pseudo, ("count", 3));
        Assert.StartsWith("[3 éxţŕåçţš ļéƒţ ", shown, StringComparison.Ordinal);
    }

    [Fact]
    public void A_text_without_letters_stays_as_it_is() => Assert.Equal("×3", PseudoText.Of("×3"));

    [Fact]
    public void It_is_about_forty_percent_longer()
    {
        const string english = "Not checked against your list yet.";
        Assert.InRange(PseudoText.Of(english).Length, english.Length * 1.3, english.Length * 1.6);
    }
}

public class LanguageChoiceTests
{
    private static readonly string[] EnglishAndGerman = [UiLanguage.English, UiLanguage.German];

    [Fact]
    public void The_setting_decides_everything_texts_and_game_names() =>
        Assert.Equal(new LanguageChoice("en", "en", LanguageSource.Setting), UiLanguage.Choose("en", "ge", "de-DE", EnglishAndGerman));

    [Fact]
    public void Automatic_takes_the_games_language_in_its_common_code() =>
        Assert.Equal(new LanguageChoice("de", "de", LanguageSource.Game), UiLanguage.Choose(null, "ge", "en-US", EnglishAndGerman));

    [Fact]
    public void A_game_language_without_texts_keeps_its_game_names_and_takes_windows_language_for_the_texts() =>
        Assert.Equal(new LanguageChoice("de", "ru", LanguageSource.Windows), UiLanguage.Choose("auto", "ru", "de-AT", EnglishAndGerman));

    [Fact]
    public void Without_the_games_language_windows_decides_both() =>
        Assert.Equal(new LanguageChoice("de", "de", LanguageSource.Windows), UiLanguage.Choose(null, null, "de-DE", EnglishAndGerman));

    [Fact]
    public void Neither_known_is_english() =>
        Assert.Equal(new LanguageChoice("en", "ja", LanguageSource.Default), UiLanguage.Choose(null, "jp", "fr-FR", EnglishAndGerman));

    [Fact]
    public void A_language_not_complete_yet_isnt_chosen() =>
        Assert.Equal(new LanguageChoice("en", "de", LanguageSource.Default), UiLanguage.Choose("de", "ge", "de-DE", [UiLanguage.English]));

    [Fact]
    public void The_pseudo_language_shows_english_game_names() =>
        Assert.Equal(new LanguageChoice(UiLanguage.Pseudo, "en", LanguageSource.Setting), UiLanguage.Choose(UiLanguage.Pseudo, "ge", null, EnglishAndGerman));

    [Fact]
    public void A_windows_language_net_doesnt_know_is_no_language() =>
        Assert.Equal(LanguageSource.Default, UiLanguage.Choose(null, null, "not a culture!", EnglishAndGerman).Source);
}
