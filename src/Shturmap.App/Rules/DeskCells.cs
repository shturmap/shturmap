using Shturmap.Core.Desk;

namespace Shturmap.App.Rules;

/// <summary>The keep notch at a cell's upper left (docs/DESIGN.md §4, *The cell*): a filled triangle in amber for now, an
/// open one for next, a corner bracket for later, the filled triangle in ink for a hideout module's need.</summary>
public enum DeskNotch
{
    None,
    Now,
    Next,
    Later,
    Hideout,
}

/// <summary>What stands before the figure: the flea's price tag, a trader's person glyph, or at 56 DIP the trader's
/// 14 DIP portrait; nothing for "one of N" (its candidates' sources differ) or where the glyph gives way.</summary>
public enum DeskSourceMark
{
    None,
    Flea,
    Trader,
    Portrait,
}

/// <summary>
/// Everything a desk cell draws, placed (docs/DESIGN.md §4, *The cell*, B1): top right the figure after its source,
/// bottom right the count or the printed condition, top left the keep notch and the "?" plate, bottom left the 48 h
/// change, behind the art the tint.
/// </summary>
/// <param name="Figure">The figure with its "~", "+" or range ("902k", "~31k", "34k+", "63–307k"); null where no item of
/// the cell has a price (the secure container).</param>
/// <param name="FigureSize">The figure's type size: the badges' 10.5 px at 40 DIP, 12 at 56, smaller where a long figure
/// would leave its cell (not under 8).</param>
/// <param name="Count">Bottom right: the printed condition ("46/50") or the stack's count, as the game prints it; null for
/// one.</param>
/// <param name="Change">Bottom left: the arrow ("↑", "↓") at 40 DIP, the change as a figure at 56 ("↓ −10%", "−1%"); null
/// where there is none or no room.</param>
/// <param name="Plate">The "?" plate: a cell that fits several items, its art at half strength.</param>
/// <param name="PlateLow">The plate stands at the lower left, not the upper: a cell too narrow for the plate and its range
/// side by side (a "?" cell has no change to show there).</param>
/// <param name="Tint">Ink behind the art, as an opacity: 0, 0.05, 0.10 or 0.16.</param>
public sealed record DeskCellMarks(string? Figure, double FigureSize, DeskSourceMark Source, string? TraderId, string? Count, DeskNotch Notch,
    string? Change, bool Plate, double ArtOpacity, double Tint)
{
    public bool PlateLow { get; init; }
}

/// <summary>The rules of a desk cell's marks (docs/DESIGN.md §4, *The cell*; step B part 2, 2026-10-19).</summary>
public static class DeskCells
{
    /// <summary>The item cell's 40 DIP, the desk's cell at the smallest window.</summary>
    public const double Small = 40;

    /// <summary>The cell from a desk 600 DIP across.</summary>
    public const double Large = 56;

    /// <summary>The keep notch's size, 11 DIP.</summary>
    public const double NotchSize = 11;

    /// <summary>The figure's size at 40 DIP: the badges' 10.5 px; 12 at 56.</summary>
    public static double FigureSizeAt(double cell) => cell >= Large ? 12 : 10.5;

    /// <summary>The count's and the change's size: 10 px at 40 DIP, 11.5 at 56.</summary>
    public static double SmallSizeAt(double cell) => cell >= Large ? 11.5 : 10;

    // Bahnschrift's semi-condensed semibold figures run about half an em wide; enough to tell what fits.
    private const double Em = 0.53;

    /// <summary>The tint behind the art, as the model's step gives it: ink at 5, 10 or 16 % from 20k, 100k or 300k ₽ a slot.</summary>
    public static double TintOf(int step) => step switch
    {
        <= 0 => 0,
        1 => 0.05,
        2 => 0.10,
        _ => 0.16,
    };

    /// <summary>The notch for a keep level.</summary>
    public static DeskNotch NotchOf(DeskKeepLevel level) => level switch
    {
        DeskKeepLevel.Now => DeskNotch.Now,
        DeskKeepLevel.Next => DeskNotch.Next,
        DeskKeepLevel.Later => DeskNotch.Later,
        DeskKeepLevel.Hideout => DeskNotch.Hideout,
        _ => DeskNotch.None,
    };

