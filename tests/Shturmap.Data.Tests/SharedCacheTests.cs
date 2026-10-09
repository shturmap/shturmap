using System.Net;
using Shturmap.Data.Http;

namespace Shturmap.Data.Tests;

// The download cache is shared by every Shturmap on the PC: the installed release and developer builds (docs/DESIGN.md
// §8, "Data folders"). Writers must never collide, and readers never see half a file.
public class SharedCacheTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-cache-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    private sealed class SlowServer(string body) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(30, ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        }
    }

    [Fact]
    public async Task Many_writers_at_once_leave_one_whole_file_and_no_leftovers()
    {
        var body = new string('x', 200_000);
        // Separate clients and caches over one folder stand in for separate processes.
        var writers = Enumerable.Range(0, 8).Select(_ => new CachedHttp(new HttpClient(new SlowServer(body)), _folder)).ToList();
        var results = await Task.WhenAll(writers.Select(w => w.GetAsync(new Uri("https://example.test/maps"), "pve_maps.json", TimeSpan.Zero)));
        Assert.All(results, r => Assert.Equal(body, File.ReadAllText(r.FilePath)));
        Assert.Empty(Directory.GetFiles(_folder, "*.download"));
        Assert.Equal(["pve_maps.json", "pve_maps.json.meta.json"], Directory.GetFiles(_folder).Select(Path.GetFileName).Order());
    }

    private sealed class CountingServer : HttpMessageHandler
    {
        public int Requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("tile " + Requests) });
        }
    }

    // A saved copy that turned out to be no use (a map tile that isn't an image) must not be read again for its
    // whole month: forgotten, it is downloaded afresh.
    [Fact]
    public async Task A_forgotten_copy_is_downloaded_again()
    {
        var server = new CountingServer();
        var cache = new CachedHttp(new HttpClient(server), _folder);
        var uri = new Uri("https://example.test/tile");
        var first = await cache.GetAsync(uri, "map/layer/2/1_1.png", TimeSpan.FromDays(30));
        Assert.True(first.FromNetwork);
        Assert.False((await cache.GetAsync(uri, "map/layer/2/1_1.png", TimeSpan.FromDays(30))).FromNetwork);

        cache.Forget("map/layer/2/1_1.png");

        var again = await cache.GetAsync(uri, "map/layer/2/1_1.png", TimeSpan.FromDays(30));
        Assert.True(again.FromNetwork);
        Assert.Equal(2, server.Requests);
        Assert.Equal("tile 2", File.ReadAllText(again.FilePath));
    }

    [Fact]
    public void Each_writer_gets_its_own_temporary_file()
    {
        var path = Path.Combine(_folder, "item.png");
        Assert.NotEqual(CachedHttp.TempFor(path), CachedHttp.TempFor(path));
        Assert.StartsWith(path + ".", CachedHttp.TempFor(path));
    }

    // The downloads wait without holding a thread; a picture drawn on the spot waits on its own.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Replacing_waits_for_a_reader_that_holds_the_file_a_moment(bool asynchronously)
    {
        var path = Path.Combine(_folder, "data.json");
        File.WriteAllText(path, "old");
        var temp = CachedHttp.TempFor(path);
        File.WriteAllText(temp, "new");
        // A reader in another process: File.ReadAllText's sharing, which Windows won't replace a file under.
        var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var release = Task.Run(async () =>
        {
            await Task.Delay(120);
            await reader.DisposeAsync();
        });
        if (asynchronously)
            await CachedHttp.ReplaceAsync(temp, path);
        else
            CachedHttp.Replace(temp, path);
        await release;
        Assert.Equal("new", File.ReadAllText(path));
        Assert.False(File.Exists(temp));
    }
}
