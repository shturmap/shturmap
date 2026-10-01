using Spotter.Core.Navigation;

namespace Spotter.Core.Tests;

public class BearingTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(10, 0, 90)]
    [InlineData(0, -10, 180)]
    [InlineData(-10, 0, 270)]
    public void Yaw_matches_game_convention(double dx, double dz, double expected)
    {
        Assert.Equal(expected, Bearing.YawTo(new WorldPoint(0, 0, 0), new WorldPoint(dx, 0, dz)), 6);
    }

    [Theory]
    [InlineData(0, 0, RelativeDirection.Ahead)]
    [InlineData(0, 30, RelativeDirection.AheadRight)]
    [InlineData(0, 90, RelativeDirection.Right)]
    [InlineData(0, 180, RelativeDirection.Behind)]
    [InlineData(350, 10, RelativeDirection.Ahead)]
    [InlineData(10, 300, RelativeDirection.Left)]
    [InlineData(90, 30, RelativeDirection.AheadLeft)]
    public void Relative_direction_from_facing(double facing, double target, RelativeDirection expected)
    {
        Assert.Equal(expected, Bearing.Relative(facing, target));
    }
}
