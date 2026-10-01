namespace Shturmap.Session;

/// <summary>Where Shturmap keeps its own files. Nothing is ever written under the game's folders.</summary>
/// <param name="cacheRoot">Downloaded data and artwork; defaults to the root's cache folder. Simulations share the real cache.</param>
public sealed class AppPaths(string root, string? cacheRoot = null)
{
    public static AppPaths Default { get; } = CreateDefault();

    // The app was called Spotter until 2026-10-01: its folder and database move over on the first start. If the old
    // folder is in use (an old copy still running), the old folder is used as it is.
    private static AppPaths CreateDefault()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = Path.Combine(local, "Shturmap");
        var old = Path.Combine(local, "Spotter");
        if (!Directory.Exists(root) && Directory.Exists(old))
        {
            try
            {
                Directory.Move(old, root);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                root = old;
            }
        }
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var oldDb = Path.Combine(root, "spotter.db" + suffix);
            var newDb = Path.Combine(root, "shturmap.db" + suffix);
            try
            {
                if (File.Exists(oldDb) && !File.Exists(newDb))
                    File.Move(oldDb, newDb);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
        return new AppPaths(root);
    }

    public string Root { get; } = root;

    public string CacheRoot { get; } = cacheRoot ?? Path.Combine(root, "cache");

    public string Database => Path.Combine(Root, "shturmap.db");

    public string DataCache => Path.Combine(CacheRoot, "tarkov-dev");

    public string ArtworkCache => Path.Combine(CacheRoot, "artwork");

    public string PictureCache => Path.Combine(CacheRoot, "pictures");

    /// <summary>Trader portraits and item icons from tarkov.dev, fetched when first shown.</summary>
    public string GameArtCache => Path.Combine(CacheRoot, "game-art");

    public string Logs => Path.Combine(Root, "logs");

    /// <summary>The study log: game events and what the player did in Shturmap, one JSON line each.</summary>
    public string Study => Path.Combine(Root, "study");
}
