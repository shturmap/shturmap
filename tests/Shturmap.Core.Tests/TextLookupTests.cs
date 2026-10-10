using System.Globalization;

namespace Shturmap.Core.Tests;

/// <summary>Tests that change the language in use (a single value for the whole app) run alone, after the others.</summary>
[CollectionDefinition("UiLanguage", DisableParallelization = true)]
public sealed class UiLanguageCollection;

// A text is looked up in the language in use, through the class made from its .resx at build time (docs/DESIGN.md §8,
// "Texts"); Text\SampleTexts.resx and its German file stand in for a project's texts.
[Collection("UiLanguage")]
public class TextLookupTests
{
    [Fact]
    public void A_text_comes_in_the_language_in_use_and_in_english_where_that_lacks_it()
    {
        try
        {
            UiLanguage.Set(UiLanguage.German);
            Assert.Equal("Mitbringen: MS2000 Marker", SampleTexts.Bring(item: "MS2000 Marker"));
            Assert.Equal("3 Schlüssel", SampleTexts.Keys(count: 3));
            Assert.Equal("Only in English", SampleTexts.OnlyEnglish);
            Assert.Equal("5.000", 5000.ToString("N0", UiLanguage.Culture));
            Assert.Equal("de", UiLanguage.Code);
        }
        finally
        {
            UiLanguage.Set(UiLanguage.English);
        }
        Assert.Equal("Bring: MS2000 Marker", SampleTexts.Bring(item: "MS2000 Marker"));
        Assert.Equal("1 key", SampleTexts.Keys(count: 1));
    }

    [Fact]
    public void The_pseudo_language_shows_every_text_accented_in_english_formats()
    {
        try
        {
            UiLanguage.Set(UiLanguage.Pseudo);
            Assert.StartsWith("[Bŕîñğ: MS2000 Marker ", SampleTexts.Bring(item: "MS2000 Marker"), StringComparison.Ordinal);
            Assert.Equal(UiLanguage.Pseudo, UiLanguage.Code);
            Assert.Equal("5,000", 5000.ToString("N0", UiLanguage.Culture));
        }
        finally
        {
            UiLanguage.Set(UiLanguage.English);
        }
    }

    [Fact]
    public void A_change_is_announced_once_and_a_language_without_texts_is_english()
    {
        var changes = 0;
        void Count() => changes++;
        UiLanguage.Changed += Count;
        try
        {
            UiLanguage.Set(UiLanguage.German);
            UiLanguage.Set(UiLanguage.German);
            Assert.Equal(1, changes);
            Assert.Equal(CultureInfo.GetCultureInfo("de-DE"), CultureInfo.CurrentCulture);
            UiLanguage.Set("ja");
            Assert.Equal("en", UiLanguage.Code);
            Assert.Equal(2, changes);
        }
        finally
        {
            UiLanguage.Changed -= Count;
            UiLanguage.Set(UiLanguage.English);
        }
    }
}
