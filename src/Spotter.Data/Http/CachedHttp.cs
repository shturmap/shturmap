using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Spotter.Data.Http;

/// <param name="FilePath">The cached body on disk.</param>
/// <param name="FromNetwork">A new body was downloaded in this call.</param>
/// <param name="Stale">The network could not be reached and an older copy is being used.</param>
public sealed record CachedResponse(string FilePath, bool FromNetwork, bool Stale, DateTimeOffset FetchedAt);

/// <summary>
/// GETs with a disk cache. A copy younger than maxAge is used as is; an older one is revalidated with its ETag
/// (an unchanged file costs one small request); when the network fails, the last good copy is used.
/// </summary>
public sealed class CachedHttp(HttpClient http, string cacheFolder)
{
    private sealed record Meta(string? ETag, DateTimeOffset? LastModified, DateTimeOffset FetchedAt);

    public static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(60),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Spotter/0.1 (personal Escape from Tarkov companion)");
        return client;
    }

    public async Task<CachedResponse> GetAsync(Uri uri, string cacheKey, TimeSpan maxAge, CancellationToken ct = default)
    {
        Directory.CreateDirectory(cacheFolder);
        var body = Path.Combine(cacheFolder, cacheKey);
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
            response.EnsureSuccessStatusCode();

            var temp = body + ".download";
            await using (var file = File.Create(temp))
                await response.Content.CopyToAsync(file, ct);
            File.Move(temp, body, overwrite: true);
            WriteMeta(metaPath, new Meta(response.Headers.ETag?.ToString(), response.Content.Headers.LastModified, now));
            return new CachedResponse(body, true, false, now);
        }
        catch (Exception e) when (cached && e is HttpRequestException or TaskCanceledException or IOException)
        {
            return new CachedResponse(body, false, true, meta!.FetchedAt);
        }
    }

    private static Meta? ReadMeta(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<Meta>(File.ReadAllText(path)) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void WriteMeta(string path, Meta meta) => File.WriteAllText(path, JsonSerializer.Serialize(meta));
}