    /// <summary>A cell's width on the desk: a grid cell its footprint, a slot two cells (80×40), a weapon's slot four
    /// and a gap (164×40; *What it mirrors*, LOADOUT).</summary>
    public static double Width(DeskCell cell, double size) =>
        !cell.IsSlot ? Math.Max(1, cell.W) * size
        : DeskSlots.IsWeapon(cell.Slot) ? 4 * size + size / 10
        : 2 * size;

    /// <summary>A cell's height on the desk: a grid cell its footprint, a slot one cell.</summary>
    public static double Height(DeskCell cell, double size) => cell.IsSlot ? size : Math.Max(1, cell.H) * size;

    /// <summary>The bottom right as the game prints it: the "current/max" (a condition, or a magazine's rounds), else the
    /// count from two.</summary>
    public static string? CountOf(DeskCell cell) =>
        cell.Print is { } print ? print.ToString() : cell.Count > 1 ? DeskNumbers.Count(cell.Count) : null;

    /// <summary>
    /// The marks of one cell at a cell size of 40 or 56 DIP. Where the bottom's two don't fit the cell's width side by
    /// side, the change gives way (a 1×1 cell at 40 DIP with "166/400" has no room for its arrow; the card keeps the
    /// change). Where the figure and its source don't fit beside the notch or the "?" plate, the source gives way; a "?"
    /// plate that leaves its range no room moves to the lower left (a "?" cell has no change there); and a figure still
    /// wider than its room is set smaller, to 8 px at the least.
    /// </summary>
    public static DeskCellMarks Of(DeskCellSheet sheet, double size)
    {
        var cell = sheet.Cell;
        var figure = sheet.Figure;
        var width = Width(cell, size);
        var large = size >= Large;
        var plate = cell.ItemId is null && cell.Ids.Count > 1;
        var notch = NotchOf(sheet.Keep);
        var count = CountOf(cell);

        string? change = figure switch
        {
            null => null,
            _ when large => figure.Change48h is { } c ? DeskNumbers.Change(c) : null,
            { Arrow: DeskArrow.Up } => "↑",
            { Arrow: DeskArrow.Down } => "↓",
            _ => null,
        };
        var small = SmallSizeAt(size);
        if (change is not null && count is not null && 2 + change.Length * small * Em + 3 + count.Length * small * Em + 2 > width)
            change = null;

        string? text = figure is null ? null
            : DeskNumbers.Figure(figure.Value, figure.High, figure.Unsure, figure.Estimate, figure.Plus);
        var source = figure is null || figure.Unsure ? DeskSourceMark.None
            : figure.Source switch
            {
                DeskSource.Flea => DeskSourceMark.Flea,
                DeskSource.Trader => large && figure.Trader is not null ? DeskSourceMark.Portrait : DeskSourceMark.Trader,
                _ => DeskSourceMark.None,
            };
        var figureSize = FigureSizeAt(size);
        var plateLow = false;
        if (text is not null)
        {
            var notchRoom = notch != DeskNotch.None ? NotchSize + 1 : 0;
            var plateRoom = plate ? PlateWidth + 2 : 0;
            var glyph = source == DeskSourceMark.Portrait ? 14 + 2 : source != DeskSourceMark.None ? (large ? 11 : 9) + 2 : 0;
            var room = width - 4;
            var words = text.Length * figureSize * Em;
            if (source != DeskSourceMark.None && notchRoom + plateRoom + glyph + words > room)
                source = DeskSourceMark.None;
            if (plate && notchRoom + plateRoom + words > room && change is null)
            {
                plateLow = true;
                plateRoom = 0;
            }
            room -= notchRoom + plateRoom;
            if (words > room)
                figureSize = Math.Max(8, Math.Floor(room / (text.Length * Em) * 2) / 2);
        }
        return new DeskCellMarks(text, figureSize, source, source == DeskSourceMark.Portrait ? figure!.Trader : null, count, notch, change, plate,
            plate ? 0.5 : 1, TintOf(figure?.Tint ?? 0))
        {
            PlateLow = plateLow,
        };
    }

    /// <summary>The "?" plate's width, 12 DIP.</summary>
    public const double PlateWidth = 12;
}
