using Shturmap.App.Rules;
using Box = Shturmap.App.Rules.TourFrames.Box;

namespace Shturmap.Core.Tests;

// The tour's cut-outs between chapters (owner, 2026-10-09: "sometimes they transition into nothingness, like from
// section 3 to section 4"; docs/DESIGN.md §4, *The tour*).
public class TourFramesTests
{
    private static readonly Box Rows = new(10, 120, 360, 150);
    private static readonly Box Map = new(384, 40, 1200, 820);
    private static readonly Box Buttons = new(1470, 4, 104, 32);

    [Fact]
    public void A_part_left_over_glides_into_the_nearest_new_part_and_none_shrinks_into_a_point()
    {
        // Chapter 3 (Plan's rows and the map) to chapter 4 (the map).
        var pairs = TourFrames.Pairs([Rows, Map], [Map]);
        Assert.Equal(2, pairs.Count);
        Assert.All(pairs, p => Assert.Equal(Map, p.To));
        Assert.Contains((Map, Map), pairs);
    }

    [Fact]
    public void A_new_part_left_over_grows_out_of_the_nearest_drawn_one()
    {
        // Back from chapter 4 to chapter 3.
        var pairs = TourFrames.Pairs([Map], [Rows, Map]);
        Assert.Equal(2, pairs.Count);
        Assert.All(pairs, p => Assert.Equal(Map, p.From));
        Assert.Contains((Map, Rows), pairs);
    }

    [Fact]
    public void Each_new_part_takes_the_nearest_drawn_one()
    {
        var pairs = TourFrames.Pairs([Buttons, Rows], [Rows, Buttons]);
        Assert.Contains((Rows, Rows), pairs);
        Assert.Contains((Buttons, Buttons), pairs);
    }

    [Fact]
    public void Without_parts_on_one_side_there_is_nothing_to_pair()
    {
        // The window shows those as a fade.
        Assert.Throws<ArgumentException>(() => TourFrames.Pairs([], [Map]));
        Assert.Throws<ArgumentException>(() => TourFrames.Pairs([Map], []));
    }

    [Fact]
    public void The_union_has_no_overlap_and_the_union_s_area()
    {
        // Two cut-outs crossing mid-glide, and one apart.
        Box a = new(0, 0, 100, 100), b = new(50, 50, 100, 100), c = new(300, 0, 10, 10);
        var pieces = TourFrames.Disjoint([a, b, c]);
        Assert.Equal(100 * 100 + 100 * 100 - 50 * 50 + 10 * 10, pieces.Sum(p => p.Area), 6);
        for (var i = 0; i < pieces.Count; i++)
            for (var j = i + 1; j < pieces.Count; j++)
                Assert.False(Overlap(pieces[i], pieces[j]), $"{pieces[i]} and {pieces[j]} overlap");
        // Every corner of the inputs is inside the union.
        Assert.All(new[] { a, b, c }, box => Assert.Contains(pieces, p => p.X <= box.X + 1 && p.Y <= box.Y + 1 && p.Right >= box.X + 1 && p.Bottom >= box.Y + 1));
    }

    [Fact]
    public void One_box_or_two_alike_stay_one_piece()
    {
        Assert.Equal([Map], TourFrames.Disjoint([Map]));
        Assert.Equal([Map], TourFrames.Disjoint([Map, Map]));
        Assert.Empty(TourFrames.Disjoint([new Box(5, 5, 0, 10)]));
    }

    [Fact]
    public void Parts_that_touch_are_one_cut_out()
    {
        // The three buttons with the room the tour leaves around each.
        Box feedback = new(1470, 4, 36, 32), help = new(1504, 4, 36, 32), settings = new(1538, 4, 36, 32);
        Assert.Equal([new Box(1470, 4, 104, 32)], TourFrames.Merged([feedback, help, settings]));
        Assert.Equal(2, TourFrames.Merged([Rows, Map]).Count);
    }

    private static bool Overlap(Box a, Box b) => a.X < b.Right - 1e-9 && b.X < a.Right - 1e-9 && a.Y < b.Bottom - 1e-9 && b.Y < a.Bottom - 1e-9;
}
