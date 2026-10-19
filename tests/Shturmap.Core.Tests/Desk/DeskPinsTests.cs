using Shturmap.App.Rules;
using Shturmap.Core.Desk;
using Shturmap.Core.Logs;

namespace Shturmap.Core.Tests.Desk;

// The cards pinned in the desk's rail (owner, 2026-10-20: pinning, by a right-click on a cell or the pin on a card;
// docs/DESIGN.md §4, *The stash desk*, *The click*; Rules.DeskPins): newest on top, the same again unpins; a pin stays
// with its cell as read and goes when a reading changes or takes the cell.
public class DeskPinsTests
{
    private static readonly DeskView View = DeskExample.View(1, GameMode.Pve, 1, null, DateTimeOffset.UnixEpoch);

    private static (DeskRegion Region, DeskCell Cell) At(DeskView view, string region, int i, int j)
    {
        var sheet = view.Sheet.Regions.Single(r => r.Region.Key == region);
        return (sheet.Region, sheet.Cells.Single(c => !c.Cell.IsSlot && c.Cell.I == i && c.Cell.J == j).Cell);
    }

    [Fact]
    public void Pins_stack_newest_on_top_and_the_same_again_unpins()
    {
        var (stash, ledx) = At(View, "stash", 8, 0);
        var (backpack, grizzly) = At(View, "backpack", 4, 1);
        IReadOnlyList<DeskPin> pins = [];
        pins = DeskPins.Toggle(pins, stash, ledx);
        pins = DeskPins.Toggle(pins, backpack, grizzly);
        Assert.Equal([new DeskCellKey("backpack", null, 4, 1), new DeskCellKey("stash", null, 8, 0)], pins.Select(p => p.Key));
        Assert.True(DeskPins.IsPinned(pins, DeskCellKey.Of(stash, ledx)));
        pins = DeskPins.Toggle(pins, stash, ledx);
        Assert.Equal([new DeskCellKey("backpack", null, 4, 1)], pins.Select(p => p.Key));
        Assert.Empty(DeskPins.Unpin(pins, new DeskCellKey("backpack", null, 4, 1)));
        // A slot by its key.
        var loadout = View.Sheet.Regions.Single(r => r.Region.Key == "loadout");
        var helmet = loadout.Cells.Single(c => c.Cell.Slot == "headwear").Cell;
        Assert.Equal(new DeskCellKey("loadout", "headwear", 0, 0), DeskCellKey.Of(loadout.Region, helmet));
        Assert.Equal("loadout headwear", DeskCellKey.Of(loadout.Region, helmet).ToString());
    }

    // A reading that shows the cell as it was keeps its pin, a pick too; one that changed the cell, or a page without it,
    // takes the pin.
    [Fact]
    public void A_pin_stays_with_its_cell_as_read()
    {
        var (stash, ledx) = At(View, "stash", 8, 0);
        var (_, unsure) = At(View, "stash", 4, 1);
        IReadOnlyList<DeskPin> pins = DeskPins.Toggle(DeskPins.Toggle([], stash, ledx), stash, unsure);
        // The same page, a reading later (reading 2 changed only the backpack).
        Assert.Equal(2, DeskPins.Kept(pins, DeskExample.View(2, GameMode.Pve, 2, null, DateTimeOffset.UnixEpoch).Sheet).Count);
        // A pick on the "?" cell: kept, with the picked cell.
        var picked = DeskExample.View(1, GameMode.Pve, 1, null, DateTimeOffset.UnixEpoch,
            new Dictionary<DeskCellKey, string> { [DeskCellKey.Of(stash, unsure)] = unsure.Ids[1] });
        var kept = DeskPins.Kept(pins, picked.Sheet);
        Assert.Equal(unsure.Ids[1], kept.Single(p => p.Key.I == 4).Cell.ItemId);
        // The LEDX's cell read with another count: its pin goes.
        var page = View.Sheet.Page;
        var changed = page with
        {
            Regions = page.Regions.Select(r => r.Key != "stash" ? r : r with
            {
                Cells = r.Cells.Select(c => c.I == 8 && c.J == 0 && !c.IsSlot ? c with { Count = 2 } : c).ToList(),
            }).ToList(),
        };
        var sheet = DeskSheet.Of(changed, View.FactsOf, View.Flea);
        Assert.Equal([new DeskCellKey("stash", null, 4, 1)], DeskPins.Kept(pins, sheet).Select(p => p.Key));
        Assert.Empty(DeskPins.Kept(pins, null));
    }
}
