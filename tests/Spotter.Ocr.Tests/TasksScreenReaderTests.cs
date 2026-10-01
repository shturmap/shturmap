using Spotter.Core.Quests;

namespace Spotter.Ocr.Tests;

/// <summary>Real 2560×1440 screenshots of Character → Tasks → Side, read with Windows' own OCR.</summary>
public class TasksScreenReaderTests
{
    private static readonly QuestCandidate[] Catalog =
    [
        new("swift", "Swift", ["Woods"]),
        new("ballet", "Ballet Lover", ["Streets of Tarkov"]),
        new("secret", "Secret Message", ["Streets of Tarkov"]),
        new("road", "Road Closed", ["Streets of Tarkov"]),
        new("cpsu", "Glory to CPSU", ["Streets of Tarkov"]),
        new("cpsu2", "Glory to CPSU - Part 2", ["Streets of Tarkov"]),
        new("audit", "Audit", ["Streets of Tarkov"]),
        new("dandies", "Dandies", ["Streets of Tarkov"]),
        new("fishing", "Fishing Gear", ["Shoreline"]),
        new("revision", "Revision - Streets of Tarkov", ["Streets of Tarkov"]),
        new("humanitarian", "Humanitarian Supplies", ["Shoreline"]),
        new("import", "Import", ["Customs"]),
    ];

    [Theory]
    [InlineData("tasks-side-1440p-a.png", "swift,ballet,secret,road,cpsu,audit,dandies,fishing")]
    [InlineData("tasks-side-1440p-b.png", "swift,revision,ballet,secret,cpsu,audit,dandies,humanitarian")]
    public async Task Reads_every_visible_row(string file, string expected)
    {
        var recognizer = TextRecognizer.Create("en");
        Assert.SkipWhen(recognizer is null, "No OCR language installed on this machine.");

        var screen = await new TasksScreenReader(recognizer!).ReadAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "ocr", file));

        Assert.NotNull(screen);
        Assert.Equal(TasksTab.Side, screen.Tab);
        var matches = screen.Rows.Select(r => QuestNameMatcher.Match(r.Name, r.Location, Catalog)).ToList();
        Assert.All(matches, m => Assert.Equal(MatchVerdict.Accepted, m.Verdict));
        Assert.Equal(expected.Split(','), matches.Select(m => m.Quest!.Id).ToArray());
        Assert.All(screen.Rows, r => Assert.StartsWith("activ", r.Status, StringComparison.OrdinalIgnoreCase));
    }
}
