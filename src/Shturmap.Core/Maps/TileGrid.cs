namespace Shturmap.Core.Maps;

/// <summary>One tile of a tile layer: the layer's URL template and its zoom and column and row.</summary>
public readonly record struct TileKey(string Template, int Z, int X, int Y)
{
    /// <summary>The tile one zoom level coarser that contains this one.</summary>
    public TileKey Parent => new(Template, Z - 1, X >> 1, Y >> 1);

    public string Url => Template.Replace("{z}", Z.ToString(System.Globalization.CultureInfo.InvariantCulture))
        .Replace("{x}", X.ToString(System.Globalization.CultureInfo.InvariantCulture))
        .Replace("{y}", Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
}

/// <summary>
/// Where tarkov.dev's tiles lie in map units (docs/DESIGN.md §3, "Maps without SVG artwork"). Its map page is a Leaflet map
/// whose CRS is Leaflet's Simple one with tarkov.dev's transform: a map unit is a pixel at zoom 0 and zoom z scales by
/// 2^z, so the tile at (z, x, y) covers map units [x·T, (x+1)·T] × [y·T, (y+1)·T] divided by 2^z, T being the map's
/// tileSize (175 for The Lab, 256 elsewhere) whatever the image's own pixel size. Worked out from Leaflet's documented
/// CRS and tile maths and checked against the tiles themselves (extracts land in their rooms); no code was taken.
/// </summary>
public static class TileGrid
{
    /// <summary>The map units a tile covers.</summary>
    public static MapRect TileRect(int tileSize, int z, int x, int y)
    {
        var size = tileSize / Math.Pow(2, z);
        return new MapRect(x * size, y * size, (x + 1) * size, (y + 1) * size);
    }

    /// <summary>The tile that holds a point in map units.</summary>
    public static (int X, int Y) TileAt(MapPoint p, int tileSize, int z)
    {
        var scale = Math.Pow(2, z) / tileSize;
        return ((int)Math.Floor(p.X * scale), (int)Math.Floor(p.Y * scale));
    }

    /// <summary>
    /// The zoom level whose tiles are at least as sharp as the screen at <paramref name="screenPerUnit"/> pixels per
    /// map unit (a tile's image holds <paramref name="imagePixels"/> pixels across its tileSize), within the levels
    /// tarkov.dev publishes.
    /// </summary>
    public static int ZoomFor(double screenPerUnit, int tileSize, int imagePixels, int minZoom, int maxZoom)
    {
        // Image pixels per map unit at zoom z: imagePixels / tileSize · 2^z. A quarter-step of slack keeps a tile
        // that is only a little soft rather than loading four times as many.
        var wanted = Math.Log2(screenPerUnit * tileSize / Math.Max(1, imagePixels));
        var z = (int)Math.Ceiling(wanted - 0.25);
        return Math.Clamp(z, minZoom, Math.Max(minZoom, maxZoom));
    }

    /// <summary>The tiles at zoom <paramref name="z"/> that a view needs, inside the map's bounds only (tarkov.dev
    /// publishes none outside them).</summary>
    public static IEnumerable<(int X, int Y)> Visible(MapRect view, MapRect bounds, int tileSize, int z)
    {
        var left = Math.Max(view.Left, bounds.Left);
        var top = Math.Max(view.Top, bounds.Top);
        var right = Math.Min(view.Right, bounds.Right);
        var bottom = Math.Min(view.Bottom, bounds.Bottom);
        if (right <= left || bottom <= top)
            yield break;
        var (x1, y1) = TileAt(new MapPoint(left, top), tileSize, z);
        var (x2, y2) = TileAt(new MapPoint(right, bottom), tileSize, z);
        // A view edge exactly on a tile's edge doesn't need the next tile.
        if (TileRect(tileSize, z, x2, y2) is var last && last.Left >= right)
            x2--;
        if (TileRect(tileSize, z, x2, y2).Top >= bottom)
            y2--;
        for (var y = y1; y <= y2; y++)
            for (var x = x1; x <= x2; x++)
                yield return (x, y);
    }

    /// <summary>
    /// Where a tile is kept on disk, below the cache's map-tiles folder: the map, the layer's folder in the URL (e.g.
    /// "1st", "technical", "06_infirmary"), the zoom and "x_y.png".
    /// </summary>
    public static string CacheKey(string mapKey, TileKey tile)
    {
        var path = new Uri(tile.Template.Replace("{z}", "0").Replace("{x}", "0").Replace("{y}", "0")).AbsolutePath;
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        // ".../<layer>/{z}/{x}/{y}.png": the layer folder is the fourth part from the end.
        var layer = parts.Length >= 4 ? parts[^4] : "base";
        return Path.Combine(Safe(mapKey), Safe(layer), tile.Z.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"{tile.X}_{tile.Y}.png");
    }

    private static string Safe(string name) =>
        string.Concat(name.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
}
