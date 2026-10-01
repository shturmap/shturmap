namespace Shturmap.Core.Maps;

/// <summary>An axis-aligned box in world X/Z, as tarkov.dev writes it: [[x1, z1], [x2, z2]].</summary>
public readonly record struct WorldBox(double X1, double Z1, double X2, double Z2)
{
    public bool Contains(double x, double z) =>
        x >= Math.Min(X1, X2) && x <= Math.Max(X1, X2) && z >= Math.Min(Z1, Z2) && z <= Math.Max(Z1, Z2);
}

public readonly record struct HeightRange(double Min, double Max)
{
    public static HeightRange Unbounded { get; } = new(double.NegativeInfinity, double.PositiveInfinity);

    public bool Contains(double y) => y >= Min && y < Max;
}

/// <summary>Where a floor layer applies: a height band, optionally limited to some boxes.</summary>
public sealed record LayerExtent(HeightRange Height, IReadOnlyList<WorldBox> Boxes)
{
    public bool Contains(WorldPoint p) =>
        Height.Contains(p.Y) && (Boxes.Count == 0 || Boxes.Any(b => b.Contains(p.X, p.Z)));
}

public sealed record MapLayer(string Name, string? SvgLayer, string? TilePath, bool ShownByDefault, IReadOnlyList<LayerExtent> Extents)
{
    public bool Contains(WorldPoint p) => Extents.Any(e => e.Contains(p));
}

public sealed record MapLabel(double X, double Z, string Text, double Rotation, double Size);

/// <summary>
/// One interactive map from tarkov.dev's maps.json (MIT): how game coordinates land on its artwork.
/// </summary>
public sealed record MapDefinition
{
    /// <summary>tarkov.dev normalizedName, e.g. "streets-of-tarkov".</summary>
    public required string Key { get; init; }

    /// <summary>Other normalized names drawn with this definition, e.g. "night-factory".</summary>
    public IReadOnlyList<string> AltKeys { get; init; } = [];

    /// <summary>[a, b, c, d]: X = a·rx + b, Y = −c·ry + d (Leaflet pixels at zoom 0).</summary>
    public required double[] Transform { get; init; }

    public double CoordinateRotation { get; init; }

    public required WorldBox Bounds { get; init; }

    public WorldBox? SvgBounds { get; init; }

    public string? SvgPath { get; init; }

    /// <summary>Id of the base &lt;g&gt; in the SVG, e.g. "Ground_Level".</summary>
    public string? SvgLayer { get; init; }

    /// <summary>Tile URL template with {z}/{x}/{y}, for maps published as tiles.</summary>
    public string? TilePath { get; init; }

    public int TileSize { get; init; } = 256;

    public double MinZoom { get; init; }

    public double MaxZoom { get; init; } = 5;

    public HeightRange BaseHeight { get; init; } = HeightRange.Unbounded;

    public IReadOnlyList<MapLayer> Layers { get; init; } = [];

    public IReadOnlyList<MapLabel> Labels { get; init; } = [];

    public string? Author { get; init; }

    public string? AuthorLink { get; init; }

    public bool Matches(string normalizedName) =>
        string.Equals(Key, normalizedName, StringComparison.OrdinalIgnoreCase) ||
        AltKeys.Any(k => string.Equals(k, normalizedName, StringComparison.OrdinalIgnoreCase));
}
