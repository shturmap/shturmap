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

    /// <summary>
    /// Every tile asked for so far failed (offline without a saved copy, or tarkov.dev has none): the grid sheet stands
    /// in. It stays so while the tiles are asked for again, until one arrives.
    /// </summary>
    Unavailable,
}

/// <summary>
/// A map's top-down tile render from tarkov.dev's image service (The Lab, Labyrinth, Icebreaker; docs/DESIGN.md §3,
/// "Maps without SVG artwork"): the tiles a view needs, at the zoom level that matches the screen, loaded in the
/// background a few at a time, kept decoded in memory (least recently used go first) and on disk by the fetcher.
/// While a tile loads, the nearest coarser tile that is loaded stands in, stretched. A tile that couldn't be had is
/// asked for again by itself while the view still needs it, so a render that failed offline appears once the
/// network is back.
/// </summary>
public sealed class MapTiles : IDisposable
{
    /// <summary>Gets a tile's image file, or null when tarkov.dev has no such tile (404). Throws when it can't be had now.</summary>
    public delegate Task<byte[]?> Fetch(TileKey tile, CancellationToken ct);

    private readonly Fetch _fetch;
    private readonly int _capacity;
    private readonly TimeSpan _retryAfter;
    private readonly Lock _gate = new();
    private readonly Dictionary<TileKey, LinkedListNode<(TileKey Key, SKImage Image)>> _images = [];
    // Most recently used first.
    private readonly LinkedList<(TileKey Key, SKImage Image)> _order = new();
    private readonly HashSet<TileKey> _missing = [];
    private readonly Dictionary<TileKey, DateTime> _failedAt = [];
    private readonly Dictionary<TileKey, Task> _pending = [];
    // What the view last asked for, per layer (its URL template): what is asked for again after a failure, and what
    // never leaves memory while it is the view (a 4K view needs more tiles than the capacity).
    private readonly Dictionary<string, HashSet<TileKey>> _view = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _slots = new(4);
    private readonly CancellationTokenSource _stop = new();
    private int _imagePixels = 256;
    private int _arrived;
    private int _failed;
    private bool _unavailable;
    private bool _retrying;

    /// <summary>A tile that couldn't be had is asked for again after this long (the network may be back).</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(30);

    /// <param name="capacity">How many decoded tiles stay in memory (a 256 px tile takes 256 KB), beyond what the view
    /// on screen needs: its own tiles always stay, however many they are.</param>
    /// <param name="retryAfter">How long a tile that couldn't be had waits before it is asked for again;
    /// <see cref="RetryAfter"/> unless a test gives another.</param>
    /// <param name="undecodable">Told when a tile's file was there but isn't an image (a download cut short): the
    /// saved copy shouldn't be trusted, so whoever keeps it can throw it away before the tile is asked for again.</param>
    public MapTiles(MapDefinition definition, Fetch fetch, int capacity = 192, TimeSpan? retryAfter = null, Action<TileKey>? undecodable = null)
    {
        Definition = definition;
        _fetch = fetch;
        _capacity = Math.Max(16, capacity);
        _retryAfter = retryAfter ?? RetryAfter;
        _undecodable = undecodable;
    }

    private readonly Action<TileKey>? _undecodable;

    public MapDefinition Definition { get; }

