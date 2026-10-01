using Spotter.Core.Maps;
using Spotter.Data.Maps;
using Spotter.Map;

namespace Spotter.Session;

/// <summary>Loads each map's artwork once (download, parse, picture cache) and keeps it for the session.</summary>
public sealed class ArtworkProvider(ArtworkCache cache, string pictureCache) : IDisposable
{
    private readonly Dictionary<string, Task<MapArtwork?>> _loaded = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Null for maps tarkov.dev only publishes as image tiles (The Lab, Labyrinth, Icebreaker).</summary>
    public Task<MapArtwork?> GetAsync(MapDefinition definition)
    {
        lock (_gate)
        {
            if (!_loaded.TryGetValue(definition.Key, out var task) || task.IsFaulted)
                _loaded[definition.Key] = task = LoadAsync(definition);
            return task;
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
        }
    }
}
