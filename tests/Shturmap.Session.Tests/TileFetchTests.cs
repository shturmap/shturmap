using System.Net;
using Shturmap.Core.Maps;
using Shturmap.Data.Http;

namespace Shturmap.Session.Tests;

// A map tile tarkov.dev doesn't have (the caching check of 2026-10-04): asked for once, then not again for a week,
// where it used to be asked for in every session.
public class TileFetchTests : IDisposable
{
    private static readonly TileKey Tile = new("https://assets.tarkov.dev/maps/test/main/{z}/{x}/{y}.png", 3, 1, 2);
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-tiles-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    private sealed class Server(HttpStatusCode status) : HttpMessageHandler
    {
        public int Requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent([1, 2, 3]) });
        }
    }

    private Task<byte[]?> Fetch(Server server) =>
        ArtworkProvider.FetchTileAsync(new CachedHttp(new HttpClient(server), _folder), "test", Tile, TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_tile_that_is_not_there_is_asked_for_once()
    {
        var server = new Server(HttpStatusCode.NotFound);
        Assert.Null(await Fetch(server));
        // The next session: a new cache object over the same folder.
        Assert.Null(await Fetch(server));
        Assert.Equal(1, server.Requests);
    }

    [Fact]
    public async Task A_refused_tile_is_asked_for_again_next_time()
    {
        // "Forbidden" can be a block that passes: no tile now, but nothing is remembered.
        var server = new Server(HttpStatusCode.Forbidden);
        Assert.Null(await Fetch(server));
        Assert.Null(await Fetch(server));
        Assert.Equal(2, server.Requests);
    }

    [Fact]
    public async Task A_tile_that_is_there_is_read_from_the_saved_copy()
    {
        var server = new Server(HttpStatusCode.OK);
        Assert.Equal(new byte[] { 1, 2, 3 }, await Fetch(server));
        Assert.Equal(new byte[] { 1, 2, 3 }, await Fetch(server));
        Assert.Equal(1, server.Requests);
    }
}
