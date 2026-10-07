using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

// The QUEST COMPLETE cue's words (owner, 2026-10-07): one quest with its trader and what it unlocks; several that came
// together as one cue.
public class CompletionWordsTests
{
    private static CompletionWords.Done Done(string name, params string[] unlocks) => new(name, "Prapor", unlocks);

    [Fact]
    public void One_quest_says_its_trader_and_what_it_unlocks()
    {
        Assert.Equal(("QUEST COMPLETE · PRAPOR", "Gratitude", "Unlocks Setup and Shooter Born in Heaven"),
            CompletionWords.Of([Done("Gratitude", "Setup", "Shooter Born in Heaven")]));
        Assert.Equal(("QUEST COMPLETE · PRAPOR", "Dead End", ""), CompletionWords.Of([Done("Dead End")]));
        Assert.Equal("QUEST COMPLETE", CompletionWords.Of([new CompletionWords.Done("No trader", "", [])]).Eyebrow);
    }

    [Fact]
    public void Several_say_how_many_their_names_and_what_they_unlock_together()
    {
        var (eyebrow, title, detail) = CompletionWords.Of([Done("Gratitude", "Setup"), Done("Fishing Gear", "Setup", "Tigr Safari"), Done("Bad Rep Evidence")]);
        Assert.Equal("QUESTS COMPLETE", eyebrow);
        Assert.Equal("3 quests", title);
        Assert.Equal("Gratitude, Fishing Gear and Bad Rep Evidence\nUnlocks Setup and Tigr Safari", detail);
    }

    [Fact]
    public void A_long_list_ends_in_how_many_more()
    {
        var (_, title, detail) = CompletionWords.Of([Done("A", "U1", "U2"), Done("B", "U3", "U4"), Done("C"), Done("D"), Done("E")]);
        Assert.Equal("5 quests", title);
        Assert.Equal("A, B, C and 2 more\nUnlocks U1, U2, U3 and 1 more", detail);
    }
}
