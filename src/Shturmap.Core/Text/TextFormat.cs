using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Shturmap.Core.Text;

/// <summary>
/// Fills in a text's placeholders: Shturmap's texts are whole sentences with named placeholders, never pieces glued
/// together, so each language can put the words where its grammar wants them (docs/DESIGN.md §8, "Texts"). The
/// syntax is the part of ICU's MessageFormat translators know:
/// <list type="bullet">
/// <item><c>{map}</c>: the value, numbers in the language's format.</item>
/// <item><c>{count, plural, one {# extract} other {# extracts}}</c>: the branch of the number's plural category
/// (<see cref="PluralRules"/>), <c>#</c> the number; <c>=0 {…}</c> matches one number exactly, before the categories.</item>
/// <item><c>{side, select, pmc {…} scav {…} other {…}}</c>: the branch named by the value.</item>
/// </list>
/// Braces are always syntax: a text can't show one. A text that doesn't parse is shown as it is written, so a broken
/// translation can't stop the app (the translation tests catch it first).
/// </summary>
public static class TextFormat
{
    private static readonly ConcurrentDictionary<string, Node[]?> Parsed = new(StringComparer.Ordinal);

    /// <summary>The text with its placeholders filled, in the language in use (<see cref="UiLanguage"/>).</summary>
    public static string Format(string pattern, params ReadOnlySpan<(string Name, object? Value)> args) =>
        Format(UiLanguage.Culture, pattern, args);

    /// <summary>The text with its placeholders filled, numbers and plural forms as in <paramref name="culture"/>.</summary>
    public static string Format(CultureInfo culture, string pattern, params ReadOnlySpan<(string Name, object? Value)> args)
    {
        if (Parse(pattern) is not { } nodes)
            return pattern;
        var values = new Dictionary<string, object?>(args.Length, StringComparer.Ordinal);
        foreach (var (name, value) in args)
            values[name] = value;
        var text = new StringBuilder(pattern.Length + 16);
        Write(text, nodes, values, culture, hash: null);
        return text.ToString();
    }

    /// <summary>The names of a text's placeholders, in order of first use; null when the text doesn't parse.</summary>
    public static IReadOnlyList<string>? Arguments(string pattern)
    {
        if (Parse(pattern) is not { } nodes)
            return null;
        var names = new List<string>();
        Collect(nodes, names);
        return names;
    }

    /// <summary>The branch keys of each plural placeholder ("count" → one, other); null when the text doesn't parse.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>>? PluralBranches(string pattern)
    {
        if (Parse(pattern) is not { } nodes)
            return null;
        var plurals = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        CollectPlurals(nodes, plurals);
        return plurals;
    }

    /// <summary>Why a text doesn't parse, or null when it does.</summary>
    public static string? Problem(string pattern)
    {
        try
        {
            new Parser(pattern).Message(nested: false);
            return null;
        }
        catch (FormatException e)
        {
            return e.Message;
        }
    }

    /// <summary>The text with every literal part changed by <paramref name="change"/>, its placeholders kept as written.</summary>
    internal static string MapLiterals(string pattern, Func<string, string> change)
    {
        if (Parse(pattern) is not { } nodes)
            return change(pattern);
        var text = new StringBuilder(pattern.Length * 2);
        Unparse(text, nodes, change);
        return text.ToString();
    }

    private static Node[]? Parse(string pattern) => Parsed.GetOrAdd(pattern, static p =>
    {
        try
        {
            return new Parser(p).Message(nested: false);
        }
        catch (FormatException)
        {
            return null;
        }
    });

