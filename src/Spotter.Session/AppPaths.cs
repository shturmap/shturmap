namespace Spotter.Session;

/// <summary>Where Spotter keeps its own files. Nothing is ever written under the game's folders.</summary>
/// <param name="cacheRoot">Downloaded data and artwork; defaults to the root's cache folder. Simulations share the real cache.</param>
public sealed class AppPaths(string root, string? cacheRoot = null)
{
    public static AppPaths Default { get; } =
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spotter"));

    public string Root { get; } = root;

    public string CacheRoot { get; } = cacheRoot ?? Path.Combine(root, "cache");

    public string Database => Path.Combine(Root, "spotter.db");

    public string DataCache => Path.Combine(CacheRoot, "tarkov-dev");

    public string ArtworkCache => Path.Combine(CacheRoot, "artwork");

    public string PictureCache => Path.Combine(CacheRoot, "pictures");

    /// <summary>Trader portraits and item icons from tarkov.dev, fetched when first shown.</summary>
    public string GameArtCache => Path.Combine(CacheRoot, "game-art");

    public string Logs => Path.Combine(Root, "logs");
}
