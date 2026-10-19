using System.Xml.Linq;
using Shturmap.App.Rules;
using Shturmap.Core.Operational;

namespace Shturmap.Core.Tests.Operational;

// The cue for a Tasks screenshot's reading and the OPERATIONAL section's place (owner, 2026-10-19, after trying the dev
// build: "I think they should be sorted on top of the list always as they are time-sensitive. Also they should get the
// same animation as the other quests. Be mindful of multiple missions started at the same time, we might want to have
// them in one animation included"). Made-up tasks; English texts.
public class OperationalCueTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 14, 2, 0, TimeSpan.FromHours(1));

    private static OperationalTask Task(string name, string? map, TimeSpan? left, params OperationalObjective[] objectives) =>
        new(name, map, objectives, OperationalKind.Kill, null, left is { } l ? Now + l : null, Now, objectives.Length > 0 ? Now : null);

    private static readonly OperationalTask Survive = Task("Exit the location", "map-zorbs", TimeSpan.FromHours(8));
    private static readonly OperationalTask Kill = Task("Elimination", "map-blorps", TimeSpan.FromHours(10));
    private static readonly OperationalTask Transfer = Task("Find and transfer", null, TimeSpan.FromHours(10));

    // ---- which readings call the cue: OperationalReadings ----

    [Fact]
    public void A_reading_that_brings_tasks_calls_one_cue_for_all_of_them()
    {
        var readings = new OperationalReadings();
        var call = readings.Read(Now, [], [Survive, Kill, Transfer]);
        Assert.NotNull(call);
        Assert.False(call.Joins);
        Assert.Equal([Survive, Kill, Transfer], call.Tasks);
    }

    [Fact]
    public void The_same_set_again_calls_no_cue()
    {
        var readings = new OperationalReadings();
        // Read before (another run, the file): an hour later the same three again, a row expanded on it.
        var expanded = Kill with { Objectives = [new OperationalObjective("Eliminate Zorbs", 0, 3)], ReadAt = Now.AddHours(1) };
        Assert.Null(readings.Read(Now.AddHours(1), [Survive, Kill, Transfer], [Survive, expanded, Transfer]));
        // A task handed in brings nothing new either.
        Assert.Null(readings.Read(Now.AddHours(2), [Survive, Kill, Transfer], [Survive, Transfer]));
        // The name is the game's; its case is the reading's.
        Assert.Null(readings.Read(Now.AddHours(3), [Survive], [Survive with { Name = "EXIT THE LOCATION" }]));
    }

    [Fact]
    public void Two_screenshots_within_a_minute_are_one_reading_with_one_cue()
    {
        var readings = new OperationalReadings();
        Assert.NotNull(readings.Read(Now, [Survive], [Survive, Kill]));
        // 40 s later the table again with a row expanded: its objectives, no second cue.
        var expanded = Kill with { Objectives = [new OperationalObjective("Eliminate Zorbs", 0, 3)] };
        Assert.Null(readings.Read(Now.AddSeconds(40), [Survive, Kill], [Survive, expanded]));
        // Another 50 s on (each within a minute of the one before), a page with a task the cue hasn't listed: it joins it,
        // and the cue lists every task the run read, the newest reading of each.
        var call = readings.Read(Now.AddSeconds(90), [Survive, expanded], [Transfer]);
        Assert.NotNull(call);
        Assert.True(call.Joins);
        Assert.Equal([Survive, expanded, Transfer], call.Tasks);
    }

    [Fact]
    public void A_screenshot_more_than_a_minute_later_is_a_new_reading()
    {
        var readings = new OperationalReadings();
        Assert.NotNull(readings.Read(Now, [], [Survive]));
        var call = readings.Read(Now.AddSeconds(61), [Survive], [Survive, Kill]);
        Assert.NotNull(call);
        Assert.False(call.Joins);
        Assert.Equal([Survive, Kill], call.Tasks);
    }

    [Fact]
    public void A_run_whose_first_reading_brought_nothing_new_calls_its_cue_with_the_first_that_does()
    {
        var readings = new OperationalReadings();
        Assert.Null(readings.Read(Now, [Survive], [Survive]));
        var call = readings.Read(Now.AddSeconds(20), [Survive], [Survive, Kill]);
        Assert.NotNull(call);
        Assert.False(call.Joins);
        Assert.Equal([Survive, Kill], call.Tasks);
    }

    [Fact]
    public void Two_dailies_of_one_name_on_one_map_are_two_tasks()
    {
        var other = Survive with { ExpiresAt = Now.AddHours(20) };
        Assert.Equal(2, OperationalReadings.Keys([Survive, other]).Distinct().Count());
        var call = new OperationalReadings().Read(Now, [Survive], [Survive, other]);
        Assert.NotNull(call);
        Assert.Equal(2, call.Tasks.Count);
    }

    // ---- the cue's words ----

    [Theory]
    [InlineData(1, "1 DAILY OR WEEKLY")]
    [InlineData(2, "2 DAILIES AND WEEKLIES")]
    [InlineData(5, "5 DAILIES AND WEEKLIES")]
    public void The_title_says_how_many_never_which_kind(int count, string title) =>
        Assert.Equal(("FROM YOUR SCREENSHOT", title), OperationalCueWords.Of(count));

    [Fact]
    public void A_row_says_its_map_or_any_map_and_its_time_left()
    {
        var survive = new OperationalShown(Survive, "zorbs", []);
        Assert.Equal("Zorbs · 8 h left", OperationalCueWords.Line(survive, "Zorbs", Now));
        Assert.Equal("any map · 10 h left", OperationalCueWords.Line(new OperationalShown(Transfer, null, []), null, Now));
        // A countdown not taken: the map alone.
        Assert.Equal("Zorbs", OperationalCueWords.Line(new OperationalShown(Survive with { ExpiresAt = null }, "zorbs", []), "Zorbs", Now));
        Assert.Equal("25 min left", OperationalCueWords.Line(new OperationalShown(Survive with { ExpiresAt = Now.AddMinutes(25.5) }, "zorbs", []), null, Now));
    }

    [Fact]
    public void The_rows_come_the_one_that_ends_soonest_first()
    {
        var weekly = new OperationalShown(Transfer with { ExpiresAt = Now.AddDays(5) }, null, []);
        var unsure = new OperationalShown(Kill with { ExpiresAt = null }, "blorps", []);
        var soon = new OperationalShown(Survive with { ExpiresAt = Now.AddMinutes(25) }, "zorbs", []);
        Assert.Equal([soon, weekly, unsure], OperationalCueWords.Rows([weekly, unsure, soon]));
    }
}

