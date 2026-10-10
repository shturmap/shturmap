using Shturmap.Core.Logs;
using Shturmap.Core.Planning;
using Shturmap.Core.Quests;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

// The item card and BRING's source line: how each active quest needs the item, and where to get it, easiest first,
// said as the data has it.
public class ItemCardTests
{
    private const string Salewa = "salewa", Beanie = "beanie", Glasses = "glasses", Ragman = "ragman", Marker = "ms2000", Key = "key114", OtherKey = "key203";

    private static readonly Dictionary<string, string> Names = new()
    {
        [Salewa] = "Salewa first aid kit", [Beanie] = "Bomber beanie", [Glasses] = "RayBench sunglasses", ["bolts"] = "Bolts",
        [Marker] = "MS2000 Marker", [Key] = "Dorm room 114 key", [OtherKey] = "Dorm room 203 key",
    };

    private static ApiMap Map(string id, string name) => new(id, name, id, id, null, null, 40, null, null, null, null, null);

    private static GameData Data(params ApiTask[] tasks) => Data("en", tasks);

    // tarkov.dev's German data names the maps as its English does, but for Night Factory: "Factory bei Nacht".
    private static GameData Data(string language, params ApiTask[] tasks) => new()
    {
        Mode = GameMode.Pve,
        Language = language,
        Maps = new Dictionary<string, ApiMap>
        {
            ["streets"] = Map("streets", "Streets of Tarkov"), ["customs"] = Map("customs", "Customs"), ["woods"] = Map("woods", "Woods"),
            ["factory"] = Map("factory", "Factory"), ["night"] = Map("night", language == "de" ? "Factory bei Nacht" : "Night Factory"),
        },
        Tasks = tasks.ToDictionary(t => t.Id),
        Traders = new Dictionary<string, ApiTrader> { [Ragman] = new(Ragman, "Ragman", null, null) },
        ItemNames = Names,
        MapDefinitions = [],
        CheckedAt = DateTimeOffset.Now,
    };

    private static ApiTask Task(string id, params ApiObjective[] objectives) =>
        new(id, id, null, null, null, null, false, false, null, null, null, false, null, [.. objectives], null);

    private static ApiObjective Objective(string id, string type, int count = 1, List<string>? items = null, bool foundInRaid = false,
        string? map = null, string? marker = null, string? key = null) =>
        new(id, type, id, false, map is null ? null : [map], null, null, count, null, items, marker, null, key is null ? null : [[key]], foundInRaid);

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

    // The lines are whole texts with their count in them (docs/DESIGN.md §8, "Texts"): no count for one, "×n" from two.
    [Fact]
    public void A_line_says_its_count_from_two_up()
    {
        var data = Data(
            Task("Shortage", Objective("s1", "findItem", 1, [Salewa], foundInRaid: true)),
            Task("Supply", Objective("p1", "giveItem", 1, [Salewa])),
            Task("Trade", Objective("t1", "sellItem", 2, [Salewa])));
        Assert.Equal(["Shortage: Find in raid", "Supply: Hand over", "Trade: Sell ×2"], Uses(data, Salewa, "Shortage", "Supply", "Trade"));
        Assert.Equal("Loose on Customs · 1 spot", SessionTexts.ItemSourceLoose(count: 1, map: "Customs"));
        Assert.Equal("Loose on Customs · 3 spots", SessionTexts.ItemSourceLoose(count: 3, map: "Customs"));
        Assert.Equal("2× Bolts, 1× Nuts and 1 more", SessionTexts.ItemSourceBarterMore(first: "2× Bolts", more: 1, second: "1× Nuts"));
    }

    // Whether the card is a key's: from how the quests need it, not from the words of its lines, which are in the
    // language in use.
    [Fact]
    public void An_item_card_knows_a_key_by_how_it_is_needed()
    {
        var data = Data(Task("Ballet", Objective("b1", "visit", map: "customs", key: Key)), Revision);
        Assert.True(ItemCards.Build(data, null, States(QuestState.Active, "Ballet"), Key).IsKey);
        Assert.False(ItemCards.Build(data, null, States(QuestState.Active, "Revision"), Marker).IsKey);
    }

    // ---- what a quest takes, added up over its objectives as Plan's BRING adds it up ----

