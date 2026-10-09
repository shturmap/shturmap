using System.Collections.Concurrent;
using SkiaSharp;

namespace Shturmap.Data.Images;

public enum ArtKind
{
    Trader,
    Item,
}

/// <summary>
/// Trader portraits and item icons from tarkov.dev's image service. This is Battlestate's game art: it is never
/// bundled, only fetched the first time it is shown and kept in the user's cache (docs/DESIGN.md §3). Callers show
/// a glyph until the picture is there, and keep it if the picture can't be had.
/// </summary>
public sealed class GameArt(HttpClient http, string folder)
{
    private readonly ConcurrentDictionary<string, Task<string?>> _loads = new(StringComparer.Ordinal);

    public static Uri UrlFor(ArtKind kind, string id) =>
        new(kind == ArtKind.Trader ? $"https://assets.tarkov.dev/{id}.webp" : $"https://assets.tarkov.dev/{id}-icon.webp");

    /// <summary>The cached PNG, if it is already on disk.</summary>
    public string? Cached(ArtKind kind, string id) =>
        Key(kind, id) is { } key && File.Exists(PathOf(key)) ? PathOf(key) : null;

    /// <summary>The PNG on disk, downloading it once if needed; null if it isn't available (offline, unknown id).</summary>
    public Task<string?> GetAsync(ArtKind kind, string id)
    {
        if (Key(kind, id) is not { } key)
            return Task.FromResult<string?>(null);
        if (File.Exists(PathOf(key)))
            return Task.FromResult<string?>(PathOf(key));
        // A picture tarkov.dev said it doesn't have isn't asked for again for a week.
        if (Http.CachedHttp.NotedMissing(PathOf(key), Http.CachedHttp.MissingAge))
            return Task.FromResult<string?>(null);
        // One download per picture per run; a failure is not retried until the next start.
        return _loads.GetOrAdd(key, _ => DownloadAsync(UrlFor(kind, id), PathOf(key)));
    }

    private string PathOf(string key) => Path.Combine(folder, key + ".png");

    // Ids are tarkov.dev's hex ids; anything else is not a file name we'd write.
    private static string? Key(ArtKind kind, string id) =>
        id.Length is > 0 and <= 64 && id.All(char.IsAsciiLetterOrDigit) ? $"{(kind == ArtKind.Trader ? "trader" : "item")}-{id}" : null;

    private async Task<string?> DownloadAsync(Uri url, string path)
    {
        try
        {
            var bytes = await http.GetByteArrayAsync(url);
            using var bitmap = SKBitmap.Decode(bytes);
            if (bitmap is null)
                return null;
            Directory.CreateDirectory(folder);
            using var image = SKImage.FromBitmap(bitmap);
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            // The cache is shared by every Shturmap on the PC: a temporary file of its own, then one move (CachedHttp).
            var temp = Http.CachedHttp.TempFor(path);
            try
            {
                await File.WriteAllBytesAsync(temp, png.ToArray());
                await Http.CachedHttp.ReplaceAsync(temp, path);
            }
            finally
            {
                Http.CachedHttp.TryDelete(temp);
            }
            return path;
        }
        catch (HttpRequestException e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // tarkov.dev has no picture for this id: remembered, so the next starts don't ask again.
            Http.CachedHttp.NoteMissing(path);
            return null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            // Another Shturmap may have saved the same picture meanwhile.
            return File.Exists(path) ? path : null;
        }
    }
}
