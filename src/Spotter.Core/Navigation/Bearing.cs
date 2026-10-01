namespace Spotter.Core.Navigation;

public enum RelativeDirection
{
    Ahead,
    AheadRight,
    Right,
    BehindRight,
    Behind,
    BehindLeft,
    Left,
    AheadLeft,
}

public static class Bearing
{
    /// <summary>World yaw from one point to another: 0 = +Z, 90 = +X, in [0, 360).</summary>
    public static double YawTo(WorldPoint from, WorldPoint to)
    {
        var yaw = Math.Atan2(to.X - from.X, to.Z - from.Z) * 180 / Math.PI;
        return yaw < 0 ? yaw + 360 : yaw;
    }

    /// <summary>Where a target lies relative to the way the player faces, in eight sectors.</summary>
    public static RelativeDirection Relative(double facingYaw, double targetYaw)
    {
        var delta = ((targetYaw - facingYaw) % 360 + 360) % 360; // clockwise from facing
        var sector = (int)Math.Floor((delta + 22.5) / 45) % 8;
        return (RelativeDirection)sector;
    }

    /// <summary>Eight-point compass label for a heading in degrees clockwise from "up" on the map (map-up = N).</summary>
    public static string Compass(double degreesFromUp)
    {
        string[] points = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
        var normalized = ((degreesFromUp % 360) + 360) % 360;
        return points[(int)Math.Floor((normalized + 22.5) / 45) % 8];
    }

    public static string Describe(RelativeDirection direction) => direction switch
    {
        RelativeDirection.Ahead => "ahead",
        RelativeDirection.AheadRight => "ahead-right",
        RelativeDirection.Right => "right",
        RelativeDirection.BehindRight => "behind-right",
        RelativeDirection.Behind => "behind",
        RelativeDirection.BehindLeft => "behind-left",
        RelativeDirection.Left => "left",
        _ => "ahead-left",
    };
}
