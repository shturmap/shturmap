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

    [Fact]
    public void Each_writer_gets_its_own_temporary_file()
    {
        var path = Path.Combine(_folder, "item.png");
        Assert.NotEqual(CachedHttp.TempFor(path), CachedHttp.TempFor(path));
        Assert.StartsWith(path + ".", CachedHttp.TempFor(path));
    }

    [Fact]
    public async Task Replacing_waits_for_a_reader_that_holds_the_file_a_moment()
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
        CachedHttp.Replace(temp, path);
        await release;
        Assert.Equal("new", File.ReadAllText(path));
        Assert.False(File.Exists(temp));
    }
}
