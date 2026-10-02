using System.Net;
using Shturmap.Core.Logs;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Data.Tests;

// The game names some languages its own way; a German game asked tarkov.dev for "maps_ge", got 404 and loaded no data
// at all (a player's first run, 2026-10-02).
public class LoaderLanguageTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-lang-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    // tarkov.dev in miniature: every payload empty, and only the languages it really has.
    private sealed class FakeTarkovDev(params string[] languages) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add(path);
            var name = path[(path.LastIndexOf('/') + 1)..];
            var body = name switch
            {
                "maps.json" => "[]",
                "maps" => """{ "data": { "maps": {}, "mobs": {} } }""",
                "tasks" => """{ "data": { "tasks": {}, "questItems": {} } }""",
                "traders" or "hideout" => """{ "data": {} }""",
                "items" or "barters" or "crafts" => """{ "data": null }""",
                _ when name.Split('_') is [_, var language] => languages.Contains(language) ? """{ "data": {} }""" : null,
                _ => null,
            };
            return Task.FromResult(body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    [Theory]
    [InlineData("ge", "de")]
    [InlineData("cz", "cs")]
    [InlineData("jp", "ja")]
    [InlineData("kr", "ko")]
    [InlineData("po", "pt")]
    [InlineData("tu", "tr")]
    [InlineData("ch", "zh")]
    [InlineData("es-mx", "es")]
    [InlineData("EN", "en")]
    [InlineData("", "en")]
    [InlineData("fr", "fr")]
    public void Game_language_codes_become_tarkov_devs(string game, string api) =>
        Assert.Equal(api, GameDataLoader.ApiLanguage(game));

    [Fact]
    public async Task A_german_game_loads_german_data()
    {
        var server = new FakeTarkovDev("en", "de");
        var data = await new GameDataLoader(new CachedHttp(new HttpClient(server), _folder)).LoadAsync(GameMode.Pve, "ge");
        Assert.Equal("de", data.Language);
        Assert.Contains("/pve/tasks_de", server.Requests);
        Assert.DoesNotContain(server.Requests, r => r.EndsWith("_ge", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_language_tarkov_dev_lacks_falls_back_to_english()
    {
        var server = new FakeTarkovDev("en");
        var loader = new GameDataLoader(new CachedHttp(new HttpClient(server), _folder));
        var data = await loader.LoadAsync(GameMode.Pve, "xx");
        Assert.Equal("en", data.Language);
        Assert.Contains("/pve/tasks_xx", server.Requests);
        await loader.LoadSourcesAsync(GameMode.Pve, "xx");
        Assert.Contains("/pve/hideout_xx", server.Requests);
    }
}
