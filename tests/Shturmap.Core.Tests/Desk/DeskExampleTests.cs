using Shturmap.App.Rules;
using Shturmap.Core.Desk;
using Shturmap.Core.Logs;

namespace Shturmap.Core.Tests.Desk;

// The made-up desk the developer runs show ("--desk", the script's "desk", tools\fake-raid.ps1 -Desk; Rules.DeskExample):
// built through the model as a reading's page is, it holds every mark the view draws, so one picture checks them all.
public class DeskExampleTests
{
    private static DeskView Reading(int n) => DeskExample.View(n, GameMode.Pve, n, id => "trader " + id[..4], DateTimeOffset.UnixEpoch);

    private static IEnumerable<DeskCellSheet> Cells(DeskView view) => view.Sheet.Regions.SelectMany(r => r.Cells);

    // Reading 1 the mode's first; reading 2, seven minutes later, showed the backpack alone and changed it (a LEDX more), so
    // the rest is kept from reading 1, older than the page (the view dims and dates it); reading 3, the same screenshot
    // again, changed nothing and dims nothing anew. No reading shows the POUCH: its place is the dashed frame of a region
    // never read (owner, 2026-10-25).
    [Fact]
    public void Its_three_readings_are_a_first_a_change_and_none()
    {
        var first = Reading(1);
        Assert.True(first.First);
        Assert.Equal(first.Sheet.Regions.Select(r => r.Region.Key).ToHashSet(), first.Changed);
        Assert.All(first.Sheet.Page.Regions, r => Assert.Equal(first.Sheet.Page.ReadAt, r.ReadAt));
        var second = Reading(2);
        Assert.False(second.First);
        Assert.Equal(["backpack"], second.Changed.ToList());
        Assert.Equal(Cells(first).Count() + 1, Cells(second).Count());
        Assert.Equal(TimeSpan.FromMinutes(7), second.Sheet.Page.ReadAt - first.Sheet.Page.ReadAt);
        Assert.Equal(["backpack"], second.Sheet.Page.Regions.Where(r => !DeskPageRules.Kept(r, second.Sheet.Page)).Select(r => r.Key));
        var third = Reading(3);
        Assert.Empty(third.Changed);
        Assert.Equal(["backpack"], third.Sheet.Page.Regions.Where(r => !DeskPageRules.Kept(r, third.Sheet.Page)).Select(r => r.Key));
        Assert.All(new[] { first, second, third }, view => Assert.DoesNotContain(view.Sheet.Regions, r => r.Region.Kind == DeskRegionKind.Pouch));
        // Out of range: the nearest.
        Assert.True(DeskExample.View(0, GameMode.Pve, 1, null, DateTimeOffset.UnixEpoch).First);
    }

