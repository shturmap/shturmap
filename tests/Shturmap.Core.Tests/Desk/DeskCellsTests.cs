using Shturmap.App.Rules;
using Shturmap.Core.Desk;

namespace Shturmap.Core.Tests.Desk;

// A desk cell's marks and where they stand (owner, 2026-10-19, from a panel: B1, and the addendum: C1a · Da; docs/DESIGN.md
// §4, *The cell*; Rules.DeskCells): the figure top right after its source, the count bottom right, the notch top left,
// the change bottom left, the tint; the arrow giving way where a long condition takes its room. Made-up cells, English.
public class DeskCellsTests
{
    private static DeskCellSheet Cell(int w, int h, DeskFigure? figure, DeskKeepLevel keep = DeskKeepLevel.None, DeskPrint? print = null, int count = 1,
        params string[] ids) =>
        new(new DeskCell(0, 0, w, h, ids.Length > 0 ? ids : ["item"]) { Print = print, Count = count }, figure, keep, figure?.Plus ?? false);

    private static DeskFigure Flea(double value, DeskArrow arrow = DeskArrow.None, double? change = null, int tint = 0) =>
        new(DeskSource.Flea, value, value, null) { Arrow = arrow, Change48h = change, Tint = tint };

    [Fact]
    public void The_figure_stands_after_its_source_with_its_marks()
    {
        var marks = DeskCells.Of(Cell(2, 1, Flea(902_000)), DeskCells.Small);
        Assert.Equal("902k", marks.Figure);
        Assert.Equal(DeskSourceMark.Flea, marks.Source);
        Assert.Equal(10.5, marks.FigureSize);
        // A trader's person at 40 DIP, the trader's portrait at 56.
        var trader = new DeskFigure(DeskSource.Trader, 31_000, 31_000, "therapist") { Estimate = true };
        Assert.Equal(DeskSourceMark.Trader, DeskCells.Of(Cell(2, 2, trader), DeskCells.Small).Source);
        var large = DeskCells.Of(Cell(2, 2, trader), DeskCells.Large);
        Assert.Equal(DeskSourceMark.Portrait, large.Source);
        Assert.Equal("therapist", large.TraderId);
        Assert.Equal("~31k", large.Figure);
        Assert.Equal(12, large.FigureSize);
        // Contents unseen: "+" after the figure; a weapon's too, with its "~".
        Assert.Equal("34k+", DeskCells.Of(Cell(1, 1, Flea(34_000) with { Plus = true }), DeskCells.Small).Figure);
        Assert.Equal("~83k+", DeskCells.Of(Cell(4, 1, Flea(83_000) with { Plus = true, Estimate = true }), DeskCells.Small).Figure);
    }

    [Fact]
    public void A_cell_with_no_price_has_no_figure()
    {
        var marks = DeskCells.Of(Cell(3, 3, null), DeskCells.Small);
        Assert.Null(marks.Figure);
        Assert.Equal(DeskSourceMark.None, marks.Source);
        Assert.Equal(0, marks.Tint);
        Assert.Null(marks.Change);
    }

    [Fact]
    public void A_cell_that_fits_several_items_has_the_plate_its_range_and_half_its_art()
    {
        var range = new DeskFigure(DeskSource.None, 63_000, 307_000, null) { Unsure = true };
        var marks = DeskCells.Of(Cell(2, 1, range, ids: ["a", "b", "c"]), DeskCells.Small);
        Assert.True(marks.Plate);
        Assert.Equal(0.5, marks.ArtOpacity);
        Assert.Equal("63–307k", marks.Figure);
        Assert.Equal(DeskSourceMark.None, marks.Source);
        // One item named: no plate, the art whole.
        var one = DeskCells.Of(Cell(2, 1, Flea(1_000)), DeskCells.Small);
        Assert.False(one.Plate);
        Assert.Equal(1, one.ArtOpacity);
        // A 1×1 "?" with a wide range: its figure set smaller to stay in the cell, not under 8 px.
        var narrow = DeskCells.Of(Cell(1, 1, new DeskFigure(DeskSource.None, 300, 294_000, null) { Unsure = true }, ids: ["a", "b"]), DeskCells.Small);
        Assert.Equal("0.3–294k", narrow.Figure);
        Assert.InRange(narrow.FigureSize, 8, 10);
        // There the plate leaves the range no room beside it: it stands at the lower left, where a "?" has no change.
        Assert.True(narrow.PlateLow);
        Assert.False(marks.PlateLow);
    }

    [Theory]
    [InlineData(DeskKeepLevel.Now, DeskNotch.Now)]
    [InlineData(DeskKeepLevel.Next, DeskNotch.Next)]
    [InlineData(DeskKeepLevel.Later, DeskNotch.Later)]
    [InlineData(DeskKeepLevel.Hideout, DeskNotch.Hideout)]
    [InlineData(DeskKeepLevel.None, DeskNotch.None)]
    public void The_notch_says_the_strongest_need(DeskKeepLevel level, DeskNotch notch)
    {
        Assert.Equal(notch, DeskCells.Of(Cell(2, 1, Flea(50_000), level), DeskCells.Small).Notch);
    }