    private static readonly ApiTask Revision = Task("Revision",
        Objective("r1", "mark", marker: Marker, map: "streets"),
        Objective("r2", "mark", marker: Marker, map: "streets"),
        Objective("r3", "mark", marker: Marker, map: "streets"));

    [Fact]
    public void The_quest_card_adds_up_what_is_used_up_as_the_plan_does()
    {
        // The review of 2026-10-04: three "mark" objectives read "MS2000 Marker" on the card where Plan said "×3".
        var data = Data(Revision);
        var card = QuestCards.Build(data, States(QuestState.Active, "Revision"), "Revision")!;
        var need = Assert.Single(card.Needs);
        Assert.Equal(("MS2000 Marker ×3", "to mark · on Streets of Tarkov"), (need.Text, need.Where));

        var streets = new PlanMap("streets", "Streets of Tarkov", new HashSet<string> { "streets" }, 40);
        var planned = Assert.Single(RaidPlanner.Plan([Planning.ToPlan(Revision, data)], streets).Requirements);
        Assert.Equal(Planning.RequirementText(data, planned).Text, need.Text);
        // The item card says the same of the quest.
        Assert.Equal(["Revision: Bring ×3, to mark · Streets of Tarkov"], Uses(data, Marker, "Revision"));
    }

    [Fact]
    public void A_key_needed_on_two_maps_is_one_row_naming_both()
    {
        var task = Task("Ballet", Objective("b1", "visit", map: "customs", key: Key), Objective("b2", "visit", map: "streets", key: Key))
            with { NeededKeys = [new("streets", [Key])] };
        var data = Data(task);
        var need = Assert.Single(QuestCards.Build(data, States(QuestState.Active, "Ballet"), "Ballet")!.Needs);
        Assert.Equal(("Dorm room 114 key", "key · on Customs, Streets of Tarkov"), (need.Text, need.Where));
        Assert.Equal(["Ballet: Key · Customs, Streets of Tarkov"], Uses(data, Key, "Ballet"));
    }

    [Fact]
    public void One_item_used_two_ways_is_one_row_saying_both_and_gear_to_wear_stays_its_own()
    {
        var wear = Objective("d3", "shoot", map: "streets") with { Wearing = [[new(Beanie, null)]] };
        var task = Task("Dandies",
            Objective("d1", "plantItem", 2, [Beanie], map: "streets"),
            Objective("d2", "useItem", map: "customs") with { UseAny = [Beanie] },
            wear);
        var data = Data(task);
        var needs = QuestCards.Build(data, States(QuestState.Active, "Dandies"), "Dandies")!.Needs;
        Assert.Equal(
        [
            ("Bomber beanie ×3", "to plant and to use · on Customs, Streets of Tarkov"),
            ("Bomber beanie", "to wear · on Streets of Tarkov"),
        ], needs.Select(n => (n.Text, n.Where)));
        Assert.Equal(
        [
            "Dandies: Bring ×3, to plant and to use · Customs, Streets of Tarkov",
            "Dandies: Wear, for kills · Streets of Tarkov",
        ], Uses(data, Beanie, "Dandies"));
    }

    // ---- what the cards say for the linked highlight (the review of 2026-10-04, E3 and E4) ----

    [Fact]
    public void A_row_for_either_of_two_keys_stands_for_both()
    {
        // It pictures the first key and was linked to it alone.
        var task = Task("Ballet", Objective("b1", "visit", map: "customs") with { RequiredKeys = [[Key, OtherKey]] });
        var need = Assert.Single(QuestCards.Build(Data(task), States(QuestState.Active, "Ballet"), "Ballet")!.Needs);
        Assert.Equal("Dorm room 114 key or Dorm room 203 key", need.Text);
        Assert.Equal(Key, need.ItemId);
        Assert.Equal([Key, OtherKey], need.Alternatives);
        // A plain row stands for its one item.
        var plain = Assert.Single(QuestCards.Build(Data(Revision), States(QuestState.Active, "Revision"), "Revision")!.Needs);
        Assert.Equal([Marker], plain.Alternatives);
    }

