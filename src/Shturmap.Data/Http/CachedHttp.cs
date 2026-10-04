using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Shturmap.Data.Http;

/// <param name="FilePath">The cached body on disk.</param>
/// <param name="FromNetwork">A new body was downloaded in this call.</param>
/// <param name="Stale">The network could not be reached and an older copy is being used.</param>
public sealed record CachedResponse(string FilePath, bool FromNetwork, bool Stale, DateTimeOffset FetchedAt);

/// <summary>
/// GETs with a disk cache. A copy younger than maxAge is used as is; an older one is revalidated with its ETag
/// (an unchanged file costs one small request); when the network fails, the last good copy is used.
/// </summary>
/// <param name="bodyIdleLimit">How long a download may deliver nothing before it counts as stalled
/// (<see cref="BodyIdleLimit"/> unless a test wants it shorter).</param>
public sealed class CachedHttp(HttpClient http, string cacheFolder, TimeSpan? bodyIdleLimit = null)
{
    private sealed record Meta(string? ETag, DateTimeOffset? LastModified, DateTimeOffset FetchedAt);

    /// <summary>
    /// How long a download's body may deliver nothing. The client's own Timeout ends once the headers are in (requests
    /// are sent with ResponseHeadersRead), so without a limit of its own a connection that stalls mid-body never ends:
    /// "Loading game data…" for good, with no notice and no retry. It is a limit on silence, not on the whole download:
    /// the items payload is 17 MB, and a slow line that keeps delivering must be let finish.
    /// </summary>
    public static readonly TimeSpan BodyIdleLimit = TimeSpan.FromSeconds(30);

    private readonly TimeSpan _bodyIdleLimit = bodyIdleLimit ?? BodyIdleLimit;

    public static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(60),
        };
        // Says who is asking and where to find us, so tarkov.dev can get in touch about Shturmap's traffic.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Shturmap/0.1 (+https://github.com/shturmap; Escape from Tarkov companion)");
        return client;
    }

    public async Task<CachedResponse> GetAsync(Uri uri, string cacheKey, TimeSpan maxAge, CancellationToken ct = default)
    {
        var body = Path.Combine(cacheFolder, cacheKey);
        // A key may hold folders (map tiles: "<map>/<layer>/<z>/<x>_<y>.png").
        Directory.CreateDirectory(Path.GetDirectoryName(body)!);
        var metaPath = body + ".meta.json";
        var meta = ReadMeta(metaPath);
        var cached = meta is not null && File.Exists(body);

        if (cached && DateTimeOffset.UtcNow - meta!.FetchedAt < maxAge)
            return new CachedResponse(body, false, false, meta.FetchedAt);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (cached && meta!.ETag is { } etag)
                request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(etag));
            else if (cached && meta!.LastModified is { } modified)
                request.Headers.IfModifiedSince = modified;

            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            var now = DateTimeOffset.UtcNow;
            if (response.StatusCode == HttpStatusCode.NotModified && cached)
            {
                WriteMeta(metaPath, meta! with { FetchedAt = now });
                return new CachedResponse(body, false, false, now);
            }
            // Which address answered what, for the app log; the player sees LoadProblem's words.
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"{uri} answered {(int)response.StatusCode} {response.ReasonPhrase}", null, response.StatusCode);

            var temp = TempFor(body);
            try
            {
                await using (var file = File.Create(temp))
                    await CopyBodyAsync(response, file, uri, ct);
                Replace(temp, body);
            }
            finally
            {
                TryDelete(temp);
            }
            WriteMeta(metaPath, new Meta(response.Headers.ETag?.ToString(), response.Content.Headers.LastModified, now));
            return new CachedResponse(body, true, false, now);
        }
        catch (IOException) when (!cached && File.Exists(body))
        {
            // Another Shturmap wrote the same file a moment ago and still holds it: that copy is as new.
            return new CachedResponse(body, false, false, DateTimeOffset.UtcNow);
        }
        catch (Exception e) when (cached && e is HttpRequestException or TaskCanceledException or TimeoutException or IOException)
        {
            return new CachedResponse(body, false, true, meta!.FetchedAt);
        }
    }

    // The body, read by hand so that each read has the idle limit and a failure says which side failed: the network
    // (a stall is a TimeoutException, a connection that breaks off an HttpRequestException: both worth trying again,
    // LoadProblem) or the disk (the file's own IOException).
    private async Task CopyBodyAsync(HttpResponseMessage response, Stream file, Uri uri, CancellationToken ct)
    {
        await using var body = await response.Content.ReadAsStreamAsync(ct);
        using var stalled = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            while (true)
            {
                int read;
                stalled.CancelAfter(_bodyIdleLimit);
                try
                {
                    read = await body.ReadAsync(buffer, stalled.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    throw new TimeoutException($"{uri} sent nothing for {_bodyIdleLimit.TotalSeconds:0} s in the middle of its answer");
                }
                catch (IOException e)
                {
                    throw new HttpRequestException($"{uri} broke off in the middle of its answer", e);
                }
                // Writing to the disk isn't the server's silence.
                stalled.CancelAfter(Timeout.InfiniteTimeSpan);
                if (read == 0)
                    return;
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // The cache is shared by every Shturmap on the PC (the installed release and developer builds; docs/DESIGN.md §8,
    // "Data folders"). Each download goes to a temporary file of its own and replaces the cached one in one move, so
    // two processes never write the same file and a reader never sees half of one.

    /// <summary>A temporary file beside <paramref name="path"/> that no other writer uses.</summary>
    public static string TempFor(string path) => $"{path}.{Environment.ProcessId}-{Guid.NewGuid():N}.download";

    /// <summary>
    /// Moves a finished temporary file over the cached one. A reader in another process can hold the cached file open
    /// for a moment (Windows then refuses to replace it), so it tries a few times before giving up.
    /// </summary>
    public static void Replace(string temp, string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(temp, path, overwrite: true);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && attempt < 5)
            {
                Thread.Sleep(50 * attempt);
            }
        }
    }

    public static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static Meta? ReadMeta(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<Meta>(File.ReadAllText(path)) : null;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Meta that couldn't be written only means the next request revalidates: harmless.
    private static void WriteMeta(string path, Meta meta)
    {
        var temp = TempFor(path);
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(meta));
            Replace(temp, path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
        finally
        {
            TryDelete(temp);
        }
    }
}
