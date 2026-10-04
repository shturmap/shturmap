namespace Shturmap.App.Rules;

/// <summary>
/// How far the pointer may wander from a held card before it closes: holding is for reading it, not for keeping it
/// forever. The distance counts from the card and from what it was opened from (its row, or its marker on the map):
/// a click on the left of a row holds a card that opens well to the right of it, and the pointer is still on the
/// row then (2026-10-04: measured from the card alone, such a card closed with the next move of the mouse).
/// </summary>
public static class CardReach
{
    /// <summary>More than this many pixels from both the card and what it was opened from is "well away".</summary>
    public const double FarAway = 240;

    /// <summary>A box in the window's coordinates.</summary>
    public readonly record struct Box(double Left, double Top, double Right, double Bottom);

    /// <summary>The distance from a point to a box; 0 inside it.</summary>
    public static double Distance(double x, double y, Box box)
    {
        var dx = Math.Max(0, Math.Max(box.Left - x, x - box.Right));
        var dy = Math.Max(0, Math.Max(box.Top - y, y - box.Bottom));
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Whether the pointer is well away from a held card and from what it was opened from.</summary>
    public static bool Away(double x, double y, Box card, Box openedFrom) =>
        Math.Min(Distance(x, y, card), Distance(x, y, openedFrom)) > FarAway;
}
