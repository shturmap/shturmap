namespace Spotter.Core.Maps;

/// <summary>A point in "map units": tarkov.dev's Leaflet pixel space at zoom 0 (Y grows downward).</summary>
public readonly record struct MapPoint(double X, double Y)
{
    public static MapPoint operator +(MapPoint a, MapPoint b) => new(a.X + b.X, a.Y + b.Y);

    public static MapPoint operator -(MapPoint a, MapPoint b) => new(a.X - b.X, a.Y - b.Y);
}

/// <summary>Axis-aligned rectangle in map units.</summary>
public readonly record struct MapRect(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;

    public double Height => Bottom - Top;
}

/// <summary>
/// Game world ↔ map units, reproducing tarkov.dev's map page (getCRS + applyRotation + svgOverlay):
/// rotate (x, z) by coordinateRotation, then X = a·rx + b, Y = −c·ry + d.
/// </summary>
public sealed class MapProjection
{
    private readonly double _a, _b, _c, _d, _cos, _sin;

    public MapProjection(MapDefinition map)
    {
        Map = map;
        (_a, _b, _c, _d) = (map.Transform[0], map.Transform[1], map.Transform[2], map.Transform[3]);
        var radians = map.CoordinateRotation * Math.PI / 180;
        _cos = Math.Cos(radians);
        _sin = Math.Sin(radians);
        ArtworkRect = ProjectBox(map.SvgBounds ?? map.Bounds);
        WorldRect = ProjectBox(map.Bounds);
    }

    public MapDefinition Map { get; }

    /// <summary>Where the SVG artwork is laid out, in map units.</summary>
    public MapRect ArtworkRect { get; }

    /// <summary>The map's playable bounds, in map units.</summary>
    public MapRect WorldRect { get; }

    public MapPoint ToMap(double x, double z)
    {
        var rx = x * _cos - z * _sin;
        var ry = x * _sin + z * _cos;
        return new MapPoint(_a * rx + _b, -_c * ry + _d);
    }

    public MapPoint ToMap(WorldPoint p) => ToMap(p.X, p.Z);

    public (double X, double Z) ToWorld(MapPoint m)
    {
        var rx = (m.X - _b) / _a;
        var ry = (m.Y - _d) / -_c;
        // inverse rotation
        return (rx * _cos + ry * _sin, -rx * _sin + ry * _cos);
    }

    /// <summary>
    /// Screen heading of a world yaw, in degrees clockwise from "up" on the map. Computed by projecting a
    /// step along the facing, so it also holds for maps with unequal X/Y scale (Icebreaker).
    /// </summary>
    public double ScreenHeadingDegrees(WorldPoint at, double yawDegrees)
    {
        var yaw = yawDegrees * Math.PI / 180;
        var from = ToMap(at.X, at.Z);
        var to = ToMap(at.X + Math.Sin(yaw), at.Z + Math.Cos(yaw));
        var heading = Math.Atan2(to.X - from.X, -(to.Y - from.Y)) * 180 / Math.PI;
        return heading < 0 ? heading + 360 : heading;
    }

    /// <summary>
    /// Places an SVG with the given viewBox inside <see cref="ArtworkRect"/> the way a browser does by default
    /// (preserveAspectRatio xMidYMid meet), which is how tarkov.dev's Leaflet svgOverlay shows it.
    /// </summary>
    public SvgPlacement PlaceSvg(double viewBoxX, double viewBoxY, double viewBoxWidth, double viewBoxHeight)
    {
        var rect = ArtworkRect;
        var scale = Math.Min(rect.Width / viewBoxWidth, rect.Height / viewBoxHeight);
        var offsetX = rect.Left + (rect.Width - viewBoxWidth * scale) / 2 - viewBoxX * scale;
        var offsetY = rect.Top + (rect.Height - viewBoxHeight * scale) / 2 - viewBoxY * scale;
        return new SvgPlacement(scale, offsetX, offsetY);
    }

    private MapRect ProjectBox(WorldBox box)
    {
        var p1 = ToMap(box.X1, box.Z1);
        var p2 = ToMap(box.X2, box.Z2);
        return new MapRect(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y), Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y));
    }
}

/// <summary>map = svg · Scale + Offset.</summary>
public readonly record struct SvgPlacement(double Scale, double OffsetX, double OffsetY)
{
    public MapPoint SvgToMap(double x, double y) => new(x * Scale + OffsetX, y * Scale + OffsetY);

    public (double X, double Y) MapToSvg(MapPoint m) => ((m.X - OffsetX) / Scale, (m.Y - OffsetY) / Scale);
}
