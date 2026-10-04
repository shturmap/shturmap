using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

/// <summary>
/// A held card stays while the pointer is on or near the card or what it was opened from (docs/DESIGN.md §4, "Quest
/// cards": "more than 240 px away from it"). Measured from the card alone, a click on the left of a rail row held a
/// card that closed with the next move of the mouse (the review of 2026-10-04).
/// </summary>
public class CardReachTests
{
    // A rail row, and the card that opens beside it, as the window lays them out.
    private static readonly CardReach.Box Row = new(25, 300, 358, 340);
    private static readonly CardReach.Box Card = new(366, 290, 726, 600);

    [Theory]
    [InlineData(30, 320)]   // the row's glyph, 336 px left of the card
    [InlineData(60, 320)]   // the start of the quest's name
    [InlineData(350, 320)]  // the row's right end
    [InlineData(500, 400)]  // on the card
    [InlineData(800, 400)]  // beside the card
    [InlineData(100, 500)]  // under the row, in the rail
    public void On_or_near_the_card_or_its_row_it_stays(double x, double y) =>
        Assert.False(CardReach.Away(x, y, Card, Row));

    [Theory]
    [InlineData(1100, 400)] // out on the map
    [InlineData(100, 700)]  // far down the rail
    [InlineData(500, 20)]   // up in the status bar
    public void Well_away_from_both_it_closes(double x, double y) =>
        Assert.True(CardReach.Away(x, y, Card, Row));

    [Fact]
    public void A_card_opened_from_a_marker_counts_from_the_marker_too()
    {
        // The card couldn't open beside its marker at the window's edge and stands 300 px to its left.
        var marker = new CardReach.Box(1500, 500, 1516, 516);
        var card = new CardReach.Box(840, 490, 1200, 800);
        Assert.False(CardReach.Away(1510, 510, card, marker));
        Assert.True(CardReach.Away(1510, 100, card, marker));
    }

    [Fact]
    public void The_distance_is_to_the_nearest_edge_and_nothing_inside()
    {
        Assert.Equal(0, CardReach.Distance(500, 400, Card));
        Assert.Equal(66, CardReach.Distance(300, 400, Card));
        Assert.Equal(5, CardReach.Distance(730, 603, Card), 3);
    }
}
