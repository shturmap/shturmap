using Shturmap.Core.Logs;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

// What an extract takes must not depend on the game's language (review of 2026-10-04, A34): "(Flare)" and "(Co-op)"
// were looked for in the translated name, and German has "Mira-Allee" for "Mira Ave (Flare)". The rules read the
// game's internal name and the English name, whatever language the data is shown in.
public class ExtractRulesTests
{
    private static ApiExtract Extract(string id, string name, params string[] switches) =>
        new(id, name, "pmc", null, null, null, null, [.. switches]);

    private static ApiMap Map(List<ApiExtract> extracts, params ApiSwitch[] switches) =>
        new("map", "Karte", "map", "map", null, null, 40, extracts, null, null, null, null, Switches: [.. switches]);

    private static GameData Data(Dictionary<string, string> keys, Dictionary<string, string> english) => new()
    {
        Mode = GameMode.Pve,
        Language = "de",
        Maps = new Dictionary<string, ApiMap>(),
        Tasks = new Dictionary<string, ApiTask>(),
        Traders = new Dictionary<string, ApiTrader>(),
        ExtractKeys = keys,
        EnglishNames = english,
        MapDefinitions = [],
        CheckedAt = DateTimeOffset.UnixEpoch,
    };

    // The flare exits take a green flare, not a red one (owner, 2026-10-17: "Fix"; the verification of 2026-10-17: the
    // wiki's "Extractions" of all six, and the RSP-30 green's "Usage").
    private const string GreenFlareLine = "Fire a green signal flare there";

    [Fact]
    public void A_flare_exit_is_one_in_any_language_and_takes_a_green_flare()
    {
        // The German name has no "(Flare)"; the internal name and the English name both say it.
        var exit = Extract("e1", "Mira-Allee");
        var byKey = Data(new() { ["e1"] = "Sniper_exit" }, new() { ["e1"] = "Mira Ave" });
        var byEnglish = Data(new() { ["e1"] = "exit_7" }, new() { ["e1"] = "Mira Ave (Flare)" });
        foreach (var data in new[] { byKey, byEnglish })
        {
            Assert.Equal(GreenFlareLine, ExtractRules.Needs(data, Map([exit]), exit).Text);
            Assert.Equal([(ExtractRules.GreenFlare, 1)], ExtractRules.Items(data, exit));
        }
        // The RSP-30 green cartridge, not the red one (62178c4d4ecf221597654e3d) the rule named until 2026-10-17.
        Assert.Equal("6217726288ed9f0845317459", ExtractRules.GreenFlare);
    }

    [Fact]
    public void Icebreakers_helicopter_takes_a_green_flare_and_its_fare()
    {
        // Its name says nothing of a flare ("Helicopter", internal name "Heli"); the hand-kept list does, by internal
        // name and id, both.
        var (id, key) = ("9bf65c4c24d0153f1542a90ddabadb37198ab9ff", "Heli");
        var heli = new ApiExtract(id, "Hubschrauber", "pmc", null, null, null, null, null, new ApiCount("569668774bdc2da2298b4568", 2400));
        var data = Data(new() { [id] = key }, new() { [id] = "Helicopter" });
        Assert.Equal(GreenFlareLine + " · Pay €2,400", ExtractRules.Needs(data, Map([heli]), heli).Text);
        Assert.Equal([(ExtractRules.GreenFlare, 1), ("569668774bdc2da2298b4568", 2400)], ExtractRules.Items(data, heli));
        // Another extract of that internal name, or that id under another name, is no flare exit.
        var other = heli with { Id = "another" };
        Assert.Equal("Pay €2,400", ExtractRules.Needs(Data(new() { ["another"] = key }, []), Map([other]), other).Text);
        Assert.Equal("Pay €2,400", ExtractRules.Needs(Data(new() { [id] = "Heli_2" }, []), Map([heli]), heli).Text);
    }

    [Fact]
    public void An_exit_with_sniper_in_its_name_is_not_a_flare_exit()
    {
        // Customs' "Sniper Roadblock" is an ordinary exit: its internal name is its English name.
        var exit = Extract("e1", "Sniper Roadblock");
        var data = Data(new() { ["e1"] = "Sniper Roadblock" }, new() { ["e1"] = "Sniper Roadblock" });
        Assert.Equal("", ExtractRules.Needs(data, Map([exit]), exit).Text);
        Assert.Empty(ExtractRules.Items(data, exit));
    }

