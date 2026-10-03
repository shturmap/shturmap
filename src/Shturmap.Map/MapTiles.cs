using SkiaSharp;
using Shturmap.Core.Maps;

namespace Shturmap.Map;

/// <summary>Whether a map's tile render can be shown.</summary>
public enum TileStatus
{
    /// <summary>No tile has arrived yet, and none has failed for good.</summary>
    Loading,

    /// <summary>At least one tile arrived: the render is shown.</summary>
    Showing,

    /// <summary>Every tile asked for so far failed (offline without a saved copy, or tarkov.dev has none): the grid sheet stands in.</summary>
    Unavailable,
}

/// <summary>
/// A map's top-down tile render from tarkov.dev's image service (The Lab, Labyrinth, Icebreaker; docs/DESIGN.md §3,
/// "Maps without SVG artwork"): the tiles a view needs, at the zoom level that matches the screen, loaded in the
/// background a few at a time, kept decoded in memory (least recently used go first) and on disk by the fetcher.
/// While a tile loads, the nearest coarser tile that is loaded stands in, stretched.
/// </summary>
public sealed class MapTiles : IDisposable
{
    /// <summary>Gets a tile's image file, or null when tarkov.dev has no such tile (404). Throws when it can't be had now.</summary>
    public delegate Task<byte[]?> Fetch(TileKey tile, CancellationToken ct);

    private readonly Fetch _fetch;
    private readonly int _capacity;
    private readonly Lock _gate = new();
    private readonly Dictionary<TileKey, LinkedListNode<(TileKey Key, SKImage Image)>> _images = [];
    // Most recently used first.
    private readonly LinkedList<(TileKey Key, SKImage Image)> _order = new();
    private readonly HashSet<TileKey> _missing = [];
    private readonly Dictionary<TileKey, DateTime> _failedAt = [];
    private readonly Dictionary<TileKey, Task> _pending = [];
    private readonly SemaphoreSlim _slots = new(4);
    private readonly CancellationTokenSource _stop = new();
    private int _imagePixels = 256;
    private int _arrived;
    private int _failed;

    // A tile that couldn't be had is asked for again after this long (the network may be back).
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(30);

    /// <param name="capacity">How many decoded tiles stay in memory (a 256 px tile takes 256 KB).</param>
    public MapTiles(MapDefinition definition, Fetch fetch, int capacity = 192)
    {
        Definition = definition;
        _fetch = fetch;
        _capacity = Math.Max(16, capacity);
    }

    public MapDefinition Definition { get; }

    public TileStatus Status
    {
        get
        {
            lock (_gate)
                return _arrived > 0 ? TileStatus.Showing : _pending.Count == 0 && _failed + _missing.Count > 0 ? TileStatus.Unavailable : TileStatus.Loading;
        }
    }

    /// <summary>A tile arrived or a load ended; raised on a background thread. The map view redraws.</summary>
    public event Action? Changed;

    public int TileSize => Definition.TileSize > 0 ? Definition.TileSize : 256;

    private int MinZoom => (int)Math.Ceiling(Definition.MinZoom);

    private int MaxZoom => (int)Math.Floor(Definition.MaxZoom);

    /// <summary>How many decoded tiles are in memory now.</summary>
    public int Cached
    {
        get
        {
            lock (_gate)
                return _images.Count;
        }
    }

    /// <summary>The zoom level drawn at <paramref name="screenPerUnit"/> screen pixels per map unit.</summary>
    public int ZoomFor(double screenPerUnit) => TileGrid.ZoomFor(screenPerUnit, TileSize, _imagePixels, MinZoom, MaxZoom);

