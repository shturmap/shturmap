using Shturmap.Core.Screenshots;
using Shturmap.Game.Screenshots;

namespace Shturmap.Game.Tests;

// "Delete position screenshots" (owner, 2026-10-04): the one thing Shturmap changes outside its own folders. What it
// may delete is narrow: a new screenshot with a position, in the screenshots folder, while the setting is on.
public sealed class ScreenshotCleanerTests : IDisposable
{
    private const string Shot = "2026-01-01[13-35]_40.00, 2.50, 120.00_0.01000, 0.99900, -0.04000, 0.02000_14.13 (0).png";
    private const string NextShot = "2026-01-01[13-40]_12.50, 4.00, 410.00_0.00500, 0.04500, -0.00050, 0.99900_14.15 (0).png";
    private const string MenuShot = "2026-01-01[13-30]_11.72 (0).png";
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(30);
    private readonly string _root = Directory.CreateTempSubdirectory("shturmap-clean-").FullName;
    private readonly string _folder;

    public ScreenshotCleanerTests()
    {
        _folder = Path.Combine(_root, "Screenshots");
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
    }

    private string Make(string name, string? folder = null)
    {
        var path = Path.Combine(folder ?? _folder, name);
        File.WriteAllText(path, "picture");
        return path;
    }

    private static ScreenshotSeen Seen(string path)
    {
        Assert.True(ScreenshotName.TryParse(path, out var info));
        return new ScreenshotSeen(path, info, DateTime.Now);
    }