    [Fact]
    public void A_co_op_exit_is_one_in_any_language()
    {
        var exit = Extract("e1", "Scav-Lager");
        var data = Data(new() { ["e1"] = "Interchange Cooperation" }, new() { ["e1"] = "Scav Camp (Co-Op)" });
        Assert.Equal("Co-op: a PMC and a player Scav leave together", ExtractRules.Needs(data, Map([exit]), exit).Text);
        // Nothing to bring for it.
        Assert.Empty(ExtractRules.Items(data, exit));
    }

    [Fact]
    public void Without_recorded_english_names_the_name_itself_is_read()
    {
        // English data made by hand (tests, a fake game): the name is the English name.
        var exit = Extract("e1", "Klimov Street (Flare)");
        var data = Data([], []);
        Assert.Equal(GreenFlareLine, ExtractRules.Needs(data, Map([exit]), exit).Text);
    }

    // ---- the extracts a switch gates (owner, 2026-10-17: "Some exits still require specific actions, for example one
    // exit in interchange requires the power to be switched on"; from a panel of three: "A") ----

    // A row of the hand-kept table as a map: its extract under its id and internal name, a plain extract beside it, its
    // switches named "<first>" and "Step 2" …, and a keycard's lock where the row has one. Every extract lists the
    // first switch, as tarkov.dev lists one lever on all of Customs: that tells none of them apart.
    private static (ApiMap Map, GameData Data, ApiExtract Gated, ApiExtract Plain) TableMap(SwitchExits.Row row, string first)
    {
        var switches = row.Steps.Where(s => !s.Lock).Select((s, i) => new ApiSwitch(s.Id, i == 0 ? first : $"Step {i + 1}", new ApiPosition(10 * (i + 1), 0, 0))).ToList();
        var locks = row.Steps.Where(s => s.Lock).Select(s => new ApiLock(s.Id, "door", "keycard", false, new ApiPosition(5, 0, 5))).ToList();
        var gated = new ApiExtract(row.ExtractId, "Gated", "pmc", new ApiPosition(0, 0, 0), null, null, null, [row.Steps[0].Id]);
        var plain = new ApiExtract("plain", "Plain", "pmc", new ApiPosition(100, 0, 100), null, null, null, [row.Steps[0].Id]);
        var map = new ApiMap("map-1", "Map", row.Map, row.Map, null, null, 40, [gated, plain], [], locks, [], [], Switches: switches);
        var data = Data(new() { [row.ExtractId] = row.Key, ["plain"] = "plain_exit" }, []);
        return (map, data, gated, plain);
    }

    [Theory]
    [InlineData("ex_customs_pmc_zb1013", "ZB-013 Power Switch", "ZB-013 Power Switch first")]
    [InlineData("ex_interchange_pmc_saferoom", "Mall Main Power Switch", "Mall Main Power Switch first · 4 steps")]
    [InlineData("EXFIL_Bunker_D2", "D-2 Power Switch", "D-2 Power Switch first · 2 steps")]
    [InlineData("EXFIL_Bunker", "Bunker Hermetic Door Power Switch", "Bunker Hermetic Door Power Switch first")]
    [InlineData("lab_Elevator_Cargo", "Cargo Elevator Power Button", "Cargo Elevator Power Button first · 3 steps")]
    [InlineData("lab_Elevator_Main", "Main Elevator Power Button", "Main Elevator Power Button first · 3 steps")]
    [InlineData("lab_Elevator_Med", "Med Elevator Power Button", "Med Elevator Power Button first · 3 steps")]
    [InlineData("lab_Hangar_Gate", "Hangar Gate Switch", "Hangar Gate Switch first")]
    [InlineData("lab_Parking_Gate", "Parking Gate Switch", "Parking Gate Switch first")]
    [InlineData("lab_Under_Storage_Collector", "Sewage Conduit Pump Button", "Sewage Conduit Pump Button first · 2 steps")]
    public void Each_extract_of_the_table_says_its_first_switch_and_how_many_steps(string key, string first, string line)
    {
        var row = Assert.Single(SwitchExits.Table, r => r.Key == key);
        var (map, data, gated, plain) = TableMap(row, first);
        Assert.Equal(line, ExtractRules.Needs(data, map, gated).Text);
        // A plain extract of the same map gives none, though the data lists the switch on it too.
        Assert.Equal("", ExtractRules.Needs(data, map, plain).Text);
        // Switches take no item.
        Assert.Empty(ExtractRules.Items(data, gated));
    }

