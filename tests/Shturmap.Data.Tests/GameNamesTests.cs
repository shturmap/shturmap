using System.Net;
using Shturmap.Core.Logs;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Data.Tests;

// The game data comes in the language Shturmap shows, which can be another than the game's (owner, 2026-10-10: the
// player chose English, or the game's language has no texts of Shturmap's yet). The extract list in a screenshot is in
// the game's language, so its maps' texts are loaded beside the data, for the extracts' names as the game shows them
// (docs/DESIGN.md §8, "Language").
public class GameNamesTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-gamenames-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    // tarkov.dev in miniature, made by hand: one map with two exits named by keys, in English and German; the rest empty.
    private sealed class FakeTarkovDev : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            lock (Requests)
                Requests.Add(path);
            var body = path[(path.LastIndexOf('/') + 1)..] switch
            {
                "maps.json" => "[]",
                "maps" => """
                    { "translations": ["$.data.maps.*.name", "$.data.maps.*.extracts[*].name"],
                      "data": { "maps": { "m1": { "id": "m1", "name": "map name", "normalizedName": "test", "nameId": "test",
                        "extracts": [ { "id": "e1", "name": "Sniper_exit" }, { "id": "e2", "name": "Gate_exit" } ] } }, "mobs": {} } }
                    """,
                "maps_en" => """{ "data": { "map name": "Test map", "Sniper_exit": "Test Avenue (Flare)", "Gate_exit": "Old Gate" } }""",
                // German has a text for one exit only.
                "maps_de" => """{ "data": { "map name": "Testkarte", "Sniper_exit": "Testallee" } }""",
                "tasks" => """{ "data": { "tasks": {}, "questItems": {} } }""",
                var name when name.Split('_') is [_, var language] && language is not ("en" or "de") => null,
                _ => """{ "data": {} }""",
            };
            return Task.FromResult(body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    [Fact]
    public async Task English_data_for_a_german_game_has_the_german_names_of_the_extracts_beside_it()
    {
        var server = new FakeTarkovDev();
        var data = await new GameDataLoader(new CachedHttp(new HttpClient(server), _folder))
            .LoadAsync(GameMode.Pve, "en", TestContext.Current.CancellationToken, gameLanguage: "ge");
        Assert.Equal("en", data.Language);
        Assert.Equal("Test Avenue (Flare)", data.Maps["m1"].Extracts![0].Name);
        Assert.Equal("de", data.GameNamesLanguage);
        Assert.Equal("Testallee", data.GameNames["e1"]);
        // An exit the game's language has no text for has no name in it.
        Assert.False(data.GameNames.ContainsKey("e2"));
        // Only the maps' texts: quests, traders and items stay in the data's language.
        Assert.Contains("/pve/maps_de", server.Requests);
        Assert.DoesNotContain("/pve/tasks_de", server.Requests);
    }

    [Theory]
    [InlineData("ge", "ge")]
    [InlineData("de", "ge")]
    [InlineData("en", "en")]
    [InlineData("en", null)]
    public async Task Data_in_the_games_own_language_loads_nothing_more(string language, string? game)
    {
        var server = new FakeTarkovDev();
        var data = await new GameDataLoader(new CachedHttp(new HttpClient(server), _folder))
            .LoadAsync(GameMode.Pve, language, TestContext.Current.CancellationToken, gameLanguage: game);
        Assert.Empty(data.GameNames);
        Assert.Null(data.GameNamesLanguage);
        // maps_en, and the data's language's maps texts when it isn't English; none for the game besides.
        Assert.Equal(language == "en" ? 1 : 2, server.Requests.Count(r => r.Contains("/maps_", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_game_language_tarkov_dev_lacks_leaves_the_data_whole()
    {
        var server = new FakeTarkovDev();
        var data = await new GameDataLoader(new CachedHttp(new HttpClient(server), _folder))
            .LoadAsync(GameMode.Pve, "en", TestContext.Current.CancellationToken, gameLanguage: "xx");
        Assert.Equal("en", data.Language);
        Assert.Null(data.MissingLanguage);
        Assert.Null(data.LanguageFailure);
        Assert.Empty(data.GameNames);
        Assert.Null(data.GameNamesLanguage);
        Assert.Contains("/pve/maps_xx", server.Requests);
    }
}
