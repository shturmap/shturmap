namespace Shturmap.App.Rules;

/// <summary>
/// How the trader strip under the MAP list lays its portraits out (owner, 2026-10-17, after trying it: "I like this, but
/// I do not like that the layout has two rows where Ref falls exactly on the last line"). It never wraps: one row, the
/// cells sharing the rail's inner width, each (width − gaps) / n. A cell holds the portrait and the count beside it,
/// at 20 DIP and 15 px while they fit, both scaling down with the cell; the portrait stops at 16 DIP, the count goes on
/// down to 10 px. Where even that doesn't fit (ten traders or more in the rail's 351 DIP), the count moves onto the
/// portrait as a small badge at its lower right, in the map's count-badge style, and the cells are portraits only.
/// Room is kept for a count of two digits whatever the counts are, so the layout follows the number of traders alone
/// and doesn't change as a count goes from 9 to 10.
/// </summary>
public static class TraderStrip
{
    /// <summary>Between two cells.</summary>
    public const double Gap = 4;

    /// <summary>Around a cell's portrait and count, inside it: where the linked tint shows.</summary>
    public const double Pad = 2;

    /// <summary>Between the portrait and its count.</summary>
    public const double CountGap = 3;

    /// <summary>What the badge stands out past the portrait's lower right corner, to the right and down.</summary>
    public const double BadgeRight = 4;

    public const double BadgeDown = 3;

    /// <summary>The portrait's size, and the floor it scales down to before the count goes onto it.</summary>
    public const double FullPortrait = 20;

    public const double MinPortrait = 16;

    /// <summary>The count's size (the figures' 15 px), and the floor it scales down to.</summary>
    public const double FullCount = 15;

    public const double MinCount = 10;

    /// <summary>The badge's figure, as the map's count badges have it.</summary>
    public const double BadgeCount = 10;

    /// <summary>One cell's size and what it holds.</summary>
    /// <param name="Width">The cell's width: every cell's, so they fill the row with the gaps.</param>
    /// <param name="Portrait">The portrait's size, DIP.</param>
    /// <param name="Count">The count's type size beside the portrait, px; <see cref="BadgeCount"/> on a badge.</param>
    /// <param name="Badge">The count stands on the portrait as a badge, not beside it.</param>
    public sealed record Cells(double Width, double Portrait, double Count, bool Badge);

    /// <param name="traders">How many portraits the strip shows.</param>
    /// <param name="width">The rail's inner width, DIP (351 at every window size).</param>
    /// <param name="twoDigits">How wide a count of two digits is at <see cref="FullCount"/> in the figures' face, DIP.</param>
    public static Cells Layout(int traders, double width, double twoDigits)
    {
        var n = Math.Max(1, traders);
        var cell = (width - Gap * (n - 1)) / n;
        // What the portrait and the count beside it have between them.
        var room = cell - 2 * Pad - CountGap;
        var scale = Math.Min(1, room / (FullPortrait + twoDigits));
        if (FullPortrait * scale >= MinPortrait)
            return new Cells(cell, Down(FullPortrait * scale), Half(FullCount * scale), false);
        // The portrait at its floor; the count takes what is left, down to its own.
        var count = (room - MinPortrait) / (twoDigits / FullCount);
        if (count >= MinCount)
            return new Cells(cell, MinPortrait, Half(count), false);
        return new Cells(cell, Down(Math.Min(FullPortrait, cell - 2 * Pad - BadgeRight)), BadgeCount, true);
    }

    /// <summary>How wide a cell's content is: the portrait and the count beside it (at most two digits), or the portrait
    /// with its badge standing out.</summary>
    public static double ContentWidth(Cells cells, double twoDigits) =>
        2 * Pad + cells.Portrait + (cells.Badge ? BadgeRight : CountGap + twoDigits * cells.Count / FullCount);

    // Sizes rounded down, so the frames stay on whole pixels and the content never grows past its cell.
    private static double Down(double size) => Math.Floor(size);

    private static double Half(double size) => Math.Floor(size * 2) / 2;
}
