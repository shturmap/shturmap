using System.Text.RegularExpressions;

namespace Shturmap.Map.Tests;

// One design system for the app, the map, the brand files and the website (docs/DESIGN.md §4, "Design system"): its
// colour table is Palette, and every copy of it (App.xaml, XAML literals, the design doc, brand\build.cs, the website's
// CSS) must say the same.
public partial class DesignTokenTests
{
    [GeneratedRegex(@"#(?<argb>[0-9A-Fa-f]{6,8})\b")]
    private static partial Regex Hex();

    private static readonly HashSet<string> Colours =
        Palette.All.Values.Select(v => v.ToUpperInvariant()).ToHashSet(StringComparer.Ordinal);

    private static string Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Shturmap.slnx")))
                return dir.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }

    // A colour as the palette writes it: its RGB part ("#AARRGGBB" in XAML carries an alpha step of a palette colour).
    private static string Rgb(string argb) => "#" + argb[^6..].ToUpperInvariant();

    private static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(Path.Combine(Root(), "src"), pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    [Fact]
    public void App_xaml_names_its_colours_as_the_palette_does()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "src", "Shturmap.App", "App.xaml"));
        var named = Regex.Matches(xaml, @"<Color x:Key=""(?<key>\w+)"">(?<hex>#[0-9A-Fa-f]{6})</Color>")
            .ToDictionary(m => m.Groups["key"].Value, m => m.Groups["hex"].Value.ToUpperInvariant());
        Assert.NotEmpty(named);
        // Windows' accent ramp is the amber's.
        var aliases = new Dictionary<string, string>
        {
            ["SystemAccentColor"] = nameof(Palette.Amber),
            ["SystemAccentColorLight1"] = nameof(Palette.AmberHover),
            ["SystemAccentColorLight2"] = nameof(Palette.AmberHi),
            ["SystemAccentColorDark1"] = nameof(Palette.AmberDeep),
        };
        foreach (var (key, hex) in named)
        {
            var token = aliases.TryGetValue(key, out var alias) ? alias : key.EndsWith("Color", StringComparison.Ordinal) ? key[..^5] : key;
            Assert.True(Palette.All.TryGetValue(token, out var expected), $"App.xaml's {key} has no palette colour '{token}'");
            Assert.Equal(expected, hex);
        }
    }

    [Fact]
    public void Every_xaml_colour_is_a_palette_colour_or_an_alpha_step_of_one()
    {
        var strays = SourceFiles("*.xaml")
            .SelectMany(f => Hex().Matches(File.ReadAllText(f)).Select(m => (File: Path.GetFileName(f), m.Groups["argb"].Value)))
            .Where(c => !Colours.Contains(Rgb(c.Value)))
            .Select(c => $"{c.File}: #{c.Value}")
            .ToList();
        Assert.True(strays.Count == 0, string.Join(Environment.NewLine, strays));
    }

    [Fact]
    public void Code_takes_its_colours_from_the_palette()
    {
        var strays = SourceFiles("*.cs")
            .Where(f => Path.GetFileName(f) != "Palette.cs")
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: Path.GetFileName(f), Line: i + 1, Text: line)))
            .Where(l => l.Text.Contains("SKColor.Parse(\"#", StringComparison.Ordinal))
            .Select(l => $"{l.File}:{l.Line}")
            .ToList();
        Assert.True(strays.Count == 0, "Use Palette instead: " + string.Join(", ", strays));
    }

    [Fact]
    public void The_design_doc_lists_exactly_the_palette()
    {
        var doc = File.ReadAllText(Path.Combine(Root(), "docs", "DESIGN.md"));
        var table = Regex.Matches(doc, @"^\| `(?<name>\w+)` \| `(?<hex>#[0-9A-Fa-f]{6})` \|", RegexOptions.Multiline)
            .ToDictionary(m => m.Groups["name"].Value, m => m.Groups["hex"].Value.ToUpperInvariant());
        Assert.Equal(Palette.All.OrderBy(p => p.Key), table.OrderBy(p => p.Key));
    }

    [Fact]
    public void Brand_files_draw_with_palette_colours()
    {
        var build = File.ReadAllText(Path.Combine(Root(), "brand", "build.cs"));
        var strays = Regex.Matches(build, @"""#(?<argb>[0-9A-Fa-f]{6})""")
            .Select(m => Rgb(m.Groups["argb"].Value))
            .Where(c => !Colours.Contains(c))
            .Distinct()
            .ToList();
        Assert.True(strays.Count == 0, string.Join(", ", strays));
    }

    /// <summary>The website's CSS (its own repository beside this one; skipped where it isn't checked out).</summary>
    [Fact]
    public void The_website_uses_palette_colours()
    {
        var css = Path.Combine(Root(), "..", "shturmap.github.io", "assets", "site.css");
        if (!File.Exists(css))
            Assert.Skip("The website isn't checked out beside this repository.");
        var text = File.ReadAllText(css);
        var strays = Hex().Matches(text).Select(m => Rgb(m.Groups["argb"].Value))
            .Concat(Regex.Matches(text, @"rgba?\((?<r>\d+),\s*(?<g>\d+),\s*(?<b>\d+)")
                .Select(m => $"#{int.Parse(m.Groups["r"].Value):X2}{int.Parse(m.Groups["g"].Value):X2}{int.Parse(m.Groups["b"].Value):X2}"))
            .Where(c => !Colours.Contains(c))
            .Distinct()
            .ToList();
        Assert.True(strays.Count == 0, string.Join(", ", strays));
    }
}