    public TileStatus Status
    {
        get
        {
            lock (_gate)
                return _arrived > 0 ? TileStatus.Showing : _unavailable ? TileStatus.Unavailable : TileStatus.Loading;
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
    public void Draw(string template, MapRect view, MapRect bounds, double screenPerUnit, Action<SKImage, SKRect, MapRect> draw) =>
        Visit(template, view, bounds, screenPerUnit, draw);

    /// <summary>
    /// Asks for the tiles a view of one layer needs without drawing any: what the renderer does while the sheet stands
    /// in for a render that couldn't be had. A tile that failed is asked for again only once its wait is over, so
    /// asking with every frame starts no more requests than one round per <see cref="RetryAfter"/>.
    /// </summary>
    public void Ask(string template, MapRect view, MapRect bounds, double screenPerUnit) =>
        Visit(template, view, bounds, screenPerUnit, null);

    private void Visit(string template, MapRect view, MapRect bounds, double screenPerUnit, Action<SKImage, SKRect, MapRect>? draw)
    {
        var z = ZoomFor(screenPerUnit);
        var wanted = new HashSet<TileKey>();
        lock (_gate)
        {
            foreach (var (x, y) in TileGrid.Visible(view, bounds, TileSize, z))
            {
                var key = new TileKey(template, z, x, y);
                wanted.Add(key);
                if (Touch(key) is { } image)
                {
                    draw?.Invoke(image, SKRect.Create(image.Width, image.Height), TileGrid.TileRect(TileSize, z, x, y));
                    continue;
                }
                Request(key);
                // Until it is here, the nearest coarser tile that is: the part of it that covers this one.
                var depth = 1;
                for (var parent = key.Parent; parent.Z >= MinZoom; parent = parent.Parent, depth++)
                {
                    if (Touch(parent) is not { } coarse)
                        continue;
                    // It stands in for this frame, so it stays for this view like the tiles themselves.
                    wanted.Add(parent);
                    var cells = 1 << depth;
                    var size = coarse.Width / (float)cells;
                    var source = SKRect.Create((x - (parent.X << depth)) * size, (y - (parent.Y << depth)) * size, size, size);
                    draw?.Invoke(coarse, source, TileGrid.TileRect(TileSize, z, x, y));
                    break;
                }
            }
            // A coarse level for the whole view, so something shows at once when zooming or panning into new ground.
            var coarseZoom = Math.Max(MinZoom, z - 3);
            if (coarseZoom < z)
            {
                foreach (var (x, y) in TileGrid.Visible(view, bounds, TileSize, coarseZoom))
                {
                    var key = new TileKey(template, coarseZoom, x, y);
                    wanted.Add(key);
                    Request(key);
                }
            }
            _view[template] = wanted;
            Trim();
        }
    }

    /// <summary>
    /// The layers the view shows now (their URL templates): what earlier views asked for on any other layer, a floor
    /// no longer shown, may leave memory again.
    /// </summary>
    public void Shows(params string?[] templates)
    {
        lock (_gate)
        {
            foreach (var gone in _view.Keys.Where(t => !templates.Contains(t, StringComparer.Ordinal)).ToList())
                _view.Remove(gone);
            Trim();
        }
    }

    /// <summary>Loads every tile a view needs at its zoom and waits for them (the CLI's renders and snapshots).</summary>
    public Task LoadAsync(string template, MapRect view, MapRect bounds, double screenPerUnit)
    {
        var z = ZoomFor(screenPerUnit);
        var waits = new List<Task>();
        var wanted = new HashSet<TileKey>();
        lock (_gate)
        {
            foreach (var (x, y) in TileGrid.Visible(view, bounds, TileSize, z))
            {
                var key = new TileKey(template, z, x, y);
                wanted.Add(key);
                Request(key);
                if (_pending.TryGetValue(key, out var task))
                    waits.Add(task);
            }
            _view[template] = wanted;
            Trim();
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
        if (_failedAt.TryGetValue(key, out var at) && DateTime.UtcNow - at < _retryAfter)
            return;
        _pending[key] = Task.Run(() => LoadAsync(key));
    }

    // Under _gate. Over the capacity, the least recently used tiles go, but never one the view on screen needs: a view
    // that needs more than the capacity (a 4K window; a floor's layer over the base) would otherwise throw out a tile
    // with each one that arrives, ask for it again at the next frame, and never finish loading (the review of
    // 2026-10-04).
    private void Trim()
    {
        var node = _order.Last;
        while (_images.Count > _capacity && node is not null)
        {
            var previous = node.Previous;
            if (!_view.Values.Any(keys => keys.Contains(node.Value.Key)))
            {
                _order.Remove(node);
                _images.Remove(node.Value.Key);
                node.Value.Image.Dispose();
            }
            node = previous;
        }
    }

    // Under _gate. One wait at a time: when it is over, the view's tiles that couldn't be had are asked for again,
    // whether or not a frame is drawn meanwhile (a map nobody touches is drawn only when something changes).
    private void RetryLater()
    {
        if (_retrying || _stop.IsCancellationRequested)
            return;
        _retrying = true;
        _ = Task.Delay(_retryAfter, _stop.Token).ContinueWith(_ => Retry(), _stop.Token,
            TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }

    private void Retry()
    {
        lock (_gate)
        {
            _retrying = false;
            var waiting = false;
            foreach (var key in _view.Values.SelectMany(keys => keys))
            {
                Request(key);
                // Failed a little later than the tile this wait began with: its own wait isn't over yet.
                waiting |= _failedAt.ContainsKey(key) && !_pending.ContainsKey(key);
            }
            if (waiting)
                RetryLater();
        }
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
                    if (encoded is null)
                    {
                        // A file that isn't an image (a download cut short) is a failure to try again, not a tile
                        // tarkov.dev doesn't have: counted as missing, it left a hole there for the whole session.
                        _undecodable?.Invoke(key);
                        throw new InvalidDataException($"Tile {key.Z}/{key.X}/{key.Y} is not an image.");
                    }
                    using var surface = SKSurface.Create(new SKImageInfo(encoded.Width, encoded.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
                    using var paint = new SKPaint { ColorFilter = ArtworkColors.Filter };
                    surface.Canvas.Clear(SKColors.Transparent);
                    surface.Canvas.DrawImage(encoded, 0, 0, paint);
                    image = surface.Snapshot();
                }
                lock (_gate)
                {
                    // Closed while this tile was on its way: nothing is kept (it would never be let go).
                    if (ct.IsCancellationRequested)
                    {
                        image?.Dispose();
                        return;
                    }
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
                        Trim();
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
                RetryLater();
            }
        }
        finally
        {
            lock (_gate)
            {
                _pending.Remove(key);
                // Nothing arrived and nothing is on its way: the sheet stands in, and stays through later tries
                // until a tile arrives (a try that fails again must not blank the map while it runs).
                if (_arrived == 0 && _pending.Count == 0 && _failed + _missing.Count > 0)
                    _unavailable = true;
            }
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
