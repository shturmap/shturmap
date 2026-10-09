using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Shturmap.Data.Http;

/// <param name="FilePath">The cached body on disk.</param>
/// <param name="FromNetwork">A new body was downloaded in this call.</param>
/// <param name="Stale">No usable answer came (the network failed, or a page came in place of the data) and an older
/// copy is being used.</param>
public sealed record CachedResponse(string FilePath, bool FromNetwork, bool Stale, DateTimeOffset FetchedAt);

/// <summary>
/// An answer 200 that isn't the data asked for (<see cref="CachedHttp.GetAsync"/> with json): a page a sign-in portal,
/// a network filter or a CDN sent, or a body that is empty or cut off. It is kept nowhere. A network failure in effect,
/// so a saved copy is used in its place; the message names the address, never what came.
/// </summary>
public sealed class NotDataAnswerException(Uri uri) : HttpRequestException($"{uri} answered with something that isn't its data");

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

    public static HttpClient CreateClient() => CreateClient(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All });

    /// <summary>The app's client over a given transport (tests give their own): only <see cref="Hosts"/> are asked.</summary>
    public static HttpClient CreateClient(HttpMessageHandler transport)
    {
        var client = new HttpClient(new PublicDataOnly(transport))
        {
            Timeout = TimeSpan.FromSeconds(60),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }

    /// <summary>
    /// The hosts Shturmap's downloads go to: tarkov.dev's data and its images, and the map definitions on GitHub. The
    /// code names no other address (SafetyTests), but some addresses come out of the data: maps.json gives each map's
    /// artwork and tile paths, and it is a file in someone else's repository. A changed file must not be able to send
    /// every Shturmap to another host, to plain http or into the local network (review of 2026-10-04).
    /// </summary>
    public static readonly IReadOnlySet<string> Hosts =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "json.tarkov.dev", "assets.tarkov.dev", "raw.githubusercontent.com" };

    /// <summary>Whether a download may go to this address: https, to one of <see cref="Hosts"/>.</summary>
    public static bool Allows(Uri? uri) => uri is { IsAbsoluteUri: true } && uri.Scheme == Uri.UriSchemeHttps && Hosts.Contains(uri.IdnHost);

    // Refuses before anything is sent. (A redirect one of these hosts answers with is followed by the transport
    // below this: where their own files live is theirs to say.)
    private sealed class PublicDataOnly(HttpMessageHandler transport) : DelegatingHandler(transport)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Allows(request.RequestUri)
                ? base.SendAsync(request, ct)
                : Task.FromException<HttpResponseMessage>(new HttpRequestException(
                    $"Not asked: {request.RequestUri?.Scheme}://{request.RequestUri?.Host} is not where Shturmap's data comes from"));
    }

    /// <summary>
    /// Says who is asking, in which version, and where to find us, so tarkov.dev can get in touch about Shturmap's
    /// traffic and tell a build that misbehaves from the others: "Shturmap/0.3.0 (+https://github.com/shturmap; …)".
    /// </summary>
    public static string UserAgent { get; } = $"Shturmap/{BuildVersion()} (+https://github.com/shturmap; Escape from Tarkov companion)";

    // The build's version as every Shturmap assembly carries it (Directory.Build.props), without the commit: "0.3.0",
    // or a developer build's "0.3.0-dev.<time>".
    private static string BuildVersion()
    {
        var assembly = typeof(CachedHttp).Assembly;
        var version = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString(3) ?? "0";
        var commit = version.IndexOf('+');
        return commit >= 0 ? version[..commit] : version;
    }

    /// <param name="json">The answer must be a JSON object or array to be kept. A body that isn't (a page a sign-in
    /// portal, a network filter or a CDN sent with 200 in place of the data, or a body that is empty or cut off) never
    /// replaces the saved copy: it fails as <see cref="NotDataAnswerException"/>, which, like a network failure, gives
    /// the saved copy where there is one (owner, 2026-10-09: the page replaced a good copy and left "No game data").</param>
    public async Task<CachedResponse> GetAsync(Uri uri, string cacheKey, TimeSpan maxAge, CancellationToken ct = default, bool json = false)
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
                await WriteMetaAsync(metaPath, meta! with { FetchedAt = now });
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
                if (json && !IsJson(temp))
                    throw new NotDataAnswerException(uri);
                await ReplaceAsync(temp, body);
            }
            finally
            {
                TryDelete(temp);
            }
            await WriteMetaAsync(metaPath, new Meta(response.Headers.ETag?.ToString(), response.Content.Headers.LastModified, now));
            TryDelete(MissingNote(body));
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

    /// <summary>Whether the file holds a JSON object or array: tarkov.dev's data, not a page or a cut-off body. A file
    /// that can't be read is taken as one (can't tell; reading it fails on its own).</summary>
    public static bool IsJson(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            using var json = JsonDocument.Parse(file);
            return json.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>
    /// Throws a saved copy away, so the next <see cref="GetAsync"/> downloads it afresh: for a copy that turned out
    /// to be no use (a map tile that isn't an image). The note beside it goes first; without it the copy no longer
    /// counts as saved, whatever happens to the file.
    /// </summary>
    public void Forget(string cacheKey) => ForgetFile(Path.Combine(cacheFolder, cacheKey));

    /// <summary>The same for a copy <see cref="GetAsync"/> gave.</summary>
    public void Forget(CachedResponse response) => ForgetFile(response.FilePath);

    private static void ForgetFile(string body)
    {
        TryDelete(body + ".meta.json");
        TryDelete(body);
        TryDelete(MissingNote(body));
    }

    // ---- what a server doesn't have ----

    /// <summary>
    /// How long "the server has no such file" is remembered: as long as tarkov.dev's image service lets its answers
    /// be kept, a week (its Cache-Control on a tile, a picture and a "not found" alike; checked 2026-10-04). A tile
    /// or picture it doesn't have was asked for again in every session (the owner, the same day: no unnecessary load
    /// on tarkov.dev).
    /// </summary>
    public static readonly TimeSpan MissingAge = TimeSpan.FromDays(7);

    /// <summary>Notes that the server has no file for this key (it answered "not found").</summary>
    public void RememberMissing(string cacheKey) => NoteMissing(Path.Combine(cacheFolder, cacheKey));

    /// <summary>Whether the server said within <paramref name="maxAge"/> that it has no file for this key.</summary>
    public bool KnownMissing(string cacheKey, TimeSpan maxAge) => NotedMissing(Path.Combine(cacheFolder, cacheKey), maxAge);

    private static string MissingNote(string path) => path + ".none";

    /// <summary>Leaves an empty note beside where <paramref name="path"/> would be saved: the server has no such
    /// file. A note that can't be written only means it is asked for again.</summary>
    public static void NoteMissing(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(MissingNote(path), []);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Whether such a note is there and younger than <paramref name="maxAge"/>.</summary>
    public static bool NotedMissing(string path, TimeSpan maxAge)
    {
        try
        {
            var note = MissingNote(path);
            return File.Exists(note) && DateTime.UtcNow - File.GetLastWriteTimeUtc(note) < maxAge;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // The cache is shared by every Shturmap on the PC (the installed release and developer builds; docs/DESIGN.md §8,
    // "Data folders"). Each download goes to a temporary file of its own and replaces the cached one in one move, so
    // two processes never write the same file and a reader never sees half of one.

    /// <summary>A temporary file beside <paramref name="path"/> that no other writer uses.</summary>
    public static string TempFor(string path) => $"{path}.{Environment.ProcessId}-{Guid.NewGuid():N}.download";

    /// <summary>
    /// Moves a finished temporary file over the cached one. A reader in another process can hold the cached file open
    /// for a moment (Windows then refuses to replace it), so it tries a few times before giving up, waiting between
    /// tries without holding a thread (review of 2026-10-09: the downloads' waits slept a pool thread each).
    /// </summary>
    public static async Task ReplaceAsync(string temp, string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            if (TryMove(temp, path, attempt))
                return;
            await Task.Delay(ReplaceWait(attempt));
        }
    }

    /// <summary>The same for code that can't wait asynchronously (a picture drawn on the spot): its thread waits.</summary>
    public static void Replace(string temp, string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            if (TryMove(temp, path, attempt))
                return;
            Thread.Sleep(ReplaceWait(attempt));
        }
    }

    // Five tries, 50 to 200 ms apart; the last one's failure is thrown.
    private static bool TryMove(string temp, string path, int attempt)
    {
        try
        {
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException && attempt < 5)
        {
            return false;
        }
    }

    private static TimeSpan ReplaceWait(int attempt) => TimeSpan.FromMilliseconds(50 * attempt);

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
    private static async Task WriteMetaAsync(string path, Meta meta)
    {
        var temp = TempFor(path);
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(meta));
            await ReplaceAsync(temp, path);
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
