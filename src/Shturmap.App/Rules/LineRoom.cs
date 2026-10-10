namespace Shturmap.App.Rules;

/// <summary>
/// An objective's line in the raid card (Controls.ObjectiveLine): its words, the hand-over's mark when it has one, and
/// where it is (the distance over its direction tag) at the right. The words keep at least half of the line (review of
/// 2026-10-10: in German, "HINTEN LINKS · 5 M TIEFER" beside a hand-over left tarkov.dev's sentence a word or two a
/// line): where the other two would take more, where it is gets only what leaves the words their half, and its tag's
/// parts stand one under the other, the height under the direction (Controls.TagLines). docs/DESIGN.md §4, "Screen
/// anatomy", *Raid*.
/// </summary>
public static class LineRoom
{
    /// <summary>The words' least share of the line.</summary>
    public const double WordsShare = 0.5;

    /// <summary>
    /// The width where it is may take on a line of <paramref name="line"/> DIP, beside a hand-over of
    /// <paramref name="handover"/> (0 without one) and with <paramref name="spacing"/> before each of the two: null where
    /// it takes what it needs (<paramref name="where"/>, on one line), else less.
    /// </summary>
    public static double? Where(double line, double handover, double where, double spacing)
    {
        var beside = (handover > 0 ? handover + spacing : 0) + spacing;
        if (double.IsInfinity(line) || line - beside - where >= line * WordsShare)
            return null;
        return Math.Max(0, line * (1 - WordsShare) - beside);
    }
}