    [Fact]
    public void The_table_has_the_ten_gated_extracts_with_their_steps_in_order()
    {
        Assert.Equal(10, SwitchExits.Table.Count);
        Assert.Equal(10, SwitchExits.Table.Select(r => r.ExtractId).Distinct().Count());
        var saferoom = Assert.Single(SwitchExits.Table, r => r.Map == "interchange");
        // Power, the urinal, the Object #11SR keycard's lock, the door's button.
        Assert.Equal([false, false, true, false], saferoom.Steps.Select(s => s.Lock));
        Assert.Equal("lock:7429efb2dcb7016e715077fb0a686ffdb56fae2e", saferoom.Steps[2].MarkerId);
        Assert.Equal("switch:b6ebc39ee29cb6659334df309194b696bbfef555", saferoom.Steps[0].MarkerId);
        Assert.Equal(["customs", "interchange", "reserve", "reserve", "the-lab", "the-lab", "the-lab", "the-lab", "the-lab", "the-lab"],
            SwitchExits.Table.Select(r => r.Map));
    }

    [Fact]
    public void A_row_holds_only_for_its_map_its_internal_name_and_its_id()
    {
        var row = SwitchExits.Table[0];
        var (map, data, gated, _) = TableMap(row, "ZB-013 Power Switch");
        Assert.NotNull(SwitchExits.RowOf(data, map, gated));
        // Another map of that name, another id under the internal name, another internal name under the id.
        Assert.Null(SwitchExits.RowOf(data, map with { NormalizedName = "woods" }, gated));
        var moved = gated with { Id = "new-id" };
        Assert.Null(SwitchExits.RowOf(Data(new() { ["new-id"] = row.Key }, []), map, moved));
        Assert.Null(SwitchExits.RowOf(Data(new() { [row.ExtractId] = "other_key" }, []), map, gated));
        Assert.Equal("", ExtractRules.Needs(Data(new() { [row.ExtractId] = "other_key" }, []), map, gated).Text);
    }

    [Fact]
    public void A_switch_most_exits_list_shows_nowhere()
    {
        // tarkov.dev lists one lever on every exit; it tells none of them apart, whatever its name says. Until 2026-10-17
        // it was shown where its English name held the exit's, which never matched ("ZB-013" in "ZB-013 Power Switch",
        // the exit "ZB-1013").
        var bunker = Extract("e1", "Bunker ZB-013", "s1");
        var gate = Extract("e2", "Tor", "s1");
        var road = Extract("e3", "Strasse", "s1");
        var lever = new ApiSwitch("s1", "Stromschalter am Bunker");
        var map = Map([bunker, gate, road], lever);
        var data = Data([], new() { ["e1"] = "ZB-013", ["e2"] = "Gate", ["e3"] = "Road", ["s1"] = "ZB-013 Power Switch" });
        Assert.All(new[] { bunker, gate, road }, e => Assert.Equal("", ExtractRules.Needs(data, map, e).Text));
    }

    [Fact]
    public void A_switch_the_data_lists_on_some_extracts_still_says_so_with_what_unlocks_it()
    {
        // The data's own word, where it tells extracts apart (none today): its switch, and the power switch that
        // unlocks it, first.
        var gate = Extract("e1", "Tor", "lever");
        var road = Extract("e2", "Strasse");
        var other = Extract("e3", "Weg");
        var map = Map([gate, road, other], new ApiSwitch("power", "Stromschalter", null, [new("Unlock", "lever")]), new ApiSwitch("lever", "Hebel"));
        var data = Data([], []);
        Assert.Equal("Stromschalter first · 2 steps", ExtractRules.Needs(data, map, gate).Text);
        Assert.Equal(["power", "lever"], SwitchExits.Of(data, map, gate).Select(s => s.Id));
        Assert.Equal("", ExtractRules.Needs(data, map, road).Text);
        // A switch the data leaves untranslated is "Switch", as on the map.
        var bare = Map([gate, road, other], new ApiSwitch("lever", "switch_00405_call_button"));
        Assert.Equal("Switch first", ExtractRules.Needs(data, bare, gate).Text);
    }