    [Fact]
    public void An_item_card_names_each_quest_that_needs_the_item_once()
    {
        // What the card keeps lit beside its item while it is read: Dandies uses the beanie two ways, and is one quest.
        var wear = Objective("d3", "shoot", map: "streets") with { Wearing = [[new(Beanie, null)]] };
        var data = Data(Task("Dandies", Objective("d1", "plantItem", 2, [Beanie], map: "streets"), wear),
            Task("Other", Objective("o1", "plantItem", 1, [Beanie], map: "customs")),
            Task("Idle", Objective("i1", "plantItem", 1, [Beanie], map: "customs")));
        var card = ItemCards.Build(data, null, States(QuestState.Active, "Dandies", "Other"), Beanie);
        Assert.Equal(3, card.Uses.Count);
        Assert.Equal(["Dandies", "Other"], card.QuestIds);
    }

    // ---- the quest card's objective rows: the map is said once (the review of 2026-10-04, C2) ----

    private static CardObjective Row(string text, string[] maps, string type = "visit", bool optional = false, string language = "en")
    {
        var objective = Objective("o1", type) with { Description = text, Maps = [.. maps], Optional = optional };
        return QuestCards.Build(Data(language, Task("Quest", objective)), States(QuestState.Active, "Quest"), "Quest")!.Objectives.Single();
    }

    [Fact]
    public void An_objective_whose_text_names_its_map_does_not_say_the_map_again()
    {
        Assert.Equal("", Row("Locate the convoy on Streets of Tarkov", ["streets"]).Where);
        // In a list that holds all of them, too.
        Assert.Equal("", Row("Eliminate Scavs on Customs or Streets of Tarkov", ["customs", "streets"], "shoot").Where);
        // "(optional)" is added after the text was looked at.
        var row = Row("Locate the convoy on Streets of Tarkov", ["streets"], optional: true);
        Assert.Equal(("Locate the convoy on Streets of Tarkov (optional)", ""), (row.Text, row.Where));
    }

    [Fact]
    public void The_map_stays_under_a_text_that_does_not_say_where()
    {
        Assert.Equal("Streets of Tarkov", Row("Locate the convoy", ["streets"]).Where);
        // The map's name in the text, but not as the place: a gate, not the map.
        Assert.Equal("Customs", Row("Stash the package at Customs gate", ["customs"]).Where);
        // One of its two maps named: the line keeps both.
        Assert.Equal("Customs, Streets of Tarkov", Row("Eliminate Scavs on Customs", ["customs", "streets"], "shoot").Where);
        // No map at all: "any map" stays.
        Assert.Equal("any map", Row("Eliminate Scavs", [], "shoot").Where);
    }

    // German texts say where with "auf" or "in" (owner, 2026-10-11: German cards kept both lines, lines longer).
    [Fact]
    public void A_german_objective_whose_text_names_its_map_does_not_say_the_map_again()
    {
        Assert.Equal("", Row("Finde den Konvoi auf Streets of Tarkov", ["streets"], language: "de").Where);
        Assert.Equal("", Row("Erkunde den Bunker in Customs", ["customs"], language: "de").Where);
        Assert.Equal("", Row("Eliminiere Scavs auf Factory bei Nacht", ["night"], "shoot", language: "de").Where);
        // In a list, with no comma before "oder".
        Assert.Equal("", Row("Eliminiere Scavs auf Woods, Customs oder Streets of Tarkov", ["woods", "customs", "streets"], "shoot", language: "de").Where);
        // Whatever follows in lower case: a condition, or the clause's verb at its end.
        Assert.Equal("", Row("Eliminiere Scavs auf Customs, während du eine Maske trägst", ["customs"], "shoot", language: "de").Where);
        Assert.Equal("", Row("Eliminiere Scavs auf Customs mit einer Schrotflinte", ["customs"], "shoot", language: "de").Where);
        Assert.Equal("", Row("Erkunde die Halle auf Woods (in einem Raid)", ["woods"], language: "de").Where);
        Assert.Equal("", Row("Lege die Kiste beim Tor auf Customs ab", ["customs"], language: "de").Where);
        Assert.Equal("", Row("Finde das Paket, das in Customs versteckt ist", ["customs"], language: "de").Where);
    }

