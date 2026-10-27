using Shturmap.Core;

namespace Shturmap.App.Rules;

/// <summary>
/// What's New: the card at the top of Plan's rail after an update (owner, 2026-10-07: "how to inform the user about
/// news and changes in the app", from the panels "C", then "Whats new: A"; docs/DESIGN.md §4, "Screen anatomy"). Its
/// lines come from docs/whats-new.md, built into the app: one section per version, up to <see cref="MaxItems"/> lines
/// each, "- preview · Name · What it is." The preview says what pointing at the line shows (<see cref="Previews"/>),
/// its picture too (MainWindow.WhatsNew); a click holds it (<see cref="Click"/>).
/// </summary>
public static class WhatsNew
{
    /// <summary>One line of the card.</summary>
    /// <param name="Preview">What pointing at it shows: one of <see cref="Previews"/>.</param>
    public sealed record Item(string Preview, string Name, string Text);

    /// <summary>A version's lines.</summary>
    /// <param name="Label">The version as written ("0.4.0"); what "seen" is saved as.</param>
    /// <param name="Name">The release's name ("Praetorian"), or null: see <see cref="Parse"/>.</param>
    public sealed record Section(Version Version, string Label, IReadOnlyList<Item> Items, string? Name = null);

    /// <summary>A version says at most this many things: the card stays a glance.</summary>
    public const int MaxItems = 5;

    /// <summary>
    /// The previews the app knows: the replay on an example raid, an example extract list, the raid card's clock with
    /// example times, a quest's objectives on one spot, symbols set apart on a crowded spot; and, for what you have
    /// (0.5.0), the SEEN IN YOUR STASH cue with example items ("stash"), an item card with a mark from a screenshot and
    /// NOT ANY MORE ("stash-card"), and one with a mark from the game's log ("stash-given"); for quest notes (0.6.0),
    /// a held quest card with two made-up notes ("notes"); and for 0.7.0's features (owner, 2026-10-27, D4 B: "each feature
    /// should have a icon and a appropriate picture or even animation"), a made-up desk page coming in ("desk"), the cue
    /// of made-up dailies and weeklies read ("tasks"), the same lines at NORMAL and LARGEST ("textsize"), the in-raid
    /// glance's plate with made-up loot ("glance") and the squad grid on Customs as YOU moves ("grid").
    /// </summary>
    public static readonly IReadOnlySet<string> Previews = new HashSet<string>(StringComparer.Ordinal)
        { "replay", "extracts", "clock", "joined", "leaders", "stash", "stash-card", "stash-given", "notes", "desk", "tasks", "textsize", "glance", "grid" };

    /// <summary>
    /// Whether a line's preview stages a map of its own (the replay, the extract list, the objectives on one spot, the
    /// crowded spot, the grid on Customs). The others lay only a plate or a cue over the player's map, which comes back
    /// first, made anew, where the line pointed at before staged one (UI QA re-check of the grid's fixes, 2026-10-26, R3:
    /// the clock stood over the extract list's Customs until the pointer left the card).
    /// </summary>
    public static bool StagesMap(string preview) => preview is "replay" or "extracts" or "joined" or "leaders" or "grid";

    /// <summary>
    /// Whether a line's preview is a short sequence (owner, 2026-10-27, D4 B): the features that move, the desk (a reading
    /// arriving, its cells coming in, its totals counting, a copy), the glance (its plate rising) and the grid (YOU
    /// moving into the next square). It plays once in <see cref="Plays"/> and holds its end; with Windows' animation
    /// effects off, and in a snapshot, its end shows at once. The others are stills, or the cue's own entrance.
    /// </summary>
    public static bool Moves(string preview) => preview is "desk" or "glance" or "grid";

    /// <summary>How long a moving preview plays before it holds its end (<see cref="Moves"/>): 4 to 6 s.</summary>
    public static readonly TimeSpan Plays = TimeSpan.FromSeconds(4.5);

    /// <summary>
    /// Whether the app knows what a line's preview is: one of <see cref="Previews"/>, or a line about the tour ("tour",
    /// "tour:5") of an older version, which previews nothing (<see cref="Tour.ChapterOf"/>).
    /// </summary>
    public static bool Known(string preview) => Previews.Contains(preview) || Tour.ChapterOf(preview) is not null;

    /// <summary>
    /// Whether resting on a line shows a preview on the map: not an older version's line about the tour, which shows
    /// nothing. The pointer's way onto a line and a developer script's ("whatsnew &lt;n&gt;") both ask this, so a script
    /// can't show what no player sees: until 2026-10-15 the script's way put up a tour line's preview label over nothing
    /// staged. What's New no longer opens the tour (owner, 2026-10-27, D4 B: "when I click on them the standard tour of the
    /// app starts. This should not be the case"); the tour stays in help.
    /// </summary>
    public static bool ShowsPreview(string preview) => Tour.ChapterOf(preview) is null;