// The cue's words in German: the players' Dailies and Weeklies (docs/glossary-de.md), never "Aufgabe".
[Collection("UiLanguage")]
public class OperationalCueGermanTests
{
    [Fact]
    public void The_title_says_dailies_and_weeklies_in_german_too()
    {
        try
        {
            UiLanguage.Set(UiLanguage.German);
            Assert.Equal(("AUS DEINEM SCREENSHOT", "1 DAILY ODER WEEKLY"), OperationalCueWords.Of(1));
            Assert.Equal("3 DAILIES UND WEEKLIES", OperationalCueWords.Of(3).Title);
        }
        finally
        {
            UiLanguage.Set(UiLanguage.English);
        }
    }
}

// Where the OPERATIONAL section stands: on top of the quest list, before PICKED, COMPLETE and PROGRESS, in the open map's
// card and in the raid card (owner, 2026-10-19: "sorted on top of the list always as they are time-sensitive"). Read from
// MainWindow.xaml, which the app compiles.
public class OperationalSectionOrderTests
{
    [Fact]
    public void Operational_comes_first_in_both_cards()
    {
        var root = RepositoryRoot();
        var doc = XDocument.Load(Path.Combine(root, "src", "Shturmap.App", "MainWindow.xaml"));
        var eyebrows = doc.Descendants().Select(e => (string?)e.Attribute("Text") ?? "")
            .Select(text => text switch
            {
                "{x:Bind local:ViewTexts.CardOperational}" => "OPERATIONAL",
                "{x:Bind local:ViewTexts.CardPicked}" => "PICKED",
                "{x:Bind local:ViewTexts.CardComplete}" => "COMPLETE",
                "{x:Bind local:ViewTexts.CardProgress}" => "PROGRESS",
                "{x:Bind local:ViewTexts.CardBring}" => "BRING",
                _ => null,
            }).OfType<string>().ToList();
        // The open map's card, then the raid card.
        Assert.Equal(["OPERATIONAL", "PICKED", "COMPLETE", "PROGRESS", "BRING", "OPERATIONAL", "PICKED", "COMPLETE", "PROGRESS", "BRING"], eyebrows);
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Shturmap.slnx")))
                return dir.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
