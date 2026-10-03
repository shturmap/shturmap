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

    [Fact]
    public void A_map_with_svg_artwork_never_takes_tiles()
    {
        using var tiles = Tiles(_ => null);
        Assert.Null(new MapScene(Map, null, null).Tiles);
        Assert.True(new MapScene(Map, null, null).IsSheet);
        Assert.Same(tiles, new MapScene(Map, null, tiles).Tiles);
    }
}
