using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

// The trader focus (owner, 2026-10-17: "you can only select by map and not by trader"; from a panel of three, "B", a
// trader strip under the MAP list): a portrait per trader with active quests and their count; pointing lights that
// trader's quests, a click holds the focus, which re-counts the maps to the trader and steps the other quests back.
public class TraderFocusTests
{
    // The game's trader order, as tarkov.dev lists them.
    private static readonly string[] Order = ["prapor", "therapist", "skier", "peacekeeper", "mechanic", "ragman", "jaeger"];

    private static readonly (string QuestId, string? TraderId)[] Active =
    [
        ("dandies", "ragman"), ("audit", "skier"), ("ballet", "ragman"), ("cpsu", "prapor"), ("unknown", null), ("road", "ragman"),
    ];

    // ---- the strip ----

    [Fact]
    public void The_strip_has_each_trader_with_active_quests_in_the_games_order_with_their_count()
    {
        var strip = TraderFocus.Strip(Active, Order, held: null);
        Assert.Equal(["prapor", "skier", "ragman"], strip.Select(t => t.TraderId));
        Assert.Equal([1, 1, 3], strip.Select(t => t.Count));
        Assert.Equal(["dandies", "ballet", "road"], strip.Single(t => t.TraderId == "ragman").Quests);
    }

    [Fact]
    public void No_active_quests_no_strip_and_a_trader_the_data_doesnt_list_comes_last()
    {
        Assert.Empty(TraderFocus.Strip([], Order, held: null));
        var strip = TraderFocus.Strip([("x", "btr-driver"), ("y", "jaeger"), ("x", "btr-driver")], Order, held: null);
        Assert.Equal(["jaeger", "btr-driver"], strip.Select(t => t.TraderId));
        // A quest counts once.
        Assert.Equal(1, strip.Last().Count);
    }

    [Fact]
    public void The_held_trader_keeps_its_place_with_no_quests_left_so_its_focus_can_be_let_go()
    {
        var strip = TraderFocus.Strip([("cpsu", "prapor"), ("road", "ragman")], Order, held: "therapist");
        Assert.Equal(["prapor", "therapist", "ragman"], strip.Select(t => t.TraderId));
        Assert.Equal(0, strip[1].Count);
    }

    [Fact]
    public void In_a_raid_the_strip_shows_only_the_held_trader_or_nothing()
    {
        var strip = TraderFocus.Strip(Active, Order, held: null);
        Assert.Equal(3, TraderFocus.Shown(strip, held: null, inRaid: false).Count);
        Assert.Equal(3, TraderFocus.Shown(strip, held: "ragman", inRaid: false).Count);
        Assert.Empty(TraderFocus.Shown(strip, held: null, inRaid: true));
        Assert.Equal(["ragman"], TraderFocus.Shown(strip, held: "ragman", inRaid: true).Select(t => t.TraderId));
    }

    [Fact]
    public void A_portraits_tooltip_says_the_trader_how_many_active_quests_and_what_a_click_does()
    {
        Assert.Equal("Ragman · 4 active quests · click to hold", TraderFocus.Tip("Ragman", 4, held: false));
        Assert.Equal("Ragman · 1 active quest · click to hold", TraderFocus.Tip("Ragman", 1, held: false));
        Assert.Equal("Ragman · 4 active quests · click to let go", TraderFocus.Tip("Ragman", 4, held: true));
    }

    // ---- holding and letting go ----

    [Fact]
    public void A_click_holds_one_trader_at_a_time_and_a_click_on_the_held_one_lets_go()
    {
        var held = TraderFocus.Click(null, "ragman");
        Assert.Equal("ragman", held);
        // Another portrait takes the focus over: one at a time.
        held = TraderFocus.Click(held, "skier");
        Assert.Equal("skier", held);
        Assert.Null(TraderFocus.Click(held, "skier"));
    }

