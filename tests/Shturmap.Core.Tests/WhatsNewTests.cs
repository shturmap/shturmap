using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

// What's New, a card in Plan's rail after an update (owner, 2026-10-07: "Whats new: A"; docs/DESIGN.md §4, "Screen
// anatomy"). Its lines come from docs/whats-new.md, built into the app.
public class WhatsNewTests
{
    // The longest name a line of the card holds on one line (the test of the app's list, below).
    private const int NameLength = 31;

    private const string Two = """
        # What's New in the app

        Explanations above the first section are not lines.
        - extracts · Not a line · before any version

        ## 0.4.0

        - replay · Raid replay · After a raid: where you took screenshots.
        - clock · Raid time, large · Minutes left, large.
        - nothing here

        ## 0.5.0

        - leaders · Symbols stand apart · On a line to their place.
        """;

    [Fact]
    public void Each_version_has_its_lines_newest_first()
    {
        var sections = WhatsNew.Parse(Two);
        Assert.Equal(["0.5.0", "0.4.0"], sections.Select(s => s.Label));
        Assert.Equal(new WhatsNew.Item("replay", "Raid replay", "After a raid: where you took screenshots."), sections[1].Items[0]);
        Assert.Equal(2, sections[1].Items.Count); // a line without its three parts is no line
        Assert.Equal("NEW IN 0.5.0", WhatsNew.Heading(sections[0]));
    }

    // Named major releases (owner, 2026-10-09): the name follows the version in its heading, and a patch release
    // keeps its line's name. The label stays the version alone: it is what "seen" is saved as.
    [Fact]
    public void A_release_line_has_its_name_and_a_patch_keeps_it()
    {
        var sections = WhatsNew.Parse("""
            ## 0.4.0 · Praetorian

            - tour · The tour · Seven short chapters.

            ## 0.4.1

            - replay · Raid replay · After a raid.

            ## 0.3.0

            - clock · Raid time · Large.
            """);
        Assert.Equal(["0.4.1", "0.4.0", "0.3.0"], sections.Select(s => s.Label));
        Assert.Equal(["Praetorian", "Praetorian", null], sections.Select(s => s.Name));
        Assert.Equal("NEW IN 0.4.0 · PRAETORIAN", WhatsNew.Heading(sections[1]));
        Assert.Equal("NEW IN 0.4.1 · PRAETORIAN", WhatsNew.Heading(sections[0]));
        Assert.Equal("NEW IN 0.3.0", WhatsNew.Heading(sections[2]));
        // The running build's version: a patch or a build with its commit belongs to the line; another line has none.
        Assert.Equal("Praetorian", WhatsNew.NameOf(sections, WhatsNew.VersionOf("0.4.2+d349909")));
        Assert.Null(WhatsNew.NameOf(sections, WhatsNew.VersionOf("0.3.0-dev.261009131623")));
        Assert.Null(WhatsNew.NameOf(sections, WhatsNew.VersionOf("0.5.0")));
    }

