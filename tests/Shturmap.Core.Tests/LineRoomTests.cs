using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

// An objective's line in the raid card (docs/DESIGN.md §4, "Screen anatomy", *Raid*): its words keep at least half of
// the line: beside a hand-over a long direction tag gets less room, and its parts stand one under the other (review of
// 2026-10-10).
public class LineRoomTests
{
    // The rail's objective line is about 328 DIP; 10 between its parts.
    private const double Line = 328;
    private const double Spacing = 10;

    [Fact]
    public void Where_it_is_takes_what_it_needs_while_the_words_keep_half_the_line()
    {
        // "LEFT · 4 M DOWN", no hand-over; and the longest German tag without one.
        Assert.Null(LineRoom.Where(Line, 0, 90, Spacing));
        Assert.Null(LineRoom.Where(Line, 0, 145, Spacing));
        // "AHEAD-LEFT" beside a hand-over's cell, mark and portrait.
        Assert.Null(LineRoom.Where(Line, 70, 63, Spacing));
    }

    [Fact]
    public void Beside_a_hand_over_a_long_tag_gets_what_leaves_the_words_their_half()
    {
        // "HINTEN LINKS · 5 M TIEFER" (about 135 DIP) beside a hand-over (about 70): the words would keep 103 of 328.
        var room = LineRoom.Where(Line, 70, 135, Spacing);
        Assert.NotNull(room);
        Assert.Equal(Line / 2 - 70 - Spacing - Spacing, room!.Value, 3);
        Assert.True(Line - (70 + Spacing) - (room.Value + Spacing) >= Line / 2 - 0.001);
    }

    [Fact]
    public void Where_it_is_is_never_narrower_than_its_widest_part()
    {
        // A longer language's direction (the pseudo-language's "[BÉHÎÑÐ-ĻÉƑŢ ····]", 87 DIP) beside a wide hand-over.
        Assert.Equal(87, LineRoom.Where(Line, 95, 150, Spacing, least: 87));
        // And never wider than it is on one line.
        Assert.Equal(120, LineRoom.Where(Line, 95, 120, Spacing, least: 130));
    }

    [Fact]
    public void An_unbounded_line_leaves_it_as_it_is()
    {
        Assert.Null(LineRoom.Where(double.PositiveInfinity, 70, 135, Spacing));
    }
}
