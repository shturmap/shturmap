using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

// The raid card says a need once under the quest's name when every line would repeat it (owner, 2026-10-04).
public class RaidLinesTests
{
    private static string Shared(params (string Needs, bool Done)[] lines) => RaidLines.SharedNeed(lines);

    [Fact]
    public void A_need_every_line_repeats_is_said_once() =>
        Assert.Equal("Bring: marker", Shared(("Bring: marker", false), ("Bring: marker", false), ("Bring: marker", false)));

    [Fact]
    public void A_line_that_needs_nothing_does_not_count_either_way() =>
        Assert.Equal("Key: door", Shared(("Key: door", false), ("", false), ("Key: door", false)));

    [Fact]
    public void Needs_that_differ_stay_on_their_lines() =>
        Assert.Equal("", Shared(("Key: door", false), ("Key: gate", false), ("Key: door", false)));

    [Fact]
    public void One_line_keeps_its_need() =>
        Assert.Equal("", Shared(("Key: door", false), ("", false)));

    [Fact]
    public void A_ticked_line_needs_nothing_any_more()
    {
        Assert.Equal("", Shared(("Bring: marker", false), ("Bring: marker", true)));
        Assert.Equal("Bring: marker", Shared(("Bring: marker", false), ("Bring: marker", false), ("Key: door", true)));
    }
}
