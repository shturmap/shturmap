using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

// What pointing at a linked element puts in focus (the review of 2026-10-04, E3 and E4).
public class LinkFocusTests
{
    [Fact]
    public void A_bring_row_is_its_item_and_the_quests_it_is_for()
    {
        var focus = LinkFocus.Of(null, ["ballet"], null, "key-a", null, null, null, null);
        Assert.Equal("key-a", focus.Item);
        Assert.Equal(new[] { "ballet" }, focus.Quests.ToArray());
        Assert.Empty(focus.Alternatives);
    }

    [Fact]
    public void An_item_cards_body_keeps_the_item_and_its_quests_in_focus()
    {
        // The body names the item as "also item" and the quests it is for as "also": neither lights the body itself
        // (LinkStrength is never told of them), both stay in focus while the card is read.
        var focus = LinkFocus.Of(null, null, ["ballet", "swag"], null, "key-a", null, null, null);
        Assert.Equal("key-a", focus.Item);
        Assert.Equal(new[] { "ballet", "swag" }, focus.Quests.Order().ToArray());
        var body = LinkStrength.Of(null, [], null, null, null, focus.Quests, focus.Item, focus.Marker, focus.Objective, null, focus.Alternatives);
        Assert.Equal(LinkLevel.None, body);
    }

    [Fact]
    public void A_quest_cards_body_keeps_its_quest_in_focus_without_being_lit_for_it()
    {
        var focus = LinkFocus.Of(null, null, ["revision"], null, null, null, null, null);
        Assert.Equal(new[] { "revision" }, focus.Quests.ToArray());
        Assert.Null(focus.Item);
        Assert.Equal(LinkLevel.None, LinkStrength.Of(null, [], null, null, null, focus.Quests, null, null, null));
        // The quest's rows elsewhere are.
        Assert.Equal(LinkLevel.Same, LinkStrength.Of("revision", [], null, null, null, focus.Quests, null, null, null));
    }

    [Fact]
    public void An_elements_own_item_comes_before_its_also_item()
    {
        // A row on an item's card that is an item itself is that item.
        Assert.Equal("key-b", LinkFocus.Of(null, null, null, "key-b", "key-a", null, null, null).Item);
    }

    [Fact]
    public void A_row_that_stands_for_several_items_puts_each_in_focus_once()
    {
        var focus = LinkFocus.Of(null, ["ballet"], null, "key-a", null, ["key-a", "key-b", "key-a"], null, null);
        Assert.Equal("key-a", focus.Item);
        Assert.Equal(new[] { "key-a", "key-b" }, focus.Alternatives.ToArray());
    }

    [Fact]
    public void A_line_keeps_its_marker_and_its_objective()
    {
        var focus = LinkFocus.Of("revision", null, null, null, null, null, "extract:taxi", "mark-stryker");
        Assert.Equal(("extract:taxi", "mark-stryker"), (focus.Marker, focus.Objective));
        Assert.Equal(new[] { "revision" }, focus.Quests.ToArray());
    }

    // ---- a set breathes, one thing rings (owner, 2026-10-17, from a panel of four: "B") ----

    [Fact]
    public void A_traders_portrait_a_bosss_name_and_an_item_are_sets()
    {
        // The portrait and the name say their quests (or zones) as several, even one.
        Assert.True(LinkFocus.IsSet(quests: true, null, null, null, null));
        // A need cell, a BRING row, a key's line; an item card's body.
        Assert.True(LinkFocus.IsSet(quests: false, "key-a", null, null, null));
        Assert.True(LinkFocus.IsSet(quests: true, "key-a", null, null, null));
        Assert.True(LinkFocus.IsSet(quests: false, null, "key-a", null, null));
        Assert.True(LinkFocus.IsSet(quests: false, null, null, ["key-a", "key-b"], null));
    }

    [Fact]
    public void A_quest_an_objective_and_a_marker_are_one_thing()
    {
        // A quest's row or card, an extract's row: one thing (Linked.Quest, Linked.Marker name no set).
        Assert.False(LinkFocus.IsSet(quests: false, null, null, null, null));
        // An objective's row on a quest's card stays its objective, also where it names an item: only its places ring.
        Assert.False(LinkFocus.IsSet(quests: false, "dogtag", null, null, "mark-stryker"));
        Assert.False(LinkFocus.IsSet(quests: false, null, null, null, "mark-stryker"));
    }
}