    [Fact]
    public void The_first_named_release_is_praetorian()
    {
        var sections = WhatsNew.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "whats-new.md")));
        Assert.Equal("Praetorian", WhatsNew.NameOf(sections, new Version(0, 4, 0)));
    }

    [Theory]
    [InlineData("0.4.0", "0.4.0")]
    [InlineData("0.3.0-dev.20261007123456", "0.3.0")]
    [InlineData("v1.2", "1.2")]
    public void A_version_is_read_without_its_suffix(string text, string expected) => Assert.Equal(Version.Parse(expected), WhatsNew.VersionOf(text));

    [Fact]
    public void A_first_start_shows_none()
    {
        // Help opens then; the newest version counts as seen.
        Assert.Empty(WhatsNew.Due(WhatsNew.Parse(Two), seen: null, firstStart: true));
    }

    [Fact]
    public void After_an_update_from_before_the_card_the_newest_version_shows()
    {
        // Nothing says which version that was.
        var due = WhatsNew.Due(WhatsNew.Parse(Two), seen: null, firstStart: false);
        Assert.Equal(["0.5.0"], due.Select(s => s.Label));
    }

    [Fact]
    public void Every_version_newer_than_the_one_seen_shows_and_no_other()
    {
        var sections = WhatsNew.Parse(Two);
        Assert.Equal(["0.5.0", "0.4.0"], WhatsNew.Due(sections, "0.3.0", false).Select(s => s.Label));
        Assert.Equal(["0.5.0"], WhatsNew.Due(sections, "0.4.0", false).Select(s => s.Label));
        Assert.Empty(WhatsNew.Due(sections, "0.5.0", false));
    }

    // The app's own list: short, few, and only previews the app knows.
    [Fact]
    public void The_apps_list_keeps_to_the_cards_rules()
    {
        var sections = WhatsNew.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "whats-new.md")));
        Assert.NotEmpty(sections);
        Assert.All(sections, section =>
        {
            Assert.InRange(section.Items.Count, 1, WhatsNew.MaxItems);
            Assert.All(section.Items, item =>
            {
                Assert.True(WhatsNew.Known(item.Preview), $"{section.Label}: '{item.Preview}' isn't a preview the app knows");
                // A name stands on one line of the card: 31 characters, 0.7.1's "Your newest screenshot in front", do at the
                // narrowest rail, 900×560 (the release UI QA pass's stills, 2026-10-27; 28 until then, which that line broke).
                Assert.True(item.Name.Length <= NameLength, $"{section.Label}: '{item.Name}' is longer than a line of the card");
                Assert.True(item.Text.Length <= 100, $"{section.Label}: '{item.Text}' is longer than two lines of the card");
            });
        });
    }

    // Resting on a line shows its preview, but on a line about the tour, whose click opens it: there nothing shows, for
    // the pointer and for a developer script's "whatsnew <n>" alike (2026-10-15: the script's put up a label over nothing).
    [Theory]
    [InlineData("replay", true)]
    [InlineData("extracts", true)]
    [InlineData("clock", true)]
    [InlineData("joined", true)]
    [InlineData("leaders", true)]
    [InlineData("stash", true)]
    [InlineData("stash-card", true)]
    [InlineData("stash-given", true)]
    [InlineData("notes", true)]
    [InlineData("desk", true)]
    [InlineData("tasks", true)]
    [InlineData("textsize", true)]
    [InlineData("glance", true)]
    [InlineData("grid", true)]
    [InlineData("desk-look", true)]
    [InlineData("prices", true)]
    [InlineData("grid-pointer", true)]
    [InlineData("tour", false)]
    [InlineData("tour:1", false)]
    [InlineData("tour:5", false)]
    public void A_line_about_the_tour_previews_nothing(string preview, bool shows) => Assert.Equal(shows, WhatsNew.ShowsPreview(preview));

    [Fact]
    public void Every_preview_the_app_knows_shows_one()
    {
        Assert.All(WhatsNew.Previews, preview => Assert.True(WhatsNew.ShowsPreview(preview), preview));
        // 0.5.0's tour:5 line, the one the owner reported on 2026-10-15, is fourth (tools\qa\whatsnew-tour-line.devscript).
        var hyena = WhatsNew.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "whats-new.md")))
            .Single(s => s.Label == "0.5.0");
        Assert.Equal("tour:5", hyena.Items[3].Preview);
        Assert.False(WhatsNew.ShowsPreview(hyena.Items[3].Preview));
    }

    // Each preview starts from the player's map put back (UI QA re-check of the grid's fixes, 2026-10-26, R3: the clock
    // stood over the extract list's Customs): only these stage a map of their own; the others lay a plate or a cue over it.
    [Fact]
    public void Only_the_map_previews_stage_a_map_of_their_own() =>
        Assert.Equal(["extracts", "grid", "grid-pointer", "joined", "leaders", "replay"], WhatsNew.Previews.Where(WhatsNew.StagesMap).Order(StringComparer.Ordinal));

    // The features that move play a short sequence and hold its end (owner, 2026-10-27, D4 B): the desk, the glance and
    // the grid, and 0.7.1's newest look, prices' age and grid under the pointer, in 4 to 6 s; text size and the dailies are
    // stills.
    [Fact]
    public void Only_the_features_that_move_play_a_sequence_of_four_to_six_seconds()
    {
        Assert.Equal(["desk", "desk-look", "glance", "grid", "grid-pointer", "prices"], WhatsNew.Previews.Where(WhatsNew.Moves).Order(StringComparer.Ordinal));
        Assert.InRange(WhatsNew.Plays.TotalSeconds, 4, 6);
    }

    // A click holds a line's preview, in place of any other; on the line held it lets go (owner, 2026-10-27, D4 B). What's
    // New opens no tour: an older version's line about the tour holds nothing either.
    [Fact]
    public void A_click_holds_a_preview_and_a_second_lets_it_go()
    {
        Assert.Equal("0.7.0:0", WhatsNew.Click(null, "0.7.0:0", "desk"));
        Assert.Equal("0.7.0:4", WhatsNew.Click("0.7.0:0", "0.7.0:4", "grid"));
        Assert.Null(WhatsNew.Click("0.7.0:4", "0.7.0:4", "grid"));
        Assert.Null(WhatsNew.Click(null, "0.6.2:2", "tour:3"));
        Assert.Equal("0.7.0:1", WhatsNew.Click("0.7.0:1", "0.6.2:2", "tour:3"));
    }

    // The owner's words of 2026-10-27 (D4): "when I click on them the standard tour of the app starts. This should not be
    // the case, each feature should have a icon and a appropriate picture". From 0.7.0 on, every line has a preview of its
    // own, never the tour, in English and in each translation (which keeps the English previews; the test above).
    [Fact]
    public void From_0_7_0_on_every_line_previews_its_feature_and_none_is_about_the_tour()
    {
        var sections = WhatsNew.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "whats-new.md")));
        var kaomoji = sections.Single(s => s.Label == "0.7.0");
        Assert.Equal(["desk", "tasks", "textsize", "glance", "grid"], kaomoji.Items.Select(i => i.Preview));
        // 0.7.1's three lines each their own key (the release UI QA pass of 0.7.1, 2026-10-27: they had taken 0.7.0's desk,
        // desk and grid, whose previews show none of the newest look, the prices' age or the grid under the pointer).
        Assert.Equal(["desk-look", "prices", "grid-pointer"], sections.Single(s => s.Label == "0.7.1").Items.Select(i => i.Preview));
        Assert.All(sections.Where(s => s.Version >= new Version(0, 7, 0)).SelectMany(s => s.Items), item =>
        {
            Assert.True(WhatsNew.Previews.Contains(item.Preview), $"'{item.Preview}' ({item.Name}) isn't a preview of its own");
            Assert.Null(Tour.ChapterOf(item.Preview));
        });
    }

    // The note at the card's foot says what its lines do: point to preview, click to hold, wherever a line previews; no note
    // where none does (only older versions' lines about the tour). A card with several versions counts the lines of all
    // of them.
    [Fact]
    public void The_cards_foot_says_what_its_lines_do()
    {
        var sections = WhatsNew.Parse("""
            ## 0.7.0

            - desk · The stash desk · DESK, top left.
            - grid · A grid for your squad · YOU says your square.

            ## 0.6.2

            - leaders · Bosses, stepped back · One name each.
            - tour:3 · Plan by trader · Point to light their quests.

            ## 0.4.0

            - tour · The tour · Seven short chapters.
            """);
        WhatsNew.Section Version(string label) => sections.Single(s => s.Label == label);
        Assert.Equal(WhatsNew.Foot.PointAt, WhatsNew.FootOf([Version("0.7.0")]));
        Assert.Equal(WhatsNew.Foot.PointAt, WhatsNew.FootOf([Version("0.6.2")]));
        Assert.Equal(WhatsNew.Foot.None, WhatsNew.FootOf([Version("0.4.0")]));
        Assert.Equal(WhatsNew.Foot.PointAt, WhatsNew.FootOf([Version("0.7.0"), Version("0.4.0")]));
    }

    // What's New in another language (docs/DESIGN.md §8, "Texts"): docs/whats-new.<code>.md beside docs/whats-new.md, each
    // version's lines in the translation where it has them, else in English.
    [Fact]
    public void A_translation_stands_in_for_the_versions_it_has_and_english_for_the_rest()
    {
        var english = WhatsNew.Parse(Two);
        var translated = WhatsNew.Parse("""
            ## 0.4.0

            - replay · Raid-Wiederholung · Nach einem Raid: wo du Screenshots gemacht hast.
            - clock · Raidzeit, groß · Minuten übrig, groß.

            ## 0.6.0

            - leaders · Nicht in der englischen Datei · Bleibt weg.
            """);
        var shown = WhatsNew.InLanguage(english, translated);
        Assert.Equal(["0.5.0", "0.4.0"], shown.Select(s => s.Label));
        Assert.Equal("On a line to their place.", shown[0].Items[0].Text);
        Assert.Equal("Raid-Wiederholung", shown[1].Items[0].Name);
        Assert.Same(english, WhatsNew.InLanguage(english, null));
        // A translated section whose lines aren't the English section's: out of date, shown in English.
        var behind = WhatsNew.Parse("""
            ## 0.4.0

            - replay · Raid-Wiederholung · Nach einem Raid.
            """);
        Assert.Equal("Raid replay", WhatsNew.InLanguage(english, behind)[1].Items[0].Name);
        Assert.Equal("whats-new.de.md", BuiltDocs.TranslationOf(BuiltDocs.WhatsNew, "de"));
    }

    // Each translated What's New in docs: a language Shturmap knows, only versions the English file has, each with the
    // English section's lines (their previews, in order) and the card's limits (docs/whats-new.de.md, and each one after it).
    [Fact]
    public void Each_translated_list_has_only_versions_and_lines_the_english_has()
    {
        var root = RepositoryRoot();
        var english = WhatsNew.Parse(File.ReadAllText(Path.Combine(root, "docs", "whats-new.md")));
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "docs"), "whats-new.*.md"))
        {
            var name = Path.GetFileName(file);
            var language = name["whats-new.".Length..^".md".Length];
            Assert.True(UiLanguage.IsWritten(language), $"{name}: '{language}' is neither offered nor being translated (UiLanguage)");
            foreach (var section in WhatsNew.Parse(File.ReadAllText(file)))
            {
                var source = english.FirstOrDefault(s => s.Version == section.Version);
                Assert.True(source is not null, $"{name}: {section.Label} isn't a version of docs/whats-new.md");
                Assert.True(source.Items.Select(i => i.Preview).SequenceEqual(section.Items.Select(i => i.Preview)),
                    $"{name}, {section.Label}: the lines' previews aren't the English section's ({string.Join(", ", source.Items.Select(i => i.Preview))})");
                Assert.All(section.Items, item =>
                {
                    Assert.True(item.Name.Length <= NameLength, $"{name}, {section.Label}: '{item.Name}' is longer than a line of the card");
                    Assert.True(item.Text.Length <= 100, $"{name}, {section.Label}: '{item.Text}' is longer than two lines of the card");
                });
            }
        }
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
