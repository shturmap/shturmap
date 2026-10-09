using System.Globalization;
using Shturmap.Core.Screenshots;

namespace Shturmap.Core.Tests;

public class ScreenshotNameTests
{
    // Made-up names of every shape the game writes: menu shots (several in one minute, one with a number) and raid
    // shots with a position, a facing and the raid clock.
    public static TheoryData<string, bool> Names => new()
    {
        { "2026-01-01[12-00] (0).png", false },
        { "2026-01-01[12-00] (1).png", false },
        { "2026-01-01[12-00] (2).png", false },
        { "2026-01-01[12-01] (0).png", false },
        { "2026-01-01[12-02] (0).png", false },
        { "2026-01-01[12-10]_120.00, 1.50, -80.00_0.05000, 0.68000, -0.05000, 0.72950_16.90 (0).png", true },
        { "2026-01-01[12-11]_130.00, 1.50, -75.00_0.02000, 0.67500, -0.02000, 0.73700_16.95 (0).png", true },
        { "2026-01-01[12-20]_300.00, 12.50, -500.00_0.03000, -0.72500, 0.03500, 0.68700_7.74 (0).png", true },
        { "2026-01-01[12-30]_-450.00, -0.50, 250.00_-0.04000, -0.60000, 0.03000, -0.79800_11.34 (0).png", true },
        { "2026-01-01[13-00] (0).png", false },
        { "2026-01-01[13-03]_-60.00, 3.50, 300.00_-0.02500, 0.23500, -0.00500, -0.97150_6.45 (0).png", true },
        { "2026-01-01[13-30]_11.72 (0).png", false },
        { "2026-01-01[13-35]_40.00, 2.50, 120.00_0.01000, 0.99900, -0.04000, 0.02000_14.13 (0).png", true },
    };

    [Theory]
    [MemberData(nameof(Names))]
    public void Parses_every_shape_the_game_writes(string name, bool hasPosition)
    {
        Assert.True(ScreenshotName.TryParse(name, out var info));
        Assert.Equal(hasPosition, info.HasPosition);
        Assert.Equal(hasPosition, info.Rotation is not null);
    }

    [Fact]
    public void Reads_position_rotation_and_raid_clock()
    {
        Assert.True(ScreenshotName.TryParse(
            @"C:\x\2026-01-01[13-35]_40.00, 2.50, 120.00_0.01000, 0.99900, -0.04000, 0.02000_14.13 (0).png", out var info));

        Assert.Equal(new DateTime(2026, 1, 1, 13, 35, 0), info.TakenAt);
        Assert.Equal(0, info.Counter);
        Assert.Equal(new WorldPoint(40.00, 2.50, 120.00), info.Position);
        Assert.Equal(14.13, info.RaidClockHours);
        Assert.Equal(177.75, info.YawDegrees!.Value, 2);
    }

    [Fact]
    public void Menu_number_is_not_a_raid_clock()
    {
        Assert.True(ScreenshotName.TryParse("2026-01-01[13-30]_11.72 (0).png", out var info));
        Assert.False(info.HasPosition);
        Assert.Equal(11.72, info.TrailingNumber);
        Assert.Null(info.RaidClockHours);
    }

    [Fact]
    public void Accepts_two_digit_counters_and_jpg()
    {
        Assert.True(ScreenshotName.TryParse("2026-01-01[13-35]_1.00, 2.00, 3.00_0, 0, 0, 1_9.50 (12).jpg", out var info));
        Assert.Equal(12, info.Counter);
    }

    [Fact]
    public void Ignores_the_windows_locale()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.True(ScreenshotName.TryParse("2026-01-01[12-20]_300.00, 12.50, -500.00_0.03000, -0.72500, 0.03500, 0.68700_7.74 (0).png", out var info));
            Assert.Equal(new WorldPoint(300.00, 12.50, -500.00), info.Position);
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Theory]
    [InlineData("Screenshot 2026-01-01.png")]
    [InlineData("2026-01-01[13-35]_abc (0).png")]
    [InlineData("2026-01-01[13-35]_1e9, 0, 0_0, 0, 0, 1 (0).png")]
    [InlineData("2026-01-01[13-35] (0).txt")]
    public void Rejects_other_files(string name) => Assert.False(ScreenshotName.TryParse(name, out _));
}
