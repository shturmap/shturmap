using System.Net;
using System.Text;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Data.Tests;

// A download whose headers arrive and whose body then stops must end: the client's Timeout covers the headers only, so a
// stalled connection left "Loading game data…" standing for good, with no notice and no retry (the review of 2026-10-04).
public class StalledDownloadTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-stall-").FullName;
    private static readonly Uri Items = new("https://example.test/pve/items");
    private static readonly TimeSpan Idle = TimeSpan.FromMilliseconds(250);

    public void Dispose() => Directory.Delete(_folder, true);

    /// <summary>A body that arrives in pieces: each step is a wait and then what comes, or how it ends.</summary>
    private sealed class Body(params Func<CancellationToken, ValueTask<byte[]?>>[] steps) : Stream
    {
        private int _next;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (_next >= steps.Length)
                return 0;
            var bytes = await steps[_next++](ct);
            bytes?.CopyTo(buffer);
            return bytes?.Length ?? 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class StreamedContent(Stream body) : HttpContent
    {
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult(body);
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => body.CopyToAsync(stream);
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    // The headers come at once; the body is the test's.
    private sealed class Server(Func<Stream> body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamedContent(body()) });
    }

    private static Func<CancellationToken, ValueTask<byte[]?>> Send(string text, int afterMs = 0) => async ct =>
    {
        await Task.Delay(afterMs, ct);
        return Encoding.UTF8.GetBytes(text);
    };

    private static readonly Func<CancellationToken, ValueTask<byte[]?>> Silence = async ct =>
    {
        await Task.Delay(Timeout.Infinite, ct);
        return null;
    };

    private CachedHttp Cache(Func<Stream> body, TimeSpan? idle = null) => new(new HttpClient(new Server(body)), _folder, idle ?? Idle);

    [Fact]
    public async Task A_body_that_stops_coming_ends_as_a_timeout_worth_trying_again()
    {
        var http = Cache(() => new Body(Send("""{ "data": """), Silence));
        var e = await Assert.ThrowsAsync<TimeoutException>(() => http.GetAsync(Items, "pve_items.json", TimeSpan.Zero, TestContext.Current.CancellationToken));
        var problem = LoadProblem.Explain(e);
        Assert.Equal((LoadFailure.TimedOut, true), (problem.Kind, problem.Transient));
        // Nothing half-written stays: no cached file, no temporary one.
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public async Task A_stalled_refresh_falls_back_to_the_saved_copy()
    {
        var first = await Cache(() => new Body(Send("saved"))).GetAsync(Items, "pve_items.json", TimeSpan.Zero, TestContext.Current.CancellationToken);
        Assert.True(first.FromNetwork);
        var again = await Cache(() => new Body(Send("ne"), Silence)).GetAsync(Items, "pve_items.json", TimeSpan.Zero, TestContext.Current.CancellationToken);
        Assert.True(again.Stale);
        Assert.Equal("saved", File.ReadAllText(again.FilePath));
        Assert.Empty(Directory.GetFiles(_folder, "*.download"));
    }

    [Fact]
    public async Task A_slow_body_that_keeps_coming_is_let_finish()
    {
        // Ten pieces, each well inside the limit; all of them together take longer than it.
        var pieces = Enumerable.Range(0, 10).Select(i => Send(i.ToString(), afterMs: 60)).ToArray();
        var response = await Cache(() => new Body(pieces)).GetAsync(Items, "pve_items.json", TimeSpan.Zero, TestContext.Current.CancellationToken);
        Assert.Equal("0123456789", File.ReadAllText(response.FilePath));
    }

    [Fact]
    public async Task Stopping_the_caller_is_still_a_cancel_not_a_timeout()
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var http = Cache(() => new Body(Send("x"), Silence), idle: TimeSpan.FromSeconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => http.GetAsync(Items, "pve_items.json", TimeSpan.Zero, stop.Token));
    }

    [Fact]
    public async Task A_connection_that_breaks_off_is_the_networks_failure_not_the_disks()
    {
        var http = Cache(() => new Body(Send("x"), _ => throw new IOException("The connection was reset")));
        var e = await Assert.ThrowsAsync<HttpRequestException>(() => http.GetAsync(Items, "pve_items.json", TimeSpan.Zero, TestContext.Current.CancellationToken));
        var problem = LoadProblem.Explain(e);
        Assert.Equal((LoadFailure.Unreachable, true), (problem.Kind, problem.Transient));
    }
}
