using System.Globalization;
using System.Text.RegularExpressions;

namespace Shturmap.Core.Quests;

/// <summary>One objective as the synopsis needs it.</summary>
/// <param name="Description">tarkov.dev's objective text, in English.</param>
/// <param name="Count">The objective's count field; 1 when it has none.</param>
/// <param name="Type">tarkov.dev's objective type ("shoot", "plantItem", …).</param>
/// <param name="HasPlace">The objective has a marker on the map shown: the map says where, so its place phrase can go.</param>
public sealed record SynopsisObjective(string Description, int Count, string? Type, bool HasPlace);

/// <summary>One objective's few words: a verb and what it acts on.</summary>
/// <param name="Fallback">The text starts with no known verb, so it is shown as written, less the map's name.</param>
public sealed record SynopsisPhrase(SynopsisObjective Source, string Verb, string Object, bool Fallback)
{
    public string Text => Object.Length == 0 ? Verb : Verb + " " + Object;
}

/// <summary>A quest's synopsis on one map: its phrases, joined, with consecutive ones that share a verb merged.</summary>
public sealed record SynopsisLine(string Text, IReadOnlyList<SynopsisPhrase> Phrases)
{
    public bool HasFallback => Phrases.Any(p => p.Fallback);
}

/// <summary>
/// What a quest asks on a map in a few words, made from tarkov.dev's objective texts without any language model
/// (owner, 2026-10-02), so a new or changed quest gets its line from whatever data is loaded. It only subtracts: it
/// replaces a known verb phrase from a fixed table ("Locate and obtain" becomes "Get"), removes known filler (articles,
/// the map's own name, "with an MS2000 Marker", "at the specified spot", and the place of an objective whose marker is
/// on the map) and adds the objective's count. Conditions stay word for word. A reworded text gives a longer line,
/// never a wrong one; a text with no known verb is shown as written, less the map's name. See docs/DESIGN.md.
/// </summary>
public static partial class QuestSynopsis
{
    // Known verb phrases at the start of a text, longest first, and what they become. The right-hand side is the
    // phrase's verb, which also decides what merges.
    private static readonly (string From, string To)[] Verbs =
    [
        ("Locate and obtain", "Get"),
        ("Locate and neutralize", "Kill"),
        ("Locate and eliminate", "Kill"),
        ("Locate and mark", "Mark"),
        ("Locate and scout", "Scout"),
        ("Locate", "Find"),
        ("Obtain the item:", "Get"),
        ("Obtain", "Get"),
        ("Find the items in raid:", "FIR"),
        ("Find the item in raid:", "FIR"),
        ("Find the item in raid", "FIR"),
        ("Find in raid:", "FIR"),
        ("Eliminate", "Kill"),
        ("Survive and extract through", "Extract through"),
        ("Survive and extract", "Survive and extract"),
        ("Use the transit to", "Transit to"),
        ("Gain access to", "Gain access to"),
        ("Find out", "Find out"),
        ("Set up", "Set up"),
        ("Go to", "Go to"),
        ("Return to", "Return to"),
    ];

    // Verbs that stay as written.
    private static readonly HashSet<string> PlainVerbs = new(StringComparer.Ordinal)
    {
        "Access", "Build", "Check", "Claim", "Complete", "Determine", "Extract", "Find", "Fix", "Gain", "Hide", "Install",
        "Investigate", "Launch", "Mark", "Place", "Plant", "Play", "Reflash", "Repair", "Scout", "Search", "Secure",
        "Shoot", "Stash", "Take", "Unravel", "Use", "Visit", "Win",
    };

    // Terms the table shortens wherever they stand.
    private static readonly (string From, string To)[] Terms = [("PMC operatives", "PMCs")];

    /// <summary>Words the synopsis may say that the text doesn't: the table's verbs and terms.</summary>
    public static IReadOnlySet<string> TableWords { get; } =
        Verbs.Select(v => v.To).Concat(Terms.Select(t => t.To))
            .SelectMany(Words).ToHashSet(StringComparer.OrdinalIgnoreCase);

