using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

/// <summary>
/// A card opens beside its row, and the way to it leads across things that open cards of their own: the other
/// quest glyphs of a map's row, the rows below. While the pointer heads for the card those wait (docs/DESIGN.md §4,
/// "Quest cards"; owner, 2026-10-04: "if you are too slow another quest opens").
/// </summary>
public class CardAimTests
{
    // A map's row in Plan's list, and the card that opened from a glyph at its left end.
    private static readonly CardReach.Box Card = new(366, 290, 726, 600);

    [Theory]
    [InlineData(40, 320, 70, 321)]     // along the row, to the right
    [InlineData(40, 320, 70, 345)]     // down and to the right, to the card's middle
    [InlineData(300, 320, 330, 300)]   // up and to the right, to its top corner
    [InlineData(340, 320, 362, 320)]   // the last pixels before it
    [InlineData(360, 320, 380, 325)]   // onto it
    public void Moving_to_the_card_is_heading_for_it(double fromX, double fromY, double toX, double toY) =>
        Assert.True(CardAim.Toward(fromX, fromY, toX, toY, Card));

    [Theory]
    [InlineData(200, 320, 200, 360)]   // down the list
    [InlineData(200, 320, 200, 280)]   // up the list
    [InlineData(200, 320, 170, 320)]   // away, to the left
    [InlineData(40, 320, 50, 350)]     // steeply down: it would pass under the card
    [InlineData(40, 320, 70, 290)]     // steeply up: it would pass over the card
    [InlineData(100, 700, 130, 715)]   // to the right, below the card, and falling away from it
    public void Moving_elsewhere_is_not(double fromX, double fromY, double toX, double toY) =>
        Assert.False(CardAim.Toward(fromX, fromY, toX, toY, Card));

    [Fact]
    public void A_pointer_that_rests_is_heading_nowhere()
    {
        Assert.False(CardAim.Toward(100, 320, 100, 320, Card));
        Assert.False(CardAim.Toward(100, 320, 102, 320, Card));
    }

    [Fact]
    public void A_card_on_the_left_is_reached_by_moving_left()
    {
        // No room on the right: the card opened to the left of what it came from (a marker on the map).
        var card = new CardReach.Box(840, 490, 1200, 800);
        Assert.True(CardAim.Toward(1500, 510, 1470, 515, card));
        Assert.False(CardAim.Toward(1500, 510, 1530, 515, card));
    }

    [Fact]
    public void A_path_that_just_misses_a_corner_still_counts()
    {
        // 10 px over the top edge where it reaches the card: within the slack.
        Assert.True(CardAim.Toward(40, 280, 70, 280, Card));
        // 40 px over it: past the slack.
        Assert.False(CardAim.Toward(40, 250, 70, 250, Card));
    }
}
