namespace Shturmap.Core.Navigation;

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

    /// <summary>Eight-point compass label for a heading in degrees clockwise from "up" on the map (map-up = N), in the
    /// language in use (German: N, NO, O, …).</summary>
    public static string Compass(double degreesFromUp)
    {
        var normalized = ((degreesFromUp % 360) + 360) % 360;
        return ((int)Math.Floor((normalized + 22.5) / 45) % 8) switch
        {
            0 => CoreTexts.CompassNorth,
            1 => CoreTexts.CompassNorthEast,
            2 => CoreTexts.CompassEast,
            3 => CoreTexts.CompassSouthEast,
            4 => CoreTexts.CompassSouth,
            5 => CoreTexts.CompassSouthWest,
            6 => CoreTexts.CompassWest,
            _ => CoreTexts.CompassNorthWest,
        };
    }

    public static string Describe(RelativeDirection direction) => direction switch
    {
        RelativeDirection.Ahead => CoreTexts.DirectionAhead,
        RelativeDirection.AheadRight => CoreTexts.DirectionAheadRight,
        RelativeDirection.Right => CoreTexts.DirectionRight,
        RelativeDirection.BehindRight => CoreTexts.DirectionBehindRight,
        RelativeDirection.Behind => CoreTexts.DirectionBehind,
        RelativeDirection.BehindLeft => CoreTexts.DirectionBehindLeft,
        RelativeDirection.Left => CoreTexts.DirectionLeft,
        _ => CoreTexts.DirectionAheadLeft,
    };
}
