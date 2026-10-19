using System.Net;
using Shturmap.Core.Logs;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Data.Tests;

// Extracts and switches keep their English names beside the translated ones (review of 2026-10-04, A34): the rules
// that read a name's words ("(Flare)", "(Co-op)") must find them in every game language.
public class EnglishNamesTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-names-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    // tarkov.dev in miniature, made by hand: one map with one exit and one switch, named by keys, and two languages.
    private sealed class FakeTarkovDev : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = path[(path.LastIndexOf('/') + 1)..] switch
            {
                "maps.json" => "[]",
                "maps" => """
                    { "translations": ["$.data.maps.*.name", "$.data.maps.*.extracts[*].name", "$.data.maps.*.switches[*].name"],
                      "data": { "maps": { "m1": { "id": "m1", "name": "map name", "normalizedName": "test", "nameId": "test",
                        "extracts": [ { "id": "e1", "name": "Sniper_exit" }, { "id": "e2", "name": "no_text_anywhere" } ],
                        "switches": [ { "id": "s1", "name": "lever" } ] } }, "mobs": {} } }
                    """,
                "maps_en" => """{ "data": { "map name": "Test map", "Sniper_exit": "Test Avenue (Flare)", "lever": "Gate Lever" } }""",
                "maps_de" => """{ "data": { "map name": "Testkarte", "Sniper_exit": "Testallee", "lever": "Torhebel" } }""",
                "tasks" => """{ "data": { "tasks": {}, "questItems": {} } }""",
                "items_en" => $$"""{ "data": { "{{Zorb}} Name": "Zorb drive", "{{Zorb}} ShortName": "Zorb" } }""",
                "items_de" => $$"""{ "data": { "{{Zorb}} Name": "Zorblaufwerk", "{{Zorb}} ShortName": "Zorb" } }""",
                _ => """{ "data": {} }""",
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    // A made-up item id (24 characters, as the game's).
    private const string Zorb = "000000000000000000000a01";

    private GameDataLoader Loader() => new(new CachedHttp(new HttpClient(new FakeTarkovDev()), _folder));

    [Fact]
    public async Task A_german_load_keeps_the_english_names_of_exits_and_switches()
    {
        var data = await Loader().LoadAsync(GameMode.Pve, "ge", TestContext.Current.CancellationToken);
        var map = data.Maps["m1"];
        Assert.Equal("Testallee", map.Extracts![0].Name);
        Assert.Equal("Torhebel", map.Switches![0].Name);
        Assert.Equal("Test Avenue (Flare)", data.EnglishName("e1", map.Extracts[0].Name));
        Assert.Equal("Gate Lever", data.EnglishName("s1", map.Switches[0].Name));
        Assert.Equal("Sniper_exit", data.ExtractKeys["e1"]);
        // A name with no text in any language stays the key, as the translated name does.
        Assert.Equal("no_text_anywhere", data.EnglishName("e2", map.Extracts[1].Name));
    }

    // An operational hand-over's quoted names are the English game's words ("SSD drive"): a load in another language
    // keeps the items' English names beside the translated ones, which it shows (QA, 2026-10-19: a German app matched
    // none).
    [Fact]
    public async Task A_german_load_keeps_the_items_english_names()
    {
        var german = await Loader().LoadAsync(GameMode.Pve, "ge", TestContext.Current.CancellationToken);
        Assert.Equal("Zorblaufwerk", german.ItemName(Zorb));
        Assert.Equal("Zorb drive", german.EnglishItemNames[Zorb]);
        Assert.Equal("Zorb drive", german.ItemNamesInEnglish[Zorb]);
        var english = await Loader().LoadAsync(GameMode.Pve, "en", TestContext.Current.CancellationToken);
        Assert.Empty(english.EnglishItemNames);
        Assert.Equal("Zorb drive", english.ItemNamesInEnglish[Zorb]);
    }

    [Fact]
    public async Task An_english_load_records_them_too()
    {
        var data = await Loader().LoadAsync(GameMode.Pve, "en", TestContext.Current.CancellationToken);
        Assert.Equal("Test Avenue (Flare)", data.EnglishNames["e1"]);
        Assert.Equal("Test Avenue (Flare)", data.Maps["m1"].Extracts![0].Name);
    }
}
