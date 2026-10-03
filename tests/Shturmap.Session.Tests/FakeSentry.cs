using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Shturmap.Session.Tests;

/// <summary>
/// A Sentry stand-in on this PC: a plain HTTP listener that keeps every request and answers with the status set in
/// <see cref="Status"/>. Its DSN is <c>http://testkey@127.0.0.1:&lt;port&gt;/1</c>.
/// </summary>
public sealed class FakeSentry : IAsyncDisposable
{
    public sealed record Request(string Method, string Path, IReadOnlyDictionary<string, string> Headers, byte[] Raw)
    {
        public string Body => Encoding.UTF8.GetString(Raw);
    }

    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;
    private readonly List<Request> _requests = [];

    public FakeSentry()
    {
        _listener.Start();
        _loop = Task.Run(AcceptAsync);
    }

    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    public string Dsn => $"http://testkey@127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/1";

    public IReadOnlyList<Request> Requests
    {
        get
        {
            lock (_requests)
                return _requests.ToList();
        }
    }

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception)
            {
                return;
            }
            _ = Task.Run(() => ServeAsync(client));
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using var _ = client;
        var stream = client.GetStream();
        var buffer = new List<byte>();
        var chunk = new byte[8192];
        int headerEnd;
        while ((headerEnd = IndexOf(buffer, "\r\n\r\n"u8.ToArray())) < 0)
        {
            var read = await stream.ReadAsync(chunk);
            if (read == 0)
                return;
            buffer.AddRange(chunk.AsSpan(0, read).ToArray());
        }
        var head = Encoding.ASCII.GetString(buffer.GetRange(0, headerEnd).ToArray()).Split("\r\n");
        var headers = head.Skip(1).Select(l => l.Split(':', 2)).Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);
        var length = headers.TryGetValue("Content-Length", out var value) ? int.Parse(value) : 0;
        var body = buffer.Skip(headerEnd + 4).ToList();
        while (body.Count < length)
        {
            var read = await stream.ReadAsync(chunk);
            if (read == 0)
                break;
            body.AddRange(chunk.AsSpan(0, read).ToArray());
        }
        var line = head[0].Split(' ');
        lock (_requests)
            _requests.Add(new Request(line[0], line[1], headers, body.ToArray()));
        var answer = Status == HttpStatusCode.OK ? """{"id":"0123456789abcdef0123456789abcdef"}""" : "{}";
        var response = $"HTTP/1.1 {(int)Status} {Status}\r\nContent-Type: application/json\r\nContent-Length: {answer.Length}\r\nConnection: close\r\n\r\n{answer}";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
    }

    private static int IndexOf(List<byte> data, byte[] pattern)
    {
        for (var i = 0; i <= data.Count - pattern.Length; i++)
        {
            var match = true;
            for (var j = 0; j < pattern.Length && match; j++)
                match = data[i + j] == pattern[j];
            if (match)
                return i;
        }
        return -1;
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        try
        {
            await _loop;
        }
        catch (Exception)
        {
        }
        _stop.Dispose();
    }
}
