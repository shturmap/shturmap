using System.Globalization;

namespace Shturmap.Core.Tests;

// The "Language" setting and what a run's "--culture" names (docs/DESIGN.md §8, "The app's own language"): a developer
// build offers the languages still being translated, so they can be tried; Automatic never takes one.
public class LanguageSettingTests
{
#if DEVTOOLS
    [Fact]
    public void A_developer_build_offers_the_languages_in_translation_after_the_finished_ones() =>
        Assert.Equal(UiLanguage.Supported.Concat(UiLanguage.InTranslation), UiLanguage.Offered);
#else
    [Fact]
    public void A_release_offers_only_the_finished_languages() =>
        Assert.Equal(UiLanguage.Supported, UiLanguage.Offered);
#endif

    // German is finished (2026-10-10): offered in every build, and Automatic takes it from the game or from Windows.
    [Fact]
    public void German_is_offered_and_taken_by_itself()
    {
        Assert.Equal([UiLanguage.English, UiLanguage.German], UiLanguage.Offered.Take(2));
        Assert.Equal(new LanguageChoice("de", "de", LanguageSource.Setting), UiLanguage.Choose("de", "ru", "en-US"));
        Assert.Equal(new LanguageChoice("de", "de", LanguageSource.Game), UiLanguage.Choose(null, "ge", "en-US"));
        Assert.Equal(new LanguageChoice("de", "ru", LanguageSource.Windows), UiLanguage.Choose(null, "ru", "de-DE"));
    }

    // A language in translation, French say, while only English is finished: a German game on a German Windows reads
    // the same way today.
    private static readonly string[] Finished = [UiLanguage.English];
    private static readonly string[] Written = [UiLanguage.English, "fr"];

    [Fact]
    public void Automatic_never_takes_a_language_in_translation() =>
        // A French game on a French Windows: French game names, English texts until French is finished.
        Assert.Equal(new LanguageChoice(UiLanguage.English, "fr", LanguageSource.Default), UiLanguage.Choose(null, "fr", "fr-FR", Finished, Written));

    [Fact]
    public void A_runs_own_language_may_be_one_in_translation() =>
        Assert.Equal(new LanguageChoice("fr", "fr", LanguageSource.Setting), UiLanguage.Choose("fr", "en", "en-US", Finished, Written));

    [Theory]
    [InlineData("de-DE", "de")]
    [InlineData("en-US", "en")]
    [InlineData("de", "de")]
    [InlineData("qps-ploc", UiLanguage.Pseudo)]
    [InlineData("QPS-PLOC", UiLanguage.Pseudo)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void A_cultures_name_gives_the_language(string? culture, string? code) =>
        Assert.Equal(code, UiLanguage.CodeOf(culture));

    [Theory]
    [InlineData("en", "English")]
    [InlineData("de", "Deutsch")]
    [InlineData("ru", "Русский")]
    public void Each_language_is_named_in_itself_with_a_capital(string code, string name) =>
        Assert.Equal(name, UiLanguage.NativeName(code));
}

// The current culture travels with async work: a loop started before a switch keeps the old one, unless the code that
// composes what is shown takes the language in use first (UiLanguage.ApplyHere).
[Collection("UiLanguage")]
public class LanguageHereTests
{
    [Fact]
    public async Task Work_started_before_a_switch_takes_the_new_language_where_it_composes()
    {
        var switched = new TaskCompletionSource();
        try
        {
            UiLanguage.Set(UiLanguage.English);
            var loop = Task.Run(async () =>
            {
                await switched.Task;
                var before = CultureInfo.CurrentCulture.Name;
                UiLanguage.ApplyHere();
                return (before, CultureInfo.CurrentCulture.Name, CultureInfo.CurrentUICulture.Name);
            }, TestContext.Current.CancellationToken);
            UiLanguage.Set(UiLanguage.German);
            switched.SetResult();
            var (before, after, ui) = await loop;
            Assert.Equal("en-US", before);
            Assert.Equal("de-DE", after);
            Assert.Equal("de-DE", ui);
        }
        finally
        {
            UiLanguage.Set(UiLanguage.English);
        }
    }
}
