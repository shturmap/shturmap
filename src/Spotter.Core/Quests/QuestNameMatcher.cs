using System.Globalization;
using System.Text;

namespace Spotter.Core.Quests;

/// <param name="LocationNames">Names of the maps the quest happens on; empty for quests that can be done anywhere.</param>
public sealed record QuestCandidate(string Id, string Name, IReadOnlyList<string> LocationNames);

public enum MatchVerdict
{
    /// <summary>Exact or near-exact, and clearly better than any other quest.</summary>
    Accepted,

    /// <summary>Probably right; shown for one-click confirmation.</summary>
    NeedsConfirmation,

    /// <summary>No quest is close enough; shown as an unread row.</summary>
    Unread,
}

public sealed record QuestMatch(string RowName, QuestCandidate? Quest, double Score, MatchVerdict Verdict, QuestCandidate? RunnerUp);

/// <summary>
/// Matches a quest name read from the Tasks screen against the quest catalog. Lessons kept from TarkovEyes:
/// a different number is a different quest ("Part 4" is never "Part 5"), and among equal scores the longer
/// name explains more of what was read.
/// </summary>
public static class QuestNameMatcher
{
    public const double AcceptAt = 0.92;
    public const double ConfirmAt = 0.75;
    private const double Margin = 0.04;

    public static QuestMatch Match(string rowName, string rowLocation, IReadOnlyList<QuestCandidate> candidates)
    {
        var name = Normalize(rowName);
        var location = Normalize(rowLocation);
        QuestCandidate? best = null, second = null;
        double bestScore = 0, secondScore = 0;

        foreach (var candidate in candidates)
        {
            var score = Score(name, location, candidate);
            if (score > bestScore || (score == bestScore && best is not null && candidate.Name.Length > best.Name.Length))
            {
                if (best is not null && best.Name != candidate.Name)
                    (second, secondScore) = (best, bestScore);
                (best, bestScore) = (candidate, score);
            }
            else if (score > secondScore && best?.Name != candidate.Name)
            {
                (second, secondScore) = (candidate, score);
            }
        }

        var verdict = bestScore >= AcceptAt && bestScore - secondScore >= Margin ? MatchVerdict.Accepted
            : bestScore >= ConfirmAt ? MatchVerdict.NeedsConfirmation
            : MatchVerdict.Unread;
        return new QuestMatch(rowName, verdict == MatchVerdict.Unread ? null : best, Math.Round(bestScore, 3), verdict, second);
    }

    internal static double Score(string normalizedName, string normalizedLocation, QuestCandidate candidate)
    {
        var target = Normalize(candidate.Name);
        if (target.Length == 0 || normalizedName.Length == 0)
            return 0;

        // Letters OCR confuses in the game's font compare equal once folded, but never count as fully exact.
        var score = Math.Max(Similarity(normalizedName, target), Math.Min(FoldedCap, Similarity(Fold(normalizedName), Fold(target))));
        if (!Digits(normalizedName).SequenceEqual(Digits(target)))
            score = Math.Min(score, 0.5);

        if (normalizedLocation.Length > 0 && candidate.LocationNames.Count > 0)
        {
            var place = candidate.LocationNames.Max(l => Similarity(normalizedLocation, Normalize(l)));
            if (place >= 0.8)
                score = Math.Min(1, score + 0.03);
            else if (place < 0.5)
                score -= 0.15;
        }
        return Math.Max(0, score);
    }

    public static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text.Normalize(NormalizationForm.FormKC).ToLower(CultureInfo.InvariantCulture))
        {
            if (char.IsLetterOrDigit(c))
            {
                if (space && sb.Length > 0)
                    sb.Append(' ');
                sb.Append(c);
                space = false;
            }
            else
            {
                space = true;
            }
        }
        return sb.ToString();
    }

    private const double FoldedCap = 0.95;

    // Measured on EFT's Bender font with Windows OCR: "U" reads as "IJ"; the usual suspects are included too.
    private static readonly (string From, string To)[] Confusions =
    [
        ("ij", "u"), ("rn", "m"), ("vv", "w"), ("cl", "d"),
        ("0", "o"), ("1", "l"), ("i", "l"), ("|", "l"), ("5", "s"),
    ];

    internal static string Fold(string normalized)
    {
        var s = normalized;
        foreach (var (from, to) in Confusions)
            s = s.Replace(from, to, StringComparison.Ordinal);
        return s;
    }

    private static IEnumerable<string> Digits(string s)
    {
        var current = new StringBuilder();
        foreach (var c in s)
        {
            if (char.IsDigit(c))
            {
                current.Append(c);
            }
            else if (current.Length > 0)
            {
                yield return current.ToString();
                current.Clear();
            }
        }
        if (current.Length > 0)
            yield return current.ToString();
    }

    private static double Similarity(string a, string b)
    {
        if (a == b)
            return 1;
        var max = Math.Max(a.Length, b.Length);
        return max == 0 ? 1 : 1 - (double)Levenshtein(a, b) / max;
    }

    private static int Levenshtein(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            previous[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }
}
