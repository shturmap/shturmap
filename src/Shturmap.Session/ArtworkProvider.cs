using System.Net;
using Shturmap.Core.Maps;
using Shturmap.Data.Http;
using Shturmap.Data.Maps;
using Shturmap.Map;

namespace Shturmap.Session;

/// <summary>Loads each map's artwork once (download, parse, picture cache) and keeps it for the session.</summary>
/// <param name="tiles">Where tile renders are kept (<see cref="AppPaths.MapTileCache"/>); null: no tiles, sheets instead.</param>
public sealed class ArtworkProvider(ArtworkCache cache, string pictureCache, CachedHttp? tiles = null) : IDisposable
{
    private readonly Dictionary<string, Task<MapArtwork?>> _loaded = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MapTiles> _tiles = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    // Tile renders change rarely: a saved tile is used for a month before it is checked again.
    private static readonly TimeSpan TileMaxAge = TimeSpan.FromDays(30);

    /// <summary>Null for maps tarkov.dev only publishes as image tiles (The Lab, Labyrinth, Icebreaker): see <see cref="TilesFor"/>.</summary>
    public Task<MapArtwork?> GetAsync(MapDefinition definition)
    {
        lock (_gate)
        {
            if (!_loaded.TryGetValue(definition.Key, out var task) || task.IsFaulted)
                _loaded[definition.Key] = task = LoadAsync(definition);
            return task;
        }
    }

    /// <summary>
    /// The tile render of a map tarkov.dev publishes only as tiles, kept for the session, or null (a map with SVG
    /// artwork, or tiles turned off). Battlestate's art, shown like trader portraits and item icons (docs/DESIGN.md §3).
    /// </summary>
    public MapTiles? TilesFor(MapDefinition definition)
    {
        if (tiles is null || definition.SvgPath is not null || definition.TilePath is null)
            return null;
        lock (_gate)
        {
            if (!_tiles.TryGetValue(definition.Key, out var render))
            {
                // A saved tile that isn't an image (a download cut short) is thrown away, so the retry downloads it
                // instead of reading the same broken file until the cache's month is over.
                _tiles[definition.Key] = render = new MapTiles(definition, (tile, ct) => FetchTileAsync(tiles, definition.Key, tile, ct),
                    undecodable: tile => tiles.Forget(TileGrid.CacheKey(definition.Key, tile)));
            }
            return render;
        }
    }

    /// <summary>A tile from tarkov.dev's image service through the disk cache, or null when it has none there.</summary>
    public static async Task<byte[]?> FetchTileAsync(CachedHttp http, string mapKey, TileKey tile, CancellationToken ct)
    {
        var key = TileGrid.CacheKey(mapKey, tile);
        // A tile tarkov.dev said it doesn't have isn't asked for again for a week; it used to be, in every session.
        if (http.KnownMissing(key, CachedHttp.MissingAge))
            return null;
        try
        {
            var response = await http.GetAsync(new Uri(tile.Url), key, TileMaxAge, ct);
            return await File.ReadAllBytesAsync(response.FilePath, ct);
        }
        catch (HttpRequestException e) when (e.StatusCode is HttpStatusCode.NotFound)
        {
            // tarkov.dev has no tile there (an edge of the render, or a zoom it doesn't publish).
            http.RememberMissing(key);
            return null;
        }
        catch (HttpRequestException e) when (e.StatusCode is HttpStatusCode.Forbidden)
        {
            // No tile for this session, but not remembered: "forbidden" can be a block that passes.
            return null;
        }
    }

    private async Task<MapArtwork?> LoadAsync(MapDefinition definition)
    {
        if (definition.SvgPath is null)
            return null;
        var svg = await cache.GetSvgAsync(definition.SvgPath);
        return await Task.Run(() => MapArtwork.Load(svg, definition, pictureCache));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var task in _loaded.Values.Where(t => t.IsCompletedSuccessfully))
                task.Result?.Dispose();
            _loaded.Clear();
            foreach (var render in _tiles.Values)
                render.Dispose();
            _tiles.Clear();
        }
    }
}
