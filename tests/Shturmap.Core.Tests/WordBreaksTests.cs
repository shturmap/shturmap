using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

/// <summary>
/// The roll call's rows break a name between its words only, never after a hyphen in one (QA pass, 2026-10-15:
/// "Primorsky Ave Taxi V-" over "Ex" in the pseudo-language at 900×560).
/// </summary>
public class WordBreaksTests
{
    [Theory]
    [InlineData("Primorsky Ave Taxi V-Ex", "Primorsky Ave Taxi V-⁠Ex")]
    [InlineData("ZB-1013", "ZB-⁠1013")]
    [InlineData("Railroad Passage (Flare)", "Railroad Passage (Flare)")]
    [InlineData("RUAF-Straßensperre", "RUAF-⁠Straßensperre")]
    public void A_hyphen_holds_its_word_together(string name, string drawn) =>
        Assert.Equal(drawn, WordBreaks.BetweenWordsOnly(name));

    [Fact]
    public void The_words_and_what_shows_stay_the_same()
    {
        const string name = "Primorsky Ave Taxi V-Ex";
        var drawn = WordBreaks.BetweenWordsOnly(name);
        // Split between words as the row measures its widest word: the same words.
        Assert.Equal(name.Split(' ').Length, drawn.Split(' ').Length);
        // The joiner is all that was added, and it is drawn as nothing.
        Assert.Equal(name, drawn.Replace(WordBreaks.Joiner.ToString(), "", StringComparison.Ordinal));
    }
}
