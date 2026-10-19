using Shturmap.Core.Screenshots;

namespace Shturmap.Game.Screenshots;

/// <summary>
/// The words the game writes on a cell, matched to item names: the short name at the top right, which is the item's
/// identity (lookalike keys share their art and differ in their name's digit), and the number at the bottom right,
/// which is a stack's count, or a magazine's load written with a slash.
/// </summary>
internal static class StashLabels
{
    /// <summary>How well a label fits a name for it to name the item: one letter in five may be off.</summary>
    public const double Enough = 0.8;

    /// <summary>The most a count is taken to be: a stack of money reads in the hundred thousands.</summary>
    public const int MostCount = 9999;

    /// <summary>
    /// How well a read label fits a short name, 0 to 1: the same folded letters (<see cref="ExitList.Fold"/>), or
    /// the name's beginning when the game cut the label at the cell's edge (six folded letters or more that start
    /// the name; the cut letter itself may read as anything, "Pindrak4" for "Pindrake"). A name with digits must
    /// agree in them, since lookalike keys differ by one digit alone. A cell's edge reads as "!" or "|" now and
    /// then: no label has them.
    /// </summary>
    public static double Fit(string text, string shortName)
    {
        text = text.Replace("!", "").Replace("|", "").Trim();
        var a = ExitList.Fold(text);
        var b = ExitList.Fold(shortName);
        if (a.Length == 0 || b.Length == 0)
            return 0;
        if (a.Length >= 6 && b.Length > a.Length && b.StartsWith(a, StringComparison.Ordinal))
            return 1;
        var fit = Similarity(a, b);
        if (a.Length >= 6 && a.Length <= b.Length)
        {
            var same = 0;
            while (same < a.Length && a[same] == b[same])
                same++;
            if (same >= 6)
                fit = Math.Max(fit, (double)same / a.Length);
        }
        if (!shortName.Any(char.IsDigit))
            return fit;
        // The folded digits (an O read for a 0 or an l for a 1 is no difference) must be the same.
        var digitsA = new string(a.Where(char.IsDigit).ToArray());
        var digitsB = new string(b.Where(char.IsDigit).ToArray());
        return digitsA != digitsB ? Math.Min(fit, 0.5) : fit;
    }

    /// <summary>
    /// A stack's count from what was read at the cell's bottom right: digits alone, as the game writes a count. A
    /// slash is a magazine's load or an item's uses ("30/30", "2/3"), no digits is nothing written: both one item.
    /// Zero isn't a count either (the found-in-raid mark's circle can read as one).
    /// </summary>
    public static int Count(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Contains('/'))
            return 1;
        var digits = new string(text.Where(char.IsDigit).ToArray());
        var letters = text.Count(char.IsLetter);
        // Letters among the digits are the art or an icon read as words, not a count.
        if (digits.Length == 0 || letters > 0)
            return 1;
        return digits.Length > 4 ? MostCount : Math.Clamp(int.Parse(digits), 1, MostCount);
    }

    // 1 for the same text, less by the share of letters to change (Levenshtein), as ExitList does.
    internal static double Similarity(string a, string b)
    {
        if (a == b)
            return 1;
        if (a.Length == 0 || b.Length == 0)
            return 0;
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            previous[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1], previous[j]) + 1, previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return 1 - (double)previous[b.Length] / Math.Max(a.Length, b.Length);
    }
}
