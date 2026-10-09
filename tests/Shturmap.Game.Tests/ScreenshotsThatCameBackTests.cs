using System.Collections.Concurrent;
using Shturmap.Game.Screenshots;

namespace Shturmap.Game.Tests;

// A screenshot that comes back is no new one (review of 2026-10-09): a Screenshots folder a sync tool or the Recycle
// Bin restored had every file it held reported as new, and with "Delete position screenshots" on, deleted 5 s later.
// A file last written before watching started, or before a screenshot of its name was deleted, is old.
public sealed class ScreenshotsThatCameBackTests : IDisposable
{
    private const string Kept = "2026-01-01[12-00]_100.00, 2.50, -340.00_0.00000, 0.70711, 0.00000, 0.70711_12.00 (0).png";
    private const string Older = "2026-01-01[12-05]_120.00, 2.50, -320.00_0.00000, 0.70711, 0.00000, 0.70711_12.05 (0).png";
    private const string Taken = "2026-01-01[12-10]_140.00, 2.50, -300.00_0.00000, 0.70711, 0.00000, 0.70711_12.10 (0).png";
    private readonly string _root = Directory.CreateTempSubdirectory("shturmap-came-back-").FullName;
    private readonly string _folder;
    private readonly ConcurrentQueue<string> _seen = new();

    public ScreenshotsThatCameBackTests() => _folder = Path.Combine(_root, "Screenshots");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private ScreenshotWatcher Watcher()
    {
        var watcher = new ScreenshotWatcher(_folder);
        watcher.ScreenshotTaken += s => _seen.Enqueue(Path.GetFileName(s.Path));
        return watcher;
    }

    // As a restore puts a file back: made beside the folder with its old time, then moved in whole.
    private void Restore(string name, DateTime lastWrittenUtc)
    {
        var made = Path.Combine(_root, name);
        File.WriteAllText(made, "picture");
        File.SetLastWriteTimeUtc(made, lastWrittenUtc);
        File.Move(made, Path.Combine(_folder, name));
    }

    // Reported by a notification or by the rescan, whichever comes first; then a moment for one that must not come.
    private async Task<string[]> Reported(ScreenshotWatcher watcher, int count)
    {
        watcher.Rescan();
        var until = DateTime.UtcNow.AddSeconds(10);
        while (_seen.Count < count && DateTime.UtcNow < until)
            await Task.Delay(20, TestContext.Current.CancellationToken);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        watcher.Rescan();
        return _seen.ToArray();
    }

    // Windows keeps a deleted folder's name taken until the last handle on it is closed (the watcher's).
    private async Task MakeFolderAgainAsync()
    {
        Directory.Delete(_folder, recursive: true);
        var until = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            try
            {
                Directory.CreateDirectory(_folder);
                if (Directory.Exists(_folder))
                    return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
            Assert.True(DateTime.UtcNow < until, "The folder couldn't be made again.");
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task A_restored_folder_brings_back_no_screenshot_as_new()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, Kept), "picture");
        using var watcher = Watcher();
        watcher.Start();

        await MakeFolderAgainAsync();
        // What was there, and an older one from elsewhere (another PC's, through the cloud): written yesterday.
        Restore(Kept, DateTime.UtcNow.AddDays(-1));
        Restore(Older, DateTime.UtcNow.AddDays(-2));
        File.WriteAllText(Path.Combine(_folder, Taken), "picture");

        Assert.Equal([Taken], await Reported(watcher, 1));
    }

    [Fact]
    public async Task A_folder_restored_after_the_start_brings_back_no_screenshot_as_new()
    {
        // Not there at start: the game makes it with its first screenshot, or a sync tool brings it back.
        using var watcher = Watcher();
        watcher.Start();
        Directory.CreateDirectory(_folder);
        Restore(Older, DateTime.UtcNow.AddHours(-3));
        File.WriteAllText(Path.Combine(_folder, Taken), "picture");

        Assert.Equal([Taken], await Reported(watcher, 1));
    }

    [Fact]
    public async Task A_screenshot_the_cleaner_deleted_that_comes_back_is_not_new()
    {
        Directory.CreateDirectory(_folder);
        using var watcher = Watcher();
        watcher.Start();
        var shot = Path.Combine(_folder, Taken);
        File.WriteAllText(shot, "picture");
        Assert.Equal([Taken], await Reported(watcher, 1));
        var written = File.GetLastWriteTimeUtc(shot);

        // Deleted after its grace (5 s in the app), as the cleaner does, then put back as it was.
        await Task.Delay(2500, TestContext.Current.CancellationToken);
        File.Delete(shot);
        watcher.Forget(shot);
        Restore(Taken, written);

        Assert.Equal([Taken], await Reported(watcher, 1));
    }
}
