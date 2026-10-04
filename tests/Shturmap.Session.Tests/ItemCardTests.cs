using Shturmap.Core.Logs;
using Shturmap.Core.Quests;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

// The item card and BRING's source line: how each active quest needs the item, and where to get it, easiest first,
// said as the data has it.
public class ItemCardTests
{
    private const string Salewa = "salewa", Beanie = "beanie", Glasses = "glasses", Ragman = "ragman";

    private static readonly Dictionary<string, string> Names = new()
    {
        [Salewa] = "Salewa first aid kit", [Beanie] = "Bomber beanie", [Glasses] = "RayBench sunglasses", ["bolts"] = "Bolts",
    };

    private static GameData Data(params ApiTask[] tasks) => new()
    {
        Mode = GameMode.Pve,
        Language = "en",
        Maps = new Dictionary<string, ApiMap>(),
        Tasks = tasks.ToDictionary(t => t.Id),
        Traders = new Dictionary<string, ApiTrader> { [Ragman] = new(Ragman, "Ragman", null, null) },
        ItemNames = Names,
        MapDefinitions = [],
        CheckedAt = DateTimeOffset.Now,
    };

    private static ApiTask Task(string id, params ApiObjective[] objectives) =>
        new(id, id, null, null, null, null, false, false, null, null, null, false, null, [.. objectives], null);

    private static ApiObjective Objective(string id, string type, int count = 1, List<string>? items = null, bool foundInRaid = false) =>
        new(id, type, id, false, null, null, null, count, null, items, null, null, null, foundInRaid);

    private static Dictionary<string, QuestStatus> States(QuestState state, params string[] ids) =>
        ids.ToDictionary(id => id, id => new QuestStatus(id, state, ObservationSource.Log, DateTime.Now));

    private static IEnumerable<string> Uses(GameData data, string item, params string[] active) =>
        ItemCards.Build(data, null, States(QuestState.Active, active), item).Uses.Select(u => $"{u.QuestName}: {u.How}");

    [Fact]
    public void An_item_is_found_in_raid_only_where_the_data_says_so()
    {
        var data = Data(
            Task("Shortage", Objective("s1", "findItem", 3, [Salewa], foundInRaid: true)),
            Task("Supply", Objective("p1", "findItem", 2, [Salewa])));
        Assert.Equal(["Shortage: Find in raid ×3", "Supply: Find ×2"], Uses(data, Salewa, "Shortage", "Supply"));
    }

    // ---- where to get it: an offer behind a quest isn't a way until the log has seen that quest completed ----

    // Prices under 1,000 read the same in every Windows language.
    private static ApiItem Item(string id, bool flea = true, params ApiTraderOffer[] offers) =>
        new(id, flea ? [] : ["noFlea"], null, flea ? 500 : null, [.. offers]);

    private static ItemSources Sources(ApiItem[] items, params ApiBarter[] barters) => new()
    {
        Items = items.ToDictionary(i => i.Id),
        Barters = barters.ToLookup(b => b.OfferedItem!.Item),
        Crafts = Array.Empty<ApiCraft>().ToLookup(c => ""),
        Stations = new Dictionary<string, string>(),
    };

    private static ApiTraderOffer AfterDandies(double price = 900) => new(Ragman, price, "RUB", price, 2, "Dandies");

    private static readonly ApiTask Dandies = Task("Dandies", Objective("d1", "plantItem", 1, [Beanie]));

    [Fact]
    public void An_offer_that_needs_the_quest_the_item_is_for_comes_after_every_other_way()
    {
        // The review of 2026-10-04: BRING named "Ragman LL2 · … · after Dandies" as the way to get Dandies' own beanie.
        var data = Data(Dandies);
        var sources = Sources([Item(Beanie, flea: true, AfterDandies())]);
        var card = ItemCards.Build(data, sources, States(QuestState.Active, "Dandies"), Beanie);
        Assert.Equal(["Flea market · ~500 ₽", "Ragman LL2 · 900 ₽ · after Dandies"], card.Get.Select(s => s.Text));
        Assert.Equal([false, true], card.Get.Select(s => s.Locked));
        Assert.Equal(SourceKind.Flea, ItemCards.Best(data, sources, Beanie, States(QuestState.Active, "Dandies"))!.Kind);
        // Without the quests' states at hand, an offer behind a quest counts as not open yet.
        Assert.Equal(SourceKind.Flea, ItemCards.Best(data, sources, Beanie)!.Kind);
        Assert.Equal("Flea market · ~500 ₽", ItemCards.BestOf(data, sources, [Beanie]));
    }

    [Fact]
    public void An_offer_behind_a_quest_is_still_said_when_it_is_the_only_way()
    {
        var data = Data(Dandies);
        var sources = Sources([Item(Beanie, flea: false, AfterDandies())]);
        var only = ItemCards.Best(data, sources, Beanie, States(QuestState.Active, "Dandies"))!;
        Assert.Equal("Ragman LL2 · 900 ₽ · after Dandies", only.Text);
        Assert.True(only.Locked);
    }

    [Fact]
    public void Once_the_log_saw_the_quest_completed_its_offer_is_one_like_any_other()
    {
        var data = Data(Dandies);
        var sources = Sources([Item(Beanie, flea: true, AfterDandies())]);
        var card = ItemCards.Build(data, sources, States(QuestState.Completed, "Dandies"), Beanie);
        Assert.Equal(["Ragman LL2 · 900 ₽", "Flea market · ~500 ₽"], card.Get.Select(s => s.Text));
        Assert.All(card.Get, s => Assert.False(s.Locked));
    }

    [Fact]
    public void An_open_offer_comes_before_a_cheaper_one_behind_a_quest_and_barters_follow_the_same_rule()
    {
        var data = Data(Dandies);
        var open = new ApiTraderOffer(Ragman, 950, "RUB", 950, 3, null);
        var barterOpen = new ApiBarter(Ragman, 1, null, [new("bolts", 2)], new(Beanie, 1));
        var barterLocked = new ApiBarter(Ragman, 1, "Dandies", [new("bolts", 1)], new(Beanie, 1));
        var sources = Sources([Item(Beanie, flea: false, AfterDandies(100), open)], barterLocked, barterOpen);
        var get = ItemCards.Build(data, sources, States(QuestState.Active, "Dandies"), Beanie).Get.Select(s => s.Text).ToList();
        Assert.Equal(
        [
            "Ragman LL3 · 950 ₽",
            "Ragman LL1 barter · 2× Bolts",
            "Ragman LL2 · 100 ₽ · after Dandies",
            "Ragman LL1 barter · 1× Bolts · after Dandies",
        ], get);
    }

    [Fact]
    public void Of_several_items_that_will_do_the_one_with_a_way_open_now_is_named()
    {
        var data = Data(Dandies);
        var sources = Sources([Item(Beanie, flea: false, AfterDandies()), Item(Glasses, flea: true)]);
        Assert.Equal("e.g. RayBench sunglasses · Flea market · ~500 ₽", ItemCards.BestOf(data, sources, [Beanie, Glasses], States(QuestState.Active, "Dandies")));
        // None open: the first with any way, still saying what it waits for.
        var behind = Sources([Item(Beanie, flea: false, AfterDandies()), Item(Glasses, flea: false)]);
        Assert.Equal("e.g. Bomber beanie · Ragman LL2 · 900 ₽ · after Dandies", ItemCards.BestOf(data, behind, [Beanie, Glasses]));
    }
}