    private static void Write(StringBuilder text, Node[] nodes, Dictionary<string, object?> values, CultureInfo culture, object? hash)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case Literal literal:
                    text.Append(literal.Text);
                    break;
                case Hash:
                    text.Append(hash is null ? "#" : Show(hash, culture));
                    break;
                case Argument { Branches: null } argument:
                    text.Append(values.TryGetValue(argument.Name, out var value) ? Show(value, culture) : "{" + argument.Name + "}");
                    break;
                case Argument { Plural: true } plural:
                    if (!values.TryGetValue(plural.Name, out var count) || !TryNumber(count, out var number))
                    {
                        text.Append('{').Append(plural.Name).Append('}');
                        break;
                    }
                    var exact = "=" + number.ToString(CultureInfo.InvariantCulture);
                    var category = PluralRules.Category(culture.TwoLetterISOLanguageName, number);
                    if ((Branch(plural, exact) ?? Branch(plural, category) ?? Branch(plural, "other")) is { } branch)
                        Write(text, branch, values, culture, count);
                    break;
                case Argument select:
                    var key = values.TryGetValue(select.Name, out var chosen) ? Convert.ToString(chosen, CultureInfo.InvariantCulture) : null;
                    if (((key is null ? null : Branch(select, key)) ?? Branch(select, "other")) is { } selected)
                        Write(text, selected, values, culture, hash);
                    break;
            }
        }
    }

    private static Node[]? Branch(Argument argument, string key)
    {
        foreach (var (name, body) in argument.Branches!)
        {
            if (name == key)
                return body;
        }
        return null;
    }

    private static string Show(object? value, CultureInfo culture) => value switch
    {
        null => "",
        IFormattable formattable => formattable.ToString(null, culture),
        _ => value.ToString() ?? "",
    };

    private static bool TryNumber(object? value, out decimal number)
    {
        switch (value)
        {
            case int or long or short or byte or uint or ulong or ushort or sbyte or decimal or double or float:
                number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return true;
            default:
                number = 0;
                return false;
        }
    }

    private static void Collect(Node[] nodes, List<string> names)
    {
        foreach (var node in nodes)
        {
            if (node is not Argument argument)
                continue;
            if (!names.Contains(argument.Name))
                names.Add(argument.Name);
            foreach (var (_, body) in argument.Branches ?? [])
                Collect(body, names);
        }
    }

    private static void CollectPlurals(Node[] nodes, Dictionary<string, IReadOnlyList<string>> plurals)
    {
        foreach (var node in nodes)
        {
            if (node is not Argument argument)
                continue;
            if (argument.Plural)
                plurals[argument.Name] = argument.Branches!.Select(b => b.Key).ToList();
            foreach (var (_, body) in argument.Branches ?? [])
                CollectPlurals(body, plurals);
        }
    }

    private static void Unparse(StringBuilder text, Node[] nodes, Func<string, string> change)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case Literal literal:
                    text.Append(change(literal.Text));
                    break;
                case Hash:
                    text.Append('#');
                    break;
                case Argument { Branches: null } argument:
                    text.Append('{').Append(argument.Name).Append('}');
                    break;
                case Argument argument:
                    text.Append('{').Append(argument.Name).Append(argument.Plural ? ", plural," : ", select,");
                    foreach (var (key, body) in argument.Branches)
                    {
                        text.Append(' ').Append(key).Append(" {");
                        Unparse(text, body, change);
                        text.Append('}');
                    }
                    text.Append('}');
                    break;
            }
        }
    }

    private abstract record Node;

    private sealed record Literal(string Text) : Node;

    private sealed record Hash : Node;

    // Branches null: a plain value; otherwise a plural (Plural true) or a select.
    private sealed record Argument(string Name, bool Plural, List<(string Key, Node[] Body)>? Branches) : Node;

    private sealed class Parser(string pattern)
    {
        private int _at;

        // A message up to the end, or up to the '}' that closes a branch (nested); '#' is the number inside a plural.
        public Node[] Message(bool nested, bool inPlural = false)
        {
            var nodes = new List<Node>();
            var literal = new StringBuilder();
            while (_at < pattern.Length)
            {
                var c = pattern[_at];
                if (c == '}')
                {
                    if (!nested)
                        throw Error("a '}' that closes nothing");
                    break;
                }
                if (c == '{' || (c == '#' && inPlural))
                {
                    if (literal.Length > 0)
                    {
                        nodes.Add(new Literal(literal.ToString()));
                        literal.Clear();
                    }
                    if (c == '#')
                    {
                        nodes.Add(new Hash());
                        _at++;
                    }
                    else
                    {
                        nodes.Add(Placeholder());
                    }
                    continue;
                }
                literal.Append(c);
                _at++;
            }
            if (nested && _at >= pattern.Length)
                throw Error("a '{' that is never closed");
            if (literal.Length > 0)
                nodes.Add(new Literal(literal.ToString()));
            return [.. nodes];
        }

        private Argument Placeholder()
        {
            _at++; // '{'
            var name = Word();
            if (name.Length == 0)
                throw Error("a placeholder without a name");
            Spaces();
            if (Next('}'))
                return new Argument(name, false, null);
            if (!Next(','))
                throw Error($"'{name}' isn't followed by '}}' or ','");
            Spaces();
            var kind = Word();
            var plural = kind == "plural";
            if (!plural && kind != "select")
                throw Error($"'{name}' is of an unknown kind '{kind}' (plural or select)");
            Spaces();
            if (!Next(','))
                throw Error($"'{name}, {kind}' isn't followed by ','");
            var branches = new List<(string, Node[])>();
            while (true)
            {
                Spaces();
                if (Next('}'))
                    break;
                var key = Next('=') ? "=" + Word() : Word();
                if (key.Length == 0 || key == "=")
                    throw Error($"a branch of '{name}' without a key");
                Spaces();
                if (!Next('{'))
                    throw Error($"the branch '{key}' of '{name}' doesn't start with '{{'");
                var body = Message(nested: true, inPlural: plural);
                _at++; // '}'
                if (branches.Any(b => b.Item1 == key))
                    throw Error($"'{name}' has the branch '{key}' twice");
                branches.Add((key, body));
            }
            if (!branches.Any(b => b.Item1 == "other"))
                throw Error($"'{name}' has no 'other' branch");
            return new Argument(name, plural, branches);
        }

        private string Word()
        {
            var start = _at;
            while (_at < pattern.Length && (char.IsAsciiLetterOrDigit(pattern[_at]) || pattern[_at] == '_'))
                _at++;
            return pattern[start.._at];
        }

        private void Spaces()
        {
            while (_at < pattern.Length && char.IsWhiteSpace(pattern[_at]))
                _at++;
        }

        private bool Next(char c)
        {
            if (_at < pattern.Length && pattern[_at] == c)
            {
                _at++;
                return true;
            }
            return false;
        }

        private FormatException Error(string what) => new($"{what} at {_at} in \"{pattern}\"");
    }
}
