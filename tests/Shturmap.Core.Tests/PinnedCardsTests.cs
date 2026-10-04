using Shturmap.App.Rules;
using Shturmap.Core.Quests;

namespace Shturmap.Core.Tests;

/// <summary>
/// Popped-out cards are forgotten only when their quest is over or the player closes them: a change of mode closed
/// them all and saved an empty list (the review of 2026-10-04).
/// </summary>
public class PinnedCardsTests
{
    private const string A = "quest-a";
    private const string B = "quest-b";

    [Fact]
    public void The_list_comes_back_as_it_was_saved()
    {
        var saved = PinnedCards.Format([new(A, (1960, 40)), new(B, (-1200, 300))]);
        Assert.Equal($"{A}@1960,40;{B}@-1200,300", saved);
        Assert.Equal([new PinnedCards.Entry(A, (1960, 40)), new PinnedCards.Entry(B, (-1200, 300))], PinnedCards.Parse(saved));
    }

    [Fact]
    public void An_entry_without_a_readable_place_keeps_its_quest()
    {
        Assert.Equal([new PinnedCards.Entry(A, null)], PinnedCards.Parse(A));
        Assert.Equal([new PinnedCards.Entry(A, null)], PinnedCards.Parse($"{A}@left,top"));
        Assert.Equal(A, PinnedCards.Format([new(A, null)]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(";;")]
    [InlineData("@1,2")]
    public void Nothing_saved_is_no_cards(string? setting) => Assert.Empty(PinnedCards.Parse(setting));

    [Fact]
    public void A_quest_is_listed_once() =>
        Assert.Equal([new PinnedCards.Entry(A, (1, 2))], PinnedCards.Parse($"{A}@1,2;{A}@3,4"));

    [Fact]
    public void An_active_quest_shows_its_card() => Assert.Equal(PinnedCards.Fate.Show, PinnedCards.For(QuestState.Active));

    [Theory]
    [InlineData(QuestState.Completed)]
    [InlineData(QuestState.Failed)]
    public void A_finished_quest_s_card_is_forgotten(QuestState state) => Assert.Equal(PinnedCards.Fate.Forget, PinnedCards.For(state));

    [Fact]
    public void A_quest_the_shown_data_doesn_t_have_as_active_waits()
    {
        // The other mode's data after a switch between PvE and PvP: the quest isn't started there, or isn't known.
        Assert.Equal(PinnedCards.Fate.Wait, PinnedCards.For(QuestState.NotStarted));
        Assert.Equal(PinnedCards.Fate.Wait, PinnedCards.For(null));
    }
}
