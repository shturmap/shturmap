using SkiaSharp;
using Shturmap.Core.Maps;

namespace Shturmap.Map.Tests;

// tarkov.dev's tile renders of The Lab, Labyrinth and Icebreaker (docs/DESIGN.md §3, "Maps without SVG artwork").
public class MapTilesTests
{
    private const string Template = "https://assets.tarkov.dev/maps/test/base/{z}/{x}/{y}.png";

    private static readonly MapDefinition Map = new()
    {
        Key = "test",
        Transform = [1, 0, 1, 0],
        Bounds = new WorldBox(0, 0, 256, -256),
        TilePath = Template,
        TileSize = 256,
        MinZoom = 1,
        MaxZoom = 4,
    };

    private static readonly MapRect Bounds = new(0, 0, 256, 256);

    private static byte[] Png(SKColor color)
    {
        using var bitmap = new SKBitmap(256, 256);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    // A tile server in miniature: every tile there, or none, or no network.
    private static MapTiles Tiles(Func<TileKey, byte[]?> serve, int capacity = 192) =>
        new(Map, (tile, _) => Task.FromResult(serve(tile)), capacity);

    private static List<(SKRect Source, MapRect Target, int Width)> Drawn(MapTiles tiles, MapRect view, double perUnit)
    {
        var drawn = new List<(SKRect, MapRect, int)>();
        tiles.Draw(Template, view, Bounds, perUnit, (image, source, target) => drawn.Add((source, target, image.Width)));
        return drawn;
    }

    [Fact]
    public async Task Loaded_tiles_are_drawn_where_they_lie()
    {
        using var tiles = Tiles(_ => Png(SKColors.Gray));
        var view = new MapRect(0, 0, 64, 64);
        await tiles.LoadAsync(Template, view, Bounds, 8);   // 8 px a unit: zoom 3, tiles of 32 units
        var drawn = Drawn(tiles, view, 8);
        Assert.Equal(4, drawn.Count);
        Assert.Contains(drawn, d => d.Target == new MapRect(32, 32, 64, 64) && d.Source == SKRect.Create(256, 256));
        Assert.Equal(TileStatus.Showing, tiles.Status);
    }

    [Fact]
    public async Task While_a_tile_loads_the_coarser_tile_stands_in_with_the_part_that_covers_it()
    {
        // Zoom 2 is here; zoom 3 never arrives (no network for it).
        using var tiles = Tiles(t => t.Z == 2 ? Png(SKColors.Gray) : throw new HttpRequestException("offline"));
        var view = new MapRect(0, 0, 64, 64);
        await tiles.LoadAsync(Template, view, Bounds, 4);   // zoom 2: one 64-unit tile
        var drawn = Drawn(tiles, view, 8);                  // now asked at zoom 3
        Assert.Equal(4, drawn.Count);
        // The zoom-3 tile at (1, 1) is the lower right quarter of the zoom-2 tile.
        Assert.Contains(drawn, d => d.Target == new MapRect(32, 32, 64, 64) && d.Source == SKRect.Create(128, 128, 128, 128));
    }

    [Fact]
    public async Task Only_the_most_recently_used_tiles_stay_in_memory()
    {
        using var tiles = Tiles(_ => Png(SKColors.Gray), capacity: 16);
        await tiles.LoadAsync(Template, Bounds, Bounds, 2); // zoom 1: 4 tiles
        await tiles.LoadAsync(Template, Bounds, Bounds, 4); // zoom 2: 16 tiles
        Assert.Equal(16, tiles.Cached);
        // The zoom-2 tiles came last and stay; a zoom-1 tile is gone and is loaded again when asked.
        Assert.Equal(16, Drawn(tiles, Bounds, 4).Count);
    }

    // ---- the view's own tiles always stay (the review of 2026-10-04: a 4K view needs more than 192 tiles, and each
    // tile that arrived threw out one the next frame asked for again, without end) ----

    [Fact]
    public async Task A_view_that_needs_more_tiles_than_the_capacity_keeps_them_all_and_stops_loading()
    {
        var asked = 0;
        using var tiles = new MapTiles(Map, (_, _) =>
        {
            Interlocked.Increment(ref asked);
            return Task.FromResult<byte[]?>(Png(SKColors.Gray));
        }, capacity: 16);
        await tiles.LoadAsync(Template, Bounds, Bounds, 8); // zoom 3: 64 tiles, four times the capacity
        Assert.Equal(64, tiles.Cached);

        // The first frame draws all 64 from memory and asks for the coarse level under them (zoom 1: 4 tiles).
        Assert.Equal(64, Drawn(tiles, Bounds, 8).Count(d => d.Source == SKRect.Create(256, 256)));
        for (var i = 0; i < 300 && tiles.Cached < 68; i++)
            await Task.Delay(10, TestContext.Current.CancellationToken);
        Assert.Equal(68, tiles.Cached);

        // Every later frame finds them all: nothing is thrown out, so nothing is asked for again.
        var loaded = Volatile.Read(ref asked);
        for (var frame = 0; frame < 5; frame++)
            Assert.Equal(64, Drawn(tiles, Bounds, 8).Count(d => d.Source == SKRect.Create(256, 256)));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(loaded, Volatile.Read(ref asked));
        Assert.Equal(68, tiles.Cached);
    }

    [Fact]
    public async Task A_floor_no_longer_shown_gives_its_tiles_back()
    {
        const string floor = "https://assets.tarkov.dev/maps/test/2nd/{z}/{x}/{y}.png";
        using var tiles = Tiles(_ => Png(SKColors.Gray), capacity: 16);
        await tiles.LoadAsync(Template, Bounds, Bounds, 4); // the base layer: 16 tiles
        await tiles.LoadAsync(floor, Bounds, Bounds, 4);    // a floor's layer over it: 16 more, both on screen
        Assert.Equal(32, tiles.Cached);
        tiles.Shows(Template);                               // back on the ground
        Assert.Equal(16, tiles.Cached);
        Assert.Equal(16, Drawn(tiles, Bounds, 4).Count(d => d.Source == SKRect.Create(256, 256)));
    }

    [Fact]
    public async Task A_tile_that_isnt_an_image_is_tried_again_and_not_taken_as_missing()
    {
        var broken = 1;
        var distrusted = new HashSet<TileKey>();
        using var tiles = new MapTiles(Map, (_, _) => Task.FromResult<byte[]?>(Volatile.Read(ref broken) == 1 ? new byte[] { 1, 2, 3 } : Png(SKColors.Gray)),
            retryAfter: TimeSpan.FromMilliseconds(60), undecodable: tile => { lock (distrusted) distrusted.Add(tile); });
        await tiles.LoadAsync(Template, Bounds, Bounds, 2); // zoom 1: 4 tiles, each a download cut short
        Assert.Equal(TileStatus.Unavailable, tiles.Status);
        // Whoever keeps the files is told which ones not to trust.
        lock (distrusted)
            Assert.Equal(4, distrusted.Count);

        // A tile tarkov.dev doesn't have is never asked for again; these are, and arrive once the file is whole.
        Volatile.Write(ref broken, 0);
        await Showing(tiles);
    }

    [Fact]
    public async Task A_tile_that_arrives_after_closing_isnt_kept()
    {
        var asked = new TaskCompletionSource();
        var arriving = new TaskCompletionSource<byte[]?>();
        var tiles = new MapTiles(Map, (_, _) =>
        {
            asked.TrySetResult();
            return arriving.Task;
        });
        var load = tiles.LoadAsync(Template, new MapRect(0, 0, 64, 64), Bounds, 2); // one tile
        await asked.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        tiles.Dispose();
        arriving.SetResult(Png(SKColors.Gray));
        await load;
        Assert.Equal(0, tiles.Cached);
    }

    [Fact]
    public async Task Without_any_tile_the_render_is_unavailable_and_the_sheet_stands_in()
    {
        using var offline = Tiles(_ => throw new HttpRequestException("offline"));
        await offline.LoadAsync(Template, Bounds, Bounds, 2);
        Assert.Equal(TileStatus.Unavailable, offline.Status);
        Assert.True(new MapScene(Map, null, offline).IsSheet);

        using var none = Tiles(_ => null); // tarkov.dev has none (404)
        await none.LoadAsync(Template, Bounds, Bounds, 2);
        Assert.Equal(TileStatus.Unavailable, none.Status);

        using var working = Tiles(_ => Png(SKColors.Gray));
        Assert.Equal(TileStatus.Loading, working.Status);
        Assert.False(new MapScene(Map, null, working).IsSheet);
        await working.LoadAsync(Template, Bounds, Bounds, 2);
        Assert.Equal(TileStatus.Showing, working.Status);
    }

    // ---- a render that couldn't be had comes back by itself (the review of 2026-10-04: The Lab opened offline stayed
    // a sheet until the app was started again) ----

    // A tile server that is off the network until told otherwise, counting what it is asked for.
    private sealed class Network
    {
        private int _online;
        private int _asked;

        public bool Online
        {
            get => Volatile.Read(ref _online) == 1;
            set => Volatile.Write(ref _online, value ? 1 : 0);
        }

        public int Asked => Volatile.Read(ref _asked);

        public MapTiles Tiles(TimeSpan retryAfter) => new(Map, (_, _) =>
        {
            Interlocked.Increment(ref _asked);
            return Online ? Task.FromResult<byte[]?>(Png(SKColors.Gray)) : throw new HttpRequestException("offline");
        }, retryAfter: retryAfter);
    }

    private static async Task Showing(MapTiles tiles)
    {
        var shown = new TaskCompletionSource();
        void Check()
        {
            if (tiles.Status == TileStatus.Showing)
                shown.TrySetResult();
        }
        tiles.Changed += Check;
        Check();
        await shown.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_render_that_couldnt_be_had_comes_by_itself_once_the_network_is_back()
    {
        var network = new Network();
        using var tiles = network.Tiles(TimeSpan.FromMilliseconds(60));
        await tiles.LoadAsync(Template, Bounds, Bounds, 2);
        var scene = new MapScene(Map, null, tiles);
        Assert.Equal(TileStatus.Unavailable, tiles.Status);

        // Still offline: the tiles are tried again and fail again, and the sheet stays all the while (a try under way
        // must not blank the map).
        var asked = network.Asked;
        var statuses = new List<TileStatus>();
        tiles.Changed += () => { lock (statuses) statuses.Add(tiles.Status); };
        for (var i = 0; i < 40 && network.Asked == asked; i++)
            await Task.Delay(25, TestContext.Current.CancellationToken);
        Assert.True(network.Asked > asked, "the view's tiles weren't asked for again");
        Assert.True(scene.IsSheet);

        // Nobody draws a frame or touches the map: the render still arrives.
        network.Online = true;
        await Showing(tiles);
        Assert.False(scene.IsSheet);
        lock (statuses)
            Assert.DoesNotContain(TileStatus.Loading, statuses);
        Assert.NotEmpty(Drawn(tiles, Bounds, 2));
    }

    [Fact]
    public async Task While_the_sheet_stands_in_drawing_it_asks_for_the_tiles_but_only_once_per_wait()
    {
        var network = new Network();
        using var tiles = network.Tiles(TimeSpan.FromHours(1));
        var scene = new MapScene(Map, null, tiles);
        var camera = new Camera();
        camera.Resize(new SKSize(512, 512));
        camera.Fit(scene.Projection.WorldRect, 0);
        using var bitmap = new SKBitmap(512, 512);
        using var canvas = new SKCanvas(bitmap);

        // The first frame asks for the view's tiles; none can be had.
        MapRenderer.Render(canvas, camera, scene);
        var view = new MapRect(0, 0, 256, 256);
        await tiles.LoadAsync(Template, view, Bounds, camera.Zoom);
        Assert.Equal(TileStatus.Unavailable, tiles.Status);
        var asked = network.Asked;
        Assert.True(asked > 0);

        // A hundred frames of the sheet, and asking outright: no tile is asked for a second time before its wait is over.
        for (var i = 0; i < 100; i++)
            MapRenderer.Render(canvas, camera, scene);
        tiles.Ask(Template, view, Bounds, camera.Zoom);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(asked, network.Asked);
        Assert.True(scene.IsSheet);
    }

    [Fact]
    public async Task A_frame_of_the_sheet_asks_for_the_view_it_shows_now()
    {
        var network = new Network();
        using var tiles = network.Tiles(TimeSpan.FromHours(1));
        var scene = new MapScene(Map, null, tiles);
        var camera = new Camera();
        camera.Resize(new SKSize(512, 512));
        camera.Fit(scene.Projection.WorldRect, 0);
        using var bitmap = new SKBitmap(512, 512);
        using var canvas = new SKCanvas(bitmap);
        await tiles.LoadAsync(Template, new MapRect(0, 0, 256, 256), Bounds, camera.Zoom);
        Assert.True(scene.IsSheet);
        var asked = network.Asked;

        // Zoomed in on the sheet: other tiles are needed now, and the frame asks for those (they never failed).
        camera.ZoomAt(new SKPoint(256, 256), 2);
        MapRenderer.Render(canvas, camera, scene);
        await tiles.LoadAsync(Template, new MapRect(64, 64, 192, 192), Bounds, camera.Zoom);
        Assert.True(network.Asked > asked, "the frame didn't ask for the zoomed view's tiles");
        Assert.True(scene.IsSheet);
    }

    [Fact]
    public void A_map_with_svg_artwork_never_takes_tiles()
    {
        using var tiles = Tiles(_ => null);
        Assert.Null(new MapScene(Map, null, null).Tiles);
        Assert.True(new MapScene(Map, null, null).IsSheet);
        Assert.Same(tiles, new MapScene(Map, null, tiles).Tiles);
    }
}
