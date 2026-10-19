using System.Net;
using System.Text.Json.Nodes;
using Shturmap.Core.Desk;
using Shturmap.Core.Logs;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Data.Tests;

// The stash desk's card asks for an item's price history when it first shows the item (docs/DESIGN.md §3, "The stash
// desk's prices come per reading"; step C, 2026-10-19): json.tarkov.dev's "/{mode}/prices/{id}", one file per item, kept in
// the download cache and asked for again with its ETag after an hour; never on a timer, never in bulk. Made-up ids and
// points.
public class PriceHistoryTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-history-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    private const string Item = "0123456789abcdef01234567";

    private const string Body = """{"data":[{"priceMin":900,"price":1000,"timestamp":1767225600000},{"priceMin":950,"price":1100,"offerCount":12,"timestamp":1767232800000}],"translations":[]}""";

    private sealed class Server : HttpMessageHandler
    {
        public List<HttpRequestMessage> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Asked.Add(request);
            if (request.Headers.IfNoneMatch.Count > 0)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
            var answer = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Body) };
            answer.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"h1\"", true);
            return Task.FromResult(answer);
        }
    }

    [Fact]
    public async Task One_file_per_item_from_json_tarkov_dev_kept_with_its_etag()
    {
        var server = new Server();
        var loader = new GameDataLoader(new CachedHttp(CachedHttp.CreateClient(server), _folder));
        var ct = TestContext.Current.CancellationToken;

        var json = await loader.LoadPriceHistoryAsync(GameMode.Pve, Item, ct);
        Assert.Equal(Body, json);
        var asked = Assert.Single(server.Asked);
        Assert.Equal($"https://json.tarkov.dev/pve/prices/{Item}", asked.RequestUri!.ToString());

        // Within the hour: the saved copy, no request.
        Assert.Equal(Body, await loader.LoadPriceHistoryAsync(GameMode.Pve, Item, ct));
        Assert.Single(server.Asked);

        // Older: asked again with its ETag; "not modified" keeps the copy.
        var meta = Path.Combine(_folder, $"pve_prices_{Item}.json.meta.json");
        var node = JsonNode.Parse(File.ReadAllText(meta))!;
        node["FetchedAt"] = DateTimeOffset.UtcNow.AddHours(-2).ToString("o");
        File.WriteAllText(meta, node.ToJsonString());
        Assert.Equal(Body, await loader.LoadPriceHistoryAsync(GameMode.Pve, Item, ct));
        Assert.Equal(2, server.Asked.Count);
        Assert.Contains("\"h1\"", server.Asked[1].Headers.IfNoneMatch.ToString());

        // PvP's is its own file; an id that isn't one asks nothing.
        await loader.LoadPriceHistoryAsync(GameMode.Pvp, Item, ct);
        Assert.Equal($"https://json.tarkov.dev/regular/prices/{Item}", server.Asked[2].RequestUri!.ToString());
        Assert.Null(await loader.LoadPriceHistoryAsync(GameMode.Pve, "../items", ct));
        Assert.Equal(3, server.Asked.Count);

        // The file's points, read.
        var history = DeskHistory.Parse(json!)!;
        Assert.Equal(2, history.Points.Count);
        Assert.Equal(12, history.Points[1].Offers);
    }

    // The desk's prices (owner, 2026-10-19, after the test: "Maybe we need to fetch the prices more often then?"): when a
    // reading arrives, the item file is asked for again with its ETag once the saved copy is over ten minutes old (an hour
    // until then); a younger copy is used as it is. Never on a timer: the age is asked only when a reading arrives.
    [Fact]
    public async Task The_desks_item_file_is_asked_for_again_after_ten_minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(10), GameDataLoader.DeskPricesAge);
        var server = new Server();
        var http = new CachedHttp(CachedHttp.CreateClient(server), _folder);
        var ct = TestContext.Current.CancellationToken;
        var uri = new Uri("https://json.tarkov.dev/pve/items");
        await http.GetAsync(uri, "pve_items.json", GameDataLoader.DeskPricesAge, ct, json: true);
        Assert.Single(server.Asked);
        void FetchedAgo(int minutes)
        {
            var meta = Path.Combine(_folder, "pve_items.json.meta.json");
            var node = JsonNode.Parse(File.ReadAllText(meta))!;
            node["FetchedAt"] = DateTimeOffset.UtcNow.AddMinutes(-minutes).ToString("o");
            File.WriteAllText(meta, node.ToJsonString());
        }
        FetchedAgo(9);
        await http.GetAsync(uri, "pve_items.json", GameDataLoader.DeskPricesAge, ct, json: true);
        Assert.Single(server.Asked);
        FetchedAgo(11);
        await http.GetAsync(uri, "pve_items.json", GameDataLoader.DeskPricesAge, ct, json: true);
        Assert.Equal(2, server.Asked.Count);
        Assert.Contains("\"h1\"", server.Asked[1].Headers.IfNoneMatch.ToString());
    }
}
