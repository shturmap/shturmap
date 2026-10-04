using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

/// <summary>
/// The linked highlight has two strengths (owner, 2026-10-04): the thing pointed at, here and elsewhere, keeps the
/// full tint; what only belongs to it gets a weaker one; and an item that merely shares a quest with the item
/// pointed at is not lit at all.
/// </summary>
public class LinkStrengthTests
{
    private static readonly string[] None = [];

    private static HashSet<string> Quests(params string[] ids) => [.. ids];

    // Rows: a quest's row, a BRING row (an item, for some quests), an objective's line, a way out.
    private static LinkLevel QuestRow(string quest, HashSet<string> focusQuests, string? focusItem = null, string? focusObjective = null) =>
        LinkStrength.Of(quest, None, null, null, null, focusQuests, focusItem, null, focusObjective);

    private static LinkLevel BringRow(string item, string[] serves, HashSet<string> focusQuests, string? focusItem = null) =>
        LinkStrength.Of(null, serves, item, null, null, focusQuests, focusItem, null, null);

    [Fact]
    public void Pointing_at_a_quest_lights_its_rows_fully_and_what_it_needs_weakly()
    {
        var focus = Quests("revision");
        Assert.Equal(LinkLevel.Same, QuestRow("revision", focus));
        Assert.Equal(LinkLevel.Related, BringRow("marker", ["revision"], focus));
        Assert.Equal(LinkLevel.None, QuestRow("dandies", focus));
        Assert.Equal(LinkLevel.None, BringRow("beanie", ["dandies"], focus));
    }

    [Fact]
    public void Pointing_at_an_item_lights_the_item_fully_its_quests_weakly_and_no_other_item()
    {
        // The beanie's BRING row is under the pointer: the focus is the item and the quests it is for.
        var focus = Quests("dandies");
        Assert.Equal(LinkLevel.Same, BringRow("beanie", ["dandies"], focus, focusItem: "beanie"));
        Assert.Equal(LinkLevel.Related, QuestRow("dandies", focus, focusItem: "beanie"));
        // The sunglasses are for the same quest: they used to light up with the beanie.
        Assert.Equal(LinkLevel.None, BringRow("sunglasses", ["dandies"], focus, focusItem: "beanie"));
        Assert.Equal(LinkLevel.None, QuestRow("revision", focus, focusItem: "beanie"));
    }

    [Fact]
    public void An_objective_and_a_way_out_are_lit_fully_only_for_themselves()
    {
        var focus = Quests("revision");
        // An objective's line names no quest of its own: the block around it carries the quest's tint.
        Assert.Equal(LinkLevel.Same, LinkStrength.Of(null, None, null, null, "mark-stryker", focus, null, null, "mark-stryker"));
        Assert.Equal(LinkLevel.None, LinkStrength.Of(null, None, null, null, "mark-lav", focus, null, null, "mark-stryker"));
        Assert.Equal(LinkLevel.Same, LinkStrength.Of(null, None, null, "extract:taxi", null, Quests(), null, "extract:taxi", null));
        Assert.Equal(LinkLevel.None, LinkStrength.Of(null, None, null, "extract:sewer", null, Quests(), null, "extract:taxi", null));
    }

    [Fact]
    public void A_row_that_is_both_a_quest_and_an_objective_is_lit_for_either()
    {
        // The glance's NEXT row: the nearest objective and its quest.
        Assert.Equal(LinkLevel.Same, LinkStrength.Of("revision", None, null, null, "mark-stryker", Quests("revision"), null, null, null));
        Assert.Equal(LinkLevel.Same, LinkStrength.Of("revision", None, null, null, "mark-stryker", Quests("other"), null, null, "mark-stryker"));
    }
}