    /// <summary>
    /// Hands each tile a view of one layer needs to <paramref name="draw"/> (its image, the part of the image to use,
    /// and where it goes in map units) and starts loading the ones that aren't here yet. <paramref name="draw"/> runs
    /// while no tile can be thrown out of memory, so it must only draw.
    /// </summary>
    public void Draw(string template, MapRect view, MapRect bounds, double screenPerUnit, Action<SKImage, SKRect, MapRect> draw)
    {
        var z = ZoomFor(screenPerUnit);
        lock (_gate)
        {
            foreach (var (x, y) in TileGrid.Visible(view, bounds, TileSize, z))
            {
                var key = new TileKey(template, z, x, y);
                if (Touch(key) is { } image)
                {
                    draw(image, SKRect.Create(image.Width, image.Height), TileGrid.TileRect(TileSize, z, x, y));
                    continue;
                }
                Request(key);
                // Until it is here, the nearest coarser tile that is: the part of it that covers this one.
                var depth = 1;
                for (var parent = key.Parent; parent.Z >= MinZoom; parent = parent.Parent, depth++)
                {
                    if (Touch(parent) is not { } coarse)
                        continue;
                    var cells = 1 << depth;
                    var size = coarse.Width / (float)cells;
                    var source = SKRect.Create((x - (parent.X << depth)) * size, (y - (parent.Y << depth)) * size, size, size);
                    draw(coarse, source, TileGrid.TileRect(TileSize, z, x, y));
                    break;
                }
            }
            // A coarse level for the whole view, so something shows at once when zooming or panning into new ground.
            var coarseZoom = Math.Max(MinZoom, z - 3);
            if (coarseZoom < z)
            {
                foreach (var (x, y) in TileGrid.Visible(view, bounds, TileSize, coarseZoom))
                    Request(new TileKey(template, coarseZoom, x, y));
            }
        }
    }

    /// <summary>Loads every tile a view needs at its zoom and waits for them (the CLI's renders and snapshots).</summary>
    public Task LoadAsync(string template, MapRect view, MapRect bounds, double screenPerUnit)
    {
        var z = ZoomFor(screenPerUnit);
        var waits = new List<Task>();
        lock (_gate)
        {
            foreach (var (x, y) in TileGrid.Visible(view, bounds, TileSize, z))
            {
                var key = new TileKey(template, z, x, y);
                Request(key);
                if (_pending.TryGetValue(key, out var task))
                    waits.Add(task);
            }
        }
        return Task.WhenAll(waits);
    }

    // Under _gate.
    private SKImage? Touch(TileKey key)
    {
        if (!_images.TryGetValue(key, out var node))
            return null;
        _order.Remove(node);
        _order.AddFirst(node);
        return node.Value.Image;
    }

    // Under _gate.
    private void Request(TileKey key)
    {
        if (_images.ContainsKey(key) || _pending.ContainsKey(key) || _missing.Contains(key) || _stop.IsCancellationRequested)
            return;
        if (_failedAt.TryGetValue(key, out var at) && DateTime.UtcNow - at < RetryAfter)
            return;
        _pending[key] = Task.Run(() => LoadAsync(key));
    }

    private async Task LoadAsync(TileKey key)
    {
        var ct = _stop.Token;
        try
        {
            await _slots.WaitAsync(ct);
            try
            {
                var bytes = await _fetch(key, ct);
                SKImage? image = null;
                if (bytes is not null)
                {
                    // Decoded here, off the drawing thread (an encoded image would decode on its first draw), with the
                    // artwork's colour treatment (ArtworkColors: receded, amber-like colours muted) applied once, so a
                    // frame only copies pixels.
                    using var data = SKData.CreateCopy(bytes);
                    using var encoded = SKImage.FromEncodedData(data);
                    if (encoded is not null)
                    {
                        using var surface = SKSurface.Create(new SKImageInfo(encoded.Width, encoded.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
                        using var paint = new SKPaint { ColorFilter = ArtworkColors.Filter };
                        surface.Canvas.Clear(SKColors.Transparent);
                        surface.Canvas.DrawImage(encoded, 0, 0, paint);
                        image = surface.Snapshot();
                    }
                }
                lock (_gate)
                {
                    _failedAt.Remove(key);
                    if (image is null)
                    {
                        _missing.Add(key);
                    }
                    else
                    {
                        _imagePixels = image.Width;
                        _images[key] = _order.AddFirst((key, image));
                        _arrived++;
                        while (_images.Count > _capacity && _order.Last is { } oldest)
                        {
                            _order.RemoveLast();
                            _images.Remove(oldest.Value.Key);
                            oldest.Value.Image.Dispose();
                        }
                    }
                }
            }
            finally
            {
                _slots.Release();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            lock (_gate)
            {
                _failedAt[key] = DateTime.UtcNow;
                _failed++;
            }
        }
        finally
        {
            lock (_gate)
                _pending.Remove(key);
        }
        if (!ct.IsCancellationRequested)
            Changed?.Invoke();
    }

    public void Dispose()
    {
        _stop.Cancel();
        lock (_gate)
        {
            foreach (var (_, image) in _order)
                image.Dispose();
            _order.Clear();
            _images.Clear();
        }
    }
}
