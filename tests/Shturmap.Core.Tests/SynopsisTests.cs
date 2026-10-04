using Shturmap.Core.Quests;

namespace Shturmap.Core.Tests;

// The few words under a quest's name in Plan (owner, 2026-10-02), from tarkov.dev's objective texts.
public class SynopsisTests
{
    private static readonly string[] AllMaps =
        ["Customs", "Factory", "Night Factory", "Woods", "Shoreline", "Interchange", "Reserve", "Lighthouse", "Streets of Tarkov",
         "Ground Zero", "Ground Zero 21+", "The Lab", "The Labyrinth"];

    private static SynopsisObjective O(string text, int count = 1, string type = "visit", bool place = false) => new(text, count, type, place);

    private static string Line(string map, params SynopsisObjective[] objectives) => Line([map], objectives);

    private static string Line(string[] here, params SynopsisObjective[] objectives) => QuestSynopsis.Of(objectives, here, AllMaps).Text;

    [Fact]
    public void Replaces_the_verb_and_drops_the_maps_name() =>
        Assert.Equal("Get bronze pocket watch", Line("Customs", O("Locate and obtain the bronze pocket watch on Customs", type: "findQuestItem")));

    [Fact]
    public void Drops_the_place_when_the_map_marks_it() =>
        Assert.Equal("Get valuable item", Line("Customs", O("Locate and obtain the valuable item in dorm room 203 on Customs", type: "findQuestItem", place: true)));

    [Fact]
    public void Keeps_the_place_when_the_map_has_no_marker() =>
        Assert.Equal("Get valuable item in dorm room 203", Line("Customs", O("Locate and obtain the valuable item in dorm room 203 on Customs", type: "findQuestItem")));

    [Fact]
    public void Cuts_a_place_with_the_word_that_leads_into_it() =>
        Assert.Equal("Find water stockpile", Line("Customs", O("Locate the water stockpile hidden inside one of the dorm rooms on Customs", place: true)));

    [Fact]
    public void Keeps_the_place_when_one_word_would_be_left()
    {
        Assert.Equal("Stash package at laboratory storage room", Line("Factory", O("Stash the package at the laboratory storage room on Factory", type: "plantQuestItem", place: true)));
        Assert.Equal("Find boat hidden next to breakwater", Line("Shoreline", O("Locate the boat hidden next to the breakwater on Shoreline", place: true)));
    }

    [Fact]
    public void What_to_get_is_not_a_place() =>
        Assert.Equal("Get compromising information on Ref", Line("Customs", O("Locate and obtain the compromising information on Ref", type: "findQuestItem", place: true)));

    [Fact]
    public void A_kill_says_its_target_and_count() =>
        Assert.Equal("Kill any target ×5", Line(["Ground Zero", "Ground Zero 21+"], O("Eliminate any target on Ground Zero", 5, "shoot")));

    [Fact]
    public void A_kills_count_goes_before_its_conditions() =>
        Assert.Equal("Kill Scavs ×10 with M4A1, M16, ADAR, or TX-15",
            Line("Shoreline", O("Eliminate Scavs with an M4A1, M16, ADAR, or TX-15 on Shoreline", 10, "shoot")));

    [Fact]
    public void Exclusions_and_conditions_stay_word_for_word() =>
        Assert.Equal("Kill any Boss ×5 (excluding The Goons, The Wedge and Partisan) while using any Goons Edition tactical rig",
            Line("Factory", O("Locate and neutralize any Boss (excluding The Goons, The Wedge and Partisan) while using any Goons Edition tactical rig", 5, "shoot")));

    [Fact]
    public void Times_and_without_stay() =>
        Assert.Equal("Kill Scavs ×6 during 21:00-04:00 without using any NVGs or thermal sights (Excluding Factory)",
            Line("Customs", O("Eliminate Scavs during 21:00-04:00 without using any NVGs or thermal sights (Excluding Factory)", 6, "shoot")));