    [Fact]
    public void Esc_closes_the_cards_first_and_with_none_open_lets_go()
    {
        Assert.Equal(("ragman", true), TraderFocus.Escape("ragman", cardsOpen: true));
        Assert.Equal((null, false), TraderFocus.Escape("ragman", cardsOpen: false));
        Assert.Equal((null, true), TraderFocus.Escape(null, cardsOpen: true));
    }

    // ---- what steps back ----

    [Fact]
    public void Held_the_other_traders_quests_step_back_in_plans_rows_but_picks_and_the_raid_card_dont()
    {
        Assert.False(TraderFocus.StepsBack(null, inRaid: false, "prapor"));
        Assert.True(TraderFocus.StepsBack("ragman", inRaid: false, "prapor"));
        Assert.True(TraderFocus.StepsBack("ragman", inRaid: false, null));
        Assert.False(TraderFocus.StepsBack("ragman", inRaid: false, "ragman"));
        Assert.False(TraderFocus.StepsBack("ragman", inRaid: false, "prapor", picked: true));
        Assert.False(TraderFocus.StepsBack("ragman", inRaid: true, "prapor"));
    }

    [Fact]
    public void A_bring_row_steps_back_when_it_serves_none_of_the_traders_quests_nor_a_pick()
    {
        Assert.True(TraderFocus.StepsBackServing("ragman", false, [("prapor", false), ("skier", false)]));
        Assert.False(TraderFocus.StepsBackServing("ragman", false, [("prapor", false), ("ragman", false)]));
        Assert.False(TraderFocus.StepsBackServing("ragman", false, [("prapor", true)]));
        Assert.False(TraderFocus.StepsBackServing(null, false, [("prapor", false)]));
        Assert.False(TraderFocus.StepsBackServing("ragman", true, [("prapor", false)]));
    }

    [Fact]
    public void The_map_lights_the_held_traders_quests_in_a_raid_too()
    {
        Assert.Equal(["ballet", "dandies", "road"], TraderFocus.OnMap("ragman", Active).Order());
        Assert.Empty(TraderFocus.OnMap(null, Active));
    }

    // ---- Plan's rows and the MAP list, re-counted ----

    [Fact]
    public void A_maps_quests_count_per_trader()
    {
        var counts = PlanList.ByTrader(["ragman", "prapor", "ragman", null, "skier", "ragman"]);
        Assert.Equal(3, counts["ragman"]);
        Assert.Equal(1, counts["prapor"]);
        Assert.Equal(1, counts["skier"]);
        Assert.Equal(3, counts.Count);
        Assert.Empty(PlanList.ByTrader([]));
    }

    [Fact]
    public void Held_a_maps_count_is_the_traders_and_a_map_without_them_says_nothing_and_steps_back()
    {
        Assert.Equal("Complete 5 · progress 1", PlanList.Count("Complete 5 · progress 1", trader: null, count: 0));
        Assert.Equal("Ragman 2", PlanList.Count("Complete 5 · progress 1", "Ragman", 2));
        Assert.Equal("", PlanList.Count("Complete 5 · progress 1", "Ragman", 0));
        Assert.False(PlanList.StepsBack(null, 0));
        Assert.False(PlanList.StepsBack("Ragman", 2));
        Assert.True(PlanList.StepsBack("Ragman", 0));
    }

    [Fact]
    public void Held_the_rows_go_by_the_traders_quests_most_first_ties_in_the_planners_order_and_back_when_let_go()
    {
        // The planner's order, with the held trader's quests on each map.
        (string Map, int Count)[] rows = [("streets", 1), ("customs", 0), ("woods", 3), ("interchange", 1), ("factory", 0), ("lighthouse", 2)];
        Assert.Equal(["woods", "lighthouse", "streets", "interchange", "customs", "factory"],
            PlanList.Ranked(rows, r => r.Count, held: true).Select(r => r.Map));
        Assert.Equal(rows.Select(r => r.Map), PlanList.Ranked(rows, r => r.Count, held: false).Select(r => r.Map));
        Assert.Empty(PlanList.Ranked(Array.Empty<(string, int)>(), r => r.Item2, held: true));
    }
}
