using Shturmap.Game.Screenshots;

namespace Shturmap.Game.Tests;

// The watcher's callbacks run on timer and notification threads, where an exception left alone ends the app, and the
// folder it watches can go and come back (docs/NEXT.md, review of 2026-10-04, A28).
public sealed class ScreenshotWatcherRobustnessTests : IDisposable
{
    private const string Shot = "2026-01-01[13-35]_40.00, 2.50, 120.00_0.01000, 0.99900, -0.04000, 0.02000_14.13 (0).png";
    private const string NextShot = "2026-01-01[13-40]_12.50, 4.00, 410.00_0.00500, 0.04500, -0.00050, 0.99900_14.15 (0).png";
    private readonly string _root = Directory.CreateTempSubdirectory("shturmap-shots-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void A_listener_that_throws_does_not_leave_the_rescan()
    {
        var folder = Path.Combine(_root, "Screenshots");
        using var watcher = new ScreenshotWatcher(folder);
        var problems = new List<(string What, Exception Why)>();
        var seen = 0;
        watcher.ScreenshotTaken += _ =>
        {
            seen++;
            throw new InvalidOperationException("the listener's own");
        };
        watcher.WatchProblem += (what, why) => problems.Add((what, why));

        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, Shot), "new");
        File.WriteAllText(Path.Combine(folder, NextShot), "new");
        watcher.Rescan();

        // Both screenshots reached the listener, though it threw on each.
        Assert.Equal(2, seen);
        Assert.Equal(new[] { "a listener to a new screenshot", "a listener to a new screenshot" }, problems.Select(p => p.What).ToArray());
        Assert.All(problems, p => Assert.IsType<InvalidOperationException>(p.Why));
        watcher.Rescan();
        Assert.Equal(2, seen);
    }

    [Fact]
    public void A_listener_to_problems_that_throws_changes_nothing()
    {
        var folder = Path.Combine(_root, "Screenshots");
        using var watcher = new ScreenshotWatcher(folder);
        watcher.ScreenshotTaken += _ => throw new InvalidOperationException("the listener's own");
        watcher.WatchProblem += (_, _) => throw new InvalidOperationException("and the problem listener's");

        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, Shot), "new");
        watcher.Rescan();

        Assert.True(watcher.WatchesFolder);
    }

    [Fact]
    public void The_watch_above_a_missing_folder_ends_once_the_folder_is_watched()
    {
        var folder = Path.Combine(_root, "Escape from Tarkov", "Screenshots");
        using var watcher = new ScreenshotWatcher(folder);
        watcher.Start();
        Assert.True(watcher.WaitsForFolder);
        Assert.False(watcher.WatchesFolder);

        Directory.CreateDirectory(folder);
        watcher.Rescan();

        Assert.True(watcher.WatchesFolder);
        Assert.False(watcher.WaitsForFolder);
    }

    [Fact]
    public async Task A_folder_deleted_and_made_again_is_watched_again()
    {
        var folder = Path.Combine(_root, "Screenshots");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, Shot), "old");
        using var watcher = new ScreenshotWatcher(folder);
        var seen = new List<ScreenshotSeen>();
        watcher.ScreenshotTaken += s => { lock (seen) seen.Add(s); };
        watcher.Start();
        Assert.True(watcher.WatchesFolder);

        Directory.Delete(folder, recursive: true);
        // Windows keeps a deleted folder's name taken until the last handle on it is closed (the old watcher's).
        var until = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            try
            {
                Directory.CreateDirectory(folder);
                if (Directory.Exists(folder))
                    break;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
            Assert.True(DateTime.UtcNow < until, "The folder couldn't be made again.");
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
        File.WriteAllText(Path.Combine(folder, NextShot), "new");
        watcher.Rescan();

        // Reported once, by the rescan or by a notification that was quicker.
        until = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < until)
        {
            lock (seen)
            {
                if (seen.Count > 0)
                    break;
            }
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
        await Task.Delay(300, TestContext.Current.CancellationToken);
        watcher.Rescan();
        lock (seen)
            Assert.Equal(new Shturmap.Core.WorldPoint(12.50, 4.00, 410.00), Assert.Single(seen).Info.Position);
        Assert.True(watcher.WatchesFolder);

        // And the new folder is watched like the old one.
        var arrived = new TaskCompletionSource();
        watcher.ScreenshotTaken += _ => arrived.TrySetResult();
        File.WriteAllText(Path.Combine(folder, "2026-01-01[13-49]_1.00, 2.00, 3.00_0.01000, 0.99900, -0.04000, 0.02000_14.13 (0).png"), "newer");
        await arrived.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }
}
