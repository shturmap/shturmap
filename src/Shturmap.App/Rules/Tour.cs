namespace Shturmap.App.Rules;

/// <summary>
/// The tour (owner, 2026-10-09: "a proper first-time-opening tour of the app ... later accessible through the help
/// menu"; from the panel, the briefing told through an example raid; docs/DESIGN.md §4, "Screen anatomy", *The tour*):
/// seven chapters over the real window, each framing the parts it is about and staging an example on the map. Its
/// chapters come from docs/tour.md, built into the app: "## stage · anchors", the title, at most two lines, and the
/// stage's own words as "- key · text" lines.
/// </summary>
public static class Tour
{
    /// <summary>A word of a chapter's stage: a "- key · text" line (what is read and never done; a button's label).</summary>
    public sealed record Word(string Key, string Text);

    /// <summary>One chapter.</summary>
    /// <param name="Stage">What the chapter draws: one of <see cref="Stages"/>.</param>
    /// <param name="Anchors">The parts it frames, each as its x:Name and the ones framed instead when it isn't shown.</param>
    public sealed record Chapter(string Stage, IReadOnlyList<IReadOnlyList<string>> Anchors, string Title, IReadOnlyList<string> Lines,
        IReadOnlyList<Word> Words);

    /// <summary>The tour stays short: seven chapters, about a minute and a half.</summary>
    public const int MaxChapters = 7;

    /// <summary>A chapter says at most this much under its title.</summary>
    public const int MaxLines = 2;

    /// <summary>
    /// The stages the app draws: what Shturmap reads and never does; Plan and Raid; NEXT RAID's rows; a pick and the
    /// linked highlight on an example; the screenshot key and the position it brings; the kit and the raid card;
    /// the feedback, help and settings buttons.
    /// </summary>
    public static readonly IReadOnlySet<string> Stages = new HashSet<string>(StringComparer.Ordinal)
        { "safe", "follows", "next", "pick", "key", "raid", "know" };

    /// <summary>The setting that says the tour was seen, and in which version.</summary>
    public const string SeenSetting = "tour.seen";

    /// <summary>The chapters of docs/tour.md, in their order.</summary>
    public static IReadOnlyList<Chapter> Parse(string markdown)
    {
        var chapters = new List<Chapter>();
        string? stage = null;
        IReadOnlyList<IReadOnlyList<string>> anchors = [];
        string? title = null;
        var lines = new List<string>();
        var words = new List<Word>();
        void Close()
        {
            if (stage is not null && title is not null)
                chapters.Add(new Chapter(stage, anchors, title, lines.ToList(), words.ToList()));
            stage = title = null;
            lines.Clear();
            words.Clear();
        }
        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Close();
                var head = line[3..].Split(" · ", 2, StringSplitOptions.TrimEntries);
                stage = head[0];
                anchors = head.Length < 2 ? [] : head[1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => (IReadOnlyList<string>)a.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).ToList();
                continue;
            }
            if (stage is null || line.Length == 0)
                continue;
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                var at = line.IndexOf(" · ", StringComparison.Ordinal);
                if (at > 2)
                    words.Add(new Word(line[2..at].Trim(), line[(at + 3)..].Trim()));
                continue;
            }
            if (title is null)
                title = line;
            else
                lines.Add(line);
        }
        Close();
        return chapters;
    }

    /// <summary>
    /// Whether the tour starts by itself: at a first start (neither it nor help seen on this PC), in help's place.
    /// Never during a raid (<see cref="WhileInRaid.Waits"/>): it waits until the raid is over.
    /// </summary>
    public static bool StartsByItself(string? tourSeen, string? helpSeen) => tourSeen is null && helpSeen is null;

    /// <summary>
    /// The chapter to go to from <paramref name="at"/>, one step forward or back; null past the last (the tour ends).
    /// Back from the first stays there.
    /// </summary>
    public static int? Step(int at, bool forward, int count) =>
        forward ? (at + 1 < count ? at + 1 : null) : Math.Max(0, at - 1);

    /// <summary>"TOUR · 3 OF 7": the band's eyebrow.</summary>
    public static string Eyebrow(int at, int count) => $"TOUR · {at + 1} OF {count}";

    /// <summary>
    /// A What's New line that opens the tour: "tour", or "tour:5" for its fifth chapter (a chapter a release changed).
    /// The chapter's index from 0, or null when the preview isn't the tour's.
    /// </summary>
    public static int? ChapterOf(string preview)
    {
        if (preview == "tour")
            return 0;
        return preview.StartsWith("tour:", StringComparison.Ordinal) && int.TryParse(preview[5..], out var n) && n >= 1 ? n - 1 : null;
    }
}
