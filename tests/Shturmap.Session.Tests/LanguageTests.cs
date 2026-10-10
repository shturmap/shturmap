using Shturmap.Core;
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
}
