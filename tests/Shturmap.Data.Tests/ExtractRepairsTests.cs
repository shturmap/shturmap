using System.Net;
using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Data.Tests;

// The general rules for the defects the verification of 2026-10-17 found in tarkov.dev's extracts (owner, 2026-10-17:
// rules, not hand fixes, so an upstream fix needs no undoing; docs/DESIGN.md, "Extract requirements", *The data's
// defects*). Made-up maps, ids and names only; the real data is ExtractRulesTests' (Shturmap.Session.Tests).
public class ExtractRepairsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-repairs-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    private static MapDefinition Definition(string key, double half, params string[] alt) => new()
    {
        Key = key,
        AltKeys = alt,
        Transform = [1, 0, 1, 0],
        Bounds = new WorldBox(half, -half, -half, half),
    };

    private static ApiExtract Exit(string id, string? faction, double x, double z = 0, string? name = null) =>
        new(id, name ?? id, faction, new ApiPosition(x, 0, z), null, null, null);

    private static ApiMap Map(string id, string normalizedName, params ApiExtract[] extracts) =>
        new(id, normalizedName, normalizedName, normalizedName, null, null, 30, [.. extracts], [], [], [], []);

    private static Dictionary<string, ApiMap> Maps(params ApiMap[] maps) => maps.ToDictionary(m => m.Id);

    [Fact]
    public void A_copy_of_another_maps_extract_outside_the_map_is_dropped_and_one_near_the_edge_kept()
    {
        // Ground Zero carried Woods' UN Roadblock some 430 m off its western edge; its own gate stands 2 m past it.
        var gz = Map("m-gz", "ground-zero", Exit("inside", "pmc", 10), Exit("copy", "pmc", -528), Exit("edge", "pmc", 0, 102));
        var woods = Map("m-woods", "woods", Exit("w-un", "scav", -535, 287));
        var keys = new Dictionary<string, string> { ["inside"] = "Gate", ["copy"] = "UN Roadblock", ["edge"] = "Gate_o", ["w-un"] = "UN Roadblock" };
        var (maps, done) = ExtractRepairs.Apply(Maps(gz, woods), keys, [Definition("ground-zero", 100), Definition("woods", 1000)]);
        Assert.Equal(["inside", "edge"], maps["m-gz"].Extracts!.Select(e => e.Id));
        Assert.Same(woods, maps["m-woods"]);
        Assert.Equal([(ExtractRepairs.Rule.Outside, "copy"), (ExtractRepairs.Rule.OutsideKept, "edge")], done.Select(d => (d.Rule, d.ExtractId!)));
        Assert.Contains("428 m outside the map and named as an extract of woods", done[0].Text);
        Assert.Contains("2 m outside the map, kept", done[1].Text);
    }

    [Fact]
    public void A_namesake_on_a_map_drawn_with_the_same_artwork_is_no_copy()
    {
        // Night Factory's exits are Factory's at the same places: an exit past Factory's edge on both stays on both.
        var factory = Map("m-f", "factory", Exit("f-gate", "pmc", 0, 102));
        var night = Map("m-n", "night-factory", Exit("n-gate", "pmc", 0, 102));
        var keys = new Dictionary<string, string> { ["f-gate"] = "Gate_o", ["n-gate"] = "Gate_o" };
        var (maps, done) = ExtractRepairs.Apply(Maps(factory, night), keys, [Definition("factory", 100, "night-factory")]);
        Assert.Single(maps["m-f"].Extracts!);
        Assert.Single(maps["m-n"].Extracts!);
        Assert.All(done, d => Assert.Equal(ExtractRepairs.Rule.OutsideKept, d.Rule));
    }

    [Fact]
    public void A_variants_extract_without_a_side_takes_its_base_maps_by_internal_name_the_nearest_where_there_are_two()
    {
        // Factory lists Gate 3 twice under one id, a Scav's and a PMC's a few metres apart; Night Factory the same, with no
        // side at all.
        var factory = Map("m-f", "factory", Exit("g3", "scav", 59, 61), Exit("g3", "pmc", 58, 63), Exit("office", "scav", 18, 40));
        var night = Map("m-n", "night-factory", Exit("n-g3", null, 59, 61), Exit("n-g3", null, 58, 63), Exit("n-office", null, 18, 40),
            Exit("n-own", null, 0, 0), Exit("n-pmc", "pmc", 1, 1));
        var keys = new Dictionary<string, string>
        {
            ["g3"] = "Gate 3", ["office"] = "Office Window", ["n-g3"] = "Gate 3", ["n-office"] = "Office Window", ["n-own"] = "Only at night", ["n-pmc"] = "Gate 0",
        };
        var (maps, done) = ExtractRepairs.Apply(Maps(factory, night), keys, [Definition("factory", 100, "night-factory")]);
        // Its own side stays; one its base hasn't stays without (shared, as before).
        Assert.Equal(["scav", "pmc", "scav", null, "pmc"], maps["m-n"].Extracts!.Select(e => e.Faction));
        Assert.Equal(3, done.Count(d => d.Rule == ExtractRepairs.Rule.FactionFromBase));
        Assert.Same(factory, maps["m-f"]);
    }

    [Fact]
    public void A_variant_without_extracts_takes_its_base_maps_and_their_switches_as_it_takes_its_artwork()
    {
        // tarkov.dev's maps.json draws The Lab (Dark) with The Lab's artwork (`altMaps`). Its data has no extracts and no
        // switches: it takes The Lab's of both, the elevators' chains with their steps.
        var lever = new ApiSwitch("power", "Power Button", new ApiPosition(5, 0, 5));
        var lab = Map("m-lab", "the-lab", Exit("vent", "pmc", 1), Exit("cargo", "shared", 2)) with { Switches = [lever] };
        var dark = Map("m-dark", "the-lab-dark");
        MapDefinition[] art = [Definition("the-lab", 100, "the-lab-dark")];
        var (maps, done) = ExtractRepairs.Apply(Maps(lab, dark), new Dictionary<string, string>(), art);
        Assert.Equal(["vent", "cargo"], maps["m-dark"].Extracts!.Select(e => e.Id));
        Assert.Same(lab.Switches, maps["m-dark"].Switches);
        var took = Assert.Single(done);
        Assert.Equal((ExtractRepairs.Rule.ExtractsFromBase, "the-lab-dark", "the-lab"), (took.Rule, took.Map, took.From));
        Assert.Equal("the-lab-dark: no extracts in the data, takes the 2 of the-lab and its 1 switches", took.Text);
        // Once the data gives it extracts of its own, they are its.
        var fixedDark = Map("m-dark", "the-lab-dark", Exit("own", "pmc", 3));
        var (again, none) = ExtractRepairs.Apply(Maps(lab, fixedDark), new Dictionary<string, string>(), art);
        Assert.Equal(["own"], again["m-dark"].Extracts!.Select(e => e.Id));
        Assert.Empty(none);
        // A map with artwork of its own has no base.
        Assert.Null(ExtractRepairs.BaseOf(lab, Maps(lab, dark), art));
    }

    [Fact]
    public void A_map_drawn_with_another_maps_artwork_only_by_its_name_borrows_nothing()
    {
        // Ground Zero Tutorial: maps.json has no entry for it and lists it under no map's altMaps; the app draws it with
        // Ground Zero's artwork only because its name starts "ground-zero-" (GameData.DefinitionFor). Nothing says its
        // exits are Ground Zero's: it takes none (coordinator, 2026-10-17).
        var gz = Map("m-gz", "ground-zero", Exit("gate", "pmc", 1));
        var tutorial = Map("m-t", "ground-zero-tutorial");
        MapDefinition[] art = [Definition("ground-zero", 100, "ground-zero-21")];
        Assert.Same(art[0], GameData.DefinitionFor(art, "ground-zero-tutorial"));
        var (maps, done) = ExtractRepairs.Apply(Maps(gz, tutorial), new Dictionary<string, string>(), art);
        Assert.Empty(maps["m-t"].Extracts!);
        Assert.Empty(done);
        Assert.Null(ExtractRepairs.BaseOf(tutorial, Maps(gz, tutorial), art));
        // Nor does a map with an entry of its own.
        var own = Map("m-o", "ground-zero-own");
        var (owned, nothing) = ExtractRepairs.Apply(Maps(gz, own), new Dictionary<string, string>(), [.. art, Definition("ground-zero-own", 50)]);
        Assert.Empty(owned["m-o"].Extracts!);
        Assert.Empty(nothing);
    }

    [Fact]
    public void The_pmcs_rail_to_reserve_is_a_pmcs_by_hand_where_the_data_says_scav()
    {
        var row = ExitFacts.Table.Single(r => r.Faction is not null);
        var customs = Map("m-c", "customs", Exit(row.ExtractId, "scav", 0), Exit("scav-rail", "scav", 0));
        var keys = new Dictionary<string, string> { [row.ExtractId] = row.Key, ["scav-rail"] = "ex_customs_scav_rail_milbase" };
        var (maps, done) = ExtractRepairs.Apply(Maps(customs), keys, [Definition("customs", 1000)]);
        Assert.Equal(["pmc", "scav"], maps["m-c"].Extracts!.Select(e => e.Faction));
        Assert.Contains("pmc by hand, the data says scav", Assert.Single(done).Text);
        // Under another internal name, or with the data right, nothing.
        var renamed = new Dictionary<string, string> { [row.ExtractId] = "ex_customs_scav_rail_other" };
        Assert.Empty(ExtractRepairs.Apply(Maps(customs), renamed, [Definition("customs", 1000)]).Done);
        var right = Map("m-c", "customs", Exit(row.ExtractId, "pmc", 0));
        Assert.Empty(ExtractRepairs.Apply(Maps(right), keys, [Definition("customs", 1000)]).Done);
    }

    [Fact]
    public void Data_that_is_right_comes_through_as_it_is()
    {
        // The upstream fix: every map as the data has it, and nothing done.
        var factory = Map("m-f", "factory", Exit("g3", "pmc", 1));
        var night = Map("m-n", "night-factory", Exit("n-g3", "pmc", 1));
        var keys = new Dictionary<string, string> { ["g3"] = "Gate 3", ["n-g3"] = "Gate 3" };
        var input = Maps(factory, night);
        var (maps, done) = ExtractRepairs.Apply(input, keys, [Definition("factory", 100, "night-factory")]);
        Assert.Empty(done);
        Assert.All(input, m => Assert.Same(m.Value, maps[m.Key]));
    }

    // ---- the facts table against the data (ExitFacts.Drift) ----

    private static GameData Data(ApiMap map, IReadOnlyDictionary<string, string> keys, IReadOnlySet<string>? fields = null,
        IReadOnlyList<ExtractRepairs.Done>? repairs = null) => new()
    {
        Mode = GameMode.Pve,
        Language = "en",
        Maps = new Dictionary<string, ApiMap> { [map.Id] = map },
        Tasks = new Dictionary<string, ApiTask>(),
        Traders = new Dictionary<string, ApiTrader>(),
        ExtractKeys = keys,
        ExtractFields = fields ?? ExitFacts.KnownFields,
        ExtractRepairs = repairs ?? [],
        MapDefinitions = [],
        CheckedAt = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public void A_row_whose_door_lock_is_gone_or_takes_another_key_is_stale()
    {
        var row = ExitFacts.Table.Single(r => r.Map == "woods" && r.Key == "ZB-014");
        var exit = Exit(row.ExtractId, "pmc", 0, name: "ZB-014");
        var keys = new Dictionary<string, string> { [row.ExtractId] = row.Key };
        ApiMap Woods(params ApiLock[] locks) => new("m-w", "Woods", "woods", "Woods", null, null, 35, [exit], [], [.. locks], [], []);
        var drift = (ApiMap map) => ExitFacts.Drift(Data(map, keys)).Where(f => f.Text.StartsWith("woods ZB-014", StringComparison.Ordinal)).ToList();
        Assert.Empty(drift(Woods(new ApiLock(row.DoorLock, "door", row.DoorKey, false, new ApiPosition(1, 0, 1)))));
        Assert.Contains(drift(Woods()), f => f.Stale && f.Text.Contains("isn't in the data", StringComparison.Ordinal));
        Assert.Contains(drift(Woods(new ApiLock(row.DoorLock, "door", "another-key", false, new ApiPosition(1, 0, 1)))),
            f => f.Stale && f.Text.Contains("takes another-key now", StringComparison.Ordinal));
        // Its extract under another id.
        var moved = new ApiMap("m-w", "Woods", "woods", "Woods", null, null, 35, [exit with { Id = "new-id" }], [], [], [], []);
        Assert.Contains(ExitFacts.Drift(Data(moved, new Dictionary<string, string> { ["new-id"] = row.Key })),
            f => f.Stale && f.Text == $"woods ZB-014: its id is new-id now, the table has {row.ExtractId}");
    }

    [Fact]
    public void The_data_carrying_a_fact_itself_is_news_not_stale()
    {
        var row = ExitFacts.Table.Single(r => r.Faction is not null);
        var keys = new Dictionary<string, string> { [row.ExtractId] = row.Key };
        var customs = new ApiMap("m-c", "Customs", "customs", "bigmap", null, null, 35, [Exit(row.ExtractId, "pmc", 0)], [], [], [], []);
        // The rail tagged pmc by the data itself (no repair needed): the row's faction may go.
        Assert.Contains(ExitFacts.Drift(Data(customs, keys)), f => !f.Stale && f.Text.Contains("the data now says pmc by itself", StringComparison.Ordinal));
        // Set by the rule as the data loaded: as it should be.
        var repaired = new ExtractRepairs.Done(ExtractRepairs.Rule.FactionByHand, "customs", row.ExtractId, "set");
        Assert.DoesNotContain(ExitFacts.Drift(Data(customs, keys, repairs: [repaired])), f => f.Text.Contains("by itself", StringComparison.Ordinal));
        // A field the extracts didn't carry on 2026-10-17.
        var fields = new HashSet<string>(ExitFacts.KnownFields) { "singleUse" };
        Assert.Contains(ExitFacts.Drift(Data(customs, keys, fields)), f => !f.Stale && f.Text == "extracts now carry singleUse: compare with the table");
    }

    // ---- through the loader: everything after it sees the repaired extracts ----

    // tarkov.dev in miniature: a base map and a variant without extracts drawn with its artwork, a copy of another map's
    // extract outside the base, and a variant's extract without a side.
    private sealed class FakeTarkovDev : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = path[(path.LastIndexOf('/') + 1)..] switch
            {
                "maps.json" => """
                    [ { "normalizedName": "base", "maps": [ { "key": "base", "projection": "interactive", "transform": [1, 0, 1, 0],
                        "bounds": [[100, -100], [-100, 100]], "altMaps": ["base-21", "base-dark"] } ] },
                      { "normalizedName": "far", "maps": [ { "key": "far", "projection": "interactive", "transform": [1, 0, 1, 0],
                        "bounds": [[1000, -1000], [-1000, 1000]] } ] } ]
                    """,
                "maps" => """
                    { "translations": [],
                      "data": { "maps": {
                        "m-base": { "id": "m-base", "name": "Base", "normalizedName": "base", "nameId": "base", "raidDuration": 30, "extracts": [
                          { "id": "gate", "name": "Gate", "faction": "pmc", "position": { "x": 10, "y": 0, "z": 10 }, "size": 3 },
                          { "id": "copy", "name": "Far Road", "faction": "pmc", "position": { "x": -500, "y": 0, "z": 0 } } ] },
                        "m-21": { "id": "m-21", "name": "Base 21+", "normalizedName": "base-21", "nameId": "base_high", "raidDuration": 30, "extracts": [
                          { "id": "gate-21", "name": "Gate", "faction": null, "position": { "x": 10, "y": 0, "z": 10 } } ] },
                        "m-dark": { "id": "m-dark", "name": "Base (Dark)", "normalizedName": "base-dark", "nameId": "base_dark", "raidDuration": 30, "extracts": [] },
                        "m-tutorial": { "id": "m-tutorial", "name": "Base Tutorial", "normalizedName": "base-tutorial", "nameId": "base_start", "raidDuration": 30, "extracts": [] },
                        "m-far": { "id": "m-far", "name": "Far", "normalizedName": "far", "nameId": "far", "raidDuration": 30, "extracts": [
                          { "id": "far-road", "name": "Far Road", "faction": "scav", "position": { "x": -505, "y": 0, "z": 0 } } ] } },
                        "mobs": {} } }
                    """,
                "tasks" => """{ "data": { "tasks": {}, "questItems": {} } }""",
                _ => """{ "data": {} }""",
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    [Fact]
    public async Task The_loader_applies_the_rules_and_says_what_they_did()
    {
        var data = await new GameDataLoader(new CachedHttp(new HttpClient(new FakeTarkovDev()), _folder)).LoadAsync(GameMode.Pve, "en", TestContext.Current.CancellationToken);
        Assert.Equal(["gate"], data.Maps["m-base"].Extracts!.Select(e => e.Id));
        Assert.Equal("pmc", Assert.Single(data.Maps["m-21"].Extracts!).Faction);
        Assert.Equal(["gate"], data.Maps["m-dark"].Extracts!.Select(e => e.Id));
        // The tutorial is drawn with the base's artwork by its name only (no altMaps entry): it takes nothing.
        Assert.Same(data.DefinitionFor("base"), data.DefinitionFor("base-tutorial"));
        Assert.Empty(data.Maps["m-tutorial"].Extracts!);
        Assert.Equal([ExtractRepairs.Rule.Outside, ExtractRepairs.Rule.FactionFromBase, ExtractRepairs.Rule.ExtractsFromBase],
            data.ExtractRepairs.Select(r => r.Rule));
        // The tables name the dark variant's extracts by the base's name.
        Assert.Equal("base", data.ExtractMapName(data.Maps["m-dark"]));
        Assert.Equal("base-21", data.ExtractMapName(data.Maps["m-21"]));
        // The fields the extracts carried with a value ("faction": null isn't one).
        Assert.Equal(["faction", "id", "name", "position", "size"], data.ExtractFields.Order(StringComparer.Ordinal));
    }
}