    [Fact]
    public void A_number_the_count_contradicts_is_left_out()
    {
        // Job for a Patriot: the text says 20, the count field 10.
        var line = Line("Customs", O("Eliminate 20 PMCs with AK-12 with the proprietary suppressor and PS-320 scope on Lighthouse, Customs, or Reserve", 10, "shoot"));
        Assert.Equal("Kill PMCs with AK-12 with proprietary suppressor and PS-320 scope", line);
    }

    [Fact]
    public void A_number_the_count_confirms_stays_once() =>
        Assert.Equal("Kill 20 PMCs", Line("Customs", O("Eliminate 20 PMCs on Customs", 20, "shoot")));

    [Fact]
    public void A_list_of_maps_holding_this_one_goes() =>
        Assert.Equal("Kill Scavs ×5", Line("Customs", O("Eliminate Scavs on Woods, Ground Zero, Interchange, or Customs", 5, "shoot")));

    [Fact]
    public void Map_names_match_in_any_case() =>
        Assert.Equal("Find first pharmacy on Primorsky Ave", Line("Streets of Tarkov", O("Locate the first pharmacy on Primorsky Ave on Streets of tarkov")));

    [Fact]
    public void A_name_that_starts_a_longer_place_is_not_the_map() =>
        Assert.Equal("Find tank at Factory gate", Line("Factory", O("Locate the tank at Factory gate")));

    [Fact]
    public void Extracts_say_where() =>
        Assert.Equal("Extract through Railroad Passage (Flare)", Line("Customs", O("Survive and extract from Customs through Railroad Passage (Flare)", type: "extract")));

    [Fact]
    public void Extracting_from_this_map_is_said_short() =>
        Assert.Equal("Survive and extract", Line("Customs", O("Survive and extract from the location", type: "extract")));

    [Fact]
    public void Transits_say_where_to() =>
        Assert.Equal("Transit to Reserve", Line("Customs", O("Use the transit from Customs to Reserve", type: "extract")));

    [Fact]
    public void Found_in_raid_items_merge_with_their_counts() =>
        Assert.Equal("FIR Respirator ×4, Medical bloodset ×3",
            Line("Customs", O("Find the item in raid: Respirator", 4, "findItem"), O("Find the item in raid Medical bloodset", 3, "findItem")));

    [Fact]
    public void An_items_count_goes_at_the_end() =>
        Assert.Equal("Stash Golden neck chains ×3",
            Line("Customs", O("Stash Golden neck chains in the microwave on the 3rd floor of the dorm on Customs", 3, "plantItem", place: true)));

    [Fact]
    public void The_marker_and_the_specified_spot_go() =>
        Assert.Equal("Mark first LAV III, Stryker, second LAV III", Line("Streets of Tarkov",
            O("Locate and mark the first LAV III with an MS2000 Marker on Streets of Tarkov", type: "mark", place: true),
            O("Locate and mark the Stryker with an MS2000 Marker on Streets of Tarkov", type: "mark", place: true),
            O("Locate and mark the second LAV III with an MS2000 Marker on Streets of Tarkov", type: "mark", place: true)));

    // The raid card says each objective by its own phrase (owner, 2026-10-04): the same words as Plan's line, unmerged.
    [Fact]
    public void Each_objective_has_its_own_phrase()
    {
        var line = QuestSynopsis.Of(
        [
            O("Locate and mark the first LAV III with an MS2000 Marker on Streets of Tarkov", type: "mark", place: true),
            O("Locate and mark the Stryker with an MS2000 Marker on Streets of Tarkov", type: "mark", place: true),
        ], ["Streets of Tarkov"], AllMaps);
        Assert.Equal(new[] { "Mark first LAV III", "Mark Stryker" }, line.Phrases.Select(p => p.Text).ToArray());
    }

