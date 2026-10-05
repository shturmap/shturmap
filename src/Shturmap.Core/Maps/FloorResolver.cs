namespace Shturmap.Core.Maps;

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

    /// <summary>
    /// The floors of the building a point stands in, from the top down, the base layer as null: every layer with an
    /// extent over the point's place, those without artwork of their own too, ordered as <see cref="Stack"/> orders
    /// floors but by their extents here. For telling which way another floor is (owner, 2026-10-05: on Customs' oil rig
    /// the first WI-FI camera, at 14 m on the 4th floor, which has no artwork, read as below the 2nd floor shown, since
    /// a floor drawn in the base layer counted as the ground). Only the base layer when no floor covers the place.
    /// </summary>
    public static IReadOnlyList<MapLayer?> Ladder(MapDefinition map, WorldPoint p)
    {
        static double Clamp(double v) => Math.Clamp(v, -1000, 1000);
        return map.Layers
            .Select(l => (Layer: (MapLayer?)l, Here: l.Extents.Where(e => e.Boxes.Count == 0 || e.Boxes.Any(b => b.Contains(p.X, p.Z))).ToList()))
            .Where(f => f.Here.Count > 0)
            .Select(f => (f.Layer, Low: f.Here.Min(e => Clamp(e.Height.Min)), High: f.Here.Max(e => Clamp(e.Height.Max))))
            .Append((Layer: null, Low: Clamp(map.BaseHeight.Min), High: Clamp(map.BaseHeight.Max)))
            .OrderByDescending(f => f.Low)
            .ThenByDescending(f => f.High)
            .Select(f => f.Layer)
            .ToList();
    }

    /// <summary>
    /// The map's floors that have artwork of their own, from the top down, the base layer as null ("Ground").
    /// Ordered by where each floor's height bands start (open-ended bands count as ±1000 m), then by where they
    /// end, so a basement sharing the base layer's lower bound still sorts below it. Floors without their own
    /// artwork (Customs' 4th floor, Reserve's upper floors) are drawn in the base layer and aren't listed.
    /// </summary>
    public static IReadOnlyList<MapLayer?> Stack(MapDefinition map)
    {
        static double Clamp(double v) => Math.Clamp(v, -1000, 1000);
        var floors = map.Layers
            .Where(l => l.Extents.Count > 0 && (map.SvgPath is null ? l.TilePath is not null : l.SvgLayer is not null))
            .Select(l => (Layer: (MapLayer?)l, Low: l.Extents.Min(e => Clamp(e.Height.Min)), High: l.Extents.Max(e => Clamp(e.Height.Max))))
            .Append((Layer: null, Low: Clamp(map.BaseHeight.Min), High: Clamp(map.BaseHeight.Max)))
            .OrderByDescending(f => f.Low)
            .ThenByDescending(f => f.High)
            .Select(f => f.Layer)
            .ToList();
        return floors.Count > 1 ? floors : [];
    }
}
