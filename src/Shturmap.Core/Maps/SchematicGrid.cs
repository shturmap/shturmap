namespace Shturmap.Core.Maps;

/// <summary>A grid line in world X/Z, and whether it is a major one.</summary>
public readonly record struct GridLine(double X1, double Z1, double X2, double Z2, bool Major);

/// <summary>
/// The metric grid drawn where a map has no artwork (docs/DESIGN.md §3, "The sheet"). Lines sit on
/// whole multiples of the spacing in world metres, so they line up with the game's coordinates, and run across
/// the map's bounds.
/// </summary>
public static class SchematicGrid
{
    public static IReadOnlyList<GridLine> Lines(WorldBox bounds, double spacing, int majorEvery)
    {
        double minX = Math.Min(bounds.X1, bounds.X2), maxX = Math.Max(bounds.X1, bounds.X2);
        double minZ = Math.Min(bounds.Z1, bounds.Z2), maxZ = Math.Max(bounds.Z1, bounds.Z2);
        var lines = new List<GridLine>();
        for (var i = (long)Math.Ceiling(minX / spacing); i * spacing <= maxX; i++)
            lines.Add(new GridLine(i * spacing, minZ, i * spacing, maxZ, i % majorEvery == 0));
        for (var i = (long)Math.Ceiling(minZ / spacing); i * spacing <= maxZ; i++)
            lines.Add(new GridLine(minX, i * spacing, maxX, i * spacing, i % majorEvery == 0));
        return lines;
    }
}
