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
/// the cell has a price (the secure container), and where it doesn't fit even at its least size (it moves to the card).</param>
/// <param name="FigureSize">The figure's type size as written (the window's text size grows it): the badges' 10.5 px at 40
/// DIP, 12 at 56, smaller where a long figure would leave its cell (never drawn under 8).</param>
/// <param name="Count">Bottom right: the printed condition ("46/50") or the stack's count, as the game prints it; null for
/// one, and where it doesn't fit the cell's width (it moves to the card).</param>
/// <param name="Change">Bottom left: the arrow ("↑", "↓") at 40 DIP, the change as a figure at 56 ("↓ −10%", "−1%"; "−10%"
/// or the arrow alone where the count leaves less room); null where there is none or no room.</param>
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

    /// <summary>The least size a figure is drawn at: past it, it moves to the card.</summary>
    public const double LeastFigure = 8;

    // Bahnschrift's semi-condensed semibold figures run about half an em wide; enough to tell what fits.
    private const double Em = 0.53;

    // A count's digits run narrower, its slash more so: "166/300" at 10 px stands 28 DIP wide (the desk's 40 DIP cells at
    // 100 %, measured on the QA pass's pictures of 2026-10-19).
    private const double DigitEm = 0.45, SlashEm = 0.3;

    // A corner's plate round its words: 2 DIP of padding a side, 1 of margin.
    private const double PlateRoom = 6;

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
    /// The marks of one cell at a cell size of 40 or 56 DIP, by the size its text is drawn at (<paramref name="drawn"/>:
    /// a size as written to the size the window's text size draws it; the same without one). A number is never cut
    /// (QA pass of 2026-10-19, F6: at 125 % "166/300" read "166/30"): what gives way first is the change, beside the count
    /// (a 1×1 cell at 40 DIP with "166/400" has no room for its arrow; at 56 the change stands as a figure, "↓ −10%",
    /// then without its arrow, "−10%", then the arrow alone, as at 40, where the count leaves less room; the card keeps
    /// the change); then the source's glyph, beside the notch or the "?" plate; a "?" plate that leaves its range no room
    /// moves to the lower left (a "?" cell has no change there); a figure still wider than its room is set smaller, never
    /// drawn under 8 px; and a count or a figure that doesn't fit even so moves to the card.
    /// </summary>
    /// <param name="trim">DIP the cell gives up of its width to its compartment's gaps (<see cref="DeskPageRules.Inset"/>).</param>
    public static DeskCellMarks Of(DeskCellSheet sheet, double size, Func<double, double>? drawn = null, double trim = 0)
    {
        drawn ??= s => s;
        var cell = sheet.Cell;
        var figure = sheet.Figure;
        var width = Width(cell, size) - trim;
        var large = size >= Large;
        var plate = cell.ItemId is null && cell.Ids.Count > 1;
        var notch = NotchOf(sheet.Keep);
        var count = CountOf(cell);

        // The bottom: the count at the right and the change at the left, each on its plate.
        var small = drawn(SmallSizeAt(size));
        double Plate(string words) => PlateRoom + small * words.Sum(c => char.IsAsciiDigit(c) ? DigitEm : c == '/' ? SlashEm : Em);
        if (count is not null && Plate(count) > width)
            count = null;
        var arrow = figure?.Arrow switch
        {
            DeskArrow.Up => "↑",
            DeskArrow.Down => "↓",
            _ => null,
        };
        IEnumerable<string?> changes = figure is null ? []
            : large && figure.Change48h is { } c ? [DeskNumbers.Change(c), DeskNumbers.Change(c, arrow: false), arrow]
            : [arrow];
        var change = changes.FirstOrDefault(words => words is not null && Plate(words) + (count is null ? 0 : Plate(count)) <= width);

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
            var glyph = source == DeskSourceMark.Portrait ? 14 + 2 : source != DeskSourceMark.None ? drawn(large ? 11 : 9) + 2 : 0;
            var room = width - 4;
            double Words(double at) => text.Length * drawn(at) * Em;
            if (source != DeskSourceMark.None && notchRoom + plateRoom + glyph + Words(figureSize) > room)
                source = DeskSourceMark.None;
            if (plate && notchRoom + plateRoom + Words(figureSize) > room && change is null)
            {
                plateLow = true;
                plateRoom = 0;
            }
            room -= notchRoom + plateRoom;
            // Smaller by half a pixel at a time, never drawn under the least (a larger text size grows small text most:
            // at 150 % no size is drawn under 10, so the size as written stops at 4).
            while (Words(figureSize) > room && figureSize - 0.5 >= 4 && drawn(figureSize - 0.5) >= LeastFigure - 1e-9)
                figureSize -= 0.5;
            if (Words(figureSize) > room)
            {
                text = null;
                source = DeskSourceMark.None;
            }
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
