using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

// A quest lights its doors; a door lights the quests it is for as well (the review of 2026-10-04, E5).
public class LinkDoorTests
{
    // Per quest, the key groups it needs on the shown map, as the map's content gives them.
    private static readonly Dictionary<string, IReadOnlyList<string>> QuestKeys = new()
    {
        ["ballet"] = ["key:skybridge"],
        ["swag"] = ["key:cabin", "key:skybridge"],
        ["audit"] = ["key:office"],
    };

    [Fact]
    public void A_door_is_for_every_quest_that_needs_its_key_here()
    {
        Assert.Equal(new[] { "ballet", "swag" }, LinkDoor.Quests("key:skybridge", QuestKeys).Order().ToArray());
        Assert.Equal(new[] { "audit" }, LinkDoor.Quests("key:office", QuestKeys).ToArray());
    }

    [Fact]
    public void A_door_no_active_quest_needs_is_for_none()
    {
        Assert.Empty(LinkDoor.Quests("key:dorm", QuestKeys));
        Assert.Empty(LinkDoor.Quests(null, QuestKeys));
        Assert.Empty(LinkDoor.Quests("key:office", null));
    }
}
