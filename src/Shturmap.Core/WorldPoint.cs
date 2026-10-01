namespace Shturmap.Core;

/// <summary>A position in game-world metres. Y is height; the map plane is X/Z.</summary>
public readonly record struct WorldPoint(double X, double Y, double Z)
{
    public double HorizontalDistanceTo(WorldPoint other) =>
        Math.Sqrt((other.X - X) * (other.X - X) + (other.Z - Z) * (other.Z - Z));

    public override string ToString() => FormattableString.Invariant($"({X:0.##}, {Y:0.##}, {Z:0.##})");
}

/// <summary>A rotation quaternion as the game writes it (Unity: left-handed, Y up).</summary>
public readonly record struct Rotation(double X, double Y, double Z, double W)
{
    /// <summary>
    /// Facing in degrees, 0 = world +Z, 90 = world +X, normalised to [0, 360).
    /// Derived from the forward vector q · (0, 0, 1).
    /// </summary>
    public double YawDegrees
    {
        get
        {
            var fx = 2 * (X * Z + W * Y);
            var fz = 1 - 2 * (X * X + Y * Y);
            var yaw = Math.Atan2(fx, fz) * 180 / Math.PI;
            return yaw < 0 ? yaw + 360 : yaw;
        }
    }
}
