using System.Globalization;
using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

// Numbers and dates are written in the language of Shturmap's own texts, not in Windows' (review of 2026-10-04: a
// German Windows showed "5.000 ₽" and "3 Okt" inside English sentences). English today; other languages later, from
// this one place (owner, 2026-10-04).
public class UiLanguageTests
{
    [Fact]
    public void Numbers_and_dates_read_as_the_english_texts_around_them()
    {
        Assert.Equal("5,000", 5000.ToString("N0", UiLanguage.Culture));
        Assert.Equal("25 Sep", UiLanguage.DayMonth(new DateTime(2026, 9, 25)));
        Assert.Equal("4 Oct", UiLanguage.DayMonth(new DateOnly(2026, 10, 4)));
        Assert.Equal("4 Oct 14:30", UiLanguage.DayMonthTime(new DateTime(2026, 10, 4, 14, 30, 0)));
    }

    // A data name inside a sentence (UiLanguage.InSentence): lower case where the name's language writes a common noun
    // so, kept where it writes every noun with a capital (German), and kept when it is an abbreviation.
    [Fact]
    public void A_name_inside_a_sentence_is_written_as_its_language_writes_a_noun_there()
    {
        Assert.Equal("sniper rifle", UiLanguage.InSentence("Sniper rifle", "en"));
        Assert.Equal("SMG", UiLanguage.InSentence("SMG", "en"));
        Assert.Equal("Scharfschützengewehr", UiLanguage.InSentence("Scharfschützengewehr", "de"));
        Assert.Equal("снайперская винтовка", UiLanguage.InSentence("Снайперская винтовка", "ru"));
        Assert.Equal("ıslak", UiLanguage.InSentence("Islak", "tr"));
        Assert.Equal("x", UiLanguage.InSentence("x", "en"));
    }

    [Fact]
    public void Applying_it_makes_it_the_current_culture_whatever_windows_says()
    {
        var before = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture, CultureInfo.DefaultThreadCurrentCulture, CultureInfo.DefaultThreadCurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal("5.000", 5000.ToString("N0", CultureInfo.CurrentCulture));
            UiLanguage.Apply();
            Assert.Equal("5,000", 5000.ToString("N0", CultureInfo.CurrentCulture));
            Assert.Equal(UiLanguage.Culture, CultureInfo.CurrentUICulture);
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (before.Item1, before.Item2);
            CultureInfo.DefaultThreadCurrentCulture = before.Item3;
            CultureInfo.DefaultThreadCurrentUICulture = before.Item4;
        }
    }
}

// Each language writes a day and month its own way (UiLanguage.DayMonth): German puts a dot after the day, so "d MMM"
// read in German gives "4 Okt.", which is wrong (review of 2026-10-10).
[Collection("UiLanguage")]
public class DayMonthTests
{
    [Fact]
    public void A_day_and_month_read_as_the_language_in_use_writes_them()
    {
        try
        {
            UiLanguage.Set(UiLanguage.German);
            Assert.Equal("25. Sept.", UiLanguage.DayMonth(new DateTime(2026, 9, 25)));
            Assert.Equal("4. Okt.", UiLanguage.DayMonth(new DateOnly(2026, 10, 4)));
            Assert.Equal("1. März", UiLanguage.DayMonth(new DateOnly(2026, 3, 1)));
            Assert.Equal("4. Okt., 14:30", UiLanguage.DayMonthTime(new DateTime(2026, 10, 4, 14, 30, 0)));
            UiLanguage.Set(UiLanguage.Pseudo);
            Assert.Equal("4 Oct 14:30", UiLanguage.DayMonthTime(new DateTime(2026, 10, 4, 14, 30, 0)));
        }
        finally
        {
            UiLanguage.Set(UiLanguage.English);
        }
        Assert.Equal("4 Oct", UiLanguage.DayMonth(new DateOnly(2026, 10, 4)));
    }
}

// Capitals keep ß in .NET ("STRAßE"), and the font has no capital ẞ: German writes SS (docs/LANGUAGES.md, "Review").
// One rule for every label in capitals (UiLanguage.Upper), whatever the language in use, since a German name can stand
// in another language's text.
[Collection("UiLanguage")]
public class UpperTests
{
    [Fact]
    public void Capitals_write_sharp_s_as_double_s_in_every_language()
    {
        Assert.Equal("STRASSE · PRAPOR", UiLanguage.Upper("Straße · Prapor"));
        Assert.Equal("", UiLanguage.Upper(null));
        Assert.Equal("QUEST COMPLETE · GROSSHÄNDLER", CompletionWords.Of([new CompletionWords.Done("Test", "Großhändler", [])]).Eyebrow);
        try
        {
            UiLanguage.Set(UiLanguage.German);
            Assert.Equal("ABSCHLIESSEN ÄÖÜ", UiLanguage.Upper("abschließen äöü"));
        }
        finally
        {
            UiLanguage.Set(UiLanguage.English);
        }
    }
}
