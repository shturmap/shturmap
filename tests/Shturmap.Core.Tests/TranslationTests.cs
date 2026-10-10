using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shturmap.Core.Text;

namespace Shturmap.Core.Tests;

// Every text file of every project (docs/DESIGN.md §8, "Texts"; docs/LANGUAGES.md): the English texts parse, and each
// translation says the same as its English text with the same placeholders, has the plural forms its language needs,
// and was made from the English text as it is now. A language Shturmap offers (UiLanguage.Supported) has every text;
// one being translated (UiLanguage.InTranslation) is checked for what is written of it.
public partial class TranslationTests
{
    private sealed record Entry(string Key, string Value, string? Comment);

    private sealed record TextFile(string Path, string Name, string? Language, IReadOnlyList<Entry> Entries);

    private static readonly Lazy<IReadOnlyList<TextFile>> Files = new(() =>
    {
        var src = System.IO.Path.Combine(SafetyTests.RepositoryRoot(), "src");
        return Directory.EnumerateFiles(src, "*.resx", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}"))
            .Select(f => (Path: f, Match: TextFileName().Match(System.IO.Path.GetFileName(f))))
            .Where(f => f.Match.Success)
            .Select(f => new TextFile(f.Path, f.Match.Groups["name"].Value, f.Match.Groups["lang"].Success ? f.Match.Groups["lang"].Value : null,
                XDocument.Load(f.Path).Root!.Elements("data")
                    .Select(d => new Entry((string)d.Attribute("name")!, (string?)d.Element("value") ?? "", (string?)d.Element("comment")))
                    .ToList()))
            .ToList();
    });

    private static string Relative(string path) => System.IO.Path.GetRelativePath(SafetyTests.RepositoryRoot(), path);

    [Fact]
    public void There_are_text_files_to_check() =>
        Assert.Contains(Files.Value, f => f.Language is null && f.Entries.Count > 0);

    [Fact]
    public void Every_english_text_parses_and_its_name_is_used_once()
    {
        var problems = Files.Value.Where(f => f.Language is null)
            .SelectMany(f => f.Entries.Select(e => TextFormat.Problem(e.Value) is { } why ? $"{Relative(f.Path)}: {e.Key}: {why}" : null)
                .Concat(f.Entries.GroupBy(e => e.Key).Where(g => g.Count() > 1).Select(g => $"{Relative(f.Path)}: {g.Key} is there twice")))
            .OfType<string>()
            .ToList();
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Every_translation_is_of_a_text_file_and_a_language_shturmap_knows()
    {
        var english = Files.Value.Where(f => f.Language is null).Select(f => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(f.Path)!, f.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var problems = Files.Value.Where(f => f.Language is not null)
            .Select(f => !english.Contains(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(f.Path)!, f.Name)) ? $"{Relative(f.Path)} has no English file beside it"
                : !UiLanguage.IsWritten(f.Language) ? $"{Relative(f.Path)} is in '{f.Language}', which is neither offered nor being translated (UiLanguage)"
                : null)
            .OfType<string>()
            .ToList();
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Each_translation_says_what_its_english_text_says()
    {
        var problems = new List<string>();
        foreach (var english in Files.Value.Where(f => f.Language is null))
        {
            var texts = english.Entries.ToDictionary(e => e.Key, e => e.Value);
            foreach (var language in UiLanguage.Supported.Concat(UiLanguage.InTranslation).Where(l => l != UiLanguage.English))
            {
                var translation = Files.Value.FirstOrDefault(f => f.Language == language && f.Name == english.Name &&
                    string.Equals(System.IO.Path.GetDirectoryName(f.Path), System.IO.Path.GetDirectoryName(english.Path), StringComparison.OrdinalIgnoreCase));
                var entries = translation?.Entries.ToDictionary(e => e.Key) ?? [];
                var where = translation is null ? $"{Relative(english.Path)} ({language})" : Relative(translation.Path);
                if (UiLanguage.IsSupported(language))
                {
                    problems.AddRange(texts.Keys.Where(k => !entries.ContainsKey(k)).Select(k => $"{where}: {k} isn't translated"));
                }
                foreach (var (key, entry) in entries)
                {
                    if (!texts.TryGetValue(key, out var source))
                    {
                        problems.Add($"{where}: {key} has no English text");
                        continue;
                    }
                    problems.AddRange(Problems(key, source, entry, language).Select(p => $"{where}: {p}"));
                }
            }
        }
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    // What is wrong with one translated text: empty, doesn't parse, other placeholders than the English, a plural
    // form its language needs missing, or made from an English text that has changed since ("en: <the English text>"
    // in its comment; docs/LANGUAGES.md).
    private static IEnumerable<string> Problems(string key, string english, Entry entry, string language)
    {
        if (entry.Value.Trim().Length == 0)
        {
            yield return $"{key} is empty";
            yield break;
        }
        if (TextFormat.Problem(entry.Value) is { } why)
        {
            yield return $"{key} doesn't parse: {why}";
            yield break;
        }
        var expected = TextFormat.Arguments(english)!.Order(StringComparer.Ordinal);
        var actual = TextFormat.Arguments(entry.Value)!.Order(StringComparer.Ordinal);
        if (!expected.SequenceEqual(actual))
            yield return $"{key} has the placeholders {{{string.Join("}, {", actual)}}}, the English text {{{string.Join("}, {", expected)}}}";
        var englishPlurals = TextFormat.PluralBranches(english)!;
        foreach (var (name, branches) in TextFormat.PluralBranches(entry.Value)!)
        {
            var missing = PluralRules.Categories(language)
                .Concat(englishPlurals.GetValueOrDefault(name, []).Where(b => b.StartsWith('=')))
                .Where(c => !branches.Contains(c))
                .ToList();
            if (missing.Count > 0)
                yield return $"{key}: '{name}' lacks the plural forms {string.Join(", ", missing)}";
        }
        foreach (var name in englishPlurals.Keys.Where(n => !TextFormat.PluralBranches(entry.Value)!.ContainsKey(n)))
            yield return $"{key}: '{name}' is a plural in English and must be one here";
        const string from = "en: ";
        if (entry.Comment is not { } comment || !comment.StartsWith(from, StringComparison.Ordinal))
            yield return $"{key} doesn't say which English text it was made from (its comment: \"{from}<the English text>\")";
        else if (comment[from.Length..] != english)
            yield return $"{key} was made from an older English text: translate it again";
    }

    [GeneratedRegex(@"^(?<name>[A-Za-z]+Texts)(\.(?<lang>[A-Za-z-]+))?\.resx$")]
    private static partial Regex TextFileName();
}