    /// <summary>
    /// A click on a line (owner, 2026-10-27, D4 B): it holds that line's preview, in place of any other held; on the line
    /// held, it lets go. A held preview stays when the pointer leaves the card; pointing at another line previews that one,
    /// and leaving it gives the held one back. A line that previews nothing holds nothing.
    /// </summary>
    /// <param name="held">The line held now (its key, "0.7.0:2"), or null.</param>
    /// <returns>The line held after the click, or null.</returns>
    public static string? Click(string? held, string line, string preview) =>
        !ShowsPreview(preview) ? held : held == line ? null : line;

    /// <summary>What the note at the card's foot says its lines do (<see cref="FootOf"/>).</summary>
    public enum Foot
    {
        /// <summary>Its lines preview on the map when pointed at, and a click holds one: "Point at one: the map shows it.
        /// Click: it stays."</summary>
        PointAt,

        /// <summary>None of its lines previews anything (only older versions' lines about the tour): no note.</summary>
        None,
    }

    /// <summary>
    /// What the card's lines do, for the note at its foot: where any previews (<see cref="ShowsPreview"/>), pointing shows
    /// it and a click holds it; where none does, nothing to say. A card shows every version newer than the one last seen,
    /// so the lines of all the sections it shows count (UI QA pass of the squad grid, F9, 2026-10-26: "Point at one: the
    /// map shows it." stood under 0.7.0's five tour lines, which previewed nothing then).
    /// </summary>
    public static Foot FootOf(IEnumerable<Section> sections) =>
        sections.SelectMany(s => s.Items).Any(i => ShowsPreview(i.Preview)) ? Foot.PointAt : Foot.None;

    /// <summary>
    /// The sections of docs/whats-new.md, newest first: "## 0.4.0" and its "- preview · Name · Text" lines. A release
    /// that raises the minor or major number has a name, the owner's (owner, 2026-10-09: "named major releases", the
    /// first "Praetorian"), after the version: "## 0.4.0 · Praetorian". A patch release keeps its line's name, so 0.4.1
    /// is Praetorian too without saying so.
    /// </summary>
    public static IReadOnlyList<Section> Parse(string markdown)
    {
        var sections = new List<Section>();
        string? label = null, name = null;
        Version? version = null;
        var items = new List<Item>();
        void Close()
        {
            if (version is not null && items.Count > 0)
                sections.Add(new Section(version, label!, items.ToList(), name));
            items.Clear();
        }
        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Close();
                var heading = line[3..].Split(" · ", 2, StringSplitOptions.TrimEntries);
                label = heading[0];
                name = heading is [_, { Length: > 0 } given] ? given : null;
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
        return sections.Select(s => s with { Name = s.Name ?? NameOf(sections, s.Version) }).OrderByDescending(s => s.Version).ToList();
    }

    /// <summary>
    /// What's New in the language in use: each English section's translation where the translated file
    /// (docs/whats-new.&lt;code&gt;.md; <see cref="BuiltDocs"/>) has its version, else the English section, since a language
    /// is translated from its release on (docs/LANGUAGES.md). The versions are the English file's: a translated section
    /// for a version it lacks is left out, and one whose lines don't have the English section's previews, in order, is
    /// taken for out of date and shown in English. The label stays the English one: it is what "seen" is saved as.
    /// </summary>
    /// <param name="translated">The translation's sections, or null where the language has none.</param>
    public static IReadOnlyList<Section> InLanguage(IReadOnlyList<Section> english, IReadOnlyList<Section>? translated) =>
        translated is null ? english
            : english.Select(e => translated.FirstOrDefault(t => t.Version == e.Version) is { } t
                && t.Items.Select(i => i.Preview).SequenceEqual(e.Items.Select(i => i.Preview))
                    ? t with { Label = e.Label, Name = t.Name ?? e.Name }
                    : e).ToList();

    /// <summary>
    /// The sections with every word they show changed by <paramref name="change"/>, the previews and versions kept: the
    /// pseudo-language's card (<see cref="Shturmap.Core.Text.PseudoText"/>).
    /// </summary>
    public static IReadOnlyList<Section> Map(IReadOnlyList<Section> sections, Func<string, string> change) =>
        sections.Select(s => s with { Items = s.Items.Select(i => i with { Name = change(i.Name), Text = change(i.Text) }).ToList() }).ToList();

    /// <summary>The name of the release line a version belongs to (0.4.1: the name of 0.4.0), or null.</summary>
    public static string? NameOf(IReadOnlyList<Section> sections, Version? version) =>
        version is null ? null
            : sections.FirstOrDefault(s => s.Name is not null && s.Version.Major == version.Major && s.Version.Minor == version.Minor)?.Name;

    /// <summary>A section's version with its release's name, in capitals as the card and help say it: "0.4.0 · PRAETORIAN".</summary>
    public static string Tag(Section section) => section.Name is { } name ? $"{section.Label} · {UiLanguage.Upper(name)}" : section.Label;

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

    /// <summary>The words the card heads a section with: "NEW IN 0.4.0 · PRAETORIAN".</summary>
    public static string Heading(Section section) => RuleTexts.WhatsNewHeading(version: Tag(section));
}