    [Fact]
    public void Its_page_holds_every_mark_the_view_draws()
    {
        var view = Reading(1);
        var cells = Cells(view).ToList();
        // "?" cells: a slot's and a grid's, with their ranges.
        Assert.Contains(cells, c => c.Cell.IsSlot && c.Figure is { Unsure: true });
        Assert.Contains(cells, c => !c.Cell.IsSlot && c.Figure is { Unsure: true });
        // A condition scaled (the body armour at 98/147), a full print without "~" (the Salewa at 400/400).
        var armour = cells.Single(c => c.Cell.Slot == "bodyarmor");
        Assert.True(armour.Figure is { Estimate: true } f && f.Value < f.Full);
        Assert.Contains(cells, c => c.Cell.Print is { Full: true } && c.Figure is { Estimate: false });
        // Contents unseen ("+"): a container, a weapon; and the Zhuk with its window attached, without one.
        Assert.Contains(cells, c => c.Cell.Holds == DeskHolds.Contents && c.Figure is { Plus: true });
        Assert.Contains(cells, c => c.Cell.Holds == DeskHolds.Mods && c.Figure is { Plus: true });
        var zhuk = view.Sheet.Regions.Single(r => r.Region.Key == "backpack").Cells.Single(c => c.Cell.Ids.Count == 2);
        Assert.False(zhuk.Unseen);
        Assert.NotNull(view.Sheet.Page.WindowOf(view.Sheet.Page.Region("backpack")!, zhuk.Cell));
        // No price: the streamer case, the roubles (the secure container, with the POUCH, the glance's alone since 2026-10-25).
        Assert.Contains(cells, c => c.Cell.ItemId == DeskExample.StreamerCase && c.Figure is null);
        Assert.Contains(cells, c => c.Cell.ItemId == DeskExample.Roubles && c.Figure is null);
        Assert.Contains(DeskExample.Pouch().Cells, c => c.Slot == "pouch");
        // Empty slots in LOADOUT, drawn as empty boxes: the earpiece, the face cover, the eyewear, on back, the sheath.
        Assert.Equal(["earpiece", "eyewear", "facecover", "onback", "sheath"],
            DeskSlots.Loadout.Select(s => s.Key).Except(view.Sheet.Regions.Single(r => r.Region.Key == "loadout").Cells.Select(c => c.Cell.Slot!)).Order());
        // Every notch, both arrows, every tint, both sources.
        Assert.Equal(
            [DeskKeepLevel.None, DeskKeepLevel.Later, DeskKeepLevel.Hideout, DeskKeepLevel.Next, DeskKeepLevel.Now],
            cells.Select(c => c.Keep).Distinct().Order());
        Assert.Contains(cells, c => c.Figure?.Arrow == DeskArrow.Up);
        Assert.Contains(cells, c => c.Figure?.Arrow == DeskArrow.Down);
        Assert.Equal([0, 1, 2, 3], cells.Where(c => c.Figure is not null).Select(c => c.Figure!.Tint).Distinct().Order());
        Assert.Contains(cells, c => c.Figure?.Source == DeskSource.Flea);
        Assert.Contains(cells, c => c.Figure is { Source: DeskSource.Trader, TraderName: not null });
        // A cell nothing was said of, and a stack with its count.
        Assert.Contains(cells, c => c.Cell.Ids.Count == 0);
        Assert.Contains(cells, c => c.Cell.Count > 1);
        // A 1×1 cell whose long condition takes its arrow's room at 40 DIP.
        Assert.Contains(cells, c => c.Cell is { W: 1, H: 1, Print: not null } && c.Figure?.Arrow != DeskArrow.None
                                    && DeskCells.Of(c, DeskCells.Small).Change is null);
    }

    [Fact]
    public void Its_totals_carry_the_plus_and_the_split_window_counts_in_neither()
    {
        var view = Reading(1);
        Assert.True(view.Sheet.Totals.OnYouPlus);
        Assert.True(view.Sheet.Totals.StashPlus);
        Assert.Equal(DeskPart.Neither, view.Sheet.Regions.Single(r => r.Region.Key == "window:wz").Part);
        Assert.Equal(DeskPart.OnYou, view.Sheet.Regions.Single(r => r.Region.Key == "window:zhuk").Part);
        Assert.True(view.Sheet.Totals.KeepNow > 0 && view.Sheet.Totals.KeepNext > 0 && view.Sheet.Totals.KeepLater > 0);
    }

    // Each cell of a grid of compartments stands inside one of them, as the game holds them (the Zhuk's window: ten
    // pockets of one by two, QA pass of 2026-10-19, F3).
    [Fact]
    public void Its_cells_stand_inside_their_compartments()
    {
        var page = DeskExample.Page(2);
        Assert.Equal(10, page.Region("window:zhuk")!.Parts.Count);
        foreach (var region in page.Regions.Where(r => r.Parts.Count > 0))
            Assert.All(region.Cells.Where(c => !c.IsSlot), c => Assert.Contains(region.Parts, p => p.Contains(c.Rect)));
    }

    // The page's cells don't overlap and stand inside their grids, as a reading's would.
    [Fact]
    public void Its_cells_stand_inside_their_grids_without_overlapping()
    {
        foreach (var region in DeskExample.Page(2).Regions.Where(r => r.Cols > 0))
        {
            var grid = new DeskRect(0, 0, region.Cols, region.Rows);
            var cells = region.Cells.Where(c => !c.IsSlot).ToList();
            Assert.All(cells, c => Assert.True(grid.Contains(c.Rect), $"{region.Key}: {c.Rect} outside its grid"));
            for (var a = 0; a < cells.Count; a++)
            {
                for (var b = a + 1; b < cells.Count; b++)
                    Assert.False(cells[a].Rect.Overlaps(cells[b].Rect), $"{region.Key}: {cells[a].Rect} overlaps {cells[b].Rect}");
            }
        }
    }
}
