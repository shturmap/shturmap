using System.Net;
using Shturmap.Data.Http;
using Shturmap.Data.Images;

namespace Shturmap.Data.Tests;

// The caching check of 2026-10-04 (owner: no unnecessary load on tarkov.dev): a file the server said it doesn't have
// was asked for again in every session. It is remembered for a week now, as long as the server lets its answers be kept.
public class MissingNoteTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-missing-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    private sealed class Server(Func<Uri, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            return Task.FromResult(respond(request.RequestUri!));
        }
    }

    private void MakeOlder(string savedPath, TimeSpan age) =>
        File.SetLastWriteTimeUtc(savedPath + ".none", DateTime.UtcNow - age);

    [Fact]
    public void A_missing_file_is_remembered_until_the_note_is_old()
    {
        var cache = new CachedHttp(new HttpClient(new Server(_ => new HttpResponseMessage(HttpStatusCode.NotFound))), _folder);
        const string key = "map/layer/9/1_1.png";
        Assert.False(cache.KnownMissing(key, CachedHttp.MissingAge));

        cache.RememberMissing(key);

        Assert.True(cache.KnownMissing(key, CachedHttp.MissingAge));
        // Another tile is another question.
        Assert.False(cache.KnownMissing("map/layer/9/1_2.png", CachedHttp.MissingAge));
        MakeOlder(Path.Combine(_folder, key), TimeSpan.FromDays(8));
        Assert.False(cache.KnownMissing(key, CachedHttp.MissingAge));
    }

    [Fact]
    public async Task A_file_that_arrives_after_all_ends_the_note_and_forgetting_does_too()
    {
        var cache = new CachedHttp(new HttpClient(new Server(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("tile") })), _folder);
        const string key = "map/layer/2/1_1.png";
        cache.RememberMissing(key);

        await cache.GetAsync(new Uri("https://example.test/tile"), key, TimeSpan.FromDays(30), TestContext.Current.CancellationToken);
        Assert.False(cache.KnownMissing(key, CachedHttp.MissingAge));

        cache.RememberMissing(key);
        cache.Forget(key);
        Assert.False(cache.KnownMissing(key, CachedHttp.MissingAge));
        Assert.Empty(Directory.GetFiles(_folder, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task A_picture_the_server_does_not_have_is_asked_for_once_a_week_not_once_a_start()
    {
        var server = new Server(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        const string id = "54cb50c76803fa8b248b4571";

        // Two starts: each makes its own GameArt over the same folder.
        Assert.Null(await new GameArt(new HttpClient(server), _folder).GetAsync(ArtKind.Trader, id));
        Assert.Null(await new GameArt(new HttpClient(server), _folder).GetAsync(ArtKind.Trader, id));
        Assert.Equal(1, server.Requests);

        MakeOlder(Path.Combine(_folder, "trader-" + id + ".png"), TimeSpan.FromDays(8));
        Assert.Null(await new GameArt(new HttpClient(server), _folder).GetAsync(ArtKind.Trader, id));
        Assert.Equal(2, server.Requests);
    }

    [Fact]
    public async Task A_picture_that_failed_for_another_reason_is_asked_for_again_at_the_next_start()
    {
        var server = new Server(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        const string id = "54cb50c76803fa8b248b4571";
        Assert.Null(await new GameArt(new HttpClient(server), _folder).GetAsync(ArtKind.Trader, id));
        Assert.Null(await new GameArt(new HttpClient(server), _folder).GetAsync(ArtKind.Trader, id));
        Assert.Equal(2, server.Requests);
    }
}
