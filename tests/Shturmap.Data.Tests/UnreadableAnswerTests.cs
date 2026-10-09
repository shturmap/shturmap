using System.Net;
using System.Text.Json.Nodes;
using Shturmap.Core.Logs;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Data.Tests;

// A page sent with 200 in place of the data (a captive portal's, a CDN's) was saved like the data, failed as unreadable,
// and was read from the cache again at the next start for as long as it counted as fresh (review of 2026-10-09). Such
// a file is forgotten now; a file that is JSON in a shape Shturmap doesn't know, and a good copy offline, stay.
public class UnreadableAnswerTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-unreadable-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    private const string Page = "<!DOCTYPE html><html><body>Sign in to the network</body></html>";

    // tarkov.dev in miniature: every payload empty unless the test gives a body; counts what was asked for.
    private sealed class FakeTarkovDev : HttpMessageHandler
    {
        public readonly Dictionary<string, string> Bodies = new(StringComparer.Ordinal);
        public readonly Dictionary<string, int> Asked = new(StringComparer.Ordinal);
        public bool Offline;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var name = path[(path.LastIndexOf('/') + 1)..];
            lock (Asked)
                Asked[name] = Asked.GetValueOrDefault(name) + 1;
            if (Offline)
                return Task.FromException<HttpResponseMessage>(new HttpRequestException("offline"));
            var body = Bodies.GetValueOrDefault(name) ?? name switch
            {
                "maps.json" => "[]",
                "maps" => """{ "data": { "maps": {}, "mobs": {} } }""",
                "tasks" => """{ "data": { "tasks": {}, "questItems": {} } }""",
                "items" or "barters" or "crafts" => """{ "data": null }""",
                _ => """{ "data": {} }""",
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    private readonly FakeTarkovDev _tarkovDev = new();

    private GameDataLoader Loader() => new(new CachedHttp(new HttpClient(_tarkovDev), _folder));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_page_in_place_of_the_data_is_asked_for_again_and_the_good_files_are_not()
    {
        _tarkovDev.Bodies["tasks"] = Page;
        var problem = LoadProblem.Explain(await Assert.ThrowsAnyAsync<Exception>(() => Loader().LoadAsync(GameMode.Pve, "en", Ct)));
        Assert.Equal(LoadFailure.Unreadable, problem.Kind);

        // The next start, within the hour the copies count as fresh: the page isn't read again, the data is asked for.
        _tarkovDev.Bodies.Remove("tasks");
        var data = await Loader().LoadAsync(GameMode.Pve, "en", Ct);
        Assert.False(data.Offline);
        Assert.Equal(2, _tarkovDev.Asked["tasks"]);
        Assert.Equal(1, _tarkovDev.Asked["maps"]);
        Assert.Equal(1, _tarkovDev.Asked["items_en"]);
    }

    [Fact]
    public async Task The_item_sources_too()
    {
        _tarkovDev.Bodies["items"] = "";
        await Assert.ThrowsAnyAsync<Exception>(() => Loader().LoadSourcesAsync(GameMode.Pve, "en", Ct));
        _tarkovDev.Bodies.Remove("items");
        await Loader().LoadSourcesAsync(GameMode.Pve, "en", Ct);
        Assert.Equal(2, _tarkovDev.Asked["items"]);
        Assert.Equal(1, _tarkovDev.Asked["barters"]);
    }

    [Fact]
    public async Task Data_in_a_shape_Shturmap_doesnt_know_stays_saved()
    {
        // JSON, only not what Shturmap reads: tarkov.dev changed its format, and a download would bring the same.
        _tarkovDev.Bodies["tasks"] = """{ "data": { "tasks": { "t1": 5 }, "questItems": {} } }""";
        for (var start = 0; start < 2; start++)
        {
            var e = await Assert.ThrowsAnyAsync<Exception>(() => Loader().LoadAsync(GameMode.Pve, "en", Ct));
            Assert.Equal(LoadFailure.Unreadable, LoadProblem.Explain(e).Kind);
        }
        Assert.Equal(1, _tarkovDev.Asked["tasks"]);
    }

    [Fact]
    public async Task Offline_the_good_saved_copy_is_used_and_kept()
    {
        await Loader().LoadAsync(GameMode.Pve, "en", Ct);
        // The copies are older than their keep time, and tarkov.dev can't be reached.
        foreach (var meta in Directory.GetFiles(_folder, "*.meta.json"))
        {
            var node = JsonNode.Parse(File.ReadAllText(meta))!;
            node["FetchedAt"] = DateTimeOffset.UtcNow.AddDays(-2);
            File.WriteAllText(meta, node.ToJsonString());
        }
        _tarkovDev.Offline = true;
        var saved = Directory.GetFiles(_folder).Order(StringComparer.Ordinal).ToList();
        Assert.True((await Loader().LoadAsync(GameMode.Pve, "en", Ct)).Offline);
        Assert.Equal(saved, Directory.GetFiles(_folder).Order(StringComparer.Ordinal));
    }
}
