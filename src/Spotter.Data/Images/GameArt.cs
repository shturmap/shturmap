using System.Collections.Concurrent;
using SkiaSharp;

namespace Spotter.Data.Images;

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
            var temp = path + ".download";
            await File.WriteAllBytesAsync(temp, png.ToArray());
            File.Move(temp, path, overwrite: true);
            return path;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
