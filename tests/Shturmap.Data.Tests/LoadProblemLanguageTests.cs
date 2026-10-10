using System.Net;
using System.Text.Json;
using Shturmap.Core;
using Shturmap.Core.Text;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Data.Tests;

/// <summary>Tests that change the language in use (a single value for the whole app) run alone, after the others.</summary>
[CollectionDefinition("UiLanguage", DisableParallelization = true)]
public sealed class UiLanguageCollection;

// A load problem is kept while Shturmap waits to ask again (the status line says it), and the language can change
// meanwhile (docs/DESIGN.md §8, "Texts"): its words are looked up when they are read, not when the failure was
// explained. Until 2026-10-10 they were written once, and the page-in-place-of-the-data problem was one instance for
// the whole run.
[Collection("UiLanguage")]
public class LoadProblemLanguageTests
{
    [Fact]
    public void A_problem_kept_across_a_language_change_reads_in_the_new_language()
    {
        var busy = LoadProblem.Explain(new HttpRequestException("x", null, HttpStatusCode.ServiceUnavailable));
        var page = LoadProblem.Explain(new NotDataException(["regular_tasks"], new JsonException("x")));
        try
        {
            UiLanguage.Set(UiLanguage.Pseudo);
            Assert.True(PseudoText.IsPseudo(busy.What), busy.What);
            Assert.Contains("503", busy.What, StringComparison.Ordinal);
            Assert.True(PseudoText.IsPseudo(page.What) && PseudoText.IsPseudo(page.Advice), page.Text);
            Assert.True(PseudoText.IsPseudo(RetrySchedule.InWords(TimeSpan.FromMinutes(2))));
        }
        finally
        {
            UiLanguage.Set(UiLanguage.English);
        }
        Assert.Equal("tarkov.dev answered 503. It is busy or down for a moment; try again in a few minutes.", busy.Text);
        Assert.Equal("tarkov.dev's answer wasn't its data: a sign-in page or a filter in between? Shturmap tries again in a few minutes.", page.Text);
    }

    // The app log and diagnostics stay English, whatever the language in use: they are read by whoever fixes Shturmap
    // (docs/DESIGN.md §8, "The app's own language"). Until 2026-10-10 diagnostics printed What, in the language in use.
    [Fact]
    public void The_english_words_for_the_log_are_the_english_texts_in_any_language()
    {
        var problems = Enum.GetValues<LoadFailure>().Select(kind => new LoadProblem(kind, kind is LoadFailure.ServerBusy or LoadFailure.Refused ? 503 : null)).ToList();
        var english = problems.Select(p => p.What).ToList();
        Assert.Equal(english, problems.Select(p => p.InEnglish));
        Assert.Equal("tarkov.dev answered 503.", problems.Single(p => p.Kind == LoadFailure.ServerBusy).InEnglish);
        try
        {
            UiLanguage.Set(UiLanguage.Pseudo);
            Assert.All(problems, p => Assert.True(PseudoText.IsPseudo(p.What), p.What));
            Assert.Equal(english, problems.Select(p => p.InEnglish));
            Assert.Equal("2 minutes", RetrySchedule.InEnglish(TimeSpan.FromMinutes(2)));
            Assert.Equal("1 minute", RetrySchedule.InEnglish(TimeSpan.FromMinutes(1)));
            Assert.Equal("30 seconds", RetrySchedule.InEnglish(TimeSpan.FromSeconds(30)));
        }
        finally
        {
            UiLanguage.Set(UiLanguage.English);
        }
    }
}
