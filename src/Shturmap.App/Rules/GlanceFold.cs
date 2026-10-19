namespace Shturmap.App.Rules;

/// <summary>
/// The raid card's glance (NEXT, EXIT, OR) at a larger text size (docs/DESIGN.md §4, "Screen anatomy", *Text size*;
/// 2026-10-19). Its labels share a column before the names and its figures stand after them, both measured in the
/// language and text size in use; at 150 % the two left the name about 130 DIP, a word or two a line ("Markiere den /
/// Stryker mit / einem / MS2000 …"), and pushed EXIT out of the smallest window. Where the name would keep fewer than
/// <see cref="LeastEms"/> of its own size, each label stands above its name instead, which then runs under the label
/// too; the figures stay at the right.
/// </summary>
public static class GlanceFold
{
    /// <summary>The least room a name keeps beside its label, in its own size: about sixteen letters, the longest
    /// German words of the objectives ("Finanzunterlagen") whole.</summary>
    public const double LeastEms = 8;

    /// <param name="inner">The glance row's width inside its padding.</param>
    /// <param name="label">The labels' column.</param>
    /// <param name="figure">The figures' column, its gap before it included.</param>
    /// <param name="nameSize">The size the name is drawn at (15 DIP at 100 %, larger with the text size).</param>
    public static bool Stacked(double inner, double label, double figure, double nameSize) =>
        inner > 0 && inner - label - figure < LeastEms * nameSize;
}