    // Words that, wherever the text has them, make a condition the line must keep.
    private static readonly string[] ConditionWords =
        ["excluding", "except", "without", "only", "not", "while", "using", "wearing", "during", "in one raid", "headshot", "headshots"];

    private static readonly Dictionary<string, int> NumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5, ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10,
    };

    // A line must not stop on one of these.
    private static readonly HashSet<string> Dangling = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "of", "in", "on", "at", "to", "for", "with", "by", "from", "and", "or", "near", "inside", "behind",
        "under", "next", "into", "onto", "through", "while", "without", "using", "wearing", "any", "its", "their",
    };

    /// <summary>The synopsis of one quest's objectives on a map, in the quest's order.</summary>
    /// <param name="mapNamesHere">Names of the map shown and its variants ("Ground Zero", "Ground Zero 21+").</param>
    /// <param name="allMapNames">Every map's name, to recognise lists like "on Woods, Ground Zero, or Customs".</param>
    public static SynopsisLine Of(IEnumerable<SynopsisObjective> objectives, IReadOnlyCollection<string> mapNamesHere, IReadOnlyCollection<string> allMapNames)
    {
        var list = objectives.Where(o => !string.IsNullOrWhiteSpace(o.Description)).ToList();
        var mapList = MapList(mapNamesHere, allMapNames);
        var phrases = list.Select(o => Phrase(o, mapList, shortForm: true)).ToList();
        // Two objectives that would read the same keep their places, which tell them apart (Spotter's two sniping
        // positions).
        for (var i = 0; i < phrases.Count; i++)
        {
            var same = Enumerable.Range(0, phrases.Count).Where(j => j != i && string.Equals(phrases[j].Text, phrases[i].Text, StringComparison.OrdinalIgnoreCase)).ToList();
            if (same.Count == 0)
                continue;
            foreach (var j in same.Append(i))
                phrases[j] = Phrase(list[j], mapList, shortForm: false);
        }
        return new SynopsisLine(Join(phrases), phrases);
    }

    /// <summary>Phrases in order, consecutive ones with the same verb merged ("Mark first LAV III, Stryker"), each said once.</summary>
    public static string Join(IEnumerable<SynopsisPhrase> phrases)
    {
        var parts = new List<(string Verb, List<string> Objects)>();
        foreach (var p in phrases)
        {
            if (parts.Count > 0 && parts[^1].Verb == p.Verb && !p.Fallback)
            {
                if (!parts[^1].Objects.Contains(p.Object, StringComparer.OrdinalIgnoreCase))
                    parts[^1].Objects.Add(p.Object);
                continue;
            }
            if (parts.Any(q => q.Verb == p.Verb && q.Objects.Count == 1 && q.Objects[0] == p.Object))
                continue;
            parts.Add((p.Verb, [p.Object]));
        }
        return string.Join(" · ", parts.Select(p =>
        {
            var objects = Shared(p.Objects.Where(o => o.Length > 0).ToList());
            return objects.Length == 0 ? p.Verb : p.Verb + " " + objects;
        }));
    }

    // Merged objects say the words they all share once: "AK-50 body, handguard, barrel", "Knight, Big Pipe, Birdeye
    // (in one raid)", "PMCs ×10, Scavs ×20 with Light machine guns". Never across a count: "Scavs ×10 at old gas
    // station, new gas station" would read as ten in all.
    private static string Shared(List<string> objects)
    {
        if (objects.Count < 2)
            return string.Join(", ", objects);
        var words = objects.Select(Tokens).ToList();
        var shortest = words.Min(w => w.Count);
        var prefix = 0;
        while (prefix < shortest - 1 && words.All(w => w[prefix] == words[0][prefix]) && !words[0][prefix].StartsWith('×'))
            prefix++;
        var suffix = 0;
        while (suffix < shortest - 1 - prefix && words.All(w => w[^(suffix + 1)] == words[0][^(suffix + 1)]) && !words[0][^(suffix + 1)].StartsWith('×'))
            suffix++;
        // A shared ending is said once only after short names or numbers ("first, second yellow minibus", "Knight, Big
        // Pipe, Birdeye (in one raid)"); after longer phrases it would seem to belong to the last one alone.
        if (words.Any(w => w.Count - prefix - suffix > 3 || w.Skip(prefix).Take(w.Count - prefix - suffix).Any(Dangling.Contains)))
            suffix = 0;
        if (prefix == 0 && suffix == 0)
            return string.Join(", ", objects);
        var middles = words.Select(w => string.Join(' ', w.Skip(prefix).Take(w.Count - prefix - suffix)));
        return string.Join(' ', new[] { string.Join(' ', words[0].Take(prefix)), string.Join(", ", middles), string.Join(' ', words[0].TakeLast(suffix)) }
            .Where(s => s.Length > 0));
    }

    // Words, with a bracket kept whole: "(Black Pawn)" and "(White Pawn)" differ.
    private static List<string> Tokens(string text)
    {
        var tokens = new List<string>();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (tokens.Count > 0 && tokens[^1].Count(c => c == '(') > tokens[^1].Count(c => c == ')'))
                tokens[^1] += " " + word;
            else
                tokens.Add(word);
        }
        return tokens;
    }

    /// <summary>One objective's phrase. <paramref name="mapList"/> comes from <see cref="MapList"/>.</summary>
    internal static SynopsisPhrase Phrase(SynopsisObjective o, Regex? mapList, bool shortForm)
    {
        var text = Spaces().Replace(o.Description.Trim(), " ").TrimEnd('.');
        if (mapList is not null)
            text = Spaces().Replace(mapList.Replace(text, ""), " ").Trim();

        var verb = Verbs.FirstOrDefault(v => StartsWithPhrase(text, v.From));
        string rest;
        if (verb.From is not null)
        {
            rest = text[verb.From.Length..].Trim();
        }
        else
        {
            var first = text.Split(' ', 2);
            if (!PlainVerbs.Contains(first[0]))
                return new SynopsisPhrase(o, Count(text, o, shoot: false), "", true);
            verb = (first[0], first[0]);
            rest = first.Length > 1 ? first[1] : "";
        }

        foreach (var (from, to) in Terms)
            rest = rest.Replace(from, to, StringComparison.Ordinal);
        rest = Filler().Replace(rest, "");
        rest = Articles().Replace(rest, "");
        rest = Spaces().Replace(rest, " ").Trim();
        var shoot = o.Type == "shoot";
        if (shortForm && o.HasPlace && !shoot)
            rest = WithoutPlace(rest);
        return new SynopsisPhrase(o, verb.To, Count(rest, o, shoot), false);
    }

    /// <summary>
    /// The objective's count, from its count field: as written when the text says the same number, left out when the
    /// text says another (one of them is wrong), else added as "×N" (before a kill's conditions, so it survives
    /// shortening; at the end otherwise, as the game shows it).
    /// </summary>
    private static string Count(string rest, SynopsisObjective o, bool shoot)
    {
        var m = LeadingNumber().Match(rest);
        if (m.Success)
        {
            var said = int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n
                : NumberWords.GetValueOrDefault(m.Groups[1].Value);
            if (said == o.Count)
            {
                return rest;
            }
            return rest[m.Length..];
        }
        if (o.Count <= 1)
            return rest;
        var count = "×" + o.Count.ToString(CultureInfo.InvariantCulture);
        if (rest.Length == 0)
            return count;
        if (shoot)
        {
            var at = ConditionStart().Match(rest);
            if (at.Success && at.Index > 0)
                return rest[..at.Index] + " " + count + rest[at.Index..];
        }
        return rest + " " + count;
    }

    // Cuts the place an objective is at ("in dorm room 203"), with a participle before it ("hidden next to the
    // breakwater"), when the map marks it. Not when the place holds a condition, or the rest would be one word
    // ("Stash package") or end mid-phrase.
    private static string WithoutPlace(string rest)
    {
        var m = Place().Match(rest);
        if (!m.Success || m.Index == 0)
            return rest;
        var cut = rest[m.Index..];
        if (cut.Contains('(') || ConditionWords.Any(w => ContainsWord(cut, w)))
            return rest;
        var kept = rest[..m.Index].Trim();
        var words = Words(kept).ToList();
        if (words.Count < 2 || Dangling.Contains(words[^1]))
            return rest;
        return kept;
    }

    /// <summary>
    /// What is wrong with a phrase, if anything: a word that is in neither its text nor the table, a condition of its
    /// text left out, a count other than the objective's, or an ending mid-phrase. Empty when it is right.
    /// </summary>
    public static IReadOnlyList<string> Problems(SynopsisPhrase phrase)
    {
        var problems = new List<string>();
        var text = phrase.Text;
        var source = phrase.Source.Description;
        if (text.Length == 0)
            return ["empty"];
        var known = Words(source).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var word in Words(text))
        {
            if (known.Contains(word) || TableWords.Contains(word))
                continue;
            if (word.StartsWith('×') && word[1..] == phrase.Source.Count.ToString(CultureInfo.InvariantCulture))
                continue;
            problems.Add($"word \"{word}\" is not in the text");
        }
        foreach (var condition in ConditionWords.Where(w => ContainsWord(source, w)))
        {
            if (!ContainsWord(text, condition))
                problems.Add($"condition \"{condition}\" left out");
        }
        foreach (Match m in Parenthetical().Matches(source))
        {
            if (!text.Contains(m.Value, StringComparison.OrdinalIgnoreCase))
                problems.Add($"\"{m.Value}\" left out");
        }
        var last = Words(text).LastOrDefault();
        if (last is not null && Dangling.Contains(last) || text.EndsWith(',') || text.EndsWith(':') || text.EndsWith('-'))
            problems.Add("ends mid-phrase");
        return problems;
    }

    /// <summary>The problems of every phrase, and of the joined line: a merged item that stops mid-phrase.</summary>
    public static IReadOnlyList<string> Problems(SynopsisLine line)
    {
        var problems = line.Phrases.SelectMany(p => Problems(p).Select(x => $"{x}: \"{p.Source.Description}\"")).ToList();
        foreach (var piece in line.Text.Split([" · ", ", "], StringSplitOptions.None))
        {
            if (Words(piece).LastOrDefault() is { } last && Dangling.Contains(last))
                problems.Add($"\"{piece}\" ends mid-phrase");
        }
        return problems;
    }

    /// <summary>
    /// A pattern for the map's own name and lists that hold it, where a text in <paramref name="language"/> (the data's)
    /// names them as the place: "on Woods, Ground Zero, or Customs"; German "auf Woods, Ground Zero oder Customs" (owner,
    /// 2026-10-11). Null for a language it has no pattern for.
    /// </summary>
    public static Regex? MapList(IReadOnlyCollection<string> mapNamesHere, IReadOnlyCollection<string> allMapNames, string language = UiLanguage.English)
    {
        if (mapNamesHere.Count == 0)
            return null;
        static string Alternatives(IEnumerable<string> names) =>
            string.Join("|", names.Where(n => n.Length > 0).Distinct().OrderByDescending(n => n.Length).Select(Regex.Escape));
        var here = Alternatives(mapNamesHere);
        var any = Alternatives(allMapNames.Concat(mapNamesHere));
        string place, separator, after;
        switch (language)
        {
            case UiLanguage.English:
                place = "on|in|from|at";
                separator = @"(?:,\s+(?:or\s+|and\s+)?|\s+(?:or|and)\s+)";
                // Only where the name ends the place: "at Factory gate" is a gate, not the map.
                after = @"(?=$|[,.;)]|\s*\(|\s+(?:through|with|while|using|wearing|without|during|to|and|in|on|at|from|for|by|before|after)\b)";
                break;
            case UiLanguage.German:
                // "auf" and "in" say where; "nach", "aus" and "von" say where to or from (owner, 2026-10-11).
                place = "auf|in";
                separator = @"(?:,\s+(?:oder\s+|und\s+)?|\s+(?:oder|und)\s+)";
                // German writes every noun with a capital, so a word in lower case after the name ends the place,
                // whether it leads on ("mit", "während") or ends the clause ("… auf Woods ab", "… in Customs versteckt
                // ist"); a noun would make the name part of another place, as "at Factory gate" does. Nor is a name the
                // place where it only begins a longer map's: "auf Factory bei Nacht" isn't on Factory.
                var names = allMapNames.Concat(mapNamesHere).Where(n => n.Length > 0).Distinct().ToList();
                var longer = Alternatives(names.SelectMany(n => names
                    .Where(m => m.Length > n.Length && m.StartsWith(n, StringComparison.OrdinalIgnoreCase) && !char.IsLetterOrDigit(m[n.Length]))
                    .Select(m => m[n.Length..])));
                after = @"(?=$|[,.;)]|\s*\(|\s+(?-i:\p{Ll}))" + (longer.Length > 0 ? $"(?!{longer})" : "");
                break;
            default:
                return null;
        }
        return new Regex($@"\s+(?:{place})\s+(?:(?:{any}){separator})*(?:{here})(?:{separator}(?:{any}))*{after}",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool StartsWithPhrase(string text, string phrase) =>
        text.StartsWith(phrase, StringComparison.Ordinal) && (text.Length == phrase.Length || text[phrase.Length] == ' ' || phrase.EndsWith(':'));

    private static bool ContainsWord(string text, string word) =>
        Regex.IsMatch(text, $@"(?<![\w-]){Regex.Escape(word)}(?![\w-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>The words of a text, without the punctuation around them.</summary>
    public static IEnumerable<string> Words(string text) =>
        text.Split([' ', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim(',', '.', ':', ';', '·', '(', ')', '"', '“', '”', '!', '?'))
            .Where(w => w.Length > 0);

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Spaces();

    // Lowercase articles; "the" after "of" stays ("one of the dorm rooms"), capitalised ones are names (The Goons).
    [GeneratedRegex(@"(?<=^|\s)(?<!\bof\s)(?:the|a|an)\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Articles();

    // The marker is in the row's bring cells; the spot is on the map; "the location" is the map shown.
    [GeneratedRegex(@"(?:^|\s+)(?:with an? MS2000 [Mm]arker|(?:at|in|on|to) the (?:specified|designated) (?:spot|place|location)|from the location)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex Filler();

    [GeneratedRegex(@"^(\d+|one|two|three|four|five|six|seven|eight|nine|ten)\s+(?!of\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeadingNumber();

    // Where a kill's conditions start: its count goes before them.
    [GeneratedRegex(@"\s+(?:with|while|using|from|during|in|at|on|inside|without|near|around|along|within)\s|\s+\(", RegexOptions.CultureInvariant)]
    private static partial Regex ConditionStart();

    // Not "on" or "by": "information on Ref", "on the fate of" are what to get, not where.
    [GeneratedRegex(@"(?:\s+(?:hidden|located|placed|left|stored|kept|situated|lying))?\s+(?:in|at|inside|behind|near|under|next to|on top of|in front of|from|within|beneath|underneath|outside|between|around|along)\s+",
        RegexOptions.CultureInvariant)]
    private static partial Regex Place();

    [GeneratedRegex(@"\([^)]*\)")]
    private static partial Regex Parenthetical();
}
