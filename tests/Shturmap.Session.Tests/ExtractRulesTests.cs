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
        // The switch line first; what ExitFacts keeps of it (a key at the door, single use) follows.
        var needs = ExtractRules.Needs(data, map, gated).Text;
        Assert.StartsWith(line, needs);
        if (ExitFacts.RowOf(data, gated, map) is { DoorKey: not null } or { SingleUse: true })
            Assert.NotEqual(line, needs);
        else
            Assert.Equal(line, needs);
        // A plain extract of the same map gives none, though the data lists the switch on it too.
        Assert.Equal("", ExtractRules.Needs(data, map, plain).Text);
        // Switches take no item; the key to a door does (ZB-1013's, the Saferoom's keycard).
        IReadOnlyList<(string, int)> items = ExitFacts.RowOf(data, gated)?.DoorKey is { } door ? [(door, 1)] : [];
        Assert.Equal(items, ExtractRules.Items(data, gated));
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

    // ---- the facts tarkov.dev's data doesn't carry (owner, 2026-10-17: "exit facts" as words on the exit's Raid rail
    // row, nothing new on the map; ExitFacts) ----

    private const string Roubles = "5449016a4bdc2d6f028b456f";

    // A row of ExitFacts as a map whose data is in a language: its extract under its id and internal name, with the fare a
    // car extract has and the switches its SwitchExits row has (the first named "First Switch"), its door's lock with its
    // key, and the key's name in that language ("Key", "Schlüssel").
    private static (ApiMap Map, GameData Data, ApiExtract Exit) FactsMap(ExitFacts.Row row, string language, int? roubles = null)
    {
        var steps = SwitchExits.Table.FirstOrDefault(r => r.ExtractId == row.ExtractId)?.Steps ?? [];
        var switches = steps.Where(s => !s.Lock).Select((s, i) => new ApiSwitch(s.Id, i == 0 ? "First Switch" : $"Step {i + 1}", new ApiPosition(10 * (i + 1), 0, 0))).ToList();
        var locks = new List<ApiLock>();
        if (row.DoorLock is { } door)
            locks.Add(new ApiLock(door, "door", row.DoorKey, false, new ApiPosition(5, 0, 5)));
        var exit = new ApiExtract(row.ExtractId, language == "de" ? "Ausgang" : "Exit", "pmc", new ApiPosition(0, 0, 0), null, null, null, null,
            roubles is { } fare ? new ApiCount(Roubles, fare) : null);
        var map = new ApiMap("map-1", "Map", row.Map, row.Map, null, null, 40, [exit], [], locks, [], [], Switches: switches);
        var data = new GameData
        {
            Mode = GameMode.Pve,
            Language = language,
            Maps = new Dictionary<string, ApiMap> { [map.Id] = map },
            Tasks = new Dictionary<string, ApiTask>(),
            Traders = new Dictionary<string, ApiTrader>(),
            ExtractKeys = new Dictionary<string, string> { [row.ExtractId] = row.Key },
            EnglishNames = new Dictionary<string, string> { [row.ExtractId] = "Exit" },
            ItemNames = row.DoorKey is { } key ? new Dictionary<string, string> { [key] = KeyName(language) } : new Dictionary<string, string>(),
            MapDefinitions = [],
            CheckedAt = DateTimeOffset.UnixEpoch,
        };
        return (map, data, exit);
    }

    private static string KeyName(string language) => language == "de" ? "Schlüssel" : "Key";

    // Every row of the table, with its gold line and its muted one before the game's list was read (English words; the
    // key's name is the data's). Where a fare stands, the car extract's.
    [Theory]
    [InlineData("customs", "ex_customs_pmc_zb1013", null, "First Switch first · Key for the door", "")]
    [InlineData("customs", "ex_customs_pmc_auto", 20000, "Pay 20,000 ₽ · single use · up to 4 players", "may be closed")]
    [InlineData("customs", "ex_customs_pmc_gasstation", null, "", "may be closed")]
    [InlineData("customs", "ex_customs_pmc_rail_milbase", null, "", "")]
    [InlineData("factory", "Cellars", null, "Key for the door", "")]
    [InlineData("factory", "Gate m", null, "Key for the door", "")]
    [InlineData("night-factory", "Cellars", null, "Key for the door", "")]
    [InlineData("night-factory", "Gate m", null, "Key for the door", "")]
    [InlineData("woods", "ZB-014", null, "Key for the door", "may be closed")]
    [InlineData("woods", "ZB-016", null, "", "may be closed")]
    [InlineData("woods", "RUAF Gate", null, "", "may be closed")]
    [InlineData("woods", "South V-Ex", 5000, "Pay 5,000 ₽ · single use · up to 4 players", "may be closed")]
    [InlineData("shoreline", "Shorl_V-Ex", 5000, "Pay 5,000 ₽ · single use · up to 5 players", "")]
    [InlineData("shoreline", "Pier Boat", null, "", "may be closed")]
    [InlineData("interchange", "ex_interchange_pmc_saferoom", null, "First Switch first · 4 steps · Key for the door · single use", "")]
    [InlineData("interchange", "ex_interchange_pmc_auto", 20000, "Pay 20,000 ₽ · single use · up to 4 players", "may be closed")]
    [InlineData("reserve", "EXFIL_Train", null, "", "Train from 16 min left · stays 7 min · may be closed")]
    [InlineData("reserve", "EXFIL_vent", null, "No backpack", "")]
    [InlineData("reserve", "EXFIL_Bunker", null, "First Switch first", "open 4 min after the lever")]
    [InlineData("the-lab", "lab_Vent", null, "No backpack", "")]
    [InlineData("the-lab", "lab_Elevator_Cargo", null, "First Switch first · 3 steps · single use", "")]
    [InlineData("the-lab", "lab_Hangar_Gate", null, "First Switch first", "may be closed")]
    [InlineData("the-lab", "lab_Parking_Gate", null, "First Switch first", "may be closed")]
    [InlineData("the-lab", "lab_Under_Storage_Collector", null, "First Switch first · 2 steps", "~80 s after the pump")]
    [InlineData("lighthouse", "EXFIL_Train", null, "", "Train from 20 min left · stays 7 min")]
    [InlineData("streets-of-tarkov", "E8_yard", null, "", "may be closed")]
    [InlineData("streets-of-tarkov", "E7_car", 5000, "Pay 5,000 ₽ · single use · up to 4 players", "may be closed")]
    [InlineData("ground-zero", "Sandbox_VExit", 5000, "Pay 5,000 ₽ · single use · up to 4 players", "")]
    [InlineData("ground-zero-21", "Sandbox_VExit", 5000, "Pay 5,000 ₽ · single use · up to 4 players", "")]
    [InlineData("the-labyrinth", "labir_exit", null, "", "from 15 min in")]
    public void Each_exit_fact_is_said_on_its_row_the_same_in_any_language(string mapName, string key, int? roubles, string needs, string notes)
    {
        var row = Assert.Single(ExitFacts.Table, r => r.Map == mapName && r.Key == key);
        foreach (var language in new[] { "en", "de" })
        {
            var (map, data, exit) = FactsMap(row, language, roubles);
            var (text, item) = ExtractRules.Needs(data, map, exit);
            // The rules' words are the app's; only the key's name is the data's.
            Assert.Equal(needs.Replace("Key for", KeyName(language) + " for", StringComparison.Ordinal), text);
            // The key is pictured beside the line where nothing is handed over, and BRING takes it.
            Assert.Equal(row.DoorKey, item);
            var items = ExtractRules.Items(data, exit);
            if (row.DoorKey is { } door)
                Assert.Contains((door, 1), items);
            Assert.Equal((row.DoorKey is null ? 0 : 1) + (roubles is null ? 0 : 1), items.Count);
            Assert.Equal(notes, ExtractRules.NoteLine(ExtractRules.Notes(data, map, exit), listRead: false, minutesIn: null, minutesLeft: null, over: false).Text);
        }
    }

    [Fact]
    public void The_facts_table_has_thirty_rows_one_per_extract()
    {
        // The rows above, each once: thirteen exits may be closed, seven take a key, two no backpack.
        Assert.Equal(30, ExitFacts.Table.Count);
        Assert.Equal(30, ExitFacts.Table.Select(r => r.ExtractId).Distinct().Count());
        Assert.Equal(13, ExitFacts.Table.Count(r => r.MayBeClosed));
        Assert.Equal(7, ExitFacts.Table.Count(r => r.DoorKey is not null));
        Assert.All(ExitFacts.Table.Where(r => r.DoorKey is not null), r => Assert.NotNull(r.DoorLock));
        Assert.Equal(2, ExitFacts.Table.Count(r => r.NoBackpack));
        Assert.Equal(9, ExitFacts.Table.Count(r => r.SingleUse));
        Assert.Equal(["pmc"], ExitFacts.Table.Where(r => r.Faction is not null).Select(r => r.Faction));
    }

    [Fact]
    public void A_fact_holds_only_for_its_map_its_internal_name_and_its_id()
    {
        var row = ExitFacts.Table.Single(r => r.Map == "reserve" && r.Key == "EXFIL_vent");
        var (map, data, exit) = FactsMap(row, "en");
        Assert.Equal("No backpack", ExtractRules.Needs(data, map, exit).Text);
        // Another map of that name, another id under the internal name, another internal name under the id.
        Assert.Equal("", ExtractRules.Needs(data, map with { NormalizedName = "woods" }, exit).Text);
        var moved = exit with { Id = "new-id" };
        Assert.Equal("", ExtractRules.Needs(Data(new() { ["new-id"] = row.Key }, []), map, moved).Text);
        Assert.Equal("", ExtractRules.Needs(Data(new() { [row.ExtractId] = "EXFIL_other" }, []), map, exit).Text);
        Assert.Empty(ExtractRules.Notes(data, map with { NormalizedName = "woods" }, exit));
    }

    // The trains' words by the raid card's clock (owner, 2026-10-17: muted, worked out from the raid's length and the
    // clock, muted further once the window has passed, no cue). Reserve's comes with 16 to 12 min left and stays 7: the
    // latest has gone with 5 min left.
    [Theory]
    [InlineData(39, false)]
    [InlineData(16, false)]
    [InlineData(12, false)]
    [InlineData(6, false)]
    [InlineData(5, true)]
    [InlineData(1, true)]
    public void Reserves_train_steps_back_once_the_latest_has_gone(int left, bool passed)
    {
        var (map, data, exit) = FactsMap(ExitFacts.Table.Single(r => r.Map == "reserve" && r.Key == "EXFIL_Train"), "en");
        var line = ExtractRules.NoteLine(ExtractRules.Notes(data, map, exit), listRead: true, minutesIn: 40 - left, minutesLeft: left, over: false);
        Assert.Equal("Train from 16 min left · stays 7 min", line.Text);
        Assert.Equal(passed, line.Passed);
    }

    [Fact]
    public void The_timed_words_follow_the_clock_where_it_is_known()
    {
        var notes = (ExitFacts.Row row) => { var (map, data, exit) = FactsMap(row, "en"); return ExtractRules.Notes(data, map, exit); };
        var lighthouse = notes(ExitFacts.Table.Single(r => r.Map == "lighthouse"));
        var reserve = notes(ExitFacts.Table.Single(r => r.Map == "reserve" && r.Key == "EXFIL_Train"));
        var wayUp = notes(ExitFacts.Table.Single(r => r.Key == "labir_exit"));
        // Lighthouse's train: 20 to 15 min left, stays 7; gone with 8 left.
        Assert.False(ExtractRules.NoteLine(lighthouse, false, 31, 9, false).Passed);
        Assert.True(ExtractRules.NoteLine(lighthouse, false, 32, 8, false).Passed);
        // Past the raid's length, the train has gone; where no time left is known (a Scav, outside a raid), it is only said.
        Assert.True(ExtractRules.NoteLine(reserve, false, 41, null, true).Passed);
        Assert.False(ExtractRules.NoteLine(reserve, false, null, null, false).Passed);
        // The Way Up opens 15 min in: from then its words step back.
        Assert.Equal(("from 15 min in", false), ExtractRules.NoteLine(wayUp, false, 14, 16, false));
        Assert.Equal(("from 15 min in", true), ExtractRules.NoteLine(wayUp, false, 15, 15, false));
        Assert.False(ExtractRules.NoteLine(wayUp, false, null, null, false).Passed);
        // The words after a switch never pass by the clock.
        foreach (var key in new[] { "EXFIL_Bunker", "lab_Under_Storage_Collector" })
        {
            var after = notes(ExitFacts.Table.Single(r => r.Key == key));
            Assert.All(new[] { (In: 1, Left: 29), (In: 29, Left: 1) }, t => Assert.False(ExtractRules.NoteLine(after, false, t.In, t.Left, false).Passed));
            Assert.False(ExtractRules.NoteLine(after, false, 41, null, true).Passed);
        }
    }

    [Fact]
    public void May_be_closed_goes_once_the_games_list_was_read()
    {
        var (map, data, exit) = FactsMap(ExitFacts.Table.Single(r => r.Key == "ex_customs_pmc_gasstation"), "en");
        var notes = ExtractRules.Notes(data, map, exit);
        Assert.Equal("may be closed", ExtractRules.NoteLine(notes, listRead: false, 5, 30, false).Text);
        // Read: the list's lit, "??:??:??" and struck-out marks say it.
        Assert.Equal("", ExtractRules.NoteLine(notes, listRead: true, 5, 30, false).Text);
        // The train keeps its time.
        var (trainMap, trainData, train) = FactsMap(ExitFacts.Table.Single(r => r.Map == "reserve" && r.Key == "EXFIL_Train"), "en");
        var trainNotes = ExtractRules.Notes(trainData, trainMap, train);
        Assert.Equal("Train from 16 min left · stays 7 min · may be closed", ExtractRules.NoteLine(trainNotes, false, 5, 35, false).Text);
        Assert.Equal("Train from 16 min left · stays 7 min", ExtractRules.NoteLine(trainNotes, true, 5, 35, false).Text);
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
        int flares = 0, coOps = 0, exits = 0, doors = 0, noted = 0;
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
                // The facts the data doesn't carry: the same words in both (they are the app's), the same key pictured.
                Assert.Equal(ExtractRules.Notes(english, map, exit), ExtractRules.Notes(german, mapDe, exitDe));
                Assert.Equal(ExtractRules.Needs(english, map, exit).ItemId, ExtractRules.Needs(german, mapDe, exitDe).ItemId);
                Assert.Equal(en.Contains(" for the door", StringComparison.Ordinal), de.Contains(" for the door", StringComparison.Ordinal));
                exits++;
                flares += en.Contains(GreenFlareLine, StringComparison.Ordinal) ? 1 : 0;
                coOps += en.Contains(coOp, StringComparison.Ordinal) ? 1 : 0;
                doors += en.Contains(" for the door", StringComparison.Ordinal) ? 1 : 0;
                noted += ExtractRules.Notes(english, map, exit).Count > 0 ? 1 : 0;
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
        // The seven doors with a key, and the exits with something said muted (13 may be closed, the trains, The Way Up,
        // the bunker's door, the conduit; fewer than the table's rows where an id has changed: the other test says which).
        Assert.True(doors == 7 && noted >= 15, $"{doors} doors with a key, {noted} exits with a muted line");
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

        // The facts kept by hand: a row whose extract or door lock is gone fails (its words would go unsaid); where the
        // data carries a fact itself, it warns.
        var facts = ExitFacts.Drift(data);
        Assert.Empty(facts.Where(d => d.Stale).Select(d => d.Text));
        foreach (var news in facts.Where(d => !d.Stale))
            TestContext.Current.AddWarning("Exit facts: " + news.Text);
        foreach (var row in ExitFacts.Table.Where(r => r.Faction is null))
        {
            var map = data.MapByNormalizedName(row.Map)!;
            var exit = map.Extracts!.First(e => e.Id == row.ExtractId);
            Assert.True(ExtractRules.Needs(data, map, exit).Text.Length > 0 || ExtractRules.Notes(data, map, exit).Count > 0, $"{row.Map} {row.Key} says nothing");
        }
    }

    // The general rules for the data's defects on the real data (the verification of 2026-10-17; docs/UPDATES.md). What
    // holds whether tarkov.dev has fixed a defect or the rule did: Ground Zero has no copy of another map's extract
    // outside it, Night Factory's and Ground Zero 21+'s extracts have a side where their base map's do, The Lab (Dark)
    // has extracts, the PMC's rail to Reserve is a PMC's. Where a rule no longer had anything to do (a fix upstream), it
    // warns: the rule can stay, and the report to tarkov.dev is done.
    [Theory]
    [InlineData(GameMode.Pve)]
    [InlineData(GameMode.Pvp)]
    public async Task The_data_rules_leave_every_map_with_its_own_extracts(GameMode mode)
    {
        var data = await CachedAsync(mode, "en");
        if (data is null)
            Assert.Skip($"No complete tarkov.dev cache for {mode} at {Cache}: run Shturmap once to download it.");
        var groundZero = data!.MapByNormalizedName("ground-zero")!;
        var box = data.DefinitionFor("ground-zero")!.SvgBounds ?? data.DefinitionFor("ground-zero")!.Bounds;
        Assert.DoesNotContain(groundZero.Extracts!, e => e.Position is { } p && !box.Contains(p.X, p.Z));
        foreach (var (variant, of) in new[] { ("night-factory", "factory"), ("ground-zero-21", "ground-zero") })
        {
            var keysWithSide = data.MapByNormalizedName(of)!.Extracts!.Where(e => e.Faction is { Length: > 0 }).Select(e => data.ExtractKeys[e.Id]).ToHashSet();
            Assert.All(data.MapByNormalizedName(variant)!.Extracts!.Where(e => keysWithSide.Contains(data.ExtractKeys[e.Id])),
                e => Assert.False(string.IsNullOrEmpty(e.Faction), $"{variant} {e.Name} has no side"));
        }
        Assert.NotEmpty(data.MapByNormalizedName("the-lab-dark")!.Extracts!);
        Assert.Equal("pmc", data.MapByNormalizedName("customs")!.Extracts!.Single(e => data.ExtractKeys[e.Id] == "ex_customs_pmc_rail_milbase").Faction);
        // What the rules did on 2026-10-17: three copies dropped on Ground Zero, fifteen sides taken from a base map (Night
        // Factory's nine, Ground Zero 21+'s six), The Lab (Dark)'s and Ground Zero Tutorial's extracts, the rail's side.
        var did = data.ExtractRepairs.GroupBy(r => r.Rule).ToDictionary(g => g.Key, g => g.Count());
        foreach (var (rule, then) in new[]
                 {
                     (ExtractRepairs.Rule.Outside, 3), (ExtractRepairs.Rule.FactionFromBase, 15), (ExtractRepairs.Rule.ExtractsFromBase, 2),
                     (ExtractRepairs.Rule.FactionByHand, 1),
                 })
        {
            if (did.GetValueOrDefault(rule) != then)
                TestContext.Current.AddWarning($"Extract data rules: {rule} did {did.GetValueOrDefault(rule)}, {then} on 2026-10-17 (an upstream fix?): "
                    + string.Join("; ", data.ExtractRepairs.Where(r => r.Rule == rule).Select(r => r.Text)));
        }
    }
}
