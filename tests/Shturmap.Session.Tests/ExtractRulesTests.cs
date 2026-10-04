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

    [Fact]
    public void A_flare_exit_is_one_in_any_language()
    {
        // The German name has no "(Flare)"; the internal name and the English name both say it.
        var exit = Extract("e1", "Mira-Allee");
        var byKey = Data(new() { ["e1"] = "Sniper_exit" }, new() { ["e1"] = "Mira Ave" });
        var byEnglish = Data(new() { ["e1"] = "exit_7" }, new() { ["e1"] = "Mira Ave (Flare)" });
        foreach (var data in new[] { byKey, byEnglish })
        {
            Assert.Equal("Fire a red signal flare there", ExtractRules.Needs(data, Map([exit]), exit).Text);
            Assert.Equal([(ExtractRules.RedFlare, 1)], ExtractRules.Items(data, exit));
        }
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
        Assert.Equal("Fire a red signal flare there", ExtractRules.Needs(data, Map([exit]), exit).Text);
    }

    private sealed class Offline : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("offline: the test reads the cache only");
    }

    // tarkov.dev's own data in two languages, from the app's download cache (its data isn't in the repository);
    // skipped where there is no cache, or no German one.
    [Fact]
    public async Task Every_exit_of_the_real_data_takes_the_same_in_german_as_in_english()
    {
        var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shturmap", "cache", "tarkov-dev");
        if (!File.Exists(Path.Combine(cache, "pve_maps.json")) || !File.Exists(Path.Combine(cache, "pve_maps_de.json")))
            Assert.Skip($"No tarkov.dev cache with German texts at {cache}: run Shturmap with the game in German once to download it.");
        GameData english, german;
        try
        {
            var loader = new GameDataLoader(new Shturmap.Data.Http.CachedHttp(new HttpClient(new Offline()), cache));
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

        const string flare = "Fire a red signal flare there", coOp = "Co-op: a PMC and a player Scav leave together";
        int flares = 0, coOps = 0, exits = 0;
        foreach (var map in english.Maps.Values)
        {
            var mapDe = german.Maps[map.Id];
            // The same payload in the same order (an exit for both sides is listed twice under one id).
            foreach (var (exit, exitDe) in (map.Extracts ?? []).Zip(mapDe.Extracts ?? []))
            {
                Assert.Equal(exit.Id, exitDe.Id);
                var (en, de) = (ExtractRules.Needs(english, map, exit).Text, ExtractRules.Needs(german, mapDe, exitDe).Text);
                // The same parts in both: the rule's own words are English either way, a switch's name is the language's.
                Assert.Equal(en.Split(" · ").Length, de.Split(" · ").Length);
                Assert.Equal(en.Contains(flare, StringComparison.Ordinal), de.Contains(flare, StringComparison.Ordinal));
                Assert.Equal(en.Contains(coOp, StringComparison.Ordinal), de.Contains(coOp, StringComparison.Ordinal));
                Assert.Equal(ExtractRules.Items(english, exit), ExtractRules.Items(german, exitDe));
                exits++;
                flares += en.Contains(flare, StringComparison.Ordinal) ? 1 : 0;
                coOps += en.Contains(coOp, StringComparison.Ordinal) ? 1 : 0;
                // An ordinary exit whose name has "sniper" in it fires no flare.
                if (exit.Name == "Sniper Roadblock")
                    Assert.DoesNotContain(flare, en);
            }
        }
        Assert.True(exits > 100 && flares >= 5 && coOps >= 5, $"{exits} exits, {flares} flare exits, {coOps} co-op exits");
    }

    [Fact]
    public void A_switch_most_exits_list_shows_where_its_english_name_names_the_exit()
    {
        // tarkov.dev lists one lever on every exit; the translation words lever and exit apart.
        var bunker = Extract("e1", "Bunker ZB-013", "s1");
        var gate = Extract("e2", "Tor", "s1");
        var road = Extract("e3", "Strasse", "s1");
        var lever = new ApiSwitch("s1", "Stromschalter am Bunker");
        var map = Map([bunker, gate, road], lever);
        var data = Data([], new() { ["e1"] = "ZB-013", ["e2"] = "Gate", ["e3"] = "Road", ["s1"] = "ZB-013 Power Switch" });
        Assert.Equal("Stromschalter am Bunker first", ExtractRules.Needs(data, map, bunker).Text);
        Assert.Equal("", ExtractRules.Needs(data, map, gate).Text);
    }
}
