using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

// The raid card says an objective is optional in words, as the quest card does (owner, 2026-10-03).
public class ObjectiveTextTests
{
    private static ApiObjective Objective(string? description, bool optional) =>
        new("o", "visit", description, optional, null, null, null, null, null, null, null, null, null, false);

    [Fact]
    public void An_optional_objective_says_so() =>
        Assert.Equal("Locate the place (optional)", GameSession.ObjectiveText(Objective("Locate the place", true)));

    [Fact]
    public void A_required_objective_is_its_description() =>
        Assert.Equal("Locate the place", GameSession.ObjectiveText(Objective("Locate the place", false)));

    [Fact]
    public void No_description_still_says_optional() =>
        Assert.Equal("(no description) (optional)", GameSession.ObjectiveText(Objective(" ", true)));
}
