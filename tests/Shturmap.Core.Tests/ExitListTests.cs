using Shturmap.Core.Screenshots;

namespace Shturmap.Core.Tests;

// The game's extract list, read from a screenshot that shows it (owner, 2026-10-05): words become rows, and rows are
// matched to the map's exits by name. The words below are what Windows' text recognition gave for real screenshots
// of a 2560×1440 game ("Ø" for the slashed zero, "I" for 1).
public class ExitListTests
{
    private static readonly ExitName[] Streets =
    [
        new("courtyard", ["Courtyard"]),
        new("taxi", ["Primorsky Ave Taxi V-Ex"]),
        new("crash", ["Crash Site"]),
        new("house", ["Damaged House"]),
        new("klimov", ["Klimov Street (Flare)"]),
        new("pinewood", ["Pinewood Basement (Co-op)"]),
        new("sewer", ["Sewer River"]),
        new("crane", ["Collapsed Crane"]),
    ];

    private static ExitListReading Reading(params (string Text, bool Marked)[] rows) =>
        new(ExitList.EnglishHeader, rows.Select(r => new ExitListRow(r.Text, r.Marked)).ToList());

    [Fact]
    public void Rows_name_their_exits_whatever_the_label_was_read_as()
    {
        var found = ExitList.Match(Reading(
            ("EXFILØI Courtyard", true),
            ("EXFILØ2 Primorsky Ave Taxi V-Ex", true),
            ("EXFILØ3 Crash Site", false),
            ("EXFILØ4 Damaged House", false),
            ("EXFILØ5 Klimov Street (Flare)", false),
            ("EXFILØ6 Pinewood Basement (Co-Op)", false),
            ("TRANSITØI Transit to Ground Zero", true),
            ("TRANSIT 02 Transit to Interchange", true),
            ("TerroGroup Lobs access keycard required (1)", false)), Streets);
        Assert.Equal(["courtyard", "crash", "house", "klimov", "pinewood", "taxi"], found.Keys.Order());
        Assert.True(found["courtyard"]);
        Assert.True(found["taxi"]);
        Assert.False(found["crash"]);
    }

    [Fact]
    public void A_name_may_be_a_little_off_and_lack_its_label_or_its_bracket()
    {
        var found = ExitList.Match(Reading(("Darnaged House", false), ("EXFIL 05 Klimov Street", false), ("Sewer Rlver", true)), Streets);
        Assert.Equal(["house", "klimov", "sewer"], found.Keys.Order());
    }

    [Fact]
    public void The_data_may_name_an_exit_in_two_languages()
    {
        var exits = new[] { new ExitName("klimov", ["Klimow-Straße", "Klimov Street (Flare)"]) };
        Assert.Single(ExitList.Match(Reading(("EXFILØ5 Klimov Street (Flare)", false)), exits));
        Assert.Single(ExitList.Match(Reading(("EXFILØ5 Klimow-Straße", false)), exits));
    }

    [Fact]
    public void Look_alike_names_are_told_apart_and_a_tie_names_neither()
    {
        var customs = new[] { new ExitName("zb1011", ["ZB-1011"]), new ExitName("zb1012", ["ZB-1012"]), new ExitName("zb013", ["ZB-013"]) };
        Assert.Equal(["zb1012"], ExitList.Match(Reading(("EXFILØ3 ZB-IØI2", true)), customs).Keys);
        // One letter from both: a guess, so neither.
        Assert.Empty(ExitList.Match(Reading(("EXFILØ3 ZB-1013", false)), customs));
    }

    [Fact]
    public void Two_exits_of_one_name_are_one_row()
    {
        var exits = new[] { new ExitName("pmc", ["Crossroads"]), new ExitName("shared", ["Crossroads"]) };
        Assert.Equal(2, ExitList.Match(Reading(("EXFILØI Crossroads", false)), exits).Count);
    }

    [Fact]
    public void Something_from_the_scene_names_nothing()
    {
        Assert.Empty(ExitList.Match(Reading(("IPXU 337216 7", false), ("22G1", false)), Streets));
    }

    [Fact]
    public void A_row_that_folds_to_nothing_is_passed_over_and_the_good_rows_still_count()
    {
        // Review of 2026-10-09: "??:??:??" on a line of its own, a dash or noise threw, and the whole list was lost.
        var found = ExitList.Match(Reading(
            ("EXFILØI Courtyard", true),
            ("??:??:??", true),
            ("—", false),
            ("", false),
            ("EXFILØ3 Crash Site", false)), Streets);
        Assert.Equal(["courtyard", "crash"], found.Keys.Order());
        // An exit whose names fold to nothing is never named, and doesn't stop the others.
        var odd = Streets.Append(new ExitName("odd", ["—", "??"])).ToList();
        Assert.Equal(["courtyard", "crash"], ExitList.Match(Reading(("EXFILØI Courtyard", false), ("??", false), ("EXFILØ3 Crash Site", false)), odd).Keys.Order());
    }

    [Fact]
    public void One_named_exit_is_the_list_only_under_the_lists_own_header()
    {
        // Standing in an exit, the game shows a green bar and that one exit: no list.
        Assert.False(ExitList.IsList(new ExitListReading("Stay in the extraction point", []), 1));
        Assert.True(ExitList.IsList(new ExitListReading("Find an extroction point", []), 1));
        Assert.True(ExitList.IsList(new ExitListReading("", []), 2));
        Assert.False(ExitList.IsList(new ExitListReading(ExitList.EnglishHeader, []), 0));
    }

    [Fact]
    public void Words_become_the_header_and_rows_and_the_scene_left_of_the_list_is_left_out()
    {
        // The bar spans x 132–705, y 5–78 (a 2560×1440 screenshot's corner).
        ReadWord[] words =
        [
            new("Find", 202, 30, 60, 25), new("an", 270, 34, 30, 20), new("extraction", 310, 30, 150, 25), new("point", 470, 30, 80, 25),
            new("EXFILØI", 146, 91, 120, 25), new("Courtyard", 281, 94, 110, 23),
            new("Site", 350, 233, 50, 18), new("EXFILØ3", 146, 230, 120, 24), new("Crash", 285, 233, 60, 18),
            new("IPXU", 20, 235, 80, 30),
        ];
        var (header, lines) = ExitList.Lines(words, 132, 5, 78);
        Assert.Equal("Find an extraction point", header);
        Assert.Equal(["EXFILØI Courtyard", "EXFILØ3 Crash Site"], lines.Select(l => l.Text));
        Assert.Equal(91, lines[0].Top);
        Assert.Equal(117, lines[0].Bottom);
    }
}
