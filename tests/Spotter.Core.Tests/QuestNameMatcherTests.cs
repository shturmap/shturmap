using Spotter.Core.Quests;

namespace Spotter.Core.Tests;

public class QuestNameMatcherTests
{
    private static readonly QuestCandidate[] Catalog =
    [
        new("a", "Audit", ["Streets of Tarkov"]),
        new("b", "Glory to CPSU", ["Streets of Tarkov"]),
        new("c", "Glory to CPSU - Part 2", ["Streets of Tarkov"]),
        new("d", "Revision - Streets of Tarkov", ["Streets of Tarkov"]),
        new("e", "The Punisher - Part 1", ["Customs"]),
        new("f", "The Punisher - Part 7", []),
        new("g", "Swift", ["Woods"]),
        new("h", "Humanitarian Supplies", ["Shoreline"]),
        new("i", "Import", ["Customs"]),
        new("j", "The Tarkov Shooter - Part 1", ["Woods"]),
    ];

    [Theory]
    [InlineData("Audit", "Streets of Tarkov", "a")]
    [InlineData("Revision - Streets of Tarkov", "Streets of Tarkov", "d")]
    [InlineData("Revision – Streets of Tarkov", "Streets of Tarkov", "d")] // typographic dash
    [InlineData("Swift", "Woods", "g")]
    [InlineData("Humanitarian Supplies", "Shoreline", "h")]
    public void Exact_reads_are_accepted(string name, string location, string expected)
    {
        var match = QuestNameMatcher.Match(name, location, Catalog);
        Assert.Equal(MatchVerdict.Accepted, match.Verdict);
        Assert.Equal(expected, match.Quest?.Id);
    }

    [Fact]
    public void Known_font_confusions_still_match()
    {
        // Windows OCR reads the game font's "U" as "IJ": "Glory to CPSU" arrives as "Glory to CPSIJ".
        var match = QuestNameMatcher.Match("Glory to CPSIJ", "Streets of Tarkov", Catalog);
        Assert.Equal(MatchVerdict.Accepted, match.Verdict);
        Assert.Equal("b", match.Quest?.Id);
        Assert.True(match.Score < 1, "a folded match is never reported as exact");
    }

    [Fact]
    public void An_unexplained_misread_asks_for_confirmation()
    {
        var match = QuestNameMatcher.Match("Glory tc CPXU", "Streets of Tarkov", Catalog);
        Assert.Equal(MatchVerdict.NeedsConfirmation, match.Verdict);
        Assert.Equal("b", match.Quest?.Id);
    }

    [Fact]
    public void Folding_does_not_merge_numbers()
    {
        // "1" folds to "l", but the part-number rule runs on the unfolded text.
        var match = QuestNameMatcher.Match("The Tarkov Shooter - Part 7", "Woods", Catalog);
        Assert.NotEqual(MatchVerdict.Accepted, match.Verdict);
    }

    [Fact]
    public void A_different_part_number_is_a_different_quest()
    {
        var match = QuestNameMatcher.Match("The Punisher - Part 7", "", Catalog);
        Assert.Equal("f", match.Quest?.Id);
        Assert.Equal(MatchVerdict.Accepted, match.Verdict);

        var unknown = QuestNameMatcher.Match("The Punisher - Part 3", "Customs", Catalog);
        Assert.NotEqual(MatchVerdict.Accepted, unknown.Verdict);
    }

    [Fact]
    public void Location_breaks_doubt()
    {
        // Same text, wrong place: the score drops below acceptance.
        var match = QuestNameMatcher.Match("Swift", "Customs", Catalog);
        Assert.NotEqual(MatchVerdict.Accepted, match.Verdict);
    }

    [Fact]
    public void Garbage_is_unread()
    {
        var match = QuestNameMatcher.Match("0% IIll", "", Catalog);
        Assert.Equal(MatchVerdict.Unread, match.Verdict);
        Assert.Null(match.Quest);
    }
}
