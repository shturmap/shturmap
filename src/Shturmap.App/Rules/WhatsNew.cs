namespace Shturmap.App.Rules;

/// <summary>
/// What's New: the card at the top of Plan's rail after an update (owner, 2026-10-07: "how to inform the user about
/// news and changes in the app", from the panels "C", then "Whats new: A"; docs/DESIGN.md §4, "Screen anatomy"). Its
/// lines come from docs/whats-new.md, built into the app: one section per version, up to <see cref="MaxItems"/> lines
/// each, "- preview · Name · What it is." The preview says what pointing at the line shows (<see cref="Previews"/>).
/// </summary>
public static class WhatsNew
{
    /// <summary>One line of the card.</summary>
    /// <param name="Preview">What pointing at it shows: one of <see cref="Previews"/>.</param>
    public sealed record Item(string Preview, string Name, string Text);

    /// <summary>A version's lines.</summary>
    public sealed record Section(Version Version, string Label, IReadOnlyList<Item> Items);

    /// <summary>A version says at most this many things: the card stays a glance.</summary>
    public const int MaxItems = 5;

    /// <summary>
    /// The previews the app knows: the replay on an example raid, an example extract list, the raid card's clock with
    /// example times, a quest's objectives on one spot, symbols set apart on a crowded spot.
    /// </summary>
    public static readonly IReadOnlySet<string> Previews = new HashSet<string>(StringComparer.Ordinal)
        { "replay", "extracts", "clock", "joined", "leaders" };

    /// <summary>The sections of docs/whats-new.md, newest first: "## 0.4.0" and its "- preview · Name · Text" lines.</summary>
    public static IReadOnlyList<Section> Parse(string markdown)
    {
        var sections = new List<Section>();
        string? label = null;
        Version? version = null;
        var items = new List<Item>();
        void Close()
        {
            if (version is not null && items.Count > 0)
                sections.Add(new Section(version, label!, items.ToList()));
            items.Clear();
        }
        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Close();
                label = line[3..].Trim();
                version = VersionOf(label);
                continue;
            }
            if (version is null || !line.StartsWith("- ", StringComparison.Ordinal))
                continue;
            var parts = line[2..].Split(" · ", 3, StringSplitOptions.TrimEntries);
            if (parts.Length == 3 && parts.All(p => p.Length > 0))
                items.Add(new Item(parts[0], parts[1], parts[2]));
        }
        Close();
        return sections.OrderByDescending(s => s.Version).ToList();
    }

    /// <summary>A version as written ("0.4.0", "0.3.0-dev.20261007…": the part before a "-"), or null.</summary>
    public static Version? VersionOf(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var core = text.Split('-', '+')[0].Trim().TrimStart('v', 'V');
        return Version.TryParse(core, out var v) ? v : null;
    }

    /// <summary>
    /// The sections the card shows now, newest first. A first start shows none: help opens instead, and the newest
    /// version counts as seen. After an update from a version before the card existed, nothing says which version that
    /// was: the newest section only. Otherwise every section newer than the one last seen.
    /// </summary>
    /// <param name="seen">The newest version whose lines the player has seen (closed with ×, or after a raid); null if none.</param>
    /// <param name="firstStart">Nothing has run on this PC before (help hasn't been seen either).</param>
    public static IReadOnlyList<Section> Due(IReadOnlyList<Section> sections, string? seen, bool firstStart)
    {
        if (firstStart || sections.Count == 0)
            return [];
        if (VersionOf(seen) is not { } last)
            return [sections[0]];
        return sections.Where(s => s.Version > last).ToList();
    }

    /// <summary>The words the card heads a section with: "NEW IN 0.4.0".</summary>
    public static string Heading(Section section) => $"NEW IN {section.Label}";
}
