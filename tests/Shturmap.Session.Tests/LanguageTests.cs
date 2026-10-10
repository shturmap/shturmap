using Shturmap.Core;
using Shturmap.Core.Quests;
using Shturmap.Core.Text;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

/// <summary>Tests that change the language in use (a single value for the whole app) run alone, after the others.</summary>
[CollectionDefinition("UiLanguage", DisableParallelization = true)]
public sealed class UiLanguageCollection;

// What the Session writes in another language than English (docs/DESIGN.md §8, "The app's own language"): the pseudo-
// language stands in for one, since every text in it is marked.
[Collection("UiLanguage")]
public class LanguageTests
{
    private static T In<T>(string language, Func<T> read)
    {
        try
        {
            UiLanguage.Set(language);
            return read();
        }
        finally
        {
            UiLanguage.Set(UiLanguage.English);
        }
    }

    // Diagnostics are read by whoever fixes Shturmap, so they stay English (until 2026-10-10 the data's problem was
    // said in the language in use).
    [Fact]
    public void Diagnostics_say_the_data_problem_in_english_in_any_language()
    {
        var snapshot = new SessionSnapshot { DataProblem = LoadProblem.Explain(new HttpRequestException("x")) };
        var text = In(UiLanguage.Pseudo, () => Diagnostics.Build(snapshot, "0.4.0", "Windows 11", "dev build", [], new DateTime(2026, 1, 1, 14, 5, 0), null));
        Assert.Contains("Data: failed: Unreachable: Couldn't reach tarkov.dev, and there's no saved copy yet.", text);
        Assert.DoesNotContain('[', text);
        Assert.True(In(UiLanguage.Pseudo, () => PseudoText.IsPseudo(snapshot.DataProblem!.What)));
    }

    // A notice that names a language names it in the language in use, as its sentence needs it (docs/LANGUAGES.md,
    // "Adding a language"): in German "Keine französischen Texte", never the English name or tarkov.dev's code; a code
    // the texts don't know shows as it is.
    [Fact]
    public void The_language_notices_name_the_language_in_the_language_in_use()
    {
        Assert.Equal("Keine französischen Texte auf tarkov.dev; Anzeige auf Englisch.", In(UiLanguage.German, () => SessionTexts.LanguageMissing(language: "fr")));
        Assert.Equal("Deutsche Texte geladen.", In(UiLanguage.German, () => SessionTexts.LanguageLoaded(language: "de")));
        var refused = LoadProblem.Explain(new HttpRequestException("403", null, System.Net.HttpStatusCode.Forbidden));
        Assert.StartsWith("Anzeige auf Englisch: Die russischen Texte konnten nicht geladen werden. ",
            In(UiLanguage.German, () => GameSession.LanguageNotice("ru", refused, TimeSpan.FromMinutes(2))));
        foreach (var code in new[] { "cs", "de", "en", "es", "fr", "hu", "it", "ja", "ko", "pl", "pt", "ro", "ru", "sk", "tr", "zh" })
            Assert.Matches(@"^Keine [a-zäöü]+schen Texte auf tarkov\.dev; Anzeige auf Englisch\.$", In(UiLanguage.German, () => SessionTexts.LanguageMissing(language: code)));
        Assert.Equal("Keine Texte in xx auf tarkov.dev; Anzeige auf Englisch.", In(UiLanguage.German, () => SessionTexts.LanguageMissing(language: "xx")));
    }

    // Dates in the Session's words are written as the language in use writes them (UiLanguage.DayMonth): German
    // "4. Okt.", where "d MMM" gave "4 Okt." (review of 2026-10-10). The German texts aren't written yet, so the
    // sentences around them are English here.
    [Fact]
    public void Dates_are_written_as_the_language_in_use_writes_them()
    {
        var at = new DateTime(2026, 1, 1, 14, 5, 0);
        Assert.Equal("Done · ticked by you, 1 Jan", QuestCards.TickedText(DateOnly.FromDateTime(at)));
        Assert.EndsWith(", 1. Jan.", In(UiLanguage.German, () => QuestCards.TickedText(DateOnly.FromDateTime(at))));
        Assert.EndsWith(" 1 Jan 14:05", new ModeReading(at).Tooltip(true, at.AddDays(2)));
        Assert.EndsWith(" 1. Jan., 14:05", In(UiLanguage.German, () => new ModeReading(at).Tooltip(true, at.AddDays(2))));
        Assert.EndsWith(" 14:05", In(UiLanguage.German, () => new ModeReading(at).Tooltip(true, at.AddHours(1))));
    }

    // The raid card drops "… on Customs" while on Customs and keeps "(optional)", made from the objective's parts in the
    // language in use (ObjectiveView.TextOn). Until 2026-10-10 the app cut the English " (optional)" off the Session's
    // words, which another language writes otherwise.
    [Fact]
    public void The_raid_cards_line_is_made_from_the_objectives_parts()
    {
        var view = new ObjectiveView("q", "Quest", "Prapor", "o", "Find the stash on Customs (optional)", false, true, null, null, null,
            ObjectiveKind.Exploration, null) { Description = "Find the stash on Customs", Optional = true };
        Assert.Equal("Find the stash (optional)", view.TextOn("Customs"));
        Assert.Equal("Find the stash on Customs (optional)", view.TextOn("Woods"));
        Assert.Equal("Find the stash on Customs", (view with { Optional = false }).TextOn(null));
        Assert.Equal("Find it (optional)", (view with { Description = null, Text = "Find it (optional)" }).TextOn("Customs"));
        var pseudo = In(UiLanguage.Pseudo, () => view.TextOn("Customs"));
        Assert.True(PseudoText.IsPseudo(pseudo), pseudo);
        Assert.Contains("Find the stash", pseudo);
        Assert.DoesNotContain("Customs", pseudo);
    }

    // The synopsis rules read English texts (docs/DESIGN.md §5, "Quest synopsis"): in German game data a Plan row shows
    // the quest's name alone, and the raid card an objective's own sentence, tarkov.dev's, with no short phrase for it.
    [Fact]
    public void In_german_data_the_english_only_synopses_are_left_out()
    {
        var english = TickTests.Data(Shturmap.Core.Logs.GameMode.Pve);
        var german = new GameData
        {
            Mode = english.Mode,
            Language = "de",
            Maps = english.Maps,
            Tasks = english.Tasks,
            Traders = english.Traders,
            ItemNames = english.ItemNames,
            MapDefinitions = english.MapDefinitions,
            CheckedAt = english.CheckedAt,
        };
        string[] active = [.. english.Tasks.Keys];
        string Synopses(GameData data) => string.Join(" | ", Planning.Suggest(data, active).SelectMany(p => p.Finish.Concat(p.Progress)).Select(q => q.Synopsis));
        Assert.Contains("Mark", Synopses(english));
        Assert.Equal("", Synopses(german).Replace(" | ", "", StringComparison.Ordinal));
        var objectives = Shturmap.Map.MapContentBuilder.Build(english, "map-customs", active, new HashSet<string>()).Objectives;
        Assert.NotEmpty(Planning.ObjectiveSynopses(english, objectives, new HashSet<string> { "map-customs" }));
        Assert.Empty(Planning.ObjectiveSynopses(german, objectives, new HashSet<string> { "map-customs" }));
    }
}