    private sealed class Offline : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("offline: the test reads the cache only");
    }

    private static string Cache => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shturmap", "cache", "tarkov-dev");

    // tarkov.dev's own data from the app's download cache (its data isn't in the repository); null where there is none.
    private static async Task<GameData?> CachedAsync(GameMode mode, string language)
    {
        if (!File.Exists(Path.Combine(Cache, GameData.Slug(mode) + "_maps.json")))
            return null;
        try
        {
            var loader = new GameDataLoader(new Shturmap.Data.Http.CachedHttp(new HttpClient(new Offline()), Cache));
            return await loader.LoadAsync(mode, language, TestContext.Current.CancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    // tarkov.dev's own data in two languages, from the app's download cache (its data isn't in the repository);
    // skipped where there is no cache, or no German one.
    [Fact]
    public async Task Every_exit_of_the_real_data_takes_the_same_in_german_as_in_english()
    {
        if (!File.Exists(Path.Combine(Cache, "pve_maps.json")) || !File.Exists(Path.Combine(Cache, "pve_maps_de.json")))
            Assert.Skip($"No tarkov.dev cache with German texts at {Cache}: run Shturmap with the game in German once to download it.");
        var english = await CachedAsync(GameMode.Pve, "en");
        var german = await CachedAsync(GameMode.Pve, "ge");
        if (english is null || german is null)
            Assert.Skip("The tarkov.dev cache is incomplete.");
        if (german!.Language != "de")
            Assert.Skip("The tarkov.dev cache has no complete German texts.");

        const string coOp = "Co-op: a PMC and a player Scav leave together";
        int flares = 0, coOps = 0, exits = 0;
        var gated = new HashSet<string>(StringComparer.Ordinal);
        foreach (var map in english!.Maps.Values)
        {
            var mapDe = german.Maps[map.Id];
            // The same payload in the same order (an exit for both sides is listed twice under one id).
            foreach (var (exit, exitDe) in (map.Extracts ?? []).Zip(mapDe.Extracts ?? []))
            {
                Assert.Equal(exit.Id, exitDe.Id);
                var (en, de) = (ExtractRules.Needs(english, map, exit).Text, ExtractRules.Needs(german, mapDe, exitDe).Text);
                // The same parts in both: the rule's own words are English either way, a switch's name is the language's.
                Assert.Equal(en.Split(" · ").Length, de.Split(" · ").Length);
                Assert.Equal(en.Contains(GreenFlareLine, StringComparison.Ordinal), de.Contains(GreenFlareLine, StringComparison.Ordinal));
                Assert.Equal(en.Contains(coOp, StringComparison.Ordinal), de.Contains(coOp, StringComparison.Ordinal));
                Assert.Equal(en.Contains(" first", StringComparison.Ordinal), de.Contains(" first", StringComparison.Ordinal));
                Assert.Equal(ExtractRules.Items(english, exit), ExtractRules.Items(german, exitDe));
                exits++;
                flares += en.Contains(GreenFlareLine, StringComparison.Ordinal) ? 1 : 0;
                coOps += en.Contains(coOp, StringComparison.Ordinal) ? 1 : 0;
                if (en.Contains(" first", StringComparison.Ordinal))
                    gated.Add(exit.Id);
                // An ordinary exit whose name has "sniper" in it fires no flare.
                if (exit.Name == "Sniper Roadblock")
                    Assert.DoesNotContain(GreenFlareLine, en);
            }
        }
        // The six flare exits and Icebreaker's helicopter (more where an exit is listed for both sides, or on Ground Zero 21+
        // too); the table's ten gated extracts, and no other.
        Assert.True(exits > 100 && flares >= 7 && coOps >= 5, $"{exits} exits, {flares} flare exits, {coOps} co-op exits");
        Assert.Equal(SwitchExits.Table.Select(r => r.ExtractId).Order(StringComparer.Ordinal), gated.Order(StringComparer.Ordinal));
    }

    // The hand-kept tables against the data, in both modes (docs/UPDATES.md; shturmap-cli bring prints the same). A row
    // that no longer finds its extract or a step fails: its extract would lose its badge and line. Where the data now
    // names switches of its own for a table extract, a fix upstream to compare with the row, it warns.
    [Theory]
    [InlineData(GameMode.Pve)]
    [InlineData(GameMode.Pvp)]
    public async Task The_hand_kept_exits_still_match_the_data(GameMode mode)
    {
        var data = await CachedAsync(mode, "en");
        if (data is null)
            Assert.Skip($"No complete tarkov.dev cache for {mode} at {Cache}: run Shturmap once to download it.");
        var drift = SwitchExits.Drift(data!);
        Assert.Empty(drift.Where(d => d.Stale).Select(d => d.Text));
        foreach (var news in drift.Where(d => !d.Stale))
            TestContext.Current.AddWarning("Switch exits: " + news.Text);
        Assert.Empty(ExtractRules.FlareDrift(data!));
        // Every table extract says its first switch; the helicopter takes its flare.
        foreach (var row in SwitchExits.Table)
        {
            var map = data!.MapByNormalizedName(row.Map)!;
            var exit = map.Extracts!.First(e => e.Id == row.ExtractId);
            Assert.Contains(" first", ExtractRules.Needs(data, map, exit).Text);
        }
        var heli = data!.Maps.Values.SelectMany(m => m.Extracts ?? []).First(e => e.Id == "9bf65c4c24d0153f1542a90ddabadb37198ab9ff");
        Assert.Contains((ExtractRules.GreenFlare, 1), ExtractRules.Items(data, heli));
    }
}
