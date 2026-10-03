namespace Shturmap.Session;

/// <summary>Which data folder a build keeps its database, logs, study log and reports in.</summary>
public enum DataFolderKind
{
    /// <summary>The installed release: %LOCALAPPDATA%\Shturmap, the player's own data.</summary>
    Release,

    /// <summary>Every other build (the folder build, the dev build, <c>dotnet run</c>, tests): %LOCALAPPDATA%\Shturmap-dev.</summary>
    Dev,

    /// <summary>A folder named with <c>--data</c>.</summary>
    Custom,
}

/// <summary>Where Shturmap keeps its own files. Nothing is ever written under the game's folders.</summary>
/// <param name="cacheRoot">Downloaded data and artwork; defaults to the root's cache folder. Simulations share the real cache.</param>
/// <remarks>
/// Only the installed release uses the player's folder; developer builds have their own, so testing never mixes into
/// the player's database, settings, app log, study log or reports (owner, 2026-10-03; docs/DESIGN.md §8, "Data
/// folders"). Downloads are shared by all of them in the release's cache folder, so nothing downloads twice.
/// </remarks>
public sealed class AppPaths(string root, string? cacheRoot = null, DataFolderKind kind = DataFolderKind.Custom)
{
    private static AppPaths? _default;

    /// <summary>
    /// This process's folders: what <see cref="Use"/> chose at start, else (the CLI, tests, anything that didn't
    /// choose) the developer folder.
    /// </summary>
    public static AppPaths Default => _default ??= For(DataFolderKind.Dev);

    /// <summary>Chooses this process's folders, once, at start, before anything uses <see cref="Default"/>.</summary>
    public static AppPaths Use(DataFolderKind kind, string? folder = null) => _default = For(kind, folder);

    /// <summary>
    /// The folders for a kind of build: the installed release keeps %LOCALAPPDATA%\Shturmap; every other build
    /// %LOCALAPPDATA%\Shturmap-dev; <c>--data</c> names its own. The download cache is always the release's.
    /// </summary>
    /// <param name="localAppData">For tests: stands in for %LOCALAPPDATA% (and skips the Spotter move).</param>
    public static AppPaths For(DataFolderKind kind, string? folder = null, string? localAppData = null)
    {
        var local = localAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var release = Path.Combine(local, "Shturmap");
        var cache = Path.Combine(release, "cache");
        return kind switch
        {
            DataFolderKind.Release => new AppPaths(localAppData is null ? MovedFromSpotter(local, release) : release, null, kind),
            DataFolderKind.Custom when !string.IsNullOrWhiteSpace(folder) => new AppPaths(Path.GetFullPath(folder), cache, kind),
            _ => new AppPaths(Path.Combine(local, "Shturmap-dev"), cache, DataFolderKind.Dev),
        };
    }

    // The app was called Spotter until 2026-10-01: its folder and database move over on the first start. If the old
    // folder is in use (an old copy still running), the old folder is used as it is.
    private static string MovedFromSpotter(string local, string root)
    {
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
        return root;
    }

    public string Root { get; } = root;

    public DataFolderKind Kind { get; } = kind;

    /// <summary>How the diagnostics name the data folder: "release", "dev" or "custom (--data)".</summary>
    public string KindText => Kind switch
    {
        DataFolderKind.Release => "release",
        DataFolderKind.Dev => "dev",
        _ => "custom (--data)",
    };

    public string CacheRoot { get; } = cacheRoot ?? Path.Combine(root, "cache");

    public string Database => Path.Combine(Root, "shturmap.db");

    public string DataCache => Path.Combine(CacheRoot, "tarkov-dev");

    public string ArtworkCache => Path.Combine(CacheRoot, "artwork");

    public string PictureCache => Path.Combine(CacheRoot, "pictures");

    /// <summary>Trader portraits and item icons from tarkov.dev, fetched when first shown.</summary>
    public string GameArtCache => Path.Combine(CacheRoot, "game-art");

    /// <summary>tarkov.dev's tile renders of the maps without SVG artwork, fetched as a view needs them.</summary>
    public string MapTileCache => Path.Combine(CacheRoot, "map-tiles");

    public string Logs => Path.Combine(Root, "logs");

    /// <summary>The study log: game events and what the player did in Shturmap, one JSON line each.</summary>
    public string Study => Path.Combine(Root, "study");
}
