using System.Globalization;
using Shturmap.Core;

namespace Shturmap.App.Rules;

/// <summary>
/// The stash desk's numbers (docs/DESIGN.md §4, "Screen anatomy", *The stash desk*; step B, 2026-10-19): a figure in a
/// cell as short as the panel drew it, three figures at most ("902k", "4.5k", "1.01 M"), a range in its high end's unit
/// ("63–307k", "0.3–294k"), the head line's totals with the rouble sign ("1.01 M ₽", "1.01 M+ ₽"), and the 48 h change at
/// 56 DIP ("↓ −10%"). The decimal sign is the language's (German "1,01 M"); the units are texts, so a language may write
/// its own.
/// </summary>
public static class DeskNumbers
{
    /// <summary>A value short: under 1,000 as it is ("850"), under 10k with a tenth ("4.5k", "4k"), under a million in
    /// whole thousands ("902k"), from there in millions with three figures ("1.01 M", "12.3 M", "123 M").</summary>
    public static string Short(double value)
    {
        value = Math.Max(0, Round(value));
        if (value < 1_000)
            return Whole(value);
        if (Round(value / 1_000) < 1_000)
            return RuleTexts.DeskThousands(amount: Thousands(value));
        return RuleTexts.DeskMillions(amount: Millions(value));
    }

    /// <summary>"one of N"'s range, both ends in the high end's unit: "63–307k", "0.3–294k", "0.45–1.18 M", "300–850";
    /// one value when both ends are the same.</summary>
    public static string Range(double low, double high)
    {
        low = Math.Max(0, Round(low));
        high = Math.Max(0, Round(high));
        if (high < low)
            (low, high) = (high, low);
        if (Short(low) == Short(high))
            return Short(high);
        if (high < 1_000)
            return RuleTexts.DeskRange(low: Whole(low), high: Whole(high));
        if (Round(high / 1_000) < 1_000)
            return RuleTexts.DeskRangeThousands(low: low < 10_000 ? Tenths(low / 1_000) : Thousands(low), high: Thousands(high));
        return RuleTexts.DeskRangeMillions(low: Millions(low), high: Millions(high));
    }

    /// <summary>A cell's figure with its marks: "~" before an estimate from condition, "+" after a figure with contents
    /// or mods unseen ("~83k+", "34k+"), the range for "one of N".</summary>
    public static string Figure(double value, double high, bool unsure, bool estimate, bool plus) =>
        (estimate ? "~" : "") + (unsure ? Range(value, high) : Short(value)) + (plus ? "+" : "");

    /// <summary>A total of the head line, with the rouble sign: "1.01 M ₽", "1.01 M+ ₽" with an unseen container in it.</summary>
    public static string Total(double value, bool plus) => RuleTexts.DeskRoubles(amount: Short(value) + (plus ? "+" : ""));

    /// <summary>The 48 h change as the cell shows it at 56 DIP: the arrow from ±10 % (<see cref="Shturmap.Core.Desk.DeskValues.ArrowFrom"/>),
    /// then the whole percent with its sign ("↓ −10%", "+3%", "0%"); without <paramref name="arrow"/>, the figure alone
    /// ("−10%"). The arrow is joined to its figure by a no-break space.</summary>
    public static string Change(double percent, bool arrow = true)
    {
        var whole = Math.Round(percent, MidpointRounding.AwayFromZero);
        var sign = whole > 0 ? "+" : whole < 0 ? "−" : "";
        var text = RuleTexts.DeskChange(change: sign + Math.Abs(whole).ToString("0", UiLanguage.Culture));
        return arrow && Arrow(percent) is { } mark ? mark + "\u00A0" + text : text;
    }

    /// <summary>The change's arrow from ±10 % ("↑", "↓"), or null under it.</summary>
    public static string? Arrow(double percent) =>
        percent >= Shturmap.Core.Desk.DeskValues.ArrowFrom ? "↑" : percent <= -Shturmap.Core.Desk.DeskValues.ArrowFrom ? "↓" : null;

    private static string Whole(double value) => value.ToString("0", UiLanguage.Culture);

    // Thousands: with a tenth under 10k ("4.5"), whole above ("902").
    private static string Thousands(double value) => value < 9_950 ? Tenths(value / 1_000) : Whole(Round(value / 1_000));

    private static string Tenths(double value) => Round(value, 1).ToString("0.#", UiLanguage.Culture);

    // Millions with three figures: "1.01", "12.3", "123".
    private static string Millions(double value)
    {
        var m = value / 1_000_000;
        if (Round(m, 2) < 10)
            return Round(m, 2).ToString("0.00", UiLanguage.Culture);
        if (Round(m, 1) < 100)
            return Round(m, 1).ToString("0.0", UiLanguage.Culture);
        return Round(m).ToString("0", UiLanguage.Culture);
    }

    // Halves round up, as people read prices (a figure of 999.5k is a million).
    private static double Round(double value, int digits = 0) => Math.Round(value, digits, MidpointRounding.AwayFromZero);

    /// <summary>A count as the game prints it: plain digits, no grouping ("50000").</summary>
    public static string Count(int count) => count.ToString(CultureInfo.InvariantCulture);
}
