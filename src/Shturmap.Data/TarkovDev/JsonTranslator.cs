using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Shturmap.Data.TarkovDev;

/// <summary>
/// json.tarkov.dev ships translatable strings as keys ("638fcd23dc65553116701d33 name") plus a list of JSONPath
/// expressions saying where they are. This replaces those keys with text from the chosen language, falling back to
/// English and then to the key itself. Rather than implement JSONPath, it translates values of the properties the
/// paths end in (name, description, …) wherever they occur. Ids are never touched because "id" never appears.
/// </summary>
public static partial class JsonTranslator
{
    // ".name", "['healthEffect','playerHealthEffect']", "..bodyParts" → the property names
    [GeneratedRegex(@"(?:\.|\[')(?<prop>[A-Za-z_][A-Za-z0-9_]*)'?", RegexOptions.CultureInvariant)]
    private static partial Regex PathSegment();

    public static HashSet<string> TranslatableProperties(IEnumerable<string> paths)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            // The last named segment of each path; for a ['a','b'] group the path continues with another name.
            var segments = PathSegment().Matches(path).Select(m => m.Groups["prop"].Value).Where(s => s != "data").ToList();
            if (segments.Count > 0)
                names.Add(segments[^1]);
        }
        return names;
    }

    public static void Translate(JsonNode? node, IReadOnlySet<string> properties, IReadOnlyDictionary<string, string> language, IReadOnlyDictionary<string, string>? fallback)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, child) in obj.ToList())
                {
                    if (properties.Contains(name))
                    {
                        if (child is JsonValue v && v.GetValueKind() == JsonValueKind.String)
                        {
                            obj[name] = Lookup(v.GetValue<string>(), language, fallback);
                            continue;
                        }
                        if (child is JsonArray strings && strings.All(s => s is JsonValue sv && sv.GetValueKind() == JsonValueKind.String))
                        {
                            for (var i = 0; i < strings.Count; i++)
                                strings[i] = Lookup(strings[i]!.GetValue<string>(), language, fallback);
                            continue;
                        }
                    }
                    Translate(child, properties, language, fallback);
                }
                break;
            case JsonArray array:
                foreach (var child in array)
                    Translate(child, properties, language, fallback);
                break;
        }
    }

    public static Dictionary<string, string> ReadDictionary(string json)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return result;
        foreach (var p in data.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.String)
                result[p.Name] = p.Value.GetString()!;
        }
        return result;
    }

    /// <summary>A key's text in one language, as <see cref="Translate"/> would put it there: the text, else the key
    /// itself (empty for a key that is an id with no text).</summary>
    public static string Text(string key, IReadOnlyDictionary<string, string> language) => Lookup(key, language, null);

    private static string Lookup(string key, IReadOnlyDictionary<string, string> language, IReadOnlyDictionary<string, string>? fallback)
    {
        if (language.TryGetValue(key, out var text) && !string.IsNullOrEmpty(text))
            return text;
        if (fallback is not null && fallback.TryGetValue(key, out var en) && !string.IsNullOrEmpty(en))
            return en;
        // A key with an id and a suffix but no text anywhere is an empty field, not something to show.
        return KeyLike().IsMatch(key) ? "" : key;
    }

    [GeneratedRegex(@"^[0-9a-f]{24}(?: \w+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyLike();
}
