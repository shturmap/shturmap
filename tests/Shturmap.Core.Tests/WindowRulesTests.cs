using Shturmap.App.Rules;
using Shturmap.Core.Raid;

namespace Shturmap.Core.Tests;

// The main window's small rules from the review of 2026-10-04: which map's scene a snapshot may fill (A32), what
// gives way around a raid (A42, H9), the keys that zoom (A42), Plan's map list (B13) and the status bar (C4).
public class WindowRulesTests
{
    // ---- A32: a snapshot never writes into another map's scene ----

    [Fact]
    public void A_map_counts_as_shown_only_once_its_artwork_arrived()
    {
        var scenes = new SceneGate();
        Assert.True(scenes.Wants("customs"));
        // A second snapshot about Customs arrives while its artwork is on its way: nothing to fill yet, nothing to fetch twice.
        Assert.False(scenes.Wants("customs"));
        Assert.False(scenes.Holds("customs"));
        Assert.True(scenes.Arrived("customs"));
        Assert.True(scenes.Holds("customs"));
        Assert.False(scenes.Wants("customs"));
    }

    [Fact]
    public void While_the_next_maps_artwork_is_on_its_way_the_old_scene_takes_nothing_of_the_new_map()
    {
        var scenes = new SceneGate();
        scenes.Wants("customs");
        scenes.Arrived("customs");

        // The raid loads on Woods: the view still holds Customs' scene.
        Assert.True(scenes.Wants("woods"));
        Assert.False(scenes.Holds("woods"));
        Assert.True(scenes.Holds("customs"));
        Assert.True(scenes.Arrived("woods"));
        Assert.True(scenes.Holds("woods"));
        Assert.False(scenes.Holds("customs"));
        Assert.False(scenes.Holds(null));
    }

    [Fact]
    public void Artwork_that_arrives_for_a_map_no_longer_wanted_is_dropped()
    {
        var scenes = new SceneGate();
        scenes.Wants("customs");
        scenes.Arrived("customs");
        scenes.Wants("woods");
        // Changed again before Woods arrived: Streets is wanted now.
        Assert.True(scenes.Wants("streets"));
        Assert.False(scenes.Arrived("woods"));
        Assert.True(scenes.Arrived("streets"));

        // And back to the map in the view while another was on its way: that one is dropped, nothing is fetched.
        scenes.Wants("woods");
        Assert.False(scenes.Wants("streets"));
        Assert.False(scenes.Arrived("woods"));
        Assert.True(scenes.Holds("streets"));
    }

    [Fact]
    public void A_preview_takes_the_view_and_the_shown_maps_scene_is_made_anew_after_it()
    {
        var scenes = new SceneGate();
        scenes.Wants("customs");
        scenes.Arrived("customs");
        scenes.Forget();
        Assert.False(scenes.Holds("customs"));
        Assert.True(scenes.Wants("customs"));
        // Artwork fetched before the preview took the view isn't the one waited for any more.
        scenes.Forget();
        Assert.False(scenes.Arrived("customs"));
    }

    // ---- A42, H9: around a raid ----

    [Theory]
    [InlineData(RaidPhase.Menu, RaidPhase.Loading, true)]
    [InlineData(RaidPhase.Menu, RaidPhase.InRaid, true)]
    [InlineData(RaidPhase.Loading, RaidPhase.InRaid, true)]
    [InlineData(RaidPhase.InRaid, RaidPhase.Loading, true)] // a transit: the next map loads
    [InlineData(RaidPhase.InRaid, RaidPhase.InRaid, false)]
    [InlineData(RaidPhase.Loading, RaidPhase.Loading, false)]
    [InlineData(RaidPhase.Menu, RaidPhase.Menu, false)]
    [InlineData(RaidPhase.InRaid, RaidPhase.Menu, false)]
    [InlineData(RaidPhase.Loading, RaidPhase.Menu, false)] // matching cancelled
    public void Cards_and_help_let_go_at_each_step_into_a_raid_and_at_no_other_time(RaidPhase before, RaidPhase now, bool close) =>
        Assert.Equal(close, WhileInRaid.LetsGo(before, now));

    [Theory]
    [InlineData(RaidPhase.Menu, false)]
    [InlineData(RaidPhase.Loading, true)]
    [InlineData(RaidPhase.InRaid, true)]
    public void What_comes_up_by_itself_waits_for_the_raid_to_be_over(RaidPhase phase, bool waits) =>
        Assert.Equal(waits, WhileInRaid.Waits(phase));

    [Fact]
    public void Plus_zooms_on_a_keyboard_where_it_needs_shift()
    {
        // US: "+" is Shift and the "=" key, virtual key 0xBB; elsewhere (German) 0xBB is "+" without Shift.
        Assert.Contains((0xBB, true), ZoomKeys.In);
        Assert.Contains((0xBB, false), ZoomKeys.In);
        Assert.Contains((0x6B, false), ZoomKeys.In);
        Assert.Contains((0xBD, false), ZoomKeys.Out);
        Assert.Contains((0x6D, false), ZoomKeys.Out);
        Assert.Empty(ZoomKeys.In.Intersect(ZoomKeys.Out));
    }

    // ---- B13: Plan's rows show a map ----