    [Fact]
    public void A_german_map_line_stays_under_a_text_that_does_not_say_where()
    {
        Assert.Equal("Customs", Row("Finde den Konvoi", ["customs"], language: "de").Where);
        // Where to or from isn't where it is.
        Assert.Equal("Customs", Row("Benutze den Transit nach Customs", ["customs"], language: "de").Where);
        Assert.Equal("Customs", Row("Überlebe und entkomme aus Customs", ["customs"], language: "de").Where);
        Assert.Equal("Customs", Row("Benutze den Transit von Customs", ["customs"], language: "de").Where);
        // The map's name in a place of its own: a noun follows it, or a longer map's name starts with it.
        Assert.Equal("Factory", Row("Verstecke das Paket in Factory Tor 3", ["factory"], language: "de").Where);
        Assert.Equal("Factory", Row("Eliminiere Scavs auf Factory bei Nacht", ["factory"], "shoot", language: "de").Where);
        // One of its two maps named: the line keeps both.
        Assert.Equal("Customs, Streets of Tarkov", Row("Eliminiere Scavs auf Customs", ["customs", "streets"], "shoot", language: "de").Where);
        // Each language's pattern is its own: an English sentence in German data, a German one in English data.
        Assert.Equal("Customs", Row("Locate the convoy on Customs", ["customs"], language: "de").Where);
        Assert.Equal("Customs", Row("Finde den Konvoi auf Customs", ["customs"]).Where);
        // A language with no pattern keeps both lines.
        Assert.Equal("Customs", Row("Trouve le convoi sur Customs", ["customs"], language: "fr").Where);
    }

    private sealed class Offline : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("offline: the test reads the cache only");
    }

    // tarkov.dev's own data in both languages, from the app's download cache (its quest texts aren't in the repository);
    // skipped where there is no German cache. On 2026-10-11 the German cards left out 363 of 936 lines of map names, the
    // English ones 577: the German sentences that keep theirs say where to or from ("nach", "aus", "von"), don't name
    // every map of the line, or are still English in the German data (docs/DESIGN.md §8, "The app's own language").
    // Far fewer would mean that the German sentences say where in a way the pattern doesn't know.
    [Fact]
    public async Task German_cards_of_the_real_data_say_the_map_once_where_the_sentence_names_it()
    {
        var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shturmap", "cache", "tarkov-dev");
        if (!File.Exists(Path.Combine(cache, "pve_tasks.json")) || !File.Exists(Path.Combine(cache, "pve_tasks_de.json")))
            Assert.Skip($"No tarkov.dev cache with German texts at {cache}: run Shturmap with the game in German once to download it.");
        GameData english, german;
        try
        {
            var loader = new GameDataLoader(new CachedHttp(new HttpClient(new Offline()), cache));
            english = await loader.LoadAsync(GameMode.Pve, "en", TestContext.Current.CancellationToken);
            german = await loader.LoadAsync(GameMode.Pve, "ge", TestContext.Current.CancellationToken);
        }
        catch (HttpRequestException e)
        {
            Assert.Skip("The tarkov.dev cache is incomplete: " + e.Message);
            throw;
        }
        if (german.Language != "de")
            Assert.Skip("The tarkov.dev cache has no complete German texts.");

        int lines = 0, dropped = 0, droppedEnglish = 0;
        foreach (var task in german.Tasks.Values)
        {
            var active = States(QuestState.Active, task.Id);
            var englishRows = QuestCards.Build(english, active, task.Id)!.Objectives.ToDictionary(o => o.ObjectiveId);
            foreach (var row in QuestCards.Build(german, active, task.Id)!.Objectives)
            {
                // The rows that have a line to leave out: a map of the data's, or "any map".
                var o = task.Objectives!.Single(x => x.Id == row.ObjectiveId);
                var maps = (o.Maps ?? []).Concat((o.Zones ?? []).Select(z => z.Map)).Concat((o.PossibleLocations ?? []).Select(l => l.Map));
                if (!maps.Any(m => m is not null && german.Maps.ContainsKey(m)) && !QuestTaxonomy.WorksAnywhere(row.Kind))
                    continue;
                lines++;
                dropped += row.Where.Length == 0 ? 1 : 0;
                droppedEnglish += englishRows.GetValueOrDefault(row.ObjectiveId)?.Where.Length == 0 ? 1 : 0;
            }
        }
        var counts = $"German cards left out {dropped} of {lines} lines of map names, English ones {droppedEnglish}";
        TestContext.Current.SendDiagnosticMessage(counts);
        Assert.True(lines > 500, $"only {lines} lines: is the cache complete?");
        Assert.True(dropped * 2 > droppedEnglish, counts);
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
