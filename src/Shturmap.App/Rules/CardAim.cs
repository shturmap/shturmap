namespace Shturmap.App.Rules;

/// <summary>
/// Whether the pointer is on its way to an open card. A card opens beside the row it came from, so the way to it
/// often leads across other things that open cards of their own: the other quest glyphs in a map's row, a need
/// cell, the rows below. While the pointer keeps heading for the card, those wait, and the card isn't taken away
/// either (owner, 2026-10-04: "When you then move the mouse to the right to mouse-over the quest and you are too
/// slow another quest opens"). The "safe triangle" of nested menus: what counts is the direction, not the speed.
/// </summary>
public static class CardAim
{
    /// <summary>A move shorter than this many pixels says nothing about where the pointer is going.</summary>
    public const double Moved = 3;

    /// <summary>How far past the card's edges a path may point and still count as heading for it.</summary>
    public const double Slack = 16;

    /// <summary>
    /// The pointer went from one place to another: is it heading for the card? It is when it moved, came nearer to
    /// the card, and the line it moved along, carried on, meets the card (with a little slack around it).
    /// </summary>
    public static bool Toward(double fromX, double fromY, double toX, double toY, CardReach.Box card)
    {
        double dx = toX - fromX, dy = toY - fromY;
        if (Math.Sqrt(dx * dx + dy * dy) < Moved)
            return false;
        if (CardReach.Distance(toX, toY, card) >= CardReach.Distance(fromX, fromY, card))
            return false;
        // Where the line from the first place through the second runs inside the card's box, as a stretch of the
        // line (0 at the first place); none, or only behind the first place, and it doesn't lead there.
        double enter = 0, leave = double.PositiveInfinity;
        return Clip(fromX, dx, card.Left - Slack, card.Right + Slack, ref enter, ref leave)
            && Clip(fromY, dy, card.Top - Slack, card.Bottom + Slack, ref enter, ref leave);
    }

    // One axis of the box: narrows the stretch of the line that lies between low and high.
    private static bool Clip(double from, double step, double low, double high, ref double enter, ref double leave)
    {
        if (step == 0)
            return from >= low && from <= high;
        double a = (low - from) / step, b = (high - from) / step;
        enter = Math.Max(enter, Math.Min(a, b));
        leave = Math.Min(leave, Math.Max(a, b));
        return enter <= leave;
    }
}
