using Shturmap.Core.Screenshots;

namespace Shturmap.Game.Screenshots;

/// <summary>
/// Deletes a position screenshot a few seconds after Shturmap has read its name, when the player turned that on
/// (owner, 2026-10-04: "a mode that auto-deletes screenshots after a couple of seconds grace period"). Off unless
/// asked for: it is the one thing Shturmap changes outside its own folders, and what it deletes is gone for good.
/// So what it may delete is narrow and checked when the file is seen and again before it goes
/// (<see cref="MayDelete"/>): a file the watcher reported as new in this run, directly in the screenshots folder,
/// named as the game names a screenshot taken in a raid. What was in the folder before Shturmap looked, a menu
/// screenshot and any other file are never touched.
/// The image is not opened: the file is removed by name. One that is still open elsewhere (the game writing it)
/// can't be removed, so it is tried again a few times and then left; a file marked read-only is left too.
/// </summary>
public sealed class ScreenshotCleaner : IDisposable
{
    /// <summary>How long a screenshot stays after it was seen: time for the game to finish writing it, and for any
    /// other tool the player runs to read its name.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(5);

    private const int Tries = 6;

    private readonly Func<bool> _on;
    private readonly TimeSpan _grace;
    private readonly TimeSpan _retry;
    private readonly CancellationTokenSource _stop = new();
    private int _waiting;

    /// <param name="folder">The game's screenshots folder: nothing outside it is ever deleted.</param>
    /// <param name="on">Whether the player's setting is on now; asked when a screenshot is seen and again before it is deleted.</param>
    public ScreenshotCleaner(string folder, Func<bool> on, TimeSpan? grace = null, TimeSpan? retry = null)
    {
        Folder = folder;
        _on = on;
        _grace = grace ?? Grace;
        _retry = retry ?? TimeSpan.FromSeconds(5);
    }

    public string Folder { get; }

    /// <summary>A screenshot was deleted (its path).</summary>
    public event Action<string>? Deleted;

    /// <summary>A screenshot couldn't be deleted and stays: its path and why.</summary>
    public event Action<string, Exception>? Problem;

    /// <summary>Screenshots seen and not yet deleted or let be.</summary>
    internal int Waiting => Volatile.Read(ref _waiting);

    /// <summary>
    /// Whether a file is one this mode may delete: directly in the screenshots folder, and named as the game names a
    /// screenshot with a position (one taken in a raid). Says nothing about whether it is new; only what the watcher
    /// reports is ever asked about.
    /// </summary>
    public static bool MayDelete(string path, string folder)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(folder))
            return false;
        string file, parent, root;
        try
        {
            file = Path.GetFullPath(path);
            parent = Path.GetDirectoryName(file) ?? "";
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
        return string.Equals(Path.TrimEndingDirectorySeparator(parent), root, StringComparison.OrdinalIgnoreCase)
            && ScreenshotName.TryParse(file, out var info) && info.HasPosition;
    }

    /// <summary>A new screenshot, as the watcher reports it. With the mode on, it is deleted after the grace time.</summary>
    public void Seen(ScreenshotSeen seen)
    {
        if (_stop.IsCancellationRequested || !_on() || !MayDelete(seen.Path, Folder))
            return;
        Interlocked.Increment(ref _waiting);
        _ = Task.Run(() => DeleteLaterAsync(seen.Path));
    }

    public void Dispose()
    {
        // What still waits stays on disk: closing Shturmap deletes nothing.
        _stop.Cancel();
        _stop.Dispose();
    }

    private async Task DeleteLaterAsync(string path)
    {
        try
        {
            var stop = _stop.Token;
            await Task.Delay(_grace, stop);
            for (var attempt = 1; ; attempt++)
            {
                // Turned off within the grace time: the screenshot stays.
                if (stop.IsCancellationRequested || !_on() || !MayDelete(path, Folder))
                    return;
                try
                {
                    if (!File.Exists(path))
                        return;
                    File.Delete(path);
                    Raise(() => Deleted?.Invoke(path));
                    return;
                }
                catch (DirectoryNotFoundException)
                {
                    return;
                }
                catch (IOException) when (attempt < Tries)
                {
                    // Still open elsewhere, most likely the game writing it: tried again below.
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Raise(() => Problem?.Invoke(path, e));
                    return;
                }
                await Task.Delay(_retry, stop);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            Interlocked.Decrement(ref _waiting);
        }
    }

    // A listener's failure doesn't end the pool thread's work, and with it the process.
    private static void Raise(Action listeners)
    {
        try
        {
            listeners();
        }
        catch (Exception)
        {
        }
    }
}
