using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

// What's New, a card in Plan's rail after an update (owner, 2026-10-07: "Whats new: A"; docs/DESIGN.md §4, "Screen
// anatomy"). Its lines come from docs/whats-new.md, built into the app.
public class WhatsNewTests
{
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
                Assert.True(item.Name.Length <= 28, $"{section.Label}: '{item.Name}' is longer than a line of the card");
                Assert.True(item.Text.Length <= 100, $"{section.Label}: '{item.Text}' is longer than two lines of the card");
            });
        });
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