    [Theory]
    [InlineData("", "customs", false)]
    [InlineData("customs", "customs", false)]      // one suggested map, on screen: its card says it all
    [InlineData("customs", "woods", true)]         // another map on screen: the row is the way to the suggested one
    [InlineData("customs", "factory", true)]       // a map without quests on screen: its card has no row, Customs' row is the way back
    [InlineData("customs,woods", "customs", true)]
    [InlineData("streets,customs,woods,interchange", "woods", true)]
    [InlineData("customs", null, true)]
    public void The_map_list_is_there_whenever_a_row_is_the_way_to_a_map(string rows, string? shown, bool listed) =>
        Assert.Equal(listed, PlanList.Shown(rows.Split(',', StringSplitOptions.RemoveEmptyEntries), shown));

    // ---- 2026-10-08: the rail follows the map on screen ----

    [Theory]
    [InlineData("streets,customs", "customs", true, 1, false)]   // suggested: its own row's card
    [InlineData("streets,customs", "shoreline", true, 2, true)]  // planned but not suggested (quests below the rows, or none): its card after the rows
    [InlineData("streets,customs", "factory", false, -1, false)] // no plan for it: no card, and never another map's
    [InlineData("streets,customs", null, false, 0, false)]       // nothing on screen yet: the best suggestion's
    [InlineData("", null, false, -1, false)]
    [InlineData("", "factory", true, 0, true)]                    // no suggestions at all: still the map on screen's card
    public void The_open_card_is_the_map_on_screen(string suggested, string? shown, bool planned, int open, bool added)
    {
        var result = PlanList.Open(suggested.Split(',', StringSplitOptions.RemoveEmptyEntries), shown, planned);
        Assert.Equal((open, added), result);
    }

    // ---- 2026-10-08: the MAP list says what each map holds ----

    private static readonly MapList.Map[] Maps =
    [
        new("customs", "Customs", ["customs"]),
        new("factory", "Factory", ["factory", "night-factory"]),
        new("interchange", "Interchange", ["interchange"]),
        new("streets-of-tarkov", "Streets of Tarkov", ["streets-of-tarkov"]),
        new("woods", "Woods", ["woods"]),
    ];

    [Fact]
    public void The_maps_with_quests_come_first_in_plans_order_with_their_counts_then_the_others_by_name()
    {
        var list = MapList.Build(Maps, [("woods", "Complete 1 · progress 1"), ("customs", "Complete 2")]);
        Assert.Equal(["woods", "customs", "", "factory", "interchange", "streets-of-tarkov"], list.Select(c => c.NormalizedName));
        Assert.Equal(["Complete 1 · progress 1", "Complete 2", "", "", "", ""], list.Select(c => c.Count));
        var heading = Assert.Single(list, c => c.IsHeader);
        Assert.Equal("OTHER MAPS", heading.Name);
        Assert.Equal(2, list.ToList().IndexOf(heading));
    }

    [Fact]
    public void A_list_of_one_kind_has_no_heading()
    {
        Assert.DoesNotContain(MapList.Build(Maps, []), c => c.IsHeader);
        Assert.DoesNotContain(MapList.Build(Maps, Maps.Select(m => (m.NormalizedName, "Progress 1"))), c => c.IsHeader);
        // A map Plan counts that the list doesn't have (no artwork) adds nothing.
        Assert.Equal(5, MapList.Build(Maps, [("labs", "Complete 1")]).Count);
    }

    [Fact]
    public void A_variant_on_screen_is_its_maps_entry_and_the_heading_never_is()
    {
        var list = MapList.Build(Maps, [("customs", "Complete 2")]);
        Assert.Equal("factory", MapList.For(list, "night-factory")?.NormalizedName);
        Assert.Equal("factory", MapList.For(list, "factory")?.NormalizedName);
        Assert.Null(MapList.For(list, ""));
        Assert.Null(MapList.For(list, null));
        Assert.Null(MapList.For(list, "labs"));
        Assert.Equal("Factory", list.First(c => c.NormalizedName == "factory").ToString());
    }

    [Fact]
    public void The_list_is_made_anew_only_when_what_it_says_changes()
    {
        var a = MapList.Build(Maps, [("customs", "Complete 2")]);
        Assert.True(MapList.Same(a, MapList.Build(Maps, [("customs", "Complete 2")])));
        Assert.False(MapList.Same(a, MapList.Build(Maps, [("customs", "Complete 3")])));
        Assert.False(MapList.Same(a, MapList.Build(Maps, [("woods", "Complete 2")])));
        Assert.False(MapList.Same([], a));
    }

    // ---- C4: the status bar ----

    [Fact]
    public void No_position_is_said_only_in_a_raid()
    {
        Assert.Equal("No position yet · press PrtSc or Home", StatusBarFit.NoPosition(RaidPhase.InRaid, "PrtSc or Home"));
        Assert.Equal("", StatusBarFit.NoPosition(RaidPhase.Menu, "PrtSc or Home"));
        Assert.Equal("", StatusBarFit.NoPosition(RaidPhase.Loading, "PrtSc or Home"));
    }

    [Fact]
    public void The_lights_words_go_when_the_bar_is_too_narrow_and_come_back_with_room_to_spare()
    {
        Assert.True(StatusBarFit.Words(shown: true, room: 1600, needed: 1200));
        Assert.True(StatusBarFit.Words(shown: true, room: 1200, needed: 1200));
        Assert.False(StatusBarFit.Words(shown: true, room: 900, needed: 1200));
        // Back only with some room to spare, so a figure more in the last fix doesn't switch them on and off.
        Assert.False(StatusBarFit.Words(shown: false, room: 1200, needed: 1200));
        Assert.False(StatusBarFit.Words(shown: false, room: 1200 + StatusBarFit.Slack - 1, needed: 1200));
        Assert.True(StatusBarFit.Words(shown: false, room: 1200 + StatusBarFit.Slack, needed: 1200));
    }
}
