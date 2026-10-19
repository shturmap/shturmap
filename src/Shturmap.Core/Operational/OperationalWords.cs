using System.Globalization;
using System.Text.RegularExpressions;

namespace Shturmap.Core.Operational;

/// <summary>
/// What an operational task asks, from the English game's objective words (owner, 2026-10-19: from a panel of four, B;
/// docs/DESIGN.md §4, "Screen anatomy", *Operational tasks*): its kind, for the glyph, and a synopsis in a few words,
/// for the row ("Kill 3 Scavs · headshots over 50 m", "Survive and extract", "Find in raid: SSD drive, CPU fan"). A
/// line of no known kind is shown as read. Only subtracts and reorders: a condition it doesn't know stays as written.
/// </summary>
public static partial class OperationalWords
{
    /// <summary>The task's kind: the first objective whose words say one; a row whose objectives weren't read (not
    /// expanded on any screenshot) by its name's words ("Elimination", "Exit the location", "Find and transfer").</summary>
    public static OperationalKind KindOf(IReadOnlyList<OperationalObjective> objectives, string name)
    {
        foreach (var objective in objectives)
        {
            if (KindOfText(objective.Text) is var kind and not OperationalKind.Unknown)
                return kind;
        }
        return objectives.Count == 0 ? KindOfText(name) : OperationalKind.Unknown;
    }

    internal static OperationalKind KindOfText(string text)
    {
        var t = text.Trim().ToLowerInvariant();
        if (t.StartsWith("eliminat", StringComparison.Ordinal) || t.StartsWith("kill", StringComparison.Ordinal)
            || t.StartsWith("neutraliz", StringComparison.Ordinal) || t.Contains("elimination", StringComparison.Ordinal))
            return OperationalKind.Kill;
        if (t.Contains("found in raid", StringComparison.Ordinal) || t.Contains("find in raid", StringComparison.Ordinal)
            || t.StartsWith("hand over", StringComparison.Ordinal) || t.Contains("transfer", StringComparison.Ordinal))
            return OperationalKind.HandOver;
        if (t.StartsWith("survive", StringComparison.Ordinal) || t.StartsWith("exit", StringComparison.Ordinal)
            || t.Contains("extract", StringComparison.Ordinal))
            return OperationalKind.Survive;
        return OperationalKind.Unknown;
    }

    /// <summary>
    /// The task in a few words: its objectives' phrases, the hand-overs' items in one ("Find in raid: SSD drive, CPU
    /// fan"); a row whose objectives weren't read, its name.
    /// </summary>
    public static string Synopsis(OperationalTask task)
    {
        if (task.Objectives.Count == 0)
            return task.Name;
        var phrases = new List<string>();
        var items = new List<string>();
        foreach (var objective in task.Objectives)
        {
            switch (KindOfText(objective.Text))
            {
                case OperationalKind.HandOver:
                    items.AddRange(HandedOver(objective.Text));
                    break;
                case OperationalKind.Kill:
                    phrases.Add(Kill(objective));
                    break;
                case OperationalKind.Survive:
                    phrases.Add(CoreTexts.OperationalSurvive);
                    break;
                default:
                    phrases.Add(objective.Text);
                    break;
            }
        }
        if (items.Count > 0)
            phrases.Insert(0, CoreTexts.OperationalFindInRaid(items: string.Join(", ", items.Distinct(StringComparer.OrdinalIgnoreCase))));
        return string.Join("; ", phrases.Distinct(StringComparer.Ordinal));
    }

    /// <summary>The items a hand-over names: the quoted names ("Hand over the found in raid item: "SSD drive""), else
    /// what follows its colon.</summary>
    public static IReadOnlyList<string> HandedOver(string text)
    {
        var quoted = Quoted().Matches(text).Select(m => m.Groups[1].Value.Trim()).Where(n => n.Length > 0).ToList();
        if (quoted.Count > 0)
            return quoted;
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        return colon >= 0 && text[(colon + 1)..].Trim() is { Length: > 0 } rest ? [rest] : [];
    }

    // "Eliminate Scavs with a headshot from a distance of more than 50m", 0/3: "Kill 3 Scavs · headshots over 50 m".
    // The target is what follows the verb up to the first condition; the conditions it knows go short, the place ("on
    // the location") goes, and anything else stays as written.
    private static string Kill(OperationalObjective objective)
    {
        var m = KillPattern().Match(objective.Text.Trim());
        if (!m.Success)
            return objective.Text;
        var target = m.Groups["target"].Value.Trim();
        var rest = m.Groups["rest"].Value.Trim();
        var conditions = new List<string>();
        if (Headshot().IsMatch(rest))
        {
            conditions.Add(CoreTexts.OperationalHeadshots);
            rest = Headshot().Replace(rest, " ");
        }
        if (Farther().Match(rest) is { Success: true } far)
        {
            conditions.Add(CoreTexts.OperationalOver(metres: far.Groups["m"].Value));
            rest = Farther().Replace(rest, " ");
        }
        else if (Nearer().Match(rest) is { Success: true } near)
        {
            conditions.Add(CoreTexts.OperationalUnder(metres: near.Groups["m"].Value));
            rest = Nearer().Replace(rest, " ");
        }
        rest = Place().Replace(rest, " ");
        rest = Filler().Replace(rest, " ").Trim();
        if (rest.Any(char.IsLetter))
            conditions.Add(rest);
        var kill = objective.Of is { } of and > 0
            ? CoreTexts.OperationalKill(count: of.ToString(CultureInfo.InvariantCulture), target: target)
            : CoreTexts.OperationalKillUncounted(target: target);
        return conditions.Count == 0 ? kill : kill + " · " + string.Join(" ", conditions);
    }

    [GeneratedRegex(@"[""“”„]([^""“”„]+)[""“”„]")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"^(?:eliminate|kill|neutralize)\s+(?<target>.+?)(?<rest>\s+(?:with|from|while|using|on|in|at|wearing|without|during|by)\b.*)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KillPattern();

    [GeneratedRegex(@"(?:with\s+)?(?:an?\s+)?head\s?shots?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Headshot();

    [GeneratedRegex(@"(?:from\s+)?(?:a\s+)?(?:distance\s+of\s+)?(?:more|further|farther|greater)\s+than\s+(?<m>\d+)\s?m(?:eters|etres)?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Farther();

    [GeneratedRegex(@"(?:from\s+)?(?:a\s+)?(?:distance\s+of\s+)?(?:less\s+than|closer\s+than|within|under)\s+(?<m>\d+)\s?m(?:eters|etres)?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Nearer();

    [GeneratedRegex(@"\b(?:on|in|at)\s+(?:the\s+)?location\b|\bon\s+[A-Z][\w']*(?:\s+(?:of\s+)?[A-Z][\w']*)*", RegexOptions.CultureInvariant)]
    private static partial Regex Place();

    [GeneratedRegex(@"^(?:\s*\b(?:with|from|a|an|the|and)\b)+|(?:\b(?:with|from|a|an|the|and)\b\s*)+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Filler();
}