    // The source's glyph gives way first where the notch, the glyph and the figure don't fit a 1×1 cell at 40 DIP; the
    // figure and the notch stay.
    [Fact]
    public void Beside_a_notch_a_small_cells_source_gives_way()
    {
        var marks = DeskCells.Of(Cell(1, 1, Flea(352_000), DeskKeepLevel.Now), DeskCells.Small);
        Assert.Equal(DeskNotch.Now, marks.Notch);
        Assert.Equal("352k", marks.Figure);
        Assert.Equal(DeskSourceMark.None, marks.Source);
        Assert.Equal(DeskSourceMark.Flea, DeskCells.Of(Cell(1, 1, Flea(46_000)), DeskCells.Small).Source);
    }

    // At 40 DIP a 1×1 cell with a long condition ("166/400") has no room for the arrow, which gives way (the card keeps the
    // change); a short one ("2/4") keeps it, and so does a wider cell.
    [Fact]
    public void A_long_condition_takes_a_small_cells_arrow()
    {
        var down = Flea(13_000, DeskArrow.Down, -12);
        Assert.Null(DeskCells.Of(Cell(1, 1, down, print: new DeskPrint(166, 400)), DeskCells.Small).Change);
        Assert.Equal("166/400", DeskCells.Of(Cell(1, 1, down, print: new DeskPrint(166, 400)), DeskCells.Small).Count);
        Assert.Equal("↓", DeskCells.Of(Cell(1, 1, down, print: new DeskPrint(2, 4)), DeskCells.Small).Change);
        Assert.Equal("↓", DeskCells.Of(Cell(2, 1, down, print: new DeskPrint(166, 400)), DeskCells.Small).Change);
        Assert.Equal("↑", DeskCells.Of(Cell(2, 1, Flea(630_000, DeskArrow.Up, 14)), DeskCells.Small).Change);
        // Under ±10 %: no arrow at 40 DIP.
        Assert.Null(DeskCells.Of(Cell(2, 1, Flea(1_000, DeskArrow.None, -4)), DeskCells.Small).Change);
    }

    // At 56 DIP the change stands as a figure on every cell, the arrow before it from ±10 %.
    [Fact]
    public void At_56_the_change_is_a_figure()
    {
        Assert.Equal("↓ −12%", DeskCells.Of(Cell(2, 1, Flea(13_000, DeskArrow.Down, -12)), DeskCells.Large).Change);
        Assert.Equal("−1%", DeskCells.Of(Cell(2, 1, Flea(13_000, DeskArrow.None, -1.2)), DeskCells.Large).Change);
        Assert.Null(DeskCells.Of(Cell(2, 1, Flea(13_000)), DeskCells.Large).Change);
    }

    [Fact]
    public void The_count_is_the_print_or_a_stack_from_two()
    {
        Assert.Equal("46/50", DeskCells.Of(Cell(1, 1, Flea(1), print: new DeskPrint(46, 50)), DeskCells.Small).Count);
        Assert.Equal("120", DeskCells.Of(Cell(1, 1, Flea(1), count: 120), DeskCells.Small).Count);
        Assert.Null(DeskCells.Of(Cell(1, 1, Flea(1)), DeskCells.Small).Count);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0.05)]
    [InlineData(2, 0.10)]
    [InlineData(3, 0.16)]
    public void The_tint_is_ink_at_5_10_or_16_percent(int step, double opacity)
    {
        Assert.Equal(opacity, DeskCells.Of(Cell(1, 1, Flea(1, tint: step)), DeskCells.Small).Tint);
    }

    // The loadout's slot cells: 80×40 DIP a slot, 164×40 a weapon (*What it mirrors*, LOADOUT); a grid cell its footprint.
    [Fact]
    public void Slots_are_two_cells_wide_and_weapons_four_and_a_gap()
    {
        Assert.Equal(80, DeskCells.Width(new DeskCell(0, 0, 0, 0, ["x"]) { Slot = "headwear" }, DeskCells.Small));
        Assert.Equal(164, DeskCells.Width(new DeskCell(0, 0, 0, 0, ["x"]) { Slot = "onsling" }, DeskCells.Small));
        Assert.Equal(40, DeskCells.Height(new DeskCell(0, 0, 0, 0, ["x"]) { Slot = "onsling" }, DeskCells.Small));
        Assert.Equal(112, DeskCells.Width(new DeskCell(0, 0, 0, 0, ["x"]) { Slot = "headwear" }, DeskCells.Large));
        Assert.Equal(80, DeskCells.Width(new DeskCell(3, 2, 2, 3, ["x"]), DeskCells.Small));
        Assert.Equal(120, DeskCells.Height(new DeskCell(3, 2, 2, 3, ["x"]), DeskCells.Small));
    }
}
