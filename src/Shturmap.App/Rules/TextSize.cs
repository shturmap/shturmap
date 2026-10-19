namespace Shturmap.App.Rules;

/// <summary>Settings' text size (owner, 2026-10-19: "The text-size should still be set by an option, not only through
/// the windows settings"): three steps on top of Windows' own text size.</summary>
public enum TextStep
{
    Normal,
    Larger,
    Largest,
}

/// <summary>
/// How large Shturmap draws its text (docs/DESIGN.md §4, "Screen anatomy", *Text size*; owner, 2026-10-19: route 1, and
/// an option of the app's own). Windows' text size (Settings › Accessibility › Text size, its TextScaleFactor) is
/// honoured as WinUI honours it, and settings' step multiplies it: the text is drawn at Windows' factor × the step's,
/// never larger than Windows' own largest. WinUI grows the window's text by Windows' factor by itself; Shturmap adds the
/// step's part (<see cref="Own"/>), and the map, which WinUI doesn't reach, takes the whole factor
/// (<see cref="Effective"/>).
/// </summary>
public static class TextSize
{
    /// <summary>The setting's key in shturmap.db ("normal", "larger" or "largest"; absent is normal).</summary>
    public const string Setting = "ui.textSize";

    /// <summary>Windows' own largest text size (UISettings.TextScaleFactor runs from 1 to 2.25): the most the whole
    /// factor comes to.</summary>
    public const double Most = 2.25;

    public static TextStep Parse(string? value) => value switch
    {
        "larger" => TextStep.Larger,
        "largest" => TextStep.Largest,
        _ => TextStep.Normal,
    };

    public static string Format(TextStep step) => step.ToString().ToLowerInvariant();

    /// <summary>What a step multiplies Windows' text size by.</summary>
    public static double Of(TextStep step) => step switch
    {
        TextStep.Larger => 1.25,
        TextStep.Largest => 1.5,
        _ => 1,
    };

    /// <summary>The whole factor the text is drawn at: Windows' text size times the step, from 1 to <see cref="Most"/>.
    /// The map's labels and symbols, and the smallest window, follow it.</summary>
    /// <param name="windows">Windows' TextScaleFactor (1 at 100 %).</param>
    public static double Effective(double windows, TextStep step) => Math.Clamp(Windows(windows) * Of(step), 1, Most);

    /// <summary>The part of <see cref="Effective"/> Shturmap applies to its window's text itself: WinUI already draws it
    /// at Windows' factor, so the step's share, less where the whole would pass <see cref="Most"/>.</summary>
    public static double Own(double windows, TextStep step) => Effective(windows, step) / Windows(windows);

    /// <summary>
    /// The size WinUI draws a text of <paramref name="size"/> at a text scale factor, by its own curve (WinUI's
    /// TextFormatting::GetScaledFontSize: s + max(18 − e·ln s, 0)·(f − 1)): smaller text grows more, 11 DIP to 16.7 at
    /// 1.5, 44 to 47.9. Shturmap's step uses the same curve, so its text grows as Windows' setting would grow it.
    /// </summary>
    public static double Scaled(double size, double factor) =>
        factor <= 1 ? size : size + Math.Max(18 - Math.E * Math.Log(Math.Max(size, 1)), 0) * (factor - 1);

    private static double Windows(double windows) => double.IsFinite(windows) ? Math.Clamp(windows, 1, Most) : 1;
}
