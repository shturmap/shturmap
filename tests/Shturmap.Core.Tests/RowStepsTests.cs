using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

// The keyboard's way through the rail's rows (the review of 2026-10-04, E6): in the order they stand on screen, no
// wrapping, and a row is found again after the rows were made anew.
public class RowStepsTests
{
    private static RowBox Row(double top, double height = 30, double left = 0) => new(top, left, height);

    // A raid's rail: NEXT, EXIT, a quest's block with two lines in it, a BRING row.
    private static readonly RowBox[] Raid = [Row(0, 60), Row(60, 60), Row(140, 110), Row(165, 40), Row(205, 40), Row(270, 50)];

    [Fact]
    public void Rows_go_top_to_bottom_and_a_block_comes_before_its_first_line()
    {
        // Given out of order, and a block that starts where its first line does.
        RowBox[] rows = [Row(100, 30), Row(0, 90), Row(0, 30), Row(40, 30, left: 10), Row(40, 30)];
        Assert.Equal(new[] { 1, 2, 4, 3, 0 }, RowSteps.Order(rows));
    }

    [Fact]
    public void Down_and_up_step_one_row_and_stop_at_the_ends()
    {
        Assert.Equal(3, RowSteps.Step(Raid, 2, down: true, 0, 400));
        Assert.Equal(1, RowSteps.Step(Raid, 2, down: false, 0, 400));
        // No wrapping: the last row stays the last, the first the first.
        Assert.Equal(5, RowSteps.Step(Raid, 5, down: true, 0, 400));
        Assert.Equal(0, RowSteps.Step(Raid, 0, down: false, 0, 400));
    }

    [Fact]
    public void The_first_step_starts_in_the_part_of_the_rail_in_view()
    {
        // Nothing scrolled: the first row, and from below the last one in view.
        Assert.Equal(0, RowSteps.Step(Raid, -1, down: true, 0, 200));
        Assert.Equal(3, RowSteps.Step(Raid, -1, down: false, 0, 200));
        // Scrolled past NEXT and EXIT: Down starts at the first row that shows, the quest's block.
        Assert.Equal(2, RowSteps.Step(Raid, -1, down: true, 130, 330));
        Assert.Equal(5, RowSteps.Step(Raid, -1, down: false, 130, 330));
        // A row half scrolled away still counts as in view.
        Assert.Equal(1, RowSteps.Step(Raid, -1, down: true, 100, 300));
    }

    [Fact]
    public void With_no_row_in_view_the_first_step_takes_the_nearest_end()
    {
        Assert.Equal(0, RowSteps.Step(Raid, -1, down: true, 1000, 1200));
        Assert.Equal(5, RowSteps.Step(Raid, -1, down: false, 1000, 1200));
    }

    [Fact]
    public void No_rows_no_step()
    {
        Assert.Equal(-1, RowSteps.Step([], -1, down: true, 0, 100));
        Assert.Equal(-1, RowSteps.Step([], 3, down: false, 0, 100));
    }

    [Fact]
    public void A_row_is_found_again_by_what_it_shows()
    {
        var quest = RowId.Linked("quest-a", null, null, null);
        var line = RowId.Linked("quest-a", "objective-1", null, null);
        var marker = RowId.Linked(null, null, "item-1", null);
        RowId[] rows = [RowId.Map("customs"), quest, line, marker];
        Assert.Equal(1, RowSteps.Find(rows, quest, 0));
        Assert.Equal(2, RowSteps.Find(rows, line, 0));
        Assert.Equal(0, RowSteps.Find(rows, RowId.Map("customs"), 0));
        // Gone: the quest was completed.
        Assert.Equal(-1, RowSteps.Find(rows, RowId.Linked("quest-b", null, null, null), 0));
        Assert.Equal(-1, RowSteps.Find(rows, RowId.Map("woods"), 0));
    }

    [Fact]
    public void Of_rows_that_show_the_same_thing_the_one_in_the_same_place_among_them_is_found()
    {
        // The same item in CHECK YOUR KIT and under ALSO USEFUL.
        var item = RowId.Linked(null, null, "item-1", null);
        RowId[] rows = [item, RowId.Linked("quest-a", null, null, null), item];
        Assert.Equal(0, RowSteps.NthOf(rows, 0));
        Assert.Equal(1, RowSteps.NthOf(rows, 2));
        Assert.Equal(2, RowSteps.Find(rows, item, 1));
        // One of the two went: the keyboard stays on the one that is left.
        RowId[] fewer = [RowId.Linked("quest-a", null, null, null), item];
        Assert.Equal(1, RowSteps.Find(fewer, item, 1));
    }
}
