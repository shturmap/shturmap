using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

/// <summary>
/// The text size (owner, 2026-10-19: Windows' text size honoured everywhere, and "set by an option, not only through the
/// windows settings"): settings' step on top of Windows' own, the whole factor bounded by Windows' largest, the
/// window's share of it, WinUI's curve, and the smallest window and the glance that follow it.
/// </summary>
public class TextSizeTests
{
    [Theory]
    [InlineData(null, TextStep.Normal)]
    [InlineData("", TextStep.Normal)]
    [InlineData("normal", TextStep.Normal)]
    [InlineData("larger", TextStep.Larger)]
    [InlineData("largest", TextStep.Largest)]
    [InlineData("huge", TextStep.Normal)]
    [InlineData("Larger", TextStep.Normal)]
    public void The_setting_reads_back_and_anything_else_is_normal(string? setting, TextStep step) =>
        Assert.Equal(step, TextSize.Parse(setting));

    [Fact]
    public void Each_step_is_stored_and_read_back_as_itself()
    {
        Assert.Equal("ui.textSize", TextSize.Setting);
        foreach (var step in Enum.GetValues<TextStep>())
            Assert.Equal(step, TextSize.Parse(TextSize.Format(step)));
        Assert.Equal(["normal", "larger", "largest"], Enum.GetValues<TextStep>().Select(TextSize.Format));
    }

    [Theory]
    [InlineData(1.0, TextStep.Normal, 1.0)]
    [InlineData(1.0, TextStep.Larger, 1.25)]
    [InlineData(1.0, TextStep.Largest, 1.5)]
    [InlineData(1.25, TextStep.Normal, 1.25)]
    [InlineData(1.25, TextStep.Larger, 1.5625)]
    [InlineData(1.25, TextStep.Largest, 1.875)]
    [InlineData(1.5, TextStep.Largest, 2.25)]
    public void The_whole_factor_is_windows_times_the_step(double windows, TextStep step, double whole)
    {
        Assert.Equal(whole, TextSize.Effective(windows, step), 6);
        // WinUI draws the window's text at Windows' size by itself: the window's own share is the rest.
        Assert.Equal(whole / windows, TextSize.Own(windows, step), 6);
    }

    [Theory]
    [InlineData(2.0, TextStep.Largest, 2.25, 1.125)]
    [InlineData(2.25, TextStep.Larger, 2.25, 1.0)]
    [InlineData(0.8, TextStep.Normal, 1.0, 1.0)]
    [InlineData(0.8, TextStep.Larger, 1.25, 1.25)]
    [InlineData(9.0, TextStep.Normal, 2.25, 1.0)]
    [InlineData(double.NaN, TextStep.Largest, 1.5, 1.5)]
    public void The_whole_factor_stays_within_windows_own_bounds(double windows, TextStep step, double whole, double own)
    {
        Assert.Equal(whole, TextSize.Effective(windows, step), 6);
        Assert.Equal(own, TextSize.Own(windows, step), 6);
    }

    [Theory]
    // WinUI's curve (TextFormatting::GetScaledFontSize), as the exploration of 2026-10-19 read it: smaller text grows more.
    [InlineData(11, 1.5, 16.74)]
    [InlineData(12.5, 1.5, 18.07)]
    [InlineData(15, 1.5, 20.32)]
    [InlineData(44, 1.5, 47.86)]
    [InlineData(11, 2.0, 22.48)]
    [InlineData(11, 1.0, 11)]
    [InlineData(800, 1.5, 800)]
    public void Text_grows_by_winui_s_own_curve(double size, double factor, double drawn) =>
        Assert.Equal(drawn, TextSize.Scaled(size, factor), 2);

    [Fact]
    public void The_smallest_window_is_900_by_560_at_100_percent_and_grows_in_height_with_the_text()
    {
        Assert.Equal((900, 560), WindowPlace.Smallest(1));
        Assert.Equal((900, 663), WindowPlace.Smallest(1.25));
        Assert.Equal((900, 765), WindowPlace.Smallest(1.5));
        Assert.Equal((900, 970), WindowPlace.Smallest(2));
        // Never below 100 % or past Windows' largest; nonsense is 100 %.
        Assert.Equal((900, 560), WindowPlace.Smallest(0.5));
        Assert.Equal(WindowPlace.Smallest(TextSize.Most), WindowPlace.Smallest(5));
        Assert.Equal((900, 560), WindowPlace.Smallest(double.NaN));
        var heights = Enumerable.Range(100, 126).Select(p => WindowPlace.Smallest(p / 100.0).Height).ToList();
        Assert.Equal(heights.Order(), heights);
    }

    [Fact]
    public void The_glance_s_labels_stand_above_the_names_where_beside_them_the_names_keep_too_little()
    {
        // 321 DIP inside the row; the label column and the figures as measured in German at 100 %, 125 % and 150 %.
        Assert.False(GlanceFold.Stacked(321, 70, 90, 15));
        Assert.True(GlanceFold.Stacked(321, 85, 105, 17.7));
        Assert.True(GlanceFold.Stacked(321, 95, 115, 20.3));
        // Exactly the least room is enough; nothing laid out yet is never stacked.
        Assert.False(GlanceFold.Stacked(100 + 8 * 15, 50, 50, 15));
        Assert.True(GlanceFold.Stacked(100 + 8 * 15 - 1, 50, 50, 15));
        Assert.False(GlanceFold.Stacked(0, 70, 90, 20));
    }
}