    [Fact]
    public void Merged_objects_say_shared_words_once() =>
        Assert.Equal("Stash AK-50 body, handguard, barrel", Line("Customs",
            O("Stash the AK-50 body at the specified spot on Customs", type: "plantQuestItem", place: true),
            O("Stash the AK-50 handguard at the specified spot on Customs", type: "plantQuestItem", place: true),
            O("Stash the AK-50 barrel at the specified spot on Customs", type: "plantQuestItem", place: true)));

    [Fact]
    public void A_shared_condition_is_said_once() =>
        Assert.Equal("Kill Knight, Big Pipe, Birdeye (in one raid)", Line("Lighthouse",
            O("Locate and neutralize Knight (in one raid)", type: "shoot"),
            O("Locate and neutralize Big Pipe (in one raid)", type: "shoot"),
            O("Locate and neutralize Birdeye (in one raid)", type: "shoot")));

    [Fact]
    public void Shared_words_are_never_taken_across_a_count() =>
        Assert.Equal("Kill Scavs ×10 at old gas station, ×10 at new gas station", Line("Customs",
            O("Eliminate Scavs at the old gas station on Customs", 10, "shoot"), O("Eliminate Scavs at the new gas station on Customs", 10, "shoot")));

    [Fact]
    public void Long_places_keep_their_own_endings()
    {
        var line = Line("Lighthouse",
            O("Place a WI-FI Camera at the yellow bulldozer in the south-eastern part of the water treatment plant", type: "plantItem"),
            O("Place a WI-FI Camera at the police truck in the western part of the water treatment plant", type: "plantItem"));
        Assert.Contains("south-eastern part of the water treatment plant, ", line);
        Assert.EndsWith("western part of the water treatment plant", line);
    }

    [Fact]
    public void Objectives_that_would_read_the_same_keep_their_places() =>
        // Spotter: two sniping positions.
        Assert.Equal("Find good sniping position in Concordia overlooking construction site · Hide Trijicon REAP-IR thermal scope · " +
                     "Find good sniping position at Primorsky overlooking movie theater",
            Line("Streets of Tarkov",
                O("Locate a good sniping position in Concordia overlooking the construction site", place: true),
                O("Hide Trijicon REAP-IR thermal scope under Makhors' bed", type: "plantItem", place: true),
                O("Locate a good sniping position at Primorsky overlooking the movie theater", place: true)));

    [Fact]
    public void A_text_with_no_known_verb_is_shown_as_written_less_the_map()
    {
        var line = QuestSynopsis.Of([O("Talk to the BTR Driver on Streets of Tarkov")], ["Streets of Tarkov"], AllMaps);
        Assert.Equal("Talk to the BTR Driver", line.Text);
        Assert.True(line.HasFallback);
    }

    [Fact]
    public void Objectives_without_text_are_skipped() =>
        Assert.Equal("Kill Reshala", Line("Customs", O(""), O("Locate and neutralize Reshala", type: "shoot")));

    [Fact]
    public void The_check_finds_an_added_word()
    {
        var source = O("Eliminate Scavs on Customs", 5, "shoot");
        Assert.Contains(QuestSynopsis.Problems(new SynopsisPhrase(source, "Kill", "Scavs quickly ×5", false)), p => p.Contains("quickly"));
        Assert.Empty(QuestSynopsis.Problems(new SynopsisPhrase(source, "Kill", "Scavs ×5", false)));
    }

    [Fact]
    public void The_check_finds_a_dropped_condition_and_a_cut_mid_phrase()
    {
        var source = O("Locate and neutralize any Boss (excluding The Goons, The Wedge and Partisan) while using any Goons Edition tactical rig", 5, "shoot");
        var problems = QuestSynopsis.Problems(new SynopsisPhrase(source, "Kill", "any Boss ×5 while using", false));
        Assert.Contains(problems, p => p.Contains("(excluding The Goons, The Wedge and Partisan)"));
        Assert.Contains(problems, p => p.Contains("mid-phrase"));
    }
}
