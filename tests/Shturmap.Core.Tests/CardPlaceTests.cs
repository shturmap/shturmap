using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

/// <summary>
/// A card stands beside its row and inside the room, its whole height with 8 DIP to spare (docs/DESIGN.md §4, "Quest
/// cards"; owner, 2026-10-15: "When selecting a quest at the lower border of the screen it gets cut off"): a card from a
/// row near the foot opens moved up as far as it needs, never down and never above the top; a card that grows moves up
/// by the same rule; the room is the window, less what of it is off the screen.
/// </summary>
public class CardPlaceTests
{
    // The window of the smallest size, 884 × 521 DIP inside its frame, and of 1600 × 900, 1584 × 861.
    private static readonly CardPlace.Span Small = new(0, 521);
    private static readonly CardPlace.Span Large = new(0, 861);

    [Fact]
    public void Beside_a_row_with_room_below_its_top_stands_a_little_above_the_rows()
    {
        Assert.Equal(290, CardPlace.Top(300, 273, Large));
        Assert.Equal(8, CardPlace.Top(12, 273, Large));
    }

    // Insomnia's card from its row at the foot of Streets' card in the rail (the developer script's "show insomnia at
    // row"): 273 tall held, 495 with five notes.
    [Theory]
    [InlineData(770, 273, 861, 580)]
    [InlineData(770, 495, 861, 358)]
    [InlineData(430, 273, 521, 240)]
    [InlineData(430, 495, 521, 18)]
    public void From_a_row_near_the_foot_it_opens_moved_up_as_far_as_its_whole_height_needs(double rowTop, double height, double windowHeight, double top)
    {
        var room = new CardPlace.Span(0, windowHeight);
        Assert.Equal(top, CardPlace.Top(rowTop, height, room));
        Assert.Equal(windowHeight - CardPlace.Inset, CardPlace.Top(rowTop, height, room) + height);
    }

    [Fact]
    public void A_card_as_tall_as_the_room_lets_it_be_stands_at_the_top_and_never_above_it()
    {
        var height = CardPlace.MaxHeight(Small);
        Assert.Equal(505, height);
        Assert.Equal(8, CardPlace.Top(430, height, Small));
        // Taller than that it can't be (it scrolls inside), but a card measured taller still never goes above the top.
        Assert.Equal(8, CardPlace.Top(430, 900, Small));
        Assert.Equal(CardPlace.MinHeight, CardPlace.MaxHeight(new CardPlace.Span(0, 100)));
    }

    [Fact]
    public void A_card_that_grows_moves_up_as_far_as_it_needs_and_never_down()
    {
        // Opened at 646 with 207 DIP (its hover look), held it is 273: up by 66, its foot 8 DIP above the window's.
        Assert.Equal(580, CardPlace.Kept(646, 273, Large));
        // Room to spare: it stays; smaller again: it stays too.
        Assert.Equal(300, CardPlace.Kept(300, 273, Large));
        Assert.Equal(580, CardPlace.Kept(580, 120, Large));
        // Grown to the room's height: at the top, no further.
        Assert.Equal(8, CardPlace.Kept(240, 900, Small));
    }

    [Fact]
    public void Opening_and_growing_keep_to_the_same_room()
    {
        for (var rowTop = 0.0; rowTop <= 900; rowTop += 37)
        {
            foreach (var height in new[] { 120.0, 273, 495, 853 })
            {
                var top = CardPlace.Top(rowTop, height, Large);
                Assert.Equal(top, CardPlace.Kept(top, height, Large));
                Assert.True(top >= Large.Top + CardPlace.Inset);
                Assert.True(top + height <= Large.Bottom - CardPlace.Inset || top == Large.Top + CardPlace.Inset);
            }
        }
    }

    // The window at 100 %, its client area from y 40 to 1080 on a 1080-pixel screen whose taskbar takes the last 48:
    // the cards keep to the 992 DIP above the taskbar, so a card from the lowest row in sight is not cut by it.
    [Fact]
    public void A_window_reaching_behind_the_taskbar_keeps_its_cards_above_it()
    {
        var room = CardPlace.Room(1040, 40, 0, 1032, 1);
        Assert.Equal(new CardPlace.Span(0, 992), room);
        Assert.Equal(992 - 8 - 273, CardPlace.Top(950, 273, room));
        // At 150 %: pixels become DIPs.
        Assert.Equal(new CardPlace.Span(0, 400), CardPlace.Room(693, 400, 0, 1000, 1.5));
        // Its top above the screen's: the cards start where it is in sight.
        Assert.Equal(new CardPlace.Span(30, 521), CardPlace.Room(521, -30, 0, 1032, 1));
    }

    [Fact]
    public void A_window_on_no_screen_or_hardly_on_one_keeps_its_whole_height()
    {
        // Off every monitor (a picture-taking run's): nothing of it is on the work area.
        Assert.Equal(new CardPlace.Span(0, 521), CardPlace.Room(521, 3000, 0, 1032, 1));
        // A strip of it in sight, too little for a card.
        Assert.Equal(new CardPlace.Span(0, 521), CardPlace.Room(521, 950, 0, 1032, 1));
        // Wholly on the screen: the window.
        Assert.Equal(new CardPlace.Span(0, 861), CardPlace.Room(861, 100, 0, 1032, 1));
    }
}
