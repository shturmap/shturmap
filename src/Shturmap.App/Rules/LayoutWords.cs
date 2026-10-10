#if DEVTOOLS
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Shturmap.App.Rules;

/// <summary>
/// The layout check's words (developer builds; docs/LANGUAGES.md, "Layout check"): in the pseudo-language every text of
/// Shturmap's own is in brackets, "[Ŝéţţîñğš ···]", so what is left outside them on screen is a text that isn't in the
/// texts files yet, unless it stays the same in every language: a game name, a number with its unit, a key, a path, or
/// one of the few words on <see cref="Neutral"/>.
/// </summary>
public static partial class LayoutWords
{
    /// <summary>
    /// Words that are the same in every language and aren't game names: the brand, the sides and modes, and the names
    /// of the places Shturmap talks about. Kept short on purpose: a word that belongs in a texts file must not hide here.
    /// </summary>
    public static readonly IReadOnlyList<string> Neutral =
        ["Shturmap", "PMC", "Scav", "PvE", "PvP", "Escape from Tarkov", "tarkov.dev", "GitHub", "Sentry", "Windows"];

    private static readonly HashSet<string> NeutralKeys = new(Neutral.Select(Key), StringComparer.Ordinal);

    // Key names as Shturmap writes them (help, the tour): the key itself is no translation's business.
    private static readonly HashSet<string> KeyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Esc", "PrtSc", "PrtScn", "Print", "Ctrl", "Shift", "Alt", "Tab", "Enter", "Space", "Home", "End", "PgUp", "PgDn",
        "Del", "Ins", "Win", "Backspace",
    };

    // Units, when they follow a number ("214 m", "12 min").
    private static readonly HashSet<string> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        "m", "km", "min", "s", "h", "ms", "px", "%", "MB", "GB", "KB", "₽", "$", "€", "x", "×",
    };

    // A text of the pseudo-language (PseudoText.Of): in brackets, its letters accented, padded with "·" before "]".
    [GeneratedRegex(@"\[[^\[\]]*·\]")]
    private static partial Regex PseudoPart();

    [GeneratedRegex(@"\s*(?:[|,;:()\[\]""“”„«»…→←↑↓↗/\n]+|\s[-–—]\s)\s*")]
    private static partial Regex Separators();

    [GeneratedRegex(@"^[+\-−–]?[\d.,:]*\d[\d.,:]*[a-zA-Z%₽$€×]{0,3}$")]
    private static partial Regex Number();

    [GeneratedRegex(@"^v?\d+(?:\.\d+)+(?:[+\-][\w.\-]+)?$")]
    private static partial Regex Version();

    [GeneratedRegex(@"^F(?:[1-9]|1\d|2[0-4])$")]
    private static partial Regex FunctionKey();

    [GeneratedRegex(@"^[0-9a-f]{24}$")]
    private static partial Regex GameId();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    private static readonly char[] Around = ['.', '!', '?', '\'', '"', '‘', '’'];

    /// <summary>A name as the check compares it: trimmed of punctuation around it, one space between words, upper case.</summary>
    public static string Key(string text) => Spaces().Replace(text.Trim().Trim(Around), " ").Trim().ToUpperInvariant();

    /// <summary>
    /// The parts of a text shown in the pseudo-language that aren't the pseudo-language's: what is left outside its
    /// brackets once game names (<paramref name="names"/>, as <see cref="Key"/> makes them), numbers with their units,
    /// key names, paths, the culture's month and day names and <see cref="Neutral"/> are taken out. Empty for a text
    /// that is all the pseudo-language's.
    /// </summary>
    public static IReadOnlyList<string> EnglishParts(string text, IReadOnlySet<string> names, CultureInfo culture)
    {
        if (!text.Any(char.IsLetter))
            return [];
        // A text inside another's placeholder is in brackets inside brackets: the inner ones go first.
        var rest = text;
        string before;
        do
        {
            before = rest;
            rest = PseudoPart().Replace(rest, " ");
        }
        while (rest != before);
        if (!rest.Any(char.IsLetter))
            return [];
        var dates = culture.DateTimeFormat;
        var calendar = new HashSet<string>(dates.MonthNames.Concat(dates.AbbreviatedMonthNames).Concat(dates.MonthGenitiveNames)
            .Concat(dates.AbbreviatedMonthGenitiveNames).Concat(dates.DayNames).Concat(dates.AbbreviatedDayNames)
            .Append(dates.AMDesignator).Append(dates.PMDesignator).Where(n => n.Length > 0).Select(Key), StringComparer.Ordinal);
        bool Known(string s) => Key(s) is var key && (key.Length == 0 || names.Contains(key) || NeutralKeys.Contains(key) || calendar.Contains(key));
        var found = new List<string>();
        // "·" first: it joins names and figures in a line ("CUSTOMS · PMC · 12 MIN"), and a name may hold the other
        // separators ("Klimov Street (Flare)").
        foreach (var piece in rest.Split('·'))
        {
            if (!piece.Any(char.IsLetter) || Known(piece))
                continue;
            foreach (var segment in Separators().Split(piece))
            {
                if (!segment.Any(char.IsLetter) || Known(segment))
                    continue;
                var words = segment.Split([' ', '\t', ' ', ' '], StringSplitOptions.RemoveEmptyEntries);
                // Each word: part of a name of several words ("Streets of Tarkov"), a figure or key, a name of one
                // word ("Kaban"), or English. A name of one word counts only among names and figures ("Kaban 75%"):
                // among English words it is one of them, since many are ("Map", "Raid" and "Report" are items).
                var english = new bool[words.Length];
                var single = new bool[words.Length];
                for (var i = 0; i < words.Length;)
                {
                    var matched = 0;
                    for (var n = Math.Min(6, words.Length - i); n >= 2 && matched == 0; n--)
                    {
                        if (Known(string.Join(' ', words, i, n)))
                            matched = n;
                    }
                    if (matched > 0)
                    {
                        i += matched;
                        continue;
                    }
                    if (!IsNeutralWord(words[i], i > 0 ? words[i - 1] : null))
                    {
                        if (Known(words[i]))
                            single[i] = true;
                        else
                            english[i] = true;
                    }
                    i++;
                }
                if (english.Any(e => e))
                    found.Add(string.Join(' ', words.Where((_, i) => english[i] || single[i])));
            }
        }
        return found;
    }

    // A word that needs no translation: no letters, a number (with its unit), a unit after a number, a version, a path,
    // a key or keys pressed together ("Shift+F"); a single letter counts as a key ("F shows you", "O twice").
    private static bool IsNeutralWord(string word, string? before)
    {
        var w = word.Trim(Around);
        if (!w.Any(char.IsLetter) || Number().IsMatch(w) || Version().IsMatch(w))
            return true;
        if (Units.Contains(w) && before is not null && Number().IsMatch(before.Trim(Around).Trim(',')))
            return true;
        if (w.Contains('\\') || w.Contains("://", StringComparison.Ordinal) || w.Contains('%'))
            return true;
        return w.Split('+', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } keys
               && keys.All(k => k.Length == 1 || KeyNames.Contains(k) || FunctionKey().IsMatch(k));
    }

    // The properties of the data whose texts the app shows as they are: names and tarkov.dev's sentences. Not the
    // internal words beside them (an objective's type "visit", a kill target "Any"), which could hide an English word,
    // nor the items' short names, which only the map draws and many of which are English words ("Log", "Data", "OR").
    private static readonly HashSet<string> ShownProperties = new(StringComparer.Ordinal) { "Name", "Description" };

    private static readonly HashSet<string> SkippedProperties = new(StringComparer.Ordinal) { "ItemShortNames", "ExtractKeys", "ObjectiveFacts" };

    /// <summary>
    /// The names in a graph of Shturmap's data objects (the game data: maps, extracts, transits, quests and their
    /// objectives, traders, bosses, items), as <see cref="Key"/> makes them: what the app shows of the game data stays
    /// as tarkov.dev gives it. Texts are taken from properties named Name or Description and from
    /// dictionaries of texts (item names by id); ids (24 hex digits) and texts without letters are left out.
    /// </summary>
    public static HashSet<string> NamesIn(object root)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        void Add(object? value)
        {
            if (value is string text && text.Any(char.IsLetter) && !GameId().IsMatch(text))
                names.Add(Key(text));
        }
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<(object Value, int Depth)>();
        pending.Push((root, 0));
        while (pending.Count > 0)
        {
            var (value, depth) = pending.Pop();
            var type = value.GetType();
            if (depth > 10 || value is string || type.IsPrimitive || type.IsEnum || !seen.Add(value))
                continue;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var entry = type.GetProperty("Value")?.GetValue(value);
                Add(entry);
                if (entry is { } inner and not string)
                    pending.Push((inner, depth + 1));
                continue;
            }
            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    Add(entry.Value);
                    if (entry.Value is { } inner and not string)
                        pending.Push((inner, depth + 1));
                }
                continue;
            }
            if (value is IEnumerable items)
            {
                foreach (var item in items)
                {
                    if (item is not null and not string)
                        pending.Push((item, depth + 1));
                }
                continue;
            }
            if (type.Namespace?.StartsWith("Shturmap", StringComparison.Ordinal) != true)
                continue;
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0 || property.PropertyType.IsPrimitive || property.PropertyType.IsEnum
                    || SkippedProperties.Contains(property.Name))
                    continue;
                object? v;
                try
                {
                    v = property.GetValue(value);
                }
                catch (TargetInvocationException)
                {
                    continue;
                }
                if (v is string)
                {
                    if (ShownProperties.Contains(property.Name))
                        Add(v);
                }
                else if (v is not null)
                {
                    pending.Push((v, depth + 1));
                }
            }
        }
        return names;
    }
}
#endif
