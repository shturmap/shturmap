namespace Spotter.Core.Maps;

public static class FloorResolver
{
    /// <summary>
    /// The layer a point is on, or null for the base layer. Mirrors tarkov.dev: a layer applies when the
    /// height is inside one of its extents and the position is inside that extent's boxes (if it has any).
    /// When several apply, the narrowest height band wins.
    /// </summary>
    public static MapLayer? LayerFor(MapDefinition map, WorldPoint p)
    {
        MapLayer? best = null;
        var bestSpan = double.PositiveInfinity;
        foreach (var layer in map.Layers)
        {
            foreach (var extent in layer.Extents)
            {
                if (!extent.Contains(p))
                    continue;
                var span = extent.Height.Max - extent.Height.Min;
                if (span < bestSpan)
                {
                    best = layer;
                    bestSpan = span;
                }
            }
        }
        return best;
    }
}
