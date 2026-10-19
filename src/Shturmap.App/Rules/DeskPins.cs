using Shturmap.Core.Desk;

namespace Shturmap.App.Rules;

/// <summary>A cell's place on the page: its region and its slot, or its top left in the region's grid.</summary>
public readonly record struct DeskCellKey(string Region, string? Slot, int I, int J)
{
    public static DeskCellKey Of(DeskRegion region, DeskCell cell) => new(region.Key, cell.Slot, cell.IsSlot ? 0 : cell.I, cell.IsSlot ? 0 : cell.J);

    public bool Is(DeskRegion region, DeskCell cell) => this == Of(region, cell);

    public override string ToString() => Slot is { } slot ? $"{Region} {slot}" : $"{Region} {I},{J}";
}

/// <summary>One pinned card: the cell's place and the cell as it was read when pinned (or last kept).</summary>
public sealed record DeskPin(DeskCellKey Key, DeskCell Cell);

/// <summary>
/// The cards pinned in the rail (owner, 2026-10-20: "It is also not possible to 'pin' items to the side view"; then a
/// right-click on a cell or the pin on a card's head, the click itself copying; docs/DESIGN.md §4, *The stash desk*, *The
/// click*): several stack, newest on top; the same again unpins. A pin stays with its cell on the page and goes when a
/// reading changes or takes away that cell (a reading that shows the cell as it was keeps it, a pick too); Esc leaves the
/// pins alone.
/// </summary>
public static class DeskPins
{
    /// <summary>Pins the cell on top, or unpins it when it is pinned.</summary>
    public static IReadOnlyList<DeskPin> Toggle(IReadOnlyList<DeskPin> pins, DeskRegion region, DeskCell cell)
    {
        var key = DeskCellKey.Of(region, cell);
        return pins.Any(p => p.Key == key) ? pins.Where(p => p.Key != key).ToList() : [new DeskPin(key, cell), .. pins];
    }

    /// <summary>The pins without one cell's.</summary>
    public static IReadOnlyList<DeskPin> Unpin(IReadOnlyList<DeskPin> pins, DeskCellKey key) => pins.Where(p => p.Key != key).ToList();

    public static bool IsPinned(IReadOnlyList<DeskPin> pins, DeskCellKey key) => pins.Any(p => p.Key == key);

    /// <summary>The pins a new page keeps: those whose cell stands at its place as it was read (place, ids, count,
    /// condition: <see cref="DeskCell.SameAs"/>), each with the page's cell (its pick may be new); null for no page.</summary>
    public static IReadOnlyList<DeskPin> Kept(IReadOnlyList<DeskPin> pins, DeskSheet? sheet)
    {
        if (sheet is null)
            return [];
        var kept = new List<DeskPin>();
        foreach (var pin in pins)
        {
            if (Find(sheet, pin.Key) is { } found && found.Cell.Cell.SameAs(pin.Cell))
                kept.Add(pin with { Cell = found.Cell.Cell });
        }
        return kept;
    }

    /// <summary>The cell at a place on the page, with its region; null when there is none.</summary>
    public static (DeskRegionSheet Region, DeskCellSheet Cell)? Find(DeskSheet sheet, DeskCellKey key)
    {
        foreach (var region in sheet.Regions.Where(r => r.Region.Key == key.Region))
        {
            if (region.Cells.FirstOrDefault(c => key.Is(region.Region, c.Cell)) is { } cell)
                return (region, cell);
        }
        return null;
    }
}
