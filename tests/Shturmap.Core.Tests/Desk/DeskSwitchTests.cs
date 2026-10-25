using Shturmap.App.Rules;

namespace Shturmap.Core.Tests.Desk;

// PLAN | DESK (owner, 2026-10-19, from a panel: A1; docs/DESIGN.md §4, principle 3 and *The stash desk*, *Its place*): the
// desk opens by itself with a reading in the menus, the switch and Esc leave it, a raid loading takes the window to Raid
// and back to Plan after, and the page stays for next time (Rules.DeskSwitch, Rules.DeskPageRules.Entering).
public class DeskSwitchTests
{
    [Fact]
    public void A_raid_shows_raid_whatever_was_chosen_and_the_menus_show_the_choice()
    {
        Assert.Equal(WindowState.Raid, DeskSwitch.Shown(inRaid: true, deskChosen: true));
        Assert.Equal(WindowState.Raid, DeskSwitch.Shown(inRaid: true, deskChosen: false));
        Assert.Equal(WindowState.Desk, DeskSwitch.Shown(inRaid: false, deskChosen: true));
        Assert.Equal(WindowState.Plan, DeskSwitch.Shown(inRaid: false, deskChosen: false));
    }

    [Fact]
    public void A_new_reading_in_the_menus_opens_the_desk_and_nothing_else_moves_it()
    {
        Assert.True(DeskSwitch.ChosenAfter(chosen: false, inRaid: false, newReading: true, tourUp: false));
        // The same page published again (prices, a pick): Plan stays Plan, the desk stays the desk.
        Assert.False(DeskSwitch.ChosenAfter(chosen: false, inRaid: false, newReading: false, tourUp: false));
        Assert.True(DeskSwitch.ChosenAfter(chosen: true, inRaid: false, newReading: false, tourUp: false));
    }

    [Fact]
    public void A_raid_takes_the_window_and_gives_it_back_to_plan()
    {
        // The desk was up; the raid loads: the choice goes, a reading or not.
        Assert.False(DeskSwitch.ChosenAfter(chosen: true, inRaid: true, newReading: false, tourUp: false));
        Assert.False(DeskSwitch.ChosenAfter(chosen: false, inRaid: true, newReading: true, tourUp: false));
        // Back in the menus without a new reading: Plan.
        Assert.False(DeskSwitch.ChosenAfter(chosen: false, inRaid: false, newReading: false, tourUp: false));
        Assert.Equal(WindowState.Plan, DeskSwitch.Shown(false, DeskSwitch.ChosenAfter(false, false, false, false)));
    }

    [Fact]
    public void While_the_tour_is_up_a_reading_waits_on_the_switch()
    {
        Assert.False(DeskSwitch.ChosenAfter(chosen: false, inRaid: false, newReading: true, tourUp: true));
    }

    // The reading counter counts the readings taken while Shturmap runs: a page read back from its file is 0 and opens
    // nothing; a reading after it is new; the same counter again is not.
    [Fact]
    public void A_reading_is_new_only_past_the_last_seen()
    {
        Assert.False(DeskSwitch.IsNew(null, 0));
        Assert.False(DeskSwitch.IsNew(0, 0));
        Assert.True(DeskSwitch.IsNew(1, 0));
        Assert.False(DeskSwitch.IsNew(2, 2));
        Assert.True(DeskSwitch.IsNew(3, 2));
    }

    // The desk keeps its page through a raid: a snapshot without a new reading brings nothing in.
    [Fact]
    public void The_page_is_kept_through_a_raid()
    {
        string[] keys = ["loadout", "backpack", "stash"];
        Assert.Empty(DeskPageRules.Entering(keys, new HashSet<string> { "backpack" }, first: false, newReading: false));
    }

    // The map's keys leave the map behind the desk alone (PgUp and PgDn scroll the page); Esc and F1 stay the window's.
    [Fact]
    public void The_maps_keys_go_on_to_the_desk()
    {
        Assert.All(new[] { 0x46, 0x30, 0x60, 0x21, 0x22, ZoomKeys.Plus, ZoomKeys.Minus, ZoomKeys.NumPadPlus, ZoomKeys.NumPadMinus },
            key => Assert.True(DeskSwitch.MapKey(key)));
        Assert.False(DeskSwitch.MapKey(0x1B));
        Assert.False(DeskSwitch.MapKey(0x70));
    }

    // The switch, a primary control (owner, 2026-10-19): ← picks PLAN, → DESK; Up, Down, Space and the rest pick nothing.
    [Fact]
    public void Left_picks_plan_and_right_the_desk()
    {
        Assert.False(DeskSwitch.ByArrow(0x25));
        Assert.True(DeskSwitch.ByArrow(0x27));
        Assert.All(new[] { 0x26, 0x28, 0x20, 0x0D, 0x09 }, key => Assert.Null(DeskSwitch.ByArrow(key)));
    }

    // The desk's arrival is the feedback (owner, 2026-10-19: "It now conflicts with the 'now seen in stash' animation"): a
    // reading's needed items are said by the one-line notice while the desk opens; the SEEN IN YOUR STASH cue plays only as
    // What's New's example. The desk still opens for the reading, as before.
    [Fact]
    public void A_readings_needed_items_are_a_notice_and_the_cue_only_an_example()
    {
        Assert.Equal(StashSeenSaid.Notice, DeskSwitch.StashSeen(example: false));
        Assert.Equal(StashSeenSaid.Cue, DeskSwitch.StashSeen(example: true));
        Assert.True(DeskSwitch.ChosenAfter(false, inRaid: false, newReading: true, tourUp: false));
    }
}
