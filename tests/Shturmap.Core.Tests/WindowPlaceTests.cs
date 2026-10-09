using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

/// <summary>
/// The main window comes back where the player left it (owner, 2026-10-04: remember the window's monitor and size):
/// on its monitor, with its bounds and maximised or not; where that monitor is gone, as at a first start.
/// </summary>
public class WindowPlaceTests
{
    // The game's monitor, a second one to its right and a third above it.
    private static readonly WindowPlace.Rect Primary = new(0, 0, 2560, 1440);
    private static readonly WindowPlace.Rect Second = new(2560, 0, 1920, 1080);
    private static readonly WindowPlace.Rect Third = new(0, -1080, 1920, 1080);
    private static readonly WindowPlace.Rect[] All = [Primary, Second, Third];

    [Fact]
    public void A_place_comes_back_as_it_was_saved()
    {
        var place = new WindowPlace.Saved(new(2600, 40, 1600, 1000), true, Second);
        Assert.Equal("2600,40,1600,1000;max;2560,0,1920,1080", WindowPlace.Format(place));
        Assert.Equal(place, WindowPlace.Parse(WindowPlace.Format(place)));
        var above = new WindowPlace.Saved(new(100, -1000, 1200, 900), false, Third);
        Assert.Equal("100,-1000,1200,900;normal;0,-1080,1920,1080", WindowPlace.Format(above));
        Assert.Equal(above, WindowPlace.Parse(WindowPlace.Format(above)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("on")]
    [InlineData("2600,40,1600,1000;max")]
    [InlineData("2600,40,1600,1000;wide;2560,0,1920,1080")]
    [InlineData("2600,40,1600;max;2560,0,1920,1080")]
    [InlineData("2600,40,1600,x;max;2560,0,1920,1080")]
    [InlineData("2600,40,1600,1000;max;2560,0,0,0")]
    public void What_can_t_be_read_is_no_place(string? setting) => Assert.Null(WindowPlace.Parse(setting));

    [Fact]
    public void On_its_monitor_the_window_stands_where_it_stood()
    {
        var place = new WindowPlace.Saved(new(2700, 60, 1500, 950), false, Second);
        Assert.Equal(place, WindowPlace.Restorable(place, All));
        // The third of three monitors, which the first start's rule (the first that isn't the primary) never picks.
        var third = new WindowPlace.Saved(new(100, -1000, 1200, 900), true, Third);
        Assert.Equal(third, WindowPlace.Restorable(third, All));
    }

    [Fact]
    public void A_monitor_that_is_gone_or_moved_means_the_first_start_s_rule()
    {
        var place = new WindowPlace.Saved(new(2700, 60, 1500, 950), true, Second);
        Assert.Null(WindowPlace.Restorable(place, [Primary]));
        Assert.Null(WindowPlace.Restorable(place, [Primary, new(2560, 0, 2560, 1440)])); // another monitor in its place
        Assert.Null(WindowPlace.Restorable(place, [Primary, new(-1920, 0, 1920, 1080)])); // now on the left
        Assert.Null(WindowPlace.Restorable(null, All));
    }

    [Fact]
    public void Maximised_on_another_monitor_than_its_last_unmaximised_bounds_it_is_brought_onto_that_monitor()
    {
        // Maximised on the second monitor at the first start, then sent to the third (Win+Shift+Up): the bounds it
        // would restore to are still the second monitor's.
        var place = new WindowPlace.Saved(new(2600, 40, 1600, 1000), true, Third);
        var back = WindowPlace.Restorable(place, All);
        Assert.NotNull(back);
        Assert.True(back.Value.Maximised);
        Assert.Equal(new WindowPlace.Rect(40, -1040, 1600, 1000), back.Value.Bounds);
    }

    [Theory]
    [InlineData(2700, 60, 1500, 950)]    // well on it
    [InlineData(2553, 0, 974, 1047)]     // snapped to its left half, the border past the edge
    [InlineData(4300, 500, 1500, 900)]   // mostly off its right edge, the title bar still in reach
    [InlineData(1500, 100, 1500, 900)]   // straddling the primary monitor and this one
    [InlineData(2700, 60, 3000, 2000)]   // larger than the monitor: the player's own doing
    public void Bounds_that_can_be_grabbed_on_the_monitor_stay(int x, int y, int w, int h) =>
        Assert.Equal(new WindowPlace.Rect(x, y, w, h), WindowPlace.OnMonitor(new(x, y, w, h), Second));

    [Theory]
    [InlineData(100, 100, 1600, 1000, 2600, 40, 1600, 1000)]    // on the primary monitor: moved over, the size kept
    [InlineData(2700, 1040, 1500, 900, 2600, 40, 1500, 900)]    // its title bar below the monitor's last rows
    [InlineData(2700, -300, 1500, 900, 2600, 40, 1500, 900)]    // its title bar above the monitor
    [InlineData(2700, 60, 300, 200, 2600, 40, 1600, 1000)]      // smaller than the smallest window: the default size
    [InlineData(0, 0, 0, 0, 2600, 40, 1600, 1000)]              // never known
    [InlineData(100, 100, 2400, 1300, 2600, 40, 1840, 1000)]    // from a larger monitor: cut to fit this one
    public void Other_bounds_are_brought_onto_it(int x, int y, int w, int h, int ex, int ey, int ew, int eh) =>
        Assert.Equal(new WindowPlace.Rect(ex, ey, ew, eh), WindowPlace.OnMonitor(new(x, y, w, h), Second));

    [Fact]
    public void At_a_larger_scale_the_smallest_window_is_larger()
    {
        // 1000 × 700 pixels is a usable window at 100 %, but under the smallest one at 150 %.
        var bounds = new WindowPlace.Rect(2700, 60, 1000, 700);
        Assert.Equal(bounds, WindowPlace.OnMonitor(bounds, Second));
        Assert.NotEqual(bounds, WindowPlace.OnMonitor(bounds, Second, scale: 1.5));
    }

    [Fact]
    public void A_saved_place_is_checked_at_its_monitor_s_scale()
    {
        // Saved at 1000 × 700 pixels on a monitor at 150 % (the review of 2026-10-09: the scale wasn't passed, so it was
        // taken at 100 %): under the smallest window there, it comes back at the default size, scaled, from the
        // scaled inset, cut to the monitor.
        var place = new WindowPlace.Saved(new(2700, 60, 1000, 700), false, Second);
        Assert.Equal(place, WindowPlace.Restorable(place, All));
        var back = WindowPlace.Restorable(place, All, scale: 1.5);
        Assert.NotNull(back);
        Assert.Equal(new WindowPlace.Rect(2560 + 60, 60, 1920 - 120, 1080 - 120), back.Value.Bounds);
        Assert.False(back.Value.Maximised);
    }

    // The first start on one monitor (review of 2026-10-09): 1600 × 1000 was taken as pixels, so a 4K screen at 200 %
    // got a window of 800 × 500, under the smallest one, and a 1366 × 768 laptop one larger than its screen.
    [Theory]
    [InlineData(1920, 1040, 1.0, 40, 40, 1600, 960)]
    [InlineData(3840, 2100, 2.0, 80, 80, 3200, 1940)]
    [InlineData(2560, 1400, 1.5, 60, 60, 2400, 1280)]
    [InlineData(1366, 728, 1.0, 40, 40, 1286, 648)]
    public void A_first_window_is_the_default_size_at_the_monitor_s_scale_within_its_work_area(
        int w, int h, double scale, int ex, int ey, int ew, int eh) =>
        Assert.Equal(new WindowPlace.Rect(ex, ey, ew, eh), WindowPlace.First(new(0, 0, w, h), scale));
}