    private static async Task Settled(ScreenshotCleaner cleaner)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (cleaner.Waiting > 0 && DateTime.UtcNow < until)
            await Task.Delay(10, TestContext.Current.CancellationToken);
        Assert.Equal(0, cleaner.Waiting);
    }

    [Fact]
    public void Only_a_position_screenshot_in_the_screenshots_folder_may_go()
    {
        Assert.True(ScreenshotCleaner.MayDelete(Path.Combine(_folder, Shot), _folder));
        Assert.True(ScreenshotCleaner.MayDelete(Path.Combine(_folder, Shot), _folder + Path.DirectorySeparatorChar));
        Assert.True(ScreenshotCleaner.MayDelete(Path.Combine(_folder.ToUpperInvariant(), Shot), _folder));
        // A menu screenshot gave no position: it was taken for its picture.
        Assert.False(ScreenshotCleaner.MayDelete(Path.Combine(_folder, MenuShot), _folder));
        Assert.False(ScreenshotCleaner.MayDelete(Path.Combine(_folder, "holiday.png"), _folder));
        Assert.False(ScreenshotCleaner.MayDelete(Path.Combine(_folder, "notes.txt"), _folder));
        // Not in the folder itself: above it, below it, beside it, or reached through "..".
        Assert.False(ScreenshotCleaner.MayDelete(Path.Combine(_root, Shot), _folder));
        Assert.False(ScreenshotCleaner.MayDelete(Path.Combine(_folder, "kept", Shot), _folder));
        Assert.False(ScreenshotCleaner.MayDelete(Path.Combine(_folder + "-old", Shot), _folder));
        Assert.False(ScreenshotCleaner.MayDelete(Path.Combine(_folder, "..", Shot), _folder));
        Assert.False(ScreenshotCleaner.MayDelete("", _folder));
        Assert.False(ScreenshotCleaner.MayDelete(Path.Combine(_folder, Shot), ""));
    }

    [Fact]
    public async Task A_position_screenshot_goes_after_the_grace_time_and_nothing_else_does()
    {
        var shot = Make(Shot);
        var menu = Make(MenuShot);
        var other = Make("holiday.png");
        var outside = Make(NextShot, _root);
        var deleted = new List<string>();
        using var cleaner = new ScreenshotCleaner(_folder, () => true, grace: Short, retry: Short);
        cleaner.Deleted += deleted.Add;

        cleaner.Seen(Seen(shot));
        cleaner.Seen(Seen(menu));
        cleaner.Seen(Seen(outside));
        // Still there within the grace time.
        Assert.True(File.Exists(shot));
        await Settled(cleaner);

        Assert.False(File.Exists(shot));
        Assert.Equal(new[] { shot }, deleted);
        Assert.True(File.Exists(menu));
        Assert.True(File.Exists(other));
        Assert.True(File.Exists(outside));
    }

    [Fact]
    public async Task Nothing_goes_while_the_setting_is_off()
    {
        var shot = Make(Shot);
        using var cleaner = new ScreenshotCleaner(_folder, () => false, grace: Short, retry: Short);
        cleaner.Seen(Seen(shot));
        Assert.Equal(0, cleaner.Waiting);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.True(File.Exists(shot));
    }

    [Fact]
    public async Task Turning_the_setting_off_within_the_grace_time_keeps_the_screenshot()
    {
        var shot = Make(Shot);
        var on = true;
        using var cleaner = new ScreenshotCleaner(_folder, () => on, grace: TimeSpan.FromMilliseconds(200), retry: Short);
        cleaner.Seen(Seen(shot));
        on = false;
        await Settled(cleaner);
        Assert.True(File.Exists(shot));
    }

    [Fact]
    public async Task A_screenshot_still_being_written_is_tried_again()
    {
        var shot = Make(Shot);
        using var cleaner = new ScreenshotCleaner(_folder, () => true, grace: Short, retry: TimeSpan.FromMilliseconds(100));
        // The game holds the file open while it writes; Windows refuses to delete it.
        using (new FileStream(shot, FileMode.Open, FileAccess.Write, FileShare.Read))
        {
            cleaner.Seen(Seen(shot));
            await Task.Delay(150, TestContext.Current.CancellationToken);
            Assert.True(File.Exists(shot));
        }
        await Settled(cleaner);
        Assert.False(File.Exists(shot));
    }

    [Fact]
    public async Task A_screenshot_that_cannot_be_deleted_stays_and_is_said()
    {
        var shot = Make(Shot);
        // Marked read-only: the player protected it.
        File.SetAttributes(shot, FileAttributes.ReadOnly);
        var problems = new List<string>();
        using var cleaner = new ScreenshotCleaner(_folder, () => true, grace: Short, retry: Short);
        cleaner.Problem += (path, _) => problems.Add(path);
        cleaner.Seen(Seen(shot));
        await Settled(cleaner);
        Assert.True(File.Exists(shot));
        Assert.Equal(new[] { shot }, problems);
    }

    [Fact]
    public async Task Closing_keeps_what_still_waits()
    {
        var shot = Make(Shot);
        var cleaner = new ScreenshotCleaner(_folder, () => true, grace: TimeSpan.FromMilliseconds(300), retry: Short);
        cleaner.Seen(Seen(shot));
        cleaner.Dispose();
        await Settled(cleaner);
        await Task.Delay(400, TestContext.Current.CancellationToken);
        Assert.True(File.Exists(shot));
        // And nothing is taken on after closing.
        cleaner.Seen(Seen(shot));
        Assert.Equal(0, cleaner.Waiting);
    }

    [Fact]
    public void A_deleted_screenshots_name_counts_as_new_again()
    {
        using var watcher = new ScreenshotWatcher(_folder);
        var seen = new List<string>();
        watcher.ScreenshotTaken += s => seen.Add(s.Path);
        var shot = Make(Shot);
        watcher.Rescan();
        Assert.Equal(new[] { shot }, seen);

        // Deleted after reading; the same place, facing and minute give the same name again.
        File.Delete(shot);
        watcher.Forget(shot);
        watcher.Rescan();
        Assert.Single(seen);
        Make(Shot);
        watcher.Rescan();
        Assert.Equal(new[] { shot, shot }, seen);
    }
}
